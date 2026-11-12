using tPoseidon.Core.Game.Fiesta;
using tPoseidon.Core.Game.Party;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Warp;

public readonly record struct PortalRegionClick(int CurrentMap, int DestinationMap, int Region, int Level);

public readonly record struct PortalTraveller(int Level, int Job, Func<int, int, bool> HoldsQuest, Func<int, bool> HoldsItem);

public static class PortalClickRules
{
    public const int Accepted = 0;
    public const int Closed = 1;
    public const int LevelRefused = 3;
    public const int DailyLimit = 5;
    public const int FiestaMapClass = 4;
    public const int LowestMap = 1;
    public const int HighestMap = 999;

    public static int Check(
        PortalRegionClick click,
        Func<int, int> mapClassOf,
        PortalLevelBands bands,
        IReadOnlyList<FiestaSlotEntry> slots,
        FiestaEntryRecord entry,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(mapClassOf);
        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(slots);
        if (!IsMap(click.CurrentMap) || !IsMap(click.DestinationMap)) return LevelRefused;
        var enteringFiesta = mapClassOf(click.DestinationMap) == FiestaMapClass && mapClassOf(click.CurrentMap) != FiestaMapClass;
        if (!IsOpen(click, mapClassOf, slots, now)) return Closed;
        if (bands.Find(click.CurrentMap, click.Region) is { Redirects: false } band && !InBand(band, click.Level)) return LevelRefused;
        if (enteringFiesta && FiestaRules.OpenInZone(slots, click.DestinationMap, now) is { } window && !FiestaRules.TicketAllowed(entry, window.SlotStart, now))
        {
            return DailyLimit;
        }
        return Accepted;
    }

    public static PartyFollowRefusal? FollowRefusal(
        PortalRegionClick click,
        Func<int, int> mapClassOf,
        PortalLevelBands bands,
        IReadOnlyList<FiestaSlotEntry> slots,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(mapClassOf);
        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(slots);
        if (!IsOpen(click, mapClassOf, slots, now)) return PartyFollowRefusal.PortalClosed;
        if (bands.Find(click.CurrentMap, click.Region) is { Redirects: false } band && !InBand(band, click.Level)) return PartyFollowRefusal.IsPortalLimit;
        return null;
    }

    public static int? Redirect(int map, int region, PortalLevelBands bands, PortalTraveller traveller)
    {
        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(traveller.HoldsQuest);
        ArgumentNullException.ThrowIfNull(traveller.HoldsItem);
        if (bands.Find(map, region) is not { Redirects: true } band || !IsMap(band.DestZone)) return null;
        if (!InBand(band, traveller.Level)) return null;
        if ((band.JobType1 != 0 || band.JobType2 != 0) && NpcCheckRules.Job(traveller.Job, band.JobType1, band.JobType2) != NpcCheckRules.Allowed) return null;
        if (band.QuestId != 0 && !traveller.HoldsQuest(band.QuestType, band.QuestId)) return null;
        if (band.ItemId != 0 && !traveller.HoldsItem(band.ItemId)) return null;
        return band.DestZone;
    }

    private static bool InBand(PortalLevelBand band, int level) => level >= band.MinLevel && level <= band.MaxLevel;

    private static bool IsMap(int map) => map is >= LowestMap and <= HighestMap;

    private static bool IsOpen(PortalRegionClick click, Func<int, int> mapClassOf, IReadOnlyList<FiestaSlotEntry> slots, DateTime now)
    {
        if (mapClassOf(click.DestinationMap) != FiestaMapClass) return true;
        var leaving = mapClassOf(click.CurrentMap) == FiestaMapClass;
        var zone = leaving ? click.CurrentMap : click.DestinationMap;
        if (FiestaRules.OpenInZone(slots, zone, now) is not { } window) return false;
        if (!leaving) return true;
        return click.Region == window.Row.PortalIndex && window.Elapsed >= TimeSpan.FromMinutes(window.Row.PortalOpenMinute);
    }
}
