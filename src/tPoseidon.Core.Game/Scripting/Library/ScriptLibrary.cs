using tPoseidon.Core.Game.Scripting.Compile;
using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.Game.Scripting.Library;

public sealed record ScriptTimerEntry(CompiledTimer Timer, string File, TimeSpan TimeOfDay, CronSchedule? Cron);

public sealed class ScriptLibrary
{
    public static ScriptLibrary Empty { get; } = new(
        Array.Empty<(CompiledNpc, string)>(),
        Array.Empty<(CompiledOverride, string)>(),
        Array.Empty<(CompiledEvent, string)>(),
        Array.Empty<ScriptTimerEntry>(),
        new Dictionary<string, (CompiledCommand, string)>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, Routine>(StringComparer.Ordinal));

    private readonly IReadOnlyList<(CompiledOverride Override, string File)> _overrides;
    private readonly IReadOnlyList<(CompiledEvent Event, string File)> _events;
    private readonly IReadOnlyDictionary<string, (CompiledCommand Command, string File)> _commands;
    private readonly IReadOnlyDictionary<string, Routine> _functions;
    private readonly Dictionary<string, Routine> _npcTalks;

    public ScriptLibrary(
        IReadOnlyList<(CompiledNpc Npc, string File)> npcs,
        IReadOnlyList<(CompiledOverride Override, string File)> overrides,
        IReadOnlyList<(CompiledEvent Event, string File)> events,
        IReadOnlyList<ScriptTimerEntry> timers,
        IReadOnlyDictionary<string, (CompiledCommand Command, string File)> commands,
        IReadOnlyDictionary<string, Routine> functions)
    {
        Npcs = npcs;
        _overrides = overrides;
        _events = events;
        Timers = timers;
        _commands = commands;
        _functions = functions;
        _npcTalks = new Dictionary<string, Routine>(StringComparer.Ordinal);
        foreach (var (npc, file) in npcs) _npcTalks[NpcKey(file, npc.Declaration.Name)] = npc.Talk;
    }

    public static string NpcKey(string file, string name) => file + "#" + name;

    public Routine? FindNpcTalk(string key) => _npcTalks.GetValueOrDefault(key);

    public IReadOnlyList<(CompiledNpc Npc, string File)> Npcs { get; }

    public IReadOnlyList<ScriptTimerEntry> Timers { get; }

    public int OverrideCount => _overrides.Count;

    public int EventCount => _events.Count;

    public int CommandCount => _commands.Count;

    public int FunctionCount => _functions.Count;

    public Routine? FindFunction(string name) => _functions.GetValueOrDefault(name);

    public (CompiledCommand Command, string File)? FindCommand(string name) =>
        _commands.TryGetValue(name, out var command) ? command : null;

    public Routine? FindOverride(int sprite, int map, int x, int y)
    {
        Routine? best = null;
        var bestScore = 0;
        foreach (var (over, _) in _overrides)
        {
            var declaration = over.Declaration;
            if (declaration.Sprite != sprite) continue;
            if (declaration.Map is int declaredMap && declaredMap != map) continue;
            if (declaration.X is int declaredX && declaration.Y is int declaredY && (declaredX != x || declaredY != y)) continue;
            var score = 1 + (declaration.Map is null ? 0 : 1) + (declaration.X is null ? 0 : 2);
            if (score <= bestScore) continue;
            best = over.Talk;
            bestScore = score;
        }
        return best;
    }

    public IEnumerable<Routine> EventsFor(EventKind kind, long? subject = null, string? name = null)
    {
        foreach (var (entry, _) in _events)
        {
            var declaration = entry.Declaration;
            if (declaration.Kind != kind) continue;
            if (kind == EventKind.Named && !string.Equals(declaration.Name, name, StringComparison.Ordinal)) continue;
            if (declaration.Number is long wanted && wanted != subject) continue;
            yield return entry.Body;
        }
    }
}
