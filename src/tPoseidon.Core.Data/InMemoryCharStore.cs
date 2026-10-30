using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public sealed class InMemoryCharStore : ICharStore
{
    private readonly ConcurrentDictionary<ulong, Character> _byId = new();

    public IReadOnlyList<Character> GetCharactersForAccount(uint accountId)
    {
        var list = new List<Character>();
        foreach (var c in _byId.Values)
        {
            if (c.OwnerAccountId == accountId) list.Add(c);
        }
        list.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        return list;
    }

    public Character? GetCharacterById(ulong characterId) =>
        _byId.TryGetValue(characterId, out var c) ? c : null;

    public void AddCharacter(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (!_byId.TryAdd(character.CharacterId, character))
        {
            throw new InvalidOperationException(
                $"Cannot add character {character.CharacterId}: already present.");
        }
    }

    public bool DeleteCharacter(ulong characterId, uint ownerAccountId)
    {
        if (!_byId.TryGetValue(characterId, out var existing)) return false;
        if (existing.OwnerAccountId != ownerAccountId) return false;
        return _byId.TryRemove(characterId, out _);
    }

    public bool SetCharacterMap(ulong characterId, ushort mapId)
    {
        if (!_byId.TryGetValue(characterId, out var existing)) return false;
        var updated = CloneWith(existing, mapId: mapId);
        return _byId.TryUpdate(characterId, updated, existing);
    }

    public bool SetCharacterHome(ulong characterId, ushort homeMapId)
    {
        if (!_byId.TryGetValue(characterId, out var existing)) return false;
        var updated = CloneWith(existing, home: homeMapId);
        return _byId.TryUpdate(characterId, updated, existing);
    }

    public bool SetCharacterPosition(ulong characterId, ushort mapId, ushort positionX, ushort positionY)
    {
        if (!_byId.TryGetValue(characterId, out var existing)) return false;
        var updated = CloneWith(existing, mapId: mapId, positionX: positionX, positionY: positionY);
        return _byId.TryUpdate(characterId, updated, existing);
    }

    public bool SaveCharacterStats(ulong characterId, CharacterStatsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!_byId.TryGetValue(characterId, out var existing)) return false;
        var updated = CloneWith(existing, stats: snapshot);
        return _byId.TryUpdate(characterId, updated, existing);
    }

    public IReadOnlyCollection<string> ForbiddenNames { get; set; } = Array.Empty<string>();

    public uint EditCharacter(ulong characterId, CharacterEdit edit) =>
        InMemoryCharacterEdits.Apply(_byId, characterId, edit, ForbiddenNames);

    private static ushort HairSlotValue(CharacterEdit? edit, int slot, ushort current)
    {
        if (edit is null) return current;
        return edit.Kind switch
        {
            CharacterEditKind.Hair when edit.HairSlot == slot => edit.HairColor,
            CharacterEditKind.Gender when slot == 1 => 0,
            _ => current,
        };
    }

    internal static Character CloneWith(
        Character src,
        ushort? mapId = null,
        ushort? positionX = null,
        ushort? positionY = null,
        CharacterStatsSnapshot? stats = null,
        ushort? home = null,
        CharacterEdit? edit = null) => new()
    {
        CharacterId = src.CharacterId,
        OwnerAccountId = src.OwnerAccountId,
        Slot = src.Slot,
        Name = edit?.Kind == CharacterEditKind.Rename ? edit.NewName : src.Name,
        AccountLabel = src.AccountLabel,
        Species = edit?.Kind == CharacterEditKind.Gender ? edit.NewSpecies : src.Species,
        FType = src.FType,
        PtsP = edit?.Kind == CharacterEditKind.GrowthWeights ? edit.PtsP : src.PtsP,
        PtsM = edit?.Kind == CharacterEditKind.GrowthWeights ? edit.PtsM : src.PtsM,
        PtsS = edit?.Kind == CharacterEditKind.GrowthWeights ? edit.PtsS : src.PtsS,
        PtsC = edit?.Kind == CharacterEditKind.GrowthWeights ? edit.PtsC : src.PtsC,
        JobTier = src.JobTier,
        Hair = HairSlotValue(edit, 1, src.Hair),
        Money = stats?.Money ?? src.Money,
        Level = stats?.Level ?? src.Level,
        StarterEarItemIndex = edit?.Kind == CharacterEditKind.Gender ? edit.EarItemIndex : src.StarterEarItemIndex,
        StarterTailItemIndex = edit?.Kind == CharacterEditKind.Gender ? edit.TailItemIndex : src.StarterTailItemIndex,
        PositionX = positionX ?? src.PositionX,
        PositionY = positionY ?? src.PositionY,
        MapId = mapId ?? src.MapId,
        CurrentHp = stats?.CurrentHp ?? src.CurrentHp,
        CurrentMp = stats?.CurrentMp ?? src.CurrentMp,
        Exp = stats?.Exp ?? src.Exp,
        TmExpPower = src.TmExpPower,
        TmExpMagic = src.TmExpMagic,
        TmExpSense = src.TmExpSense,
        TmExpCharm = src.TmExpCharm,
        WarehouseSlots = edit?.Kind == CharacterEditKind.WarehouseSlots ? edit.WarehouseSlots : src.WarehouseSlots,
        LoginTime = src.LoginTime,
        LogoutTime = src.LogoutTime,
        TotalConnectedSeconds = src.TotalConnectedSeconds,
        ApPoints = stats?.ApPoints ?? src.ApPoints,
        AcPoints = stats?.AcPoints ?? src.AcPoints,
        DxPoints = stats?.DxPoints ?? src.DxPoints,
        MaPoints = stats?.MaPoints ?? src.MaPoints,
        MdPoints = stats?.MdPoints ?? src.MdPoints,
        DaPoints = stats?.DaPoints ?? src.DaPoints,
        LkPoints = stats?.LkPoints ?? src.LkPoints,
        DpPoints = stats?.DpPoints ?? src.DpPoints,
        HvPoints = stats?.HvPoints ?? src.HvPoints,
        MaxHpPoints = stats?.MaxHpPoints ?? src.MaxHpPoints,
        MaxMpPoints = stats?.MaxMpPoints ?? src.MaxMpPoints,
        MaxWtPoints = stats?.MaxWtPoints ?? src.MaxWtPoints,
        AttrPoints = stats?.AttrPoints ?? src.AttrPoints,
        TmLevelPower = stats?.TmLevelPower ?? src.TmLevelPower,
        TmLevelMagic = stats?.TmLevelMagic ?? src.TmLevelMagic,
        TmLevelSense = stats?.TmLevelSense ?? src.TmLevelSense,
        TmLevelCharm = stats?.TmLevelCharm ?? src.TmLevelCharm,
        TmPoint = stats?.TmPoint ?? src.TmPoint,
        TmLevel = stats?.TmLevel ?? src.TmLevel,
        TmExp = stats?.TmExp ?? src.TmExp,
        Type2 = src.Type2,
        Job2 = src.Job2,
        Job2ChangedAt = src.Job2ChangedAt,
        Type3 = src.Type3,
        Job3 = src.Job3,
        Job3ChangedAt = src.Job3ChangedAt,
        ChangeJobQuestState = src.ChangeJobQuestState,
        DepthCount = src.DepthCount,
        DigCount = src.DigCount,
        Hair2 = HairSlotValue(edit, 2, src.Hair2),
        Hair3 = HairSlotValue(edit, 3, src.Hair3),
        JobState = src.JobState,
        CharState = src.CharState,
        Home = home ?? src.Home,
        CharAuthority = src.CharAuthority,
        GuildState = src.GuildState,
        IsGuildChief = src.IsGuildChief,
        IsMember = src.IsMember,
        BossKills = src.BossKills,
        GenTime = src.GenTime,
    };
}
