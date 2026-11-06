using System.Text;

namespace tPoseidon.Tools.DataBench;

internal static class SyntheticFile
{
    private static readonly string[] ItemColumns =
    {
        "ID", "Class", "Type", "SubType", "Name", "Name_Eng", "MinLevel", "Weight", "Value", "FileName", "BundleNum", "InvFileName",
        "InvBundleNum", "EquipFileName", "PartFileName", "PivotID", "HideHat", "Effect", "ExistType", "HP", "HPCon", "MPCon", "AP",
        "HPRecoveryRate", "MPRecoveryRate", "RefineType", "RefineIndex", "CompoundSlot", "MP", "Money", "Options", "Life", "CardNum",
        "ItemFType", "OptionsEx", "ChrTypeFlags", "ChrFTypeFlag", "ChrGender", "MinStatType", "MinStatLv", "APPlus", "ACPlus", "DXPlus",
        "MaxMPPlus", "MAPlus", "MDPlus", "MaxWTPlus", "DAPlus", "LKPlus", "MaxHPPlus", "DPPlus", "HVPlus", "GroupId", "ReformCount",
    };

    public static string Write(string directory)
    {
        var path = Path.Combine(directory, "synthetic-libconfig.xml");
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        writer.WriteLine("<LIBCONFIG>");
        Items(writer, "ItemParam2", 6500, 0);
        Items(writer, "ItemParamEx", 1100, 7000);
        Items(writer, "ItemParamEx2", 2600, 9000);
        Items(writer, "ItemParamCM2", 6200, 60000);
        Rows(writer, "MonsterParamEx2", 1500, new[] { "ID", "Name", "Level", "HP", "MP", "AP", "AC", "ProtectTable" });
        Rows(writer, "MonItemDropInfo", 800, new[] { "ID", "Item0", "Rate0", "Cnt0", "Item1", "Rate1", "Cnt1" });
        Rows(writer, "QuestInfo", 1185, new[] { "Id", "Name", "Client", "MinLevel", "MaxLevel", "Exp", "SrcItem0", "Cnt0" });
        Rows(writer, "ScenarioQuestInfo", 189, new[] { "Id", "Name", "Client", "MinLevel", "MaxLevel", "Exp", "SrcItem0", "Cnt0" });
        Rows(writer, "MapInfoEx", 700, new[] { "ID", "Name", "FileName", "NpcTable", "MonsterTable", "ItemTable", "ShopTable" });
        Rows(writer, "SkillParam2", 320, new[] { "ID", "Type", "Name", "MaxLevel" });
        Rows(writer, "Item_SkillCardParam", 400, new[] { "ID", "Name", "SCardType", "TMLV" });
        Rows(writer, "CharacterInfo", 200, new[] { "ID", "CommonName", "Type" });
        for (var map = 0; map < 120; map++)
        {
            Rows(writer, $"NpcTalk_map{map}", 25, new[] { "Type", "PosId", "MsgTable", "TeleportId", "QuestId" });
            Rows(writer, $"NpcMsg_npc{map}", 40, new[] { "No", "NpcSType", "Message" });
            Rows(writer, $"Mon_map{map}", 15, new[] { "MonsterID", "Avr", "Max", "Ratio", "Region" });
            Rows(writer, $"RegionInfo_map{map}", 8, new[] { "ZoneID", "LTcoordX", "LTcoordY", "RBcoordX", "RBcoordY" });
        }
        writer.WriteLine("</LIBCONFIG>");
        return path;
    }

    private static void Items(StreamWriter writer, string table, int count, int firstId)
    {
        writer.WriteLine($"<TABLE name=\"{table}\">");
        for (var row = 0; row < count; row++)
        {
            writer.Write("<ROW>");
            foreach (var column in ItemColumns)
            {
                var value = column switch
                {
                    "ID" => (firstId + row).ToString(),
                    "Name" or "Name_Eng" => $"Item {firstId + row}",
                    "FileName" or "InvFileName" or "EquipFileName" or "PartFileName" => $"item{row % 300}.nri",
                    _ => (row % 7).ToString(),
                };
                writer.Write($"<{column}>{value}</{column}>");
            }
            writer.WriteLine("</ROW>");
        }
        writer.WriteLine("</TABLE>");
    }

    private static void Rows(StreamWriter writer, string table, int count, string[] columns)
    {
        writer.WriteLine($"<TABLE name=\"{table}\">");
        for (var row = 0; row < count; row++)
        {
            writer.Write("<ROW>");
            foreach (var column in columns)
            {
                var text = column.Contains("Name", StringComparison.Ordinal) || column == "Message" || column == "Client" || column.EndsWith("Table", StringComparison.Ordinal);
                var value = text ? $"{column}{row}" : (row + 1).ToString();
                writer.Write($"<{column}>{value}</{column}>");
            }
            writer.WriteLine("</ROW>");
        }
        writer.WriteLine("</TABLE>");
    }
}
