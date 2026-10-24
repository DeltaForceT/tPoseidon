using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Scripting;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;
using tPoseidon.Core.Game.Scripting.Vars;

namespace tPoseidon.Core.Net.Dialogs;

public sealed class ScriptEffects
{
    private readonly List<Func<CancellationToken, Task>> _pending = new();

    public void Queue(Func<CancellationToken, Task> effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        _pending.Add(effect);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        while (_pending.Count > 0)
        {
            var next = _pending[0];
            _pending.RemoveAt(0);
            await next(ct).ConfigureAwait(false);
        }
    }
}

public sealed class ScriptWorldServices
{
    public required ScriptVariables Variables { get; init; }

    public required Func<string, CancellationToken, Task> Announce { get; init; }

    public required Func<int> OnlinePlayers { get; init; }

    public required Action<string> TriggerEvent { get; init; }

    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.Now;

    public Func<long, long, long> Random { get; init; } = (minimum, maximum) => System.Random.Shared.NextInt64(minimum, maximum + 1);
}

public sealed class ScriptPlayerServices
{
    public required Func<(ushort Map, ushort X, ushort Y)> Position { get; init; }

    public required Func<bool> IsGameMaster { get; init; }

    public required Func<string, CancellationToken, Task> Tell { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
}

public sealed class ScriptServerHost : ScriptHostBase
{
    private readonly ScriptWorldServices _world;
    private readonly ScriptEffects _effects = new();

    public ScriptServerHost(ScriptWorldServices world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public override DateTimeOffset Now => _world.Clock();

    public override long OnlinePlayers => _world.OnlinePlayers();

    public Task FlushAsync(CancellationToken ct) => _effects.FlushAsync(ct);

    public override ScriptValue GetVariable(VariableScope scope, string name) =>
        scope == VariableScope.Server ? _world.Variables.Get(scope, ScriptVariables.ServerOwner, name) : throw NeedsPlayer();

    public override void SetVariable(VariableScope scope, string name, ScriptValue value)
    {
        if (scope != VariableScope.Server) throw NeedsPlayer();
        _world.Variables.Set(scope, ScriptVariables.ServerOwner, name, value);
    }

    public override void Announce(string text) => _effects.Queue(ct => _world.Announce(text, ct));

    public override void TriggerEvent(string name) => _world.TriggerEvent(name);

    public override long Random(long minimum, long maximum) => _world.Random(minimum, maximum);
}

public sealed class ScriptPlayerHost : ScriptHostBase, IScriptSessionHost
{
    private readonly NpcScriptExecutor _executor;
    private readonly ScriptWorldServices _world;
    private readonly ScriptPlayerServices _player;
    private readonly ScriptEffects _effects = new();
    private bool _closeRequested;

    public ScriptPlayerHost(NpcScriptExecutor executor, ScriptWorldServices world, ScriptPlayerServices player)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _player = player ?? throw new ArgumentNullException(nameof(player));
    }

    public override bool HasPlayer => true;

    public override DateTimeOffset Now => _world.Clock();

    public override string PlayerName => Character.Name;

    public override long Level => _executor.ScriptStats?.Level ?? Character.Level;

    public override long TechniqueLevel => _executor.ScriptStats?.TmLevel ?? Character.TmLevel;

    public override long JobTier => Math.Max((byte)1, Character.JobState);

    public override long JobType => OccupationRules.CurrentJobOf(Character);

    public override long CharacterClass => (long)Character.Species;

    public override long Money => _executor.ScriptStats?.Money ?? Character.Money;

    public override long Experience => (long)(_executor.ScriptStats?.Exp ?? Character.Exp);

    public override long MapId => _player.Position().Map;

    public override long PositionX => _player.Position().X;

    public override long PositionY => _player.Position().Y;

    public override bool IsGameMaster => _player.IsGameMaster();

    public override int ArgumentCount => _player.Arguments.Count;

    public override string Argument(int index) => index >= 1 && index <= _player.Arguments.Count ? _player.Arguments[index - 1] : string.Empty;

    public override long OnlinePlayers => _world.OnlinePlayers();

