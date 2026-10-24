using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Characters;

namespace tPoseidon.Core.Net.Social;

public sealed class GameMasterRates
{
    public const double PercentScale = 0.01;
    public const double SlotEpsilon = 4.9999999e-05;
    public const float AbilityScale = 0.01f;
    public const int AbilitySlots = 13;
    public const int StatSlots = 12;

    private readonly object _gate = new();
    private ushort _expPercent;
    private ushort _tmPercent;
    private readonly ushort[] _abilities = new ushort[AbilitySlots];

    public IReadOnlyList<ushort> AbilityPercents
    {
        get { lock (_gate) return _abilities.ToArray(); }
    }

    public bool SetAbility(byte stat, ushort percent)
    {
        if (stat >= AbilitySlots) return false;
        lock (_gate)
        {
            if (_abilities[stat] == percent) return false;
            _abilities[stat] = percent;
            return true;
        }
    }

    public EffectiveStats WithAbilities(EffectiveStats table)
    {
        var percents = AbilityPercents;
        return new EffectiveStats(
            table.Ap + Share(table.Ap, percents[0]), table.Ac + Share(table.Ac, percents[1]), table.Dx + Share(table.Dx, percents[2]),
            table.Mp + Share(table.Mp, percents[3]), table.Ma + Share(table.Ma, percents[4]), table.Md + Share(table.Md, percents[5]),
            table.Wt + Share(table.Wt, percents[6]), table.Da + Share(table.Da, percents[7]), table.Lk + Share(table.Lk, percents[8]),
            table.Hp + Share(table.Hp, percents[9]), table.Dp + Share(table.Dp, percents[10]), table.Hv + Share(table.Hv, percents[11]));
    }

    public byte[] AbilityPacket()
    {
        var percents = AbilityPercents;
        var w = new PacketWriter(StatSlots * sizeof(ushort));
        for (var stat = 0; stat < StatSlots; stat++) w.WriteUInt16(percents[stat]);
        return w.ToArray();
    }

    public static int Share(int tableValue, ushort percent) =>
        percent == 0 ? 0 : (int)Math.Truncate((double)tableValue * (percent * AbilityScale));

    public (ushort Exp, ushort Tm) ExpPercents
    {
        get { lock (_gate) return (_expPercent, _tmPercent); }
    }

    public bool HasExp
    {
        get { lock (_gate) return _expPercent != 0 || _tmPercent != 0; }
    }

    public bool SetExp(ushort expPercent, ushort tmPercent)
    {
        lock (_gate)
        {
            if (_expPercent == expPercent && _tmPercent == tmPercent) return false;
            _expPercent = expPercent;
            _tmPercent = tmPercent;
            return true;
        }
    }

    public long ScaleExp(long gain) => Scale(gain, ExpPercents.Exp);

    public long ScaleTm(long gain) => Scale(gain, ExpPercents.Tm);

    public float ExpSlotRate => SlotRate(ExpPercents.Exp);

    public float TmSlotRate => SlotRate(ExpPercents.Tm);

    public static float SlotRate(ushort percent)
    {
        if (percent == 0) return 0f;
        var scaled = (float)(percent * PercentScale);
        return (float)(scaled + SlotEpsilon);
    }

    public static long Scale(long gain, ushort percent)
    {
        if (gain <= 0 || percent == 0) return gain;
        var rate = SlotRate(percent);
        if (gain > int.MaxValue) return WireLimits.Saturate(gain + Math.Truncate(gain * (double)rate));
        var sum = (float)gain + MathF.Truncate(gain * rate);
        return sum >= int.MaxValue ? int.MaxValue : (int)sum;
    }
}
