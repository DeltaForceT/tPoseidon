namespace tPoseidon.Core.Game.Quests;

public readonly record struct HuntTick(bool SendsTimer, bool TimedOut);

public sealed record MonsterHuntSnapshot(bool Active, int QuestId, int Grade, int RemainingMs, int AccumulatorMs, int Kills, int Target);

public sealed class MonsterHunt
{
    public bool Active { get; private set; }
    public int QuestId { get; private set; }
    public int Grade { get; set; }
    public int RemainingMs { get; private set; }
    public int AccumulatorMs { get; private set; }
    public int Kills { get; private set; }
    public int Target { get; private set; }

    public bool IsRunningAnother(int questId) => Active && QuestId != questId;

    public void Start(int questId, int remainingMs)
    {
        Active = true;
        RemainingMs = remainingMs;
        AccumulatorMs = 0;
        QuestId = questId;
    }

    public void SetCounter(int kills, int target)
    {
        Kills = kills;
        Target = target;
    }

    public HuntTick Advance(int elapsedMs)
    {
        if (!Active) return default;
        RemainingMs -= elapsedMs;
        AccumulatorMs += elapsedMs;
        var sendsTimer = false;
        if (AccumulatorMs >= MonsterQuests.TimerStepMs)
        {
            AccumulatorMs = 0;
            sendsTimer = true;
        }
        return new HuntTick(sendsTimer, RemainingMs < 1);
    }

    public bool CountKill()
    {
        Kills++;
        return Kills == Target;
    }

    public void Stop()
    {
        Active = false;
        AccumulatorMs = 0;
        RemainingMs = 0;
    }

    public void Reset()
    {
        Stop();
        QuestId = 0;
        Grade = 0;
        Kills = 0;
        Target = 0;
    }

    public MonsterHuntSnapshot Snapshot() => new(Active, QuestId, Grade, RemainingMs, AccumulatorMs, Kills, Target);

    public void Restore(MonsterHuntSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Active = snapshot.Active;
        QuestId = snapshot.QuestId;
        Grade = snapshot.Grade;
        RemainingMs = snapshot.RemainingMs;
        AccumulatorMs = snapshot.AccumulatorMs;
        Kills = snapshot.Kills;
        Target = snapshot.Target;
    }
}
