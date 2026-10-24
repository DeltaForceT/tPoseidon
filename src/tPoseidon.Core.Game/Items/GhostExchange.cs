using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct GhostStoneRequest(ulong SourceWireUid, ushort Count, uint TargetItemId);

public readonly record struct GhostNeoRequest(ulong SourceWireUid, uint TargetItemId);

public static class GhostExchangeRules
{
    public const byte Ok = 0;
    public const byte InvalidPlayerState = 1;
    public const byte StoneInvalidSource = 2;
    public const byte StoneInvalidCount = 3;
    public const byte StoneNotExchangeable = 4;
    public const byte StoneInvalidTarget = 5;
    public const byte StoneNoTargetInfo = 6;
    public const byte NeoNoSource = 2;
    public const byte NeoInvalidSource = 3;
    public const byte NeoInvalidTarget = 4;
    public const byte NeoNoTargetInfo = 5;
    public const byte NeoDifferentKind = 6;
    public const byte InventoryFull = 7;
    public const byte OverWeight = 8;
    public const byte Unknown = 9;

    public const int NeoClass = 2;

    public static IReadOnlyList<int> Targets(GhostExchangeTables tables, ItemParamEntry source)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(source);
        if (source.GroupId == 0 || !tables.Groups.TryGetValue(source.GroupId, out var group)) return Array.Empty<int>();
        return group.TakeWhile(id => id != 0).Where(id => id != source.Id).ToList();
    }

    public static bool SameKind(ItemParamEntry source, ItemParamEntry target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        return source.Class == target.Class && source.Type == target.Type;
    }

    public static byte CheckStone(GhostExchangeTables tables, ItemParamEntry? source, int heldCount, GhostStoneRequest request, ItemParamEntry? target)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (source is null) return StoneInvalidSource;
        if (request.Count == 0 || heldCount < request.Count) return StoneInvalidCount;
        if (source.GroupId == 0 || !tables.Groups.ContainsKey(source.GroupId)) return StoneNotExchangeable;
        if (!Targets(tables, source).Contains((int)request.TargetItemId)) return StoneInvalidTarget;
        if (target is null) return StoneNoTargetInfo;
        return SameKind(source, target) ? Ok : StoneInvalidTarget;
    }

    public static byte CheckNeo(GhostExchangeTables tables, bool sourceHeld, ItemParamEntry? source, GhostNeoRequest request, ItemParamEntry? target)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (!sourceHeld) return NeoNoSource;
        if (source is null || source.Class != NeoClass || source.GroupId == 0 || !tables.Groups.ContainsKey(source.GroupId)) return NeoInvalidSource;
        if (!Targets(tables, source).Contains((int)request.TargetItemId)) return NeoInvalidTarget;
        if (target is null) return NeoNoTargetInfo;
        return SameKind(source, target) ? Ok : NeoDifferentKind;
    }

    public static int? RefineLevelAfter(GhostExchangeTables tables, int refineLevel)
    {
        ArgumentNullException.ThrowIfNull(tables);
        return refineLevel >= 0 && refineLevel < tables.RefineLevels.Count ? tables.RefineLevels[refineLevel] : null;
    }
}
