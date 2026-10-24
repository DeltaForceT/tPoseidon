using tPoseidon.Core.Common;

namespace tPoseidon.Core.Game.Combat;

public sealed class CadenceCalculator
{
    private readonly Func<ServerRates> _ratesAccessor;

    public CadenceCalculator(ServerRates rates) : this(() => rates) { }

    public CadenceCalculator(Func<ServerRates> ratesAccessor)
    {
        ArgumentNullException.ThrowIfNull(ratesAccessor);
        _ratesAccessor = ratesAccessor;
    }

    public int PlayerSwingCadenceMs(int dx)
    {
        var c = _ratesAccessor().Cadence;
        var raw = c.PlayerBaseMs - dx * c.MsPerDxPoint;
        return Math.Clamp(raw, c.MinPlayerMs, c.MaxPlayerMs);
    }

    public int MobStrikeCadenceMs(int mobAttackSpeedStat = 0)
    {
        var c = _ratesAccessor().Cadence;
        var raw = c.MobBaseMs - mobAttackSpeedStat * c.MsPerDxPoint;
        return Math.Clamp(raw, c.MinMobMs, c.MaxMobMs);
    }
}
