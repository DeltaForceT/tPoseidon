using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting;

public static class ReferencedMapRows
{
    public const string Id = "referenced-map-rows";
    public const string Title = "Add the map rows that the client data points to but does not define";

    public static readonly LibConfigRowPatch MapInfo = new(
        "MapInfoEx",
        "ID",
        ["ID", "Class", "PZType", "Type", "Group", "WMRegion", "Name", "FileName", "MiniMap", "MapSign", "MaxDepth", "MonsterAvrNum", "MonsterTable", "BossMonsterTable", "ItemPercentage", "MaxItem", "MinItemDepth", "MaxItemDepth", "ItemDelaySec", "ItemTable", "ShopPriceRate", "ShopTable", "NpcTable", "MonsterTactics", "BGEffectFileName", "SkillShopTable", "BossMonsterCnt", "BossMonCheckTime", "Option", "SubType", "NoShader"],
        [
            ["137", "2", "0", "5", "12", "0", "Beginner's Square - Monster Guild", "data\\map\\map_sq10_02.md3", " ", "data\\map\\sign\\sign_sq10.nri", "0", "0", " ", " ", "0.000000", "0", "0", "0", "0.000000", " ", "0.000000", " ", "NpcTalk_Tutorial_Monster", " ", " ", " ", "0", "0", "16/256/1024", "0", "0"],
            ["223", "2", "1", "4", "16", "0", "Ice Prison", "data\\map\\map_dg07f.md3", "data\\minimap\\Mini_map_dg07f.nri", " ", "0", "30", "Mon_Event_Dungeon1", " ", "0.000000", "0", "0", "0", "0.000000", " ", "0.000000", " ", "NpcTalk_Event_Dungeon", " ", " ", " ", "0", "0", "16/1024/256", "0", "0"],
        ]);

    public static IReadOnlyList<LibConfigRowPatch> Patches { get; } = [MapInfo];
}
