using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Progression;

namespace tPoseidon.Core.Net.World;

public sealed record ProfileGear(
    IReadOnlyList<long> Totals,
    IReadOnlyList<int> Table,
    IReadOnlyList<short> Points,
    IReadOnlyList<int> Resists,
    IReadOnlyList<ProfileViewWorn> Worn,
    IReadOnlyList<int>? AbilityShares = null)
{
    public static ProfileGear Empty { get; } = new([], [], [], [], []);

    public int AbilityShareOf(int stat) => AbilityShares is { } shares && stat < shares.Count ? shares[stat] : 0;
}

public sealed record ProfileSource(
    uint Flags,
    string Name,
    ushort EntityId,
    byte FType,
    ushort CharacterType,
    ushort Level,
    ushort TmLevel,
    byte Job,
    string GuildName,
    ProfileTitle Title,
    long Kills,
    long Deaths,
    string Message,
    IReadOnlyList<short> GrowthWeights,
    ProfileGear Gear,
    IReadOnlyList<BossKillHistoryRecord>? BossKills = null);

public static class ProfileViewBuilder
{
    public static ProfileView Build(ProfileSource source, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(text);
        ProfileViewInfo? info = null;
        ProfileViewEquipment? equipment = null;
        if (ProfileViewRules.Shows(source.Flags, ProfileViewRules.ShowInfo))
        {
            info = new ProfileViewInfo(
                source.CharacterType, source.Level, source.TmLevel, source.Job, source.GuildName, source.Title,
                unchecked((ushort)source.Kills), unchecked((ushort)source.Deaths),
                ProfileViewRules.MessageBytes(source.Message, text), source.GrowthWeights);
            if (ProfileViewRules.Shows(source.Flags, ProfileViewRules.ShowEquipment)) equipment = Equipment(source);
        }
        return new ProfileView(source.Flags, source.Name, source.EntityId, source.FType, info, equipment, source.BossKills);
    }

    private static ProfileViewEquipment Equipment(ProfileSource source)
    {
        var gear = source.Gear;
        var totals = gear.Totals.Select(ProfileViewRules.Total).ToArray();
        var bonuses = gear.Totals.Select((total, stat) => ProfileViewRules.Bonus(total, gear.Table[stat], gear.AbilityShareOf(stat))).ToArray();
        var levels = gear.Points.Select(points => ProfileViewRules.StatLevelOf(points)).ToArray();
        var remainders = gear.Points.Select(points => ProfileViewRules.RemainderOf(points)).ToArray();
        var resists = gear.Resists.Select(resist => (short)Math.Clamp(resist, short.MinValue, short.MaxValue)).ToArray();
        return new ProfileViewEquipment(totals, bonuses, levels, remainders, resists, gear.Worn);
    }
}
