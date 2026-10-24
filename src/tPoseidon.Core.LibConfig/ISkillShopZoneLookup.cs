namespace tPoseidon.Core.LibConfig;

public interface ISkillShopZoneLookup
{
    IReadOnlyList<SkillShopZoneEntry> GetByMap(string mapName);
    IReadOnlyCollection<SkillShopZoneEntry> All { get; }
    int Count { get; }
}
