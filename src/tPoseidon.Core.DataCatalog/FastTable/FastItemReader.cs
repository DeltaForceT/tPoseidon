using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.FastTable;

public static class FastItemReader
{
    private static readonly string[] StatColumns =
    {
        "APPlus", "ACPlus", "DXPlus", "MaxMPPlus", "MAPlus", "MDPlus",
        "MaxWTPlus", "DAPlus", "LKPlus", "MaxHPPlus", "DPPlus", "HVPlus",
    };

    public static IReadOnlyList<ItemParamEntry> Read(byte[] xml, string tableName)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var entries = new List<ItemParamEntry>();
        if (!LibConfigTableIndex.Of(xml).TryFind(tableName, out var span)) return entries;
        Columns? columns = null;
        FastTableReader.Read(xml, span, row => entries.Add(Map(row, columns!)), row => columns = new Columns(row));
        return entries;
    }

    internal static ItemParamEntry Map(RowView row, Columns c)
    {
        var type = row.AsInt(c.Type);
        var stats = new int[StatColumns.Length];
        for (var index = 0; index < stats.Length; index++) stats[index] = row.AsInt(c.Stats[index]);
        return new ItemParamEntry
        {
            Id = row.AsInt(c.Id),
            Class = row.AsInt(c.Class),
            Type = type,
            SubType = row.AsInt(c.SubType),
            Name = row.Str(c.Name),
            NameEng = row.Str(c.NameEng),
            MinLevel = row.AsInt(c.MinLevel),
            Weight = row.AsInt(c.Weight),
            Value = row.AsInt(c.Value),
            FileName = row.Str(c.FileName),
            BundleNum = row.AsInt(c.BundleNum),
            InvFileName = row.Str(c.InvFileName),
            InvBundleNum = row.AsInt(c.InvBundleNum),
            EquipFileName = row.Str(c.EquipFileName),
            PartFileName = row.Str(c.PartFileName),
            PivotId = row.AsInt(c.PivotId),
            HideHat = row.Flags(c.HideHat),
            Effect = row.AsInt(c.Effect),
            ExistType = row.AsInt(c.ExistType),
            Hp = row.AsInt(c.Hp),
            HpCon = row.AsInt(c.HpCon),
            MpCon = row.AsInt(c.MpCon),
            Ap = row.AsInt(c.Ap),
            HpRecoveryRate = row.AsFloat(c.HpRecoveryRate),
            MpRecoveryRate = row.AsFloat(c.MpRecoveryRate),
            RefineType = row.AsInt(c.RefineType),
            RefineIndex = row.AsInt(c.RefineIndex),
            CompoundSlot = row.AsInt(c.CompoundSlot),
            Mp = row.AsInt(c.Mp),
            Money = row.AsInt(c.Money),
            Options = ItemParamEntry.OptionsOf(type, row.AsInt(c.ReformCount), row.Flags(c.Options)),
            Life = row.AsInt(c.Life),
            CardNum = row.AsInt(c.CardNum),
            ItemFType = row.AsInt(c.ItemFType),
            ExtendedOptions = row.Flags(c.OptionsEx),
            CharacterTypeFlags = row.Flags(c.ChrTypeFlags),
            CharacterGroupFlags = row.Flags(c.ChrFTypeFlag),
            CharacterGender = row.AsInt(c.ChrGender),
            MinStatType = row.AsInt(c.MinStatType),
            MinStatLevel = row.AsInt(c.MinStatLv),
            StatBonuses = stats,
            GroupId = row.AsInt(c.GroupId),
            ReformCount = ItemParamEntry.ReformCountOf(type, row.AsInt(c.ReformCount)),
        };
    }

    internal sealed class Columns
    {
        public Columns(RowView row)
        {
            Id = row.Col("ID");
            Class = row.Col("Class");
            Type = row.Col("Type");
            SubType = row.Col("SubType");
            Name = row.Col("Name");
            NameEng = row.Col("Name_Eng");
            MinLevel = row.Col("MinLevel");
            Weight = row.Col("Weight");
            Value = row.Col("Value");
            FileName = row.Col("FileName");
            BundleNum = row.Col("BundleNum");
            InvFileName = row.Col("InvFileName");
            InvBundleNum = row.Col("InvBundleNum");
            EquipFileName = row.Col("EquipFileName");
            PartFileName = row.Col("PartFileName");
            PivotId = row.Col("PivotID");
            HideHat = row.Col("HideHat");
            Effect = row.Col("Effect");
            ExistType = row.Col("ExistType");
            Hp = row.Col("HP");
            HpCon = row.Col("HPCon");
            MpCon = row.Col("MPCon");
            Ap = row.Col("AP");
            HpRecoveryRate = row.Col("HPRecoveryRate");
            MpRecoveryRate = row.Col("MPRecoveryRate");
            RefineType = row.Col("RefineType");
            RefineIndex = row.Col("RefineIndex");
            CompoundSlot = row.Col("CompoundSlot");
            Mp = row.Col("MP");
            Money = row.Col("Money");
            Options = row.Col("Options");
            Life = row.Col("Life");
            CardNum = row.Col("CardNum");
            ItemFType = row.Col("ItemFType");
            OptionsEx = row.Col("OptionsEx");
            ChrTypeFlags = row.Col("ChrTypeFlags");
            ChrFTypeFlag = row.Col("ChrFTypeFlag");
            ChrGender = row.Col("ChrGender");
            MinStatType = row.Col("MinStatType");
            MinStatLv = row.Col("MinStatLv");
            GroupId = row.Col("GroupId");
            ReformCount = row.Col("ReformCount");
            Stats = StatColumns.Select(row.Col).ToArray();
        }

        public Column Id { get; }
        public Column Class { get; }
        public Column Type { get; }
        public Column SubType { get; }
        public Column Name { get; }
        public Column NameEng { get; }
        public Column MinLevel { get; }
        public Column Weight { get; }
        public Column Value { get; }
        public Column FileName { get; }
        public Column BundleNum { get; }
        public Column InvFileName { get; }
        public Column InvBundleNum { get; }
        public Column EquipFileName { get; }
        public Column PartFileName { get; }
        public Column PivotId { get; }
        public Column HideHat { get; }
        public Column Effect { get; }
        public Column ExistType { get; }
        public Column Hp { get; }
        public Column HpCon { get; }
        public Column MpCon { get; }
        public Column Ap { get; }
        public Column HpRecoveryRate { get; }
        public Column MpRecoveryRate { get; }
        public Column RefineType { get; }
        public Column RefineIndex { get; }
        public Column CompoundSlot { get; }
        public Column Mp { get; }
        public Column Money { get; }
        public Column Options { get; }
        public Column Life { get; }
        public Column CardNum { get; }
        public Column ItemFType { get; }
        public Column OptionsEx { get; }
        public Column ChrTypeFlags { get; }
        public Column ChrFTypeFlag { get; }
        public Column ChrGender { get; }
        public Column MinStatType { get; }
        public Column MinStatLv { get; }
        public Column GroupId { get; }
        public Column ReformCount { get; }
        public IReadOnlyList<Column> Stats { get; }
    }
}
