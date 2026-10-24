using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Scripting.Compile;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;
using tPoseidon.Core.Game.Scripting.Library;
using tPoseidon.Core.Game.Scripting.Vm;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record ScriptNpcChange(IReadOnlyList<NpcEntity> Added, IReadOnlyList<NpcEntity> Removed);

public sealed class ScriptEngine
{
    public const string ReloadRequestFile = ".reload";
    public const char ChatCommandMark = '@';

    private readonly string _directory;
    private readonly Action<string> _log;
    private readonly object _gate = new();
    private ScriptLibrary _library = ScriptLibrary.Empty;
    private ScriptScheduler _scheduler;

    public ScriptEngine(string directory, Action<string> log, int instructionBudget = ScriptMachine.DefaultInstructionBudget)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        _directory = directory;
        _log = log ?? throw new ArgumentNullException(nameof(log));
        InstructionBudget = instructionBudget;
        _scheduler = new ScriptScheduler(_library.Timers, DateTimeOffset.Now);
    }

    public int InstructionBudget { get; }

    public ScriptLibrary Library
    {
        get
        {
            lock (_gate) return _library;
        }
    }

    public ScriptLoadResult Reload()
    {
        var result = ScriptLoader.LoadDirectory(_directory);
        lock (_gate)
        {
            _library = result.Library;
            _scheduler = new ScriptScheduler(result.Library.Timers, DateTimeOffset.Now);
        }
        _log($"[scripts] loaded {result.FilesLoaded} file(s) from {_directory}: {result.Library.Npcs.Count} npc(s), {result.Library.OverrideCount} override(s), {result.Library.EventCount} event(s), {result.Library.Timers.Count} timer(s), {result.Library.CommandCount} command(s), {result.Library.FunctionCount} function(s)");
        foreach (var error in result.Errors) _log($"[scripts] {error}");
        return result;
    }

    public bool ConsumeReloadRequest()
    {
        var marker = Path.Combine(_directory, ReloadRequestFile);
        if (!File.Exists(marker)) return false;
        try
        {
            File.Delete(marker);
        }
        catch (IOException exception)
        {
            _log($"[scripts] the reload request {marker} could not be removed: {exception.Message}");
            return false;
        }
        return true;
    }

    public IReadOnlyList<ScriptTimerEntry> CollectDueTimers(DateTimeOffset now)
    {
        lock (_gate) return _scheduler.Collect(now);
    }

    public async Task RunTimersAsync(ScriptWorldServices world, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(world);
        foreach (var entry in CollectDueTimers(now))
        {
            var host = new ScriptServerHost(world);
            ScriptRunner.TryRun(entry.Timer.Body, Library, host, _log, InstructionBudget);
            await host.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task RunEventAsync(EventKind kind, long? subject, string? name, IScriptSessionHost host, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        var library = Library;
        foreach (var routine in library.EventsFor(kind, subject, name).ToList())
        {
            ScriptRunner.TryRun(routine, library, host, _log, InstructionBudget);
            await host.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    public static bool TryParseChatCommand(string line, out string name, out IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(line);
        name = string.Empty;
        arguments = Array.Empty<string>();
        var text = line.Trim();
        if (text.Length < 2 || text[0] != ChatCommandMark) return false;
        var words = SplitWords(text[1..]);
        if (words.Count == 0) return false;
        name = words[0];
        arguments = words.Skip(1).ToList();
        return true;
    }

    public async Task<bool> RunCommandAsync(string name, IReadOnlyList<string> arguments, bool isGameMaster, Func<IReadOnlyList<string>, IScriptSessionHost> hostFor, Func<string, CancellationToken, Task> tell, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hostFor);
        ArgumentNullException.ThrowIfNull(tell);
        var library = Library;
        if (library.FindCommand(name) is not { } found) return false;
        if (found.Command.Declaration.GameMasterOnly && !isGameMaster)
        {
            await tell("That command is for game masters only.", ct).ConfigureAwait(false);
            return true;
        }
        var host = hostFor(arguments);
        ScriptRunner.TryRun(found.Command.Body, library, host, _log, InstructionBudget);
        await host.FlushAsync(ct).ConfigureAwait(false);
        return true;
    }

    public ScriptNpcChange SyncNpcs(WorldEntityStore store, IdAllocator ids, Dictionary<string, NpcEntity> placed)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(placed);
        var wanted = new Dictionary<string, (CompiledNpc Npc, string File)>(StringComparer.Ordinal);
        foreach (var (npc, file) in Library.Npcs) wanted[ScriptLibrary.NpcKey(file, npc.Declaration.Name)] = (npc, file);
        var removed = new List<NpcEntity>();
        var added = new List<NpcEntity>();
        foreach (var (key, entity) in placed.ToList())
        {
            if (wanted.TryGetValue(key, out var current) && Matches(entity, current.Npc.Declaration)) continue;
            store.Remove(entity.EntityId);
            ids.Release(entity.EntityId);
            placed.Remove(key);
            removed.Add(entity);
        }
        foreach (var (key, (npc, _)) in wanted)
        {
            if (placed.ContainsKey(key)) continue;
            var declaration = npc.Declaration;
            var entity = new NpcEntity
            {
                EntityId = ids.NewId(),
                MapId = (ushort)declaration.Map,
                ChrType = declaration.Sprite,
                PosId = 0,
                Spawn = new NpcPoint(declaration.X, declaration.Y, 0f, declaration.Facing),
                DisplayName = declaration.Name,
                Source = new ScriptNpcSource(key),
            };
            store.Install(entity);
            placed[key] = entity;
            added.Add(entity);
        }
        return new ScriptNpcChange(added, removed);
    }

    private static bool Matches(NpcEntity entity, NpcDeclaration declaration) =>
        entity.MapId == declaration.Map
        && entity.ChrType == declaration.Sprite
        && (int)entity.Spawn.X == declaration.X
        && (int)entity.Spawn.Y == declaration.Y
        && (int)entity.Spawn.Orientation == declaration.Facing
        && string.Equals(entity.DisplayName, declaration.Name, StringComparison.Ordinal);

    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var started = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
                started = true;
                continue;
            }
            if (!quoted && char.IsWhiteSpace(c))
            {
                if (started) words.Add(current.ToString());
                current.Clear();
                started = false;
                continue;
            }
            current.Append(c);
            started = true;
        }
        if (started) words.Add(current.ToString());
        return words;
    }
}
