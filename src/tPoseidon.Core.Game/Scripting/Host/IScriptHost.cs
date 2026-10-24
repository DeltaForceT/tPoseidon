using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.Game.Scripting.Host;

public sealed class ScriptRuntimeException : Exception
{
    public ScriptRuntimeException(string message)
        : base(message)
    {
    }
}

public interface IScriptHost
{
    bool HasPlayer { get; }

    DateTimeOffset Now { get; }

    string PlayerName { get; }

    long Level { get; }

    long TechniqueLevel { get; }

    long JobTier { get; }

    long JobType { get; }

    long CharacterClass { get; }

    long Money { get; }

    long Experience { get; }

    long MapId { get; }

    long PositionX { get; }

    long PositionY { get; }

    bool IsGameMaster { get; }

    int ArgumentCount { get; }

    string Argument(int index);

    long OnlinePlayers { get; }

    ScriptValue GetVariable(VariableScope scope, string name);

    void SetVariable(VariableScope scope, string name, ScriptValue value);

    long CountItem(long itemId);

    bool GiveItem(long itemId, long count);

    bool TakeItem(long itemId, long count);

    void GiveMoney(long amount);

    bool TakeMoney(long amount);

    void GiveExperience(long amount);

    void GiveTechniqueExperience(long amount);

    void Warp(long mapId, long x, long y);

    bool QuestActive(long questId);

    bool QuestDone(long questId);

    void Tell(string text);

    void Announce(string text);

    void RunNpcFunction(long functionId, string? table);

    void TriggerEvent(string name);

    long Random(long minimum, long maximum);
}

public abstract class ScriptHostBase : IScriptHost
{
    public virtual bool HasPlayer => false;

    public virtual DateTimeOffset Now => DateTimeOffset.Now;

    public virtual string PlayerName => throw NeedsPlayer();

    public virtual long Level => throw NeedsPlayer();

    public virtual long TechniqueLevel => throw NeedsPlayer();

    public virtual long JobTier => throw NeedsPlayer();

    public virtual long JobType => throw NeedsPlayer();

    public virtual long CharacterClass => throw NeedsPlayer();

    public virtual long Money => throw NeedsPlayer();

    public virtual long Experience => throw NeedsPlayer();

    public virtual long MapId => throw NeedsPlayer();

    public virtual long PositionX => throw NeedsPlayer();

    public virtual long PositionY => throw NeedsPlayer();

    public virtual bool IsGameMaster => throw NeedsPlayer();

    public virtual int ArgumentCount => 0;

    public virtual string Argument(int index) => string.Empty;

    public virtual long OnlinePlayers => 0;

    public abstract ScriptValue GetVariable(VariableScope scope, string name);

    public abstract void SetVariable(VariableScope scope, string name, ScriptValue value);

    public virtual long CountItem(long itemId) => throw NeedsPlayer();

    public virtual bool GiveItem(long itemId, long count) => throw NeedsPlayer();

    public virtual bool TakeItem(long itemId, long count) => throw NeedsPlayer();

    public virtual void GiveMoney(long amount) => throw NeedsPlayer();

    public virtual bool TakeMoney(long amount) => throw NeedsPlayer();

    public virtual void GiveExperience(long amount) => throw NeedsPlayer();

    public virtual void GiveTechniqueExperience(long amount) => throw NeedsPlayer();

    public virtual void Warp(long mapId, long x, long y) => throw NeedsPlayer();

    public virtual bool QuestActive(long questId) => throw NeedsPlayer();

    public virtual bool QuestDone(long questId) => throw NeedsPlayer();

    public virtual void Tell(string text) => throw NeedsPlayer();

    public virtual void Announce(string text)
    {
    }

    public virtual void RunNpcFunction(long functionId, string? table) => throw NeedsPlayer();

    public virtual void TriggerEvent(string name)
    {
    }

    public virtual long Random(long minimum, long maximum) => System.Random.Shared.NextInt64(minimum, maximum + 1);

    protected static ScriptRuntimeException NeedsPlayer() => new("This script step needs a player, but the script was started by the server");
}