    private Character Character => _executor.ScriptCharacter ?? throw NeedsPlayer();

    public Task FlushAsync(CancellationToken ct) => _effects.FlushAsync(ct);

    public bool TakeCloseRequest()
    {
        var requested = _closeRequested;
        _closeRequested = false;
        return requested;
    }

    public override ScriptValue GetVariable(VariableScope scope, string name) => _world.Variables.Get(scope, OwnerOf(scope), name);

    public override void SetVariable(VariableScope scope, string name, ScriptValue value) => _world.Variables.Set(scope, OwnerOf(scope), name, value);

    public override long CountItem(long itemId) => _executor.CountItemForScript(itemId);

    public override bool GiveItem(long itemId, long count)
    {
        if (_executor.GiveItemForScript(itemId, count) is not { } operations) return false;
        _effects.Queue(ct => _executor.SendItemOperationsAsync(operations, ct));
        return true;
    }

    public override bool TakeItem(long itemId, long count)
    {
        if (_executor.TakeItemForScript(itemId, count) is not { } operations) return false;
        _effects.Queue(ct => _executor.SendItemOperationsAsync(operations, ct));
        return true;
    }

    public override void GiveMoney(long amount)
    {
        if (amount == 0 || !_executor.ChangeMoneyForScript(amount)) return;
        _effects.Queue(ct => _executor.SendMoneyAsync(ct));
    }

    public override bool TakeMoney(long amount)
    {
        if (amount == 0) return true;
        if (!_executor.ChangeMoneyForScript(-amount)) return false;
        _effects.Queue(ct => _executor.SendMoneyAsync(ct));
        return true;
    }

    public override void GiveExperience(long amount)
    {
        if (amount > 0) _effects.Queue(ct => _executor.GrantProgressForScript(amount, 0, ct));
    }

    public override void GiveTechniqueExperience(long amount)
    {
        if (amount > 0) _effects.Queue(ct => _executor.GrantProgressForScript(0, amount, ct));
    }

    public override void Warp(long mapId, long x, long y)
    {
        if (mapId is < 0 or > ushort.MaxValue || x is < 0 or > ushort.MaxValue || y is < 0 or > ushort.MaxValue)
        {
            throw new ScriptRuntimeException("A warp needs a map, x and y between 0 and 65535");
        }
        _effects.Queue(ct => _executor.RequestZoneChangeForScript((ushort)mapId, (ushort)x, (ushort)y, ct));
        _closeRequested = true;
    }

    public override bool QuestActive(long questId) => _executor.QuestActiveForScript(questId);

    public override bool QuestDone(long questId) => _executor.QuestDoneForScript(questId);

    public override void Tell(string text) => _effects.Queue(ct => _player.Tell(text, ct));

    public override void Announce(string text) => _effects.Queue(ct => _world.Announce(text, ct));

    public override void RunNpcFunction(long functionId, string? table)
    {
        if (functionId is < 1 or > NpcScript.MaxFunctionId || !NpcScriptExecutor.IsWindowFunction((uint)functionId))
        {
            throw new ScriptRuntimeException($"NPC function {functionId} cannot be called from a script; only functions that open a window are available");
        }
        if (NpcScriptExecutor.TakesTable((uint)functionId) != !string.IsNullOrEmpty(table))
        {
            throw new ScriptRuntimeException(NpcScriptExecutor.TakesTable((uint)functionId)
                ? $"NPC function {functionId} needs the name of its table as a second argument"
                : $"NPC function {functionId} takes no table name");
        }
        _effects.Queue(ct => _executor.OpenWindowForScriptAsync((uint)functionId, table, ct));
        _closeRequested = true;
    }

    public override void TriggerEvent(string name) => _world.TriggerEvent(name);

    public override long Random(long minimum, long maximum) => _world.Random(minimum, maximum);

    private ulong OwnerOf(VariableScope scope) => scope switch
    {
        VariableScope.Character => Character.CharacterId,
        VariableScope.Account => Character.OwnerAccountId,
        _ => ScriptVariables.ServerOwner,
    };
}
