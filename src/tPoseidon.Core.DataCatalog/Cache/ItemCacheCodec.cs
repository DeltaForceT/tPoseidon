using System.Text;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Cache;

internal static class ItemCacheCodec
{
    private const int StatCount = 12;

    public static void Write(BinaryWriter writer, IReadOnlyList<IReadOnlyList<ItemParamEntry>> tables)
    {
        var strings = new Dictionary<string, int>(StringComparer.Ordinal);
        var ordered = new List<string>();
        int Intern(string value)
        {
            if (strings.TryGetValue(value, out var index)) return index;
            index = ordered.Count;
            strings[value] = index;
            ordered.Add(value);
            return index;
        }

        var body = new MemoryStream();
        using (var rows = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
        {
            rows.Write(tables.Count);
            foreach (var table in tables)
            {
                rows.Write(table.Count);
                foreach (var item in table) WriteRow(rows, item, Intern);
            }
        }

        writer.Write(ordered.Count);
        foreach (var value in ordered) writer.Write(value);
        writer.Write(body.GetBuffer(), 0, (int)body.Length);
    }

    public static IReadOnlyList<IReadOnlyList<ItemParamEntry>> Read(BinaryReader reader)
    {
        var strings = new string[reader.ReadInt32()];
        for (var index = 0; index < strings.Length; index++) strings[index] = reader.ReadString();
        var tables = new List<IReadOnlyList<ItemParamEntry>>(reader.ReadInt32());
        var tableCount = tables.Capacity;
        for (var table = 0; table < tableCount; table++)
        {
            var rows = new List<ItemParamEntry>(reader.ReadInt32());
            var rowCount = rows.Capacity;
            for (var row = 0; row < rowCount; row++) rows.Add(ReadRow(reader, strings));
            tables.Add(rows);
        }
        return tables;
    }

    private static void WriteRow(BinaryWriter w, ItemParamEntry item, Func<string, int> intern)
    {
        w.Write(item.Id);
        w.Write(item.Class);
        w.Write(item.Type);
        w.Write(item.SubType);
        w.Write(intern(item.Name));
        w.Write(intern(item.NameEng));
        w.Write(item.MinLevel);
        w.Write(item.Weight);
        w.Write(item.Value);
        w.Write(intern(item.FileName));
        w.Write(item.BundleNum);
        w.Write(intern(item.InvFileName));
        w.Write(item.InvBundleNum);
        w.Write(intern(item.EquipFileName));
        w.Write(intern(item.PartFileName));
        w.Write(item.PivotId);
        w.Write(item.HideHat);
        w.Write(item.Effect);
        w.Write(item.ExistType);
        w.Write(item.Options);
        w.Write(item.Life);
        w.Write(item.CardNum);
        w.Write(item.ItemFType);
        w.Write(item.Hp);
        w.Write(item.HpCon);
        w.Write(item.MpCon);
        w.Write(item.Ap);
        w.Write(item.HpRecoveryRate);
        w.Write(item.MpRecoveryRate);
        w.Write(item.RefineType);
        w.Write(item.RefineIndex);
        w.Write(item.CompoundSlot);
        w.Write(item.Mp);
        w.Write(item.Money);
        w.Write(item.ExtendedOptions);
        w.Write(item.CharacterTypeFlags);
        w.Write(item.CharacterGroupFlags);
        w.Write(item.CharacterGender);
        w.Write(item.MinStatType);
        w.Write(item.MinStatLevel);
        for (var index = 0; index < StatCount; index++) w.Write(index < item.StatBonuses.Count ? item.StatBonuses[index] : 0);
        w.Write(item.GroupId);
        w.Write(item.ReformCount);
    }

    private static ItemParamEntry ReadRow(BinaryReader r, string[] strings)
    {
        var id = r.ReadInt32();
        var itemClass = r.ReadInt32();
        var type = r.ReadInt32();
        var subType = r.ReadInt32();
        var name = strings[r.ReadInt32()];
        var nameEng = strings[r.ReadInt32()];
        var minLevel = r.ReadInt32();
        var weight = r.ReadInt32();
        var value = r.ReadInt32();
        var fileName = strings[r.ReadInt32()];
        var bundleNum = r.ReadInt32();
        var invFileName = strings[r.ReadInt32()];
        var invBundleNum = r.ReadInt32();
        var equipFileName = strings[r.ReadInt32()];
        var partFileName = strings[r.ReadInt32()];
        var pivotId = r.ReadInt32();
        var hideHat = r.ReadInt32();
        var effect = r.ReadInt32();
        var existType = r.ReadInt32();
        var options = r.ReadInt32();
        var life = r.ReadInt32();
        var cardNum = r.ReadInt32();
        var itemFType = r.ReadInt32();
        var hp = r.ReadInt32();
        var hpCon = r.ReadInt32();
        var mpCon = r.ReadInt32();
        var ap = r.ReadInt32();
        var hpRecoveryRate = r.ReadSingle();
        var mpRecoveryRate = r.ReadSingle();
        var refineType = r.ReadInt32();
        var refineIndex = r.ReadInt32();
        var compoundSlot = r.ReadInt32();
        var mp = r.ReadInt32();
        var money = r.ReadInt32();
        var extendedOptions = r.ReadInt32();
        var characterTypeFlags = r.ReadInt32();
        var characterGroupFlags = r.ReadInt32();
        var characterGender = r.ReadInt32();
        var minStatType = r.ReadInt32();
        var minStatLevel = r.ReadInt32();
        var stats = new int[StatCount];
        for (var index = 0; index < StatCount; index++) stats[index] = r.ReadInt32();
        var groupId = r.ReadInt32();
        var reformCount = r.ReadInt32();
        return new ItemParamEntry
        {
            Id = id,
            Class = itemClass,
            Type = type,
            SubType = subType,
            Name = name,
            NameEng = nameEng,
            MinLevel = minLevel,
            Weight = weight,
            Value = value,
            FileName = fileName,
            BundleNum = bundleNum,
            InvFileName = invFileName,
            InvBundleNum = invBundleNum,
            EquipFileName = equipFileName,
            PartFileName = partFileName,
            PivotId = pivotId,
            HideHat = hideHat,
            Effect = effect,
            ExistType = existType,
            Options = options,
            Life = life,
            CardNum = cardNum,
            ItemFType = itemFType,
            Hp = hp,
            HpCon = hpCon,
            MpCon = mpCon,
            Ap = ap,
            HpRecoveryRate = hpRecoveryRate,
            MpRecoveryRate = mpRecoveryRate,
            RefineType = refineType,
            RefineIndex = refineIndex,
            CompoundSlot = compoundSlot,
            Mp = mp,
            Money = money,
            ExtendedOptions = extendedOptions,
            CharacterTypeFlags = characterTypeFlags,
            CharacterGroupFlags = characterGroupFlags,
            CharacterGender = characterGender,
            MinStatType = minStatType,
            MinStatLevel = minStatLevel,
            StatBonuses = stats,
            GroupId = groupId,
            ReformCount = reformCount,
        };
    }
}
