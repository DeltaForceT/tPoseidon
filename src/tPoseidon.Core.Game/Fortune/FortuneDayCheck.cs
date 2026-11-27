using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Fortune;

public sealed class FortuneDayCheck
{
    public const int ClearsAfterHour = 12;

    public bool Done { get; private set; }

    public FortuneInfo Enter(FortuneInfo info, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (Done) return info;
        Done = true;
        return FortuneHouse.IsAnotherDay(info.UpdatedAt, now) ? info with { ViewCount = 0 } : info;
    }

    public void Tick(DateTimeOffset now)
    {
        if (Done && now.ToLocalTime().Hour > ClearsAfterHour) Done = false;
    }

    public void Release() => Done = false;
}
