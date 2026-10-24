using tPoseidon.Core.Game.Scripting.Compile;
using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.Game.Scripting.Library;

public sealed record ScriptSource(string File, string Text);

public sealed record ScriptLoadResult(ScriptLibrary Library, IReadOnlyList<string> Errors, int FilesLoaded, int FilesFailed);

public static class ScriptLoader
{
    public const string Extension = ".tscript";
    public const int MaximumFileBytes = 256 * 1024;
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(5);

    public static ScriptLoadResult LoadDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        if (!Directory.Exists(directory))
        {
            return new ScriptLoadResult(ScriptLibrary.Empty, Array.Empty<string>(), 0, 0);
        }
        var sources = new List<ScriptSource>();
        var errors = new List<string>();
        var failed = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*" + Extension, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            var name = Path.GetRelativePath(directory, path).Replace('\\', '/');
            try
            {
                if (new FileInfo(path).Length > MaximumFileBytes)
                {
                    errors.Add($"{name}:1: the file is larger than {MaximumFileBytes / 1024} KiB");
                    failed++;
                    continue;
                }
                sources.Add(new ScriptSource(name, File.ReadAllText(path)));
            }
            catch (IOException exception)
            {
                errors.Add($"{name}:1: {exception.Message}");
                failed++;
            }
            catch (UnauthorizedAccessException exception)
            {
                errors.Add($"{name}:1: {exception.Message}");
                failed++;
            }
        }
        var result = Load(sources);
        return result with { Errors = errors.Concat(result.Errors).ToList(), FilesFailed = result.FilesFailed + failed };
    }

    public static ScriptLoadResult Load(IEnumerable<ScriptSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var errors = new List<string>();
        var files = new List<CompiledFile>();
        var failed = 0;
        foreach (var source in sources)
        {
            var file = CompileOne(source, errors);
            if (file is null) failed++;
            else files.Add(file);
        }
        failed += DropUnlinkableFiles(files, errors);
        return new ScriptLoadResult(Assemble(files), errors, files.Count, failed);
    }

    private static CompiledFile? CompileOne(ScriptSource source, List<string> errors)
    {
        CompiledFile compiled;
        try
        {
            compiled = ScriptCompiler.Compile(source.File, source.Text);
        }
        catch (ScriptSyntaxException exception)
        {
            errors.Add($"{source.File}:{exception.Line}:{exception.Column}: {exception.Message}");
            return null;
        }
        var problems = Validate(compiled).ToList();
        if (problems.Count == 0) return compiled;
        errors.AddRange(problems);
        return null;
    }

    private static IEnumerable<string> Validate(CompiledFile file)
    {
        foreach (var npc in file.Npcs)
        {
            var declaration = npc.Declaration;
            if (declaration.Sprite is < 1 or > ushort.MaxValue) yield return Problem(file, declaration, "the sprite id must be between 1 and 65535");
            if (declaration.Map is < 0 or > ushort.MaxValue) yield return Problem(file, declaration, "the map id must be between 0 and 65535");
            if (declaration.X is < 0 or > ushort.MaxValue || declaration.Y is < 0 or > ushort.MaxValue) yield return Problem(file, declaration, "the position must be between 0 and 65535");
            if (declaration.Name.Length is 0 or > 40) yield return Problem(file, declaration, "the NPC name must be 1 to 40 characters");
        }
        foreach (var over in file.Overrides)
        {
            if (over.Declaration.Sprite is < 1 or > ushort.MaxValue) yield return Problem(file, over.Declaration, "the NPC type id must be between 1 and 65535");
        }
        foreach (var timer in file.Timers)
        {
            var problem = ValidateTimer(timer.Declaration);
            if (problem is not null) yield return Problem(file, timer.Declaration, problem);
        }
        foreach (var command in file.Commands)
        {
            var name = command.Declaration.Name;
            if (name.Length == 0 || name.Length > 24 || !name.All(char.IsLetterOrDigit))
            {
                yield return Problem(file, command.Declaration, "a command name is 1 to 24 letters or digits");
            }
        }
        foreach (var function in file.Functions)
        {
            if (Host.Builtins.TryFind(function.Name, out _)) yield return $"{file.File}:1: the function '{function.Name}' has the name of a built in function";
        }
    }

    private static string? ValidateTimer(TimerDeclaration timer)
    {
        try
        {
            switch (timer.Kind)
            {
                case TimerKind.Interval:
                    return timer.Interval < MinimumInterval ? $"an interval must be at least {MinimumInterval.TotalSeconds:0} seconds" : null;
                case TimerKind.DailyAt:
                    CronSchedule.ParseTimeOfDay(timer.Spec);
                    return null;
                default:
                    CronSchedule.Parse(timer.Spec);
                    return null;
            }
        }
        catch (FormatException exception)
        {
            return exception.Message;
        }
    }

    private static string Problem(CompiledFile file, Node node, string message) => $"{file.File}:{node.Line}:{node.Column}: {message}";

    private static int DropUnlinkableFiles(List<CompiledFile> files, List<string> errors)
    {
        var dropped = 0;
        var changed = true;
        while (changed)
        {
            changed = false;
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            var commandOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files.ToList())
            {
                var problems = new List<string>();
                foreach (var function in file.Functions)
                {
                    if (owners.TryGetValue(function.Name, out var owner)) problems.Add($"{file.File}:1: the function '{function.Name}' is already defined in {owner}");
                    else owners[function.Name] = file.File;
                }
                foreach (var command in file.Commands)
                {
                    var commandName = command.Declaration.Name;
                    if (commandOwners.TryGetValue(commandName, out var commandOwner)) problems.Add($"{file.File}:{command.Declaration.Line}:{command.Declaration.Column}: the command '{commandName}' is already defined in {commandOwner}");
                    else commandOwners[commandName] = file.File;
                }
                if (problems.Count > 0)
                {
                    errors.AddRange(problems);
                    files.Remove(file);
                    dropped++;
                    changed = true;
                    break;
                }
            }
            if (changed) continue;
            foreach (var file in files.ToList())
            {
                var missing = file.References.Where(entry => !owners.ContainsKey(entry.Reference.Name)).ToList();
                if (missing.Count == 0) continue;
                foreach (var (reference, name) in missing)
                {
                    errors.Add($"{name}:{reference.Line}:{reference.Column}: there is no function named '{reference.Name}'");
                }
                files.Remove(file);
                dropped++;
                changed = true;
                break;
            }
        }
        return dropped;
    }

    private static ScriptLibrary Assemble(List<CompiledFile> files)
    {
        var npcs = new List<(CompiledNpc, string)>();
        var overrides = new List<(CompiledOverride, string)>();
        var events = new List<(CompiledEvent, string)>();
        var timers = new List<ScriptTimerEntry>();
        var commands = new Dictionary<string, (CompiledCommand, string)>(StringComparer.OrdinalIgnoreCase);
        var functions = new Dictionary<string, Routine>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            npcs.AddRange(file.Npcs.Select(entry => (entry, file.File)));
            overrides.AddRange(file.Overrides.Select(entry => (entry, file.File)));
            events.AddRange(file.Events.Select(entry => (entry, file.File)));
            foreach (var timer in file.Timers)
            {
                var declaration = timer.Declaration;
                timers.Add(new ScriptTimerEntry(
                    timer,
                    file.File,
                    declaration.Kind == TimerKind.DailyAt ? CronSchedule.ParseTimeOfDay(declaration.Spec) : TimeSpan.Zero,
                    declaration.Kind == TimerKind.Cron ? CronSchedule.Parse(declaration.Spec) : null));
            }
            foreach (var command in file.Commands) commands[command.Declaration.Name] = (command, file.File);
            foreach (var function in file.Functions) functions[function.Name] = function;
        }
        return new ScriptLibrary(npcs, overrides, events, timers, commands, functions);
    }
}
