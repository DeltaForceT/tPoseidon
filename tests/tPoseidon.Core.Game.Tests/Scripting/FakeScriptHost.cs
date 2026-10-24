using tPoseidon.Core.Game.Scripting;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.GameTests.Scripting;

internal sealed class FakeScriptHost : ScriptHostBase
{
    private readonly Dictionary<(VariableScope, string), ScriptValue> _variables = new();

    public Dictionary<long, long> Items { get; } = new();

    public List<string> Told { get; } = new();

    public List<string> Announced { get; } = new();

    public List<(long Map, long X, long Y)> Warps { get; } = new();

    public List<long> NpcFunctions { get; } = new();

    public List<string> Triggered { get; } = new();

    public List<string> Arguments { get; } = new();

    public override bool HasPlayer => true;

    public override DateTimeOffset Now { get; } = new(2026, 10, 22, 20, 30, 0, TimeSpan.Zero);

    public override string PlayerName => "Tester";

    public override long Level { get; } = 42;

    public override long TechniqueLevel { get; } = 7;

    public override long JobTier { get; } = 2;

    public override long JobType { get; } = 11;

    public override long CharacterClass { get; } = 3;

    private long _money;

    public override long Money => _money;

    public override long Experience { get; } = 1000;

    public override long MapId { get; } = 1;

    public override long PositionX { get; } = 100;

    public override long PositionY { get; } = 200;

    public bool GameMaster { get; set; }

    public override bool IsGameMaster => GameMaster;

    public long GrantedExperience { get; private set; }

    public long GrantedTechnique { get; private set; }

    public override int ArgumentCount => Arguments.Count;

    public override string Argument(int index) => index >= 1 && index <= Arguments.Count ? Arguments[index - 1] : string.Empty;

    public override ScriptValue GetVariable(VariableScope scope, string name) =>
        _variables.TryGetValue((scope, name), out var value) ? value : ScriptValue.Zero;

    public override void SetVariable(VariableScope scope, string name, ScriptValue value) => _variables[(scope, name)] = value;

    public override long CountItem(long itemId) => Items.GetValueOrDefault(itemId);

    public override bool GiveItem(long itemId, long count)
    {
        Items[itemId] = CountItem(itemId) + count;
        return true;
    }

    public override bool TakeItem(long itemId, long count)
    {
        if (CountItem(itemId) < count) return false;
        Items[itemId] = CountItem(itemId) - count;
        return true;
    }

    public override void GiveMoney(long amount) => _money += amount;

    public override bool TakeMoney(long amount)
    {
        if (_money < amount) return false;
        _money -= amount;
        return true;
    }

    public override void GiveExperience(long amount) => GrantedExperience += amount;

    public override void GiveTechniqueExperience(long amount) => GrantedTechnique += amount;

    public override void Warp(long mapId, long x, long y) => Warps.Add((mapId, x, y));

    public override bool QuestActive(long questId) => questId == 5;

    public override bool QuestDone(long questId) => questId == 6;

    public override void Tell(string text) => Told.Add(text);

    public override void Announce(string text) => Announced.Add(text);

    public override void RunNpcFunction(long functionId, string? table) => NpcFunctions.Add(functionId);

    public override void TriggerEvent(string name) => Triggered.Add(name);

    public override long Random(long minimum, long maximum) => minimum;
}
