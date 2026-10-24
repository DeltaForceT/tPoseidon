using System.Collections.Concurrent;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace tPoseidon.Core.LibConfig;

public sealed class LibConfigXmlReader
{
    private static readonly ConcurrentDictionary<string, Lazy<byte[]>> DecodedXmlCache =
        new(StringComparer.Ordinal);

    private readonly byte[] _decodedXmlBytes;

    private readonly TranslationOverrides _overrides;

    public LibConfigXmlReader(string xmlOrDatPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlOrDatPath);
        _decodedXmlBytes = GetOrLoadDecodedXmlBytes(xmlOrDatPath);
        _overrides = TranslationOverrides.ForLibConfigSource(xmlOrDatPath);
    }

    private static byte[] GetOrLoadDecodedXmlBytes(string xmlOrDatPath)
    {
        var absolutePath = Path.GetFullPath(xmlOrDatPath);
        var info = new FileInfo(absolutePath);
        var cacheKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{absolutePath}|{info.LastWriteTimeUtc.Ticks}|{info.Length}");

        var lazy = DecodedXmlCache.GetOrAdd(
            cacheKey,
            _ => new Lazy<byte[]>(
                () => DecodeXmlBytes(absolutePath),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    private static byte[] DecodeXmlBytes(string path)
    {
        if (Path.GetExtension(path).Equals(".dat", StringComparison.OrdinalIgnoreCase)
            && !IsPlaintextXmlFile(path))
        {
            return LibConfigDatDecryptor.DecryptFile(path);
        }

        var bytes = File.ReadAllBytes(path);
        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            return StripNulBytes(bytes);
        }
        return bytes;
    }

    private static bool IsPlaintextXmlFile(string path)
    {
        Span<byte> head = stackalloc byte[5];
        using var fs = File.OpenRead(path);
        var read = fs.Read(head);
        if (read < 5) return false;
        return head[0] == (byte)'<' && head[1] == (byte)'?' && head[2] == (byte)'x' && head[3] == (byte)'m' && head[4] == (byte)'l';
    }

    private static byte[] StripNulBytes(byte[] input)
    {
        var write = 0;
        for (var read = 0; read < input.Length; read++)
        {
            if (input[read] == 0) continue;
            input[write++] = input[read];
        }
        if (write == input.Length) return input;
        var trimmed = new byte[write];
        Array.Copy(input, trimmed, write);
        return trimmed;
    }

    private XmlReader OpenXmlReader(XmlReaderSettings settings)
    {
        var ms = new MemoryStream(_decodedXmlBytes, writable: false);
        return XmlReader.Create(ms, settings);
    }

    public IReadOnlyList<QuestInfoEntry> LoadAllQuestTables()
    {
        var entries = new List<QuestInfoEntry>(1500);
        LoadTable("QuestInfo", row => entries.Add(MapQuestInfoRow("QuestInfo", row)));
        LoadTable("ScenarioQuestInfo", row => entries.Add(MapQuestInfoRow("ScenarioQuestInfo", row)));
        return entries;
    }

    public IReadOnlyList<QuestInfoEntry> LoadQuestInfo()
    {
        var entries = new List<QuestInfoEntry>(1200);
        LoadTable("QuestInfo", row => entries.Add(MapQuestInfoRow("QuestInfo", row)));
        return entries;
    }

    private QuestInfoEntry MapQuestInfoRow(string tableName, IReadOnlyDictionary<string, string> row)
    {
        var id = ReadInt(row, "Id");
        return new QuestInfoEntry
        {
        Id = id,
        Name = Translate(tableName, id, "Name", ReadString(row, "Name")),
        Client = Translate(tableName, id, "Client", ReadString(row, "Client")),
        Pos = Translate(tableName, id, "Pos", ReadString(row, "Pos")),
        Comment = Translate(tableName, id, "Comment", ReadString(row, "Comment")),
        MinLevel = ReadInt(row, "MinLevel"),
        MaxLevel = ReadInt(row, "MaxLevel"),
        Exp = ReadInt(row, "Exp"),
        Tmxp = ReadInt(row, "Tmxp"),
        SrcItem0 = ReadInt(row, "SrcItem0"),
        Cnt0 = ReadInt(row, "Cnt0"),
        SrcItem1 = ReadInt(row, "SrcItem1"),
        Cnt1 = ReadInt(row, "Cnt1"),
        SrcItem2 = ReadInt(row, "SrcItem2"),
        Cnt2 = ReadInt(row, "Cnt2"),
        SrcItem3 = ReadInt(row, "SrcItem3"),
        Cnt3 = ReadInt(row, "Cnt3"),
        SrcItem4 = ReadInt(row, "SrcItem4"),
        Cnt4 = ReadInt(row, "Cnt4"),
        SrcItem5 = ReadInt(row, "SrcItem5"),
        Cnt5 = ReadInt(row, "Cnt5"),
        SrcSkill0 = ReadInt(row, "SrcSkill0"),
        SrcSkillLv0 = ReadInt(row, "SrcSkillLv0"),
        SrcGelder = ReadInt(row, "SrcGelder"),
        ResultTable = ReadString(row, "ResultTable"),
        MaxCount = ReadInt(row, "MaxCount"),
        RefId = ReadInt(row, "RefId"),
        Options = ReadInt(row, "Options"),
        SourceTable = tableName,
        };
    }

    private void LoadTable(string tableName, Action<IReadOnlyDictionary<string, string>> rowHandler)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = false,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Ignore,
        };

        using var reader = OpenXmlReader(settings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;

            if (reader.Name == "TABLE" && reader.GetAttribute("name") == tableName)
            {
                var tableElement = (XElement)XNode.ReadFrom(reader);
                foreach (var rowElement in tableElement.Elements("ROW"))
                {
                    var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var fieldElement in rowElement.Elements())
                    {
                        fields[fieldElement.Name.LocalName] = fieldElement.Value;
                    }
                    rowHandler(fields);
                }
                return;
            }
        }
    }

    public IReadOnlyList<QuestResultEntry> LoadAllQuestResultTables()
    {
        var entries = new List<QuestResultEntry>(1500);
        LoadTablesWithPrefix("QuestResult_", (tableName, row) =>
        {
            entries.Add(new QuestResultEntry
            {
                TableName = tableName,
                QuestId = int.TryParse(tableName["QuestResult_".Length..], out var questId) ? questId : 0,
                Count = ReadInt(row, "Count"),
                Type = ReadInt(row, "Type"),
                Item0 = ReadInt(row, "Item0"),
                Cnt0 = ReadInt(row, "Cnt0"),
                Item1 = ReadInt(row, "Item1"),
                Cnt1 = ReadInt(row, "Cnt1"),
                Item2 = ReadInt(row, "Item2"),
                Cnt2 = ReadInt(row, "Cnt2"),
                Item3 = ReadInt(row, "Item3"),
                Cnt3 = ReadInt(row, "Cnt3"),
                Gelder = ReadInt(row, "Gelder"),
                Skill = ReadInt(row, "Skill"),
                SkillLv = ReadInt(row, "SkillLv"),
                Exp = ReadInt(row, "Exp"),
                Tmxp = ReadInt(row, "Tmxp"),
                Msg0 = ReadString(row, "Msg0"),
                Msg1 = ReadString(row, "Msg1"),
                Msg2 = ReadString(row, "Msg2"),
                Msg3 = ReadString(row, "Msg3"),
                Msg4 = ReadString(row, "Msg4"),
                Msg5 = ReadString(row, "Msg5"),
                Msg6 = ReadString(row, "Msg6"),
                Msg7 = ReadString(row, "Msg7"),
                Msg8 = ReadString(row, "Msg8"),
                Msg9 = ReadString(row, "Msg9"),
                RandomTableId0 = ReadInt(row, "RandomTableID0"),
                RandomTableId1 = ReadInt(row, "RandomTableID1"),
                RandomTableId2 = ReadInt(row, "RandomTableID2"),
            });
        });
        return entries;
    }

    public IReadOnlyList<MonsterQuestResultEntry> LoadMonsterQuestResultTables() =>
        LoadResultTables(MonsterQuestResultEntry.TablePrefixes);

    public IReadOnlyList<MonsterQuestResultEntry> LoadPartyQuestResultTables() =>
        LoadResultTables(new[] { PartyQuestInfoEntry.ResultTablePrefix });

    public IReadOnlyList<PartyQuestInfoEntry> LoadPartyQuestInfo()
    {
        var entries = new List<PartyQuestInfoEntry>(80);
        LoadTable(PartyQuestInfoEntry.TableName, row =>
        {
            var id = ReadInt(row, "Id");
            var targets = new List<PartyQuestTarget>(PartyQuestInfoEntry.TargetSlots);
            for (var i = 0; i < PartyQuestInfoEntry.TargetSlots; i++)
            {
                var monster = ReadInt(row, $"SrcMonster{i}");
                if (monster != 0) targets.Add(new PartyQuestTarget(monster, ReadInt(row, $"Cnt{i}")));
            }
            entries.Add(new PartyQuestInfoEntry
            {
                Id = id,
                Name = Translate(PartyQuestInfoEntry.TableName, id, "Name", ReadString(row, "Name")),
                Client = Translate(PartyQuestInfoEntry.TableName, id, "Client", ReadString(row, "Client")),
                Pos = Translate(PartyQuestInfoEntry.TableName, id, "Pos", ReadString(row, "Pos")),
                Comment = Translate(PartyQuestInfoEntry.TableName, id, "Comment", ReadString(row, "Comment")),
                MinLevel = ReadInt(row, "MinLevel"),
                MaxLevel = ReadInt(row, "MaxLevel"),
                TimeMinutes = ReadInt(row, "Time"),
                Targets = targets,
                ResultTable = ReadString(row, "ResultTable"),
                Exp = ReadInt(row, "Exp"),
                Tmxp = ReadInt(row, "Tmxp"),
                MaxCount = ReadInt(row, "MaxCount"),
                ReturnMapId = ReadInt(row, "ReturnMapID"),
            });
        });
        return entries;
    }

    public IReadOnlyList<float> LoadPartyQuestExpRatios()
    {
        var ratios = new List<float>(50);
        LoadTable(PartyQuestInfoEntry.ExpTableName, row => ratios.Add(ReadFloat(row, "Ratio")));
        return ratios;
    }

    private List<MonsterQuestResultEntry> LoadResultTables(IEnumerable<string> prefixes)
    {
        var entries = new List<MonsterQuestResultEntry>(700);
        foreach (var prefix in prefixes)
        {
            LoadTablesWithPrefix(prefix, (tableName, row) => entries.Add(new MonsterQuestResultEntry
            {
                TableName = tableName,
                Grade = ReadInt(row, "Grade"),
                Type = ReadInt(row, "Type"),
                Item0 = ReadInt(row, "Item0"),
                Cnt0 = ReadInt(row, "Cnt0"),
                Item1 = ReadInt(row, "Item1"),
                Cnt1 = ReadInt(row, "Cnt1"),
                Item2 = ReadInt(row, "Item2"),
                Cnt2 = ReadInt(row, "Cnt2"),
                Item3 = ReadInt(row, "Item3"),
                Cnt3 = ReadInt(row, "Cnt3"),
                Item4 = ReadInt(row, "Item4"),
                Cnt4 = ReadInt(row, "Cnt4"),
                Msg0 = ReadString(row, "Msg0"),
                Msg1 = ReadString(row, "Msg1"),
                Msg2 = ReadString(row, "Msg2"),
                Msg3 = ReadString(row, "Msg3"),
                Msg4 = ReadString(row, "Msg4"),
                Msg5 = ReadString(row, "Msg5"),
                Msg6 = ReadString(row, "Msg6"),
                Msg7 = ReadString(row, "Msg7"),
                Msg8 = ReadString(row, "Msg8"),
                Msg9 = ReadString(row, "Msg9"),
                Gelder = ReadInt(row, "Gelder"),
                RandomTableId0 = ReadInt(row, "RandomTableID0"),
                RandomTableId1 = ReadInt(row, "RandomTableID1"),
                RandomTableId2 = ReadInt(row, "RandomTableID2"),
            }));
        }
        return entries;
    }

    public IReadOnlyList<NpcTalkEntry> LoadAllNpcTalkTables()
    {
        var entries = new List<NpcTalkEntry>(500);
        LoadTablesWithPrefix("NpcTalk_", (tableName, row) =>
        {
            var mapName = tableName.StartsWith("NpcTalk_", StringComparison.Ordinal)
                ? tableName["NpcTalk_".Length..]
                : tableName;
            entries.Add(new NpcTalkEntry
            {
                Type = ReadInt(row, "Type"),
                PosId = ReadInt(row, "PosId"),
                MapName = mapName,
                MsgTable = ReadString(row, "MsgTable"),
                TeleportId = ReadInt(row, "TeleportId"),
                BossPartyZone = ReadInt(row, "BossPartyZone"),
                NpcMoveId = ReadInt(row, "NpcMoveId"),
                ItemStatIds = new[] { ReadInt(row, "ItemStatID1"), ReadInt(row, "ItemStatID2") },
                JobTypeFlag1 = ReadFlags(row, "JobTypeFlag1"),
                JobTypeFlag2 = ReadFlags(row, "JobTypeFlag2"),
                SkillCheckIds = new[] { ReadInt(row, "SkillCheckID"), ReadInt(row, "SkillCheckID_1"), ReadInt(row, "SkillCheckID_2") },
                CompounderTable = ReadString(row, "CompounderTable"),
                ExchangeShopTable = ReadString(row, "ExcShopTable"),
                VipShopTable = ReadString(row, "VIPShopItemTable"),
                QuestId0 = ReadInt(row, "QuestId"),
                QuestId1 = ReadInt(row, "QuestId1"),
                QuestId2 = ReadInt(row, "QuestId2"),
                QuestId3 = ReadInt(row, "QuestId3"),
                QuestId4 = ReadInt(row, "QuestId4"),
                HeadMarkType = ReadInt(row, "HeadMarkType"),
                PreCheckType = ReadInt(row, "PreCheckType"),
                PreCheckArg = ReadInt(row, "PreCheckArg"),
                PreCheckYes = ReadInt(row, "PreCheckYes"),
                PreCheckNo = ReadInt(row, "PreCheckNo"),
                CheckQuestId0 = ReadInt(row, "CheckQuestId"),
                CheckQuestId1 = ReadInt(row, "CheckQuestId1"),
                CheckQuestId2 = ReadInt(row, "CheckQuestId2"),
                CheckQuestNum0 = ReadInt(row, "CheckQuestNum"),
                CheckQuestNum1 = ReadInt(row, "CheckQuestNum1"),
                CheckQuestNum2 = ReadInt(row, "CheckQuestNum2"),
                CheckMonQuestId = ReadInt(row, "CheckMonQuestId"),
                CheckMonQuestNum = ReadInt(row, "CheckMonQuestNum"),
                CheckItemId0 = ReadInt(row, "CheckItemId0"),
                CheckItemCnt0 = ReadInt(row, "CheckItemCnt0"),
                CheckItemId1 = ReadInt(row, "CheckItemId1"),
                CheckItemCnt1 = ReadInt(row, "CheckItemCnt1"),
                ActiveQuestId0 = ReadInt(row, "ActiveQuestID0"),
                ActiveQuestId1 = ReadInt(row, "ActiveQuestID1"),
                ActiveQuestId2 = ReadInt(row, "ActiveQuestID2"),
                GiveItemId = ReadInt(row, "GiveItemID"),
                TakeItemId = ReadInt(row, "TakeItemID"),
                GiveGelder = ReadInt(row, "GiveGelder"),
                TakeGelder = ReadInt(row, "TakeGelder"),
            });
        });
        return entries;
    }

    public IReadOnlyList<NpcMsgEntry> LoadAllNpcMsgTables()
    {
        var entries = new List<NpcMsgEntry>(2000);
        LoadTablesWithPrefix("NpcMsg_", (tableName, row) =>
        {
            var no = ReadInt(row, "No");
            entries.Add(new NpcMsgEntry
            {
                TableName = tableName,
                No = no,
                NpcSType = (byte)ReadInt(row, "NpcSType"),
                Message = Translate(tableName, no, "Message", ReadString(row, "Message")),
            });
        });
        return entries;
    }

    public IReadOnlyList<CharacterInfoEntry> LoadCharacterInfo()
    {
        var entries = new List<CharacterInfoEntry>(2200);
        LoadTable("CharacterInfo", row =>
        {
            var id = ReadInt(row, "ID");
            entries.Add(new CharacterInfoEntry
            {
                Id = id,
                CommonName = Translate("CharacterInfo", id, "CommonName", ReadString(row, "CommonName")),
                CommonNameEng = Translate("CharacterInfo", id, "CommonName_Eng", ReadString(row, "CommonName_Eng")),
                FileName = ReadString(row, "FileName"),
                WalkSpeed = ReadFloat(row, "WalkSpeed"),
                RunSpeed = ReadFloat(row, "RunSpeed"),
                MinAtkRange = ReadInt(row, "MinAtkRange"),
                MaxAtkRange = ReadInt(row, "MaxAtkRange"),
                ChaseRange = ReadInt(row, "ChaseRange"),
            });
        });
        return entries;
    }

    public IReadOnlyList<MoveSpeedParamEntry> LoadMoveSpeedParam()
    {
        var entries = new List<MoveSpeedParamEntry>(4);
        LoadTable("MoveSpeedParam", row => entries.Add(new MoveSpeedParamEntry(
            ReadInt(row, "ObjectType"),
            ReadFloat(row, "WalkSpeed"),
            ReadFloat(row, "RunSpeed"),
            ReadFloat(row, "ChaseSpeed"))));
        return entries;
    }

    public IReadOnlyList<MapKindEntry> LoadMapKinds()
    {
        var entries = new List<MapKindEntry>(700);
        LoadTable("MapInfoEx", row => entries.Add(new MapKindEntry(
            ReadInt(row, "ID"),
            ReadInt(row, "Type"),
            ReadInt(row, "Class"),
            ReadFlags(row, "Option"))));
        return entries;
    }

    public IReadOnlyList<PresentItemEntry> LoadPresentItems()
    {
        var entries = new List<PresentItemEntry>(1400);
        LoadTable(PresentItemEntry.TableName, row => entries.Add(new PresentItemEntry(
            ReadInt(row, "Id"),
            ReadInt(row, "Type"),
            ReadInt(row, "DropCnt"),
            Enumerable.Range(0, PresentItemEntry.DropColumns)
                .Select(i => new PresentDrop(ReadInt(row, "DropId_" + i), ReadInt(row, "DropRate_" + i), ReadInt(row, "ItemCnt_" + i)))
                .ToArray())));
        return entries;
    }

    public GuardianTables LoadGuardianTables()
    {
        var nextExp = new Dictionary<int, int>();
        LoadTable("SoulGuardianLV", row => nextExp[ReadInt(row, "SoulLV")] = ReadInt(row, "NextSoulExp"));
        var parameters = new Dictionary<int, GuardianParamEntry>();
        LoadTable("SoulGuardianParam", row =>
        {
            var id = ReadInt(row, "ID");
            parameters[id] = new GuardianParamEntry(
                id,
                ReadInt(row, "FType"),
                ReadInt(row, "LevelGrowth_ID"),
                Enumerable.Range(0, GuardianTables.RuneSlotCount).Select(i => ReadInt(row, "RuneStone" + i)).ToArray(),
                Enumerable.Range(0, GuardianTables.RuneSlotCount).Select(i => ReadInt(row, "DefaultRuneSkill" + i)).ToArray());
        });
        var growth = new Dictionary<int, GuardianGrowthEntry>();
        LoadTable("Guardian_GrowthTable", row =>
        {
            var id = ReadInt(row, "ID");
            growth[id] = new GuardianGrowthEntry(id, GuardianTables.GrowthColumns.Select(column => ReadFloat(row, column)).ToArray());
        });
        var basic = new Dictionary<int, GuardianBasicStatEntry>();
        LoadTable("GuardianBasicStat", row =>
        {
            var level = ReadInt(row, "BasicLV");
            basic[level] = new GuardianBasicStatEntry(level, GuardianTables.BasicStatColumns.Select(column => ReadInt(row, column)).ToArray());
        });
        var ratios = new Dictionary<int, float>();
        LoadTable("SoulGuardianExp", row => ratios[ReadInt(row, "ExpType")] = ReadFloat(row, "Ratio"));
        return new GuardianTables(nextExp, parameters, growth, basic, ratios);
    }

    public IReadOnlyList<HonorTitleEntry> LoadHonorTitles()
    {
        var entries = new List<HonorTitleEntry>(80);
        LoadTable(HonorTitleEntry.TableName, row => entries.Add(new HonorTitleEntry(
            ReadInt(row, "ID"),
            ReadInt(row, "TitleType"),
            ReadInt(row, "QuestType"),
            ReadInt(row, "QuestID"),
            ReadInt(row, "ClearCnt"),
            ReadInt(row, "Time"),
            ReadInt(row, "HuntCnt"))));
        return entries;
    }

    public IReadOnlyList<MemoryPortEntry> LoadMemoryPorts()
    {
        var entries = new List<MemoryPortEntry>(8);
        LoadTable(MemoryPortEntry.TableName, row => entries.Add(new MemoryPortEntry(
            ReadInt(row, "ItemId"),
            ReadInt(row, "type"),
            ReadInt(row, "slotNum"))));
        return entries;
    }

    public IReadOnlyDictionary<int, ImprintEntry> LoadImprints()
    {
        var imprints = new Dictionary<int, ImprintEntry>();
        LoadTable(ImprintEntry.TableName, row => imprints.TryAdd(ReadInt(row, "Id"), new ImprintEntry(
            ReadInt(row, "Id"), ReadInt(row, "Time"), ReadInt(row, "Type"), ReadFloat(row, "Value"))));
        return imprints;
    }

    public IReadOnlyDictionary<int, ExpBoostEntry> LoadExpBoosts()
    {
        var boosts = new Dictionary<int, ExpBoostEntry>();
        LoadTable(ExpBoostEntry.TableName, row => boosts.TryAdd(ReadInt(row, "ItemId"), new ExpBoostEntry(
            ReadInt(row, "ItemId"), ReadFloat(row, "ExpAdvantage"), ReadFloat(row, "TmxpAdvantage"))));
        return boosts;
    }

    public BoxTables LoadBoxTables()
    {
        var bonuses = new Dictionary<int, BonusPresentEntry>();
        var keyboxes = new Dictionary<int, KeyboxEntry>();
        LoadTablesNamed(BoxTables.TableNames, (table, row) =>
        {
            if (table == BonusPresentEntry.TableName)
            {
                bonuses.TryAdd(ReadInt(row, "PresentId"), new BonusPresentEntry(
                    ReadInt(row, "PresentId"), ReadInt(row, "SelectItemId"), ReadInt(row, "PickBonusPresentId"), ReadInt(row, "UnpickBonusPresentId")));
                return;
            }
            var keys = ReadString(row, "KeyItemID")
                .Split(KeyboxEntry.KeySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(key => int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToArray();
            keyboxes.TryAdd(ReadInt(row, "KeyBoxID"), new KeyboxEntry(
                ReadInt(row, "KeyBoxID"), keys, ReadInt(row, "OnlyKeyitem") != 0, ReadInt(row, "NoramlItemID"),
                ReadInt(row, "RareItemID_TypeA"), ReadInt(row, "RareItemID_TypeB"), ReadInt(row, "RareItem_TypeB_Droprate")));
        });
        return new BoxTables(bonuses, keyboxes);
    }

    public SpecialPortTables LoadSpecialPortTables()
    {
        var advanced = new Dictionary<int, TeleportAdvEntry>();
        var random = new Dictionary<int, RandomPortEntry>();
        LoadTablesNamed(SpecialPortTables.TableNames, (table, row) =>
        {
            if (table == SpecialPortTables.AdvancedTable)
            {
                advanced.TryAdd(ReadInt(row, "ZoneIndex"), new TeleportAdvEntry(ReadInt(row, "ZoneIndex"), ReadInt(row, "ZoneType"), ReadInt(row, "ZoneID")));
                return;
            }
            var zones = Enumerable.Range(0, SpecialPortTables.RandomZoneSlots)
                .Select(slot => new RandomPortZone(ReadInt(row, $"ZoneID_{slot}"), ReadInt(row, $"Ratio_{slot}")))
                .Where(zone => zone.Ratio > 0)
                .ToArray();
            random.TryAdd(ReadInt(row, "ID"), new RandomPortEntry(ReadInt(row, "ID"), ReadString(row, "Notice"), zones));
        });
        return new SpecialPortTables(advanced, random);
    }

    public FortuneTables LoadFortuneTables()
    {
        var cards = new Dictionary<int, FortuneCardEntry>();
        var cardOrder = new List<int>();
        var fees = new Dictionary<int, int>();
        var rules = new Dictionary<string, float>(StringComparer.Ordinal);
        var titles = new List<FortuneTitleRule>();
        var names = new HashSet<string>(StringComparer.Ordinal)
        {
            FortuneTables.CardTable, FortuneTables.FeeTable, FortuneTables.RuleTable, FortuneTables.TitleTable,
        };
        LoadTablesNamed(names, (table, row) =>
        {
            switch (table)
            {
                case FortuneTables.CardTable:
                    var id = ReadInt(row, "ID");
                    if (!cards.ContainsKey(id)) cardOrder.Add(id);
                    cards[id] = new FortuneCardEntry(
                        id,
                        ReadString(row, "Attribute"),
                        ReadFloat(row, "Value"),
                        ReadFloat(row, "Prob"),
                        ReadFloat(row, "JKProb"),
                        ReadInt(row, "Duration"),
                        Enumerable.Range(0, FortuneTables.CrossColumns)
                            .Select(i => new FortuneCross(ReadString(row, "Cross" + i), ReadFloat(row, "Value" + i)))
                            .ToArray());
                    break;
                case FortuneTables.FeeTable:
                    fees[ReadInt(row, "Times")] = ReadInt(row, "Galder");
                    break;
                case FortuneTables.RuleTable:
                    rules[ReadString(row, "RuleName")] = ReadFloat(row, "Value");
                    break;
                default:
                    titles.Add(new FortuneTitleRule(
                        ReadInt(row, "Index"),
                        Enumerable.Range(0, FortuneTables.TitleConditions)
                            .Select(i => new FortuneTitleCondition(ReadInt(row, "Category" + i), ReadInt(row, "Value" + i)))
                            .ToArray(),
                        ReadInt(row, "TitleID")));
                    break;
            }
        });
        return new FortuneTables(cards, fees, rules, titles.OrderBy(rule => rule.Index).ToArray()) { CardOrder = cardOrder };
    }

    public SoulSeedTables LoadSoulSeedTables()
    {
        var seeds = new Dictionary<int, IReadOnlyList<int>>();
        var events = new Dictionary<int, SoulSeedEventEntry>();
        var names = new Dictionary<int, string>();
        var tables = new HashSet<string>(StringComparer.Ordinal)
        {
            SoulSeedTables.SeedTable, SoulSeedTables.EventTable, SoulSeedTables.GuardianTable,
        };
        LoadTablesNamed(tables, (table, row) =>
        {
            switch (table)
            {
                case SoulSeedTables.SeedTable:
                    seeds[ReadInt(row, "ID")] = SoulSeedTables.SeedTypeColumns.Select(column => ReadInt(row, column)).ToArray();
                    break;
                case SoulSeedTables.EventTable:
                    var eventType = ReadInt(row, "EventType");
                    events[eventType] = new SoulSeedEventEntry(
                        ReadInt(row, "Index"),
                        eventType,
                        ReadInt(row, "QuestType"),
                        ReadInt(row, "Quest_ID"),
                        ReadBool(row, "Special"),
                        ReadInt(row, "MaximumLevel"),
                        ReadInt(row, "Rate"),
                        SoulSeedTables.PointColumns.Select(column => ReadInt(row, column)).ToArray());
                    break;
                default:
                    names[ReadInt(row, "ID")] = ReadString(row, "CommonName");
                    break;
            }
        });
        return new SoulSeedTables(seeds, events, names);
    }

    public GhostExchangeTables LoadGhostExchangeTables()
    {
        var groups = new Dictionary<int, IReadOnlyList<int>>();
        LoadTable(GhostExchangeTables.GroupTable, row => groups[ReadInt(row, "GroupId")] =
            Enumerable.Range(0, GhostExchangeTables.GroupColumns).Select(n => ReadInt(row, $"ItemId{n}")).ToArray());
        IReadOnlyList<int> levels = Array.Empty<int>();
        LoadTable(GhostExchangeTables.RefineLevelTable, row =>
        {
            if (levels.Count != 0) return;
            levels = Enumerable.Range(0, GhostExchangeTables.RefineLevelColumns).Select(n => ReadInt(row, $"ExchangeLv{n}")).ToArray();
        });
        return new GhostExchangeTables(groups, levels);
    }

    public CardIdentifyTables LoadCardIdentifyTables()
    {
        var grades = new Dictionary<int, CardIdentifyGradeEntry>();
        LoadTable(CardIdentifyTables.GradeTable, row =>
        {
            var entry = new CardIdentifyGradeEntry(
                ReadInt(row, "Grade"), ReadInt(row, "Table0"), ReadFloat(row, "Rate0"), ReadInt(row, "Table1"), ReadFloat(row, "Rate1"),
                ReadInt(row, "Special_Table"), ReadFloat(row, "Special_Rate"));
            grades[entry.Grade] = entry;
        });
        var results = new Dictionary<int, List<CardIdentifyResultEntry>>();
        LoadTablesWithPrefix(CardIdentifyTables.ResultTablePrefix, (tableName, row) =>
        {
            if (!int.TryParse(tableName.AsSpan(CardIdentifyTables.ResultTablePrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var table)) return;
            if (!results.TryGetValue(table, out var rows)) results[table] = rows = new List<CardIdentifyResultEntry>();
            rows.Add(new CardIdentifyResultEntry(ReadInt(row, "ItemID"), (int)ReadFloat(row, "Rate")));
        });
        var combos = new SortedDictionary<int, int>();
        LoadTable(CardIdentifyTables.ComboTable, row => combos[ReadInt(row, "Combo")] = ReadInt(row, "ComboRatio"));
        float? expRatio = null;
        var tmExpRatio = 0f;
        LoadTable(CardIdentifyTables.ExpTable, row =>
        {
            if (expRatio is not null) return;
            expRatio = ReadFloat(row, "ExpCRatio");
            tmExpRatio = ReadFloat(row, "TmxpCRatio");
        });
        return new CardIdentifyTables(
            grades,
            results.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<CardIdentifyResultEntry>)pair.Value),
            combos.Values.ToArray(),
            expRatio ?? 0f,
            tmExpRatio);
    }

    public SoulSystemTables LoadSoulSystemTables()
    {
        SoulSystemInfoEntry? info = null;
        LoadTable(SoulSystemTables.InfoTable, row => info ??= new SoulSystemInfoEntry(
            ReadInt(row, "SoulPotionID"),
            ReadInt(row, "SoulGaugeMax"),
            ReadInt(row, "SoulGaugeTime"),
            ReadInt(row, "SourceDecreaseItemCnt"),
            ReadInt(row, "SoulGaugeDecrease"),
            ReadInt(row, "SoulGaugePenaltyTime"),
            ReadInt(row, "SoulGaugePenaltyCnt")));
        var sources = new List<SoulSourceEntry>(24);
        LoadTable(SoulSystemTables.SourceTable, row => sources.Add(new SoulSourceEntry(
            ReadInt(row, "SourceID"), ReadInt(row, "GuardianID"), ReadInt(row, "SoulGaugeUp"))));
        var potions = new List<SoulPotionCountEntry>(5);
        LoadTable(SoulSystemTables.PotionTable, row => potions.Add(new SoulPotionCountEntry(
            ReadInt(row, "GuardianLvMin"), ReadInt(row, "GuardianLvMax"), ReadInt(row, "ItemCnt"))));
        var equipment = new Dictionary<int, SoulEquipmentEntry>();
        LoadTable(SoulSystemTables.EquipmentTable, row =>
        {
            var entry = new SoulEquipmentEntry(ReadInt(row, "ID"), ReadInt(row, "SkillId"), ReadInt(row, "DecreaseSoul"), ReadInt(row, "DecreaseTime"));
            equipment[entry.ItemId] = entry;
        });
        var crafts = new Dictionary<int, SoulCraftEntry>();
        LoadTable(SoulSystemTables.CraftTable, row =>
        {
            var materials = Enumerable.Range(1, SoulSystemTables.RequirementColumns)
                .Select(n => new SoulMaterial(ReadInt(row, $"ReqID{n}"), ReadInt(row, $"ReqNum{n}")))
                .Where(material => material.ItemId != 0 && material.Count > 0)
                .ToArray();
            var entry = new SoulCraftEntry(ReadInt(row, "ID"), ReadInt(row, "ResID"), ReadInt(row, "ResNum"), materials, ReadFloat(row, "Probability"), ReadInt(row, "Fee"));
            crafts.TryAdd(entry.ResultItemId, entry);
        });
        var intensify = new List<SoulIntensifyEntry>(20);
        LoadTable(SoulSystemTables.IntensifyTable, row => intensify.Add(new SoulIntensifyEntry(
            ReadInt(row, "ID"),
            ReadInt(row, "SoulLevel"),
            ReadInt(row, "WeaponLevel"),
            ReadInt(row, "Cost"),
            Enumerable.Range(1, SoulSystemTables.RequirementColumns)
                .Select(n => new SoulMaterial(ReadInt(row, $"Material{n}"), ReadInt(row, $"Cnt{n}")))
                .Where(material => material.ItemId != 0 && material.Count > 0)
                .ToArray(),
            ReadInt(row, "Ratio"))));
        var tartaros = new HashSet<int>();
        LoadTable(SoulSystemTables.TartarosTable, row => tartaros.Add(ReadInt(row, "ID")));
        return new SoulSystemTables(info, sources, potions, equipment, crafts, intensify, tartaros);
    }

    public MeltingTables LoadMeltingTables()
    {
        MeltingInfoEntry? info = null;
        LoadTable(MeltingTables.InfoTable, row => info ??= new MeltingInfoEntry(
            ReadFloat(row, "ItemGradeRate"),
            ReadFloat(row, "CompoundSlotRate"),
            ReadFloat(row, "CompoundValueRate"),
            ReadFloat(row, "ItemValueRate"),
            ReadFloat(row, "CraftGrade"),
            ReadInt(row, "CMOption")));
        var drops = new List<MeltingDropEntry>(9);
        LoadTable(MeltingTables.DropTable, row => drops.Add(new MeltingDropEntry(
            ReadInt(row, "ID"), ReadInt(row, "DropItem"), ReadInt(row, "MeltingCost"), ReadInt(row, "MinValue"), ReadInt(row, "MaxValue"))));
        int? min = null;
        var max = 0;
        LoadTable(MeltingTables.RandomStatRatioTable, row =>
        {
            if (min is not null) return;
            min = ReadInt(row, "Min");
            max = ReadInt(row, "Max");
        });
        return new MeltingTables(info, drops, min ?? 0, max);
    }

    public EquipReformTables LoadEquipReformTables()
    {
        var materials = new List<ItemReformEntry>(26);
        LoadTable(EquipReformTables.MaterialTable, row => materials.Add(new ItemReformEntry(
            ReadInt(row, "MinLv"), ReadInt(row, "MaxLv"), ReadInt(row, "ItemId"), ReadInt(row, "ItemCnt"))));
        var curves = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        LoadTable(EquipReformTables.CurveTable, row => curves.TryAdd(ReadString(row, "Name"), CurveSteps.Select(step => ReadInt(row, step)).ToArray()));
        int? min = null;
        var max = 0;
        LoadTable(EquipReformTables.RatioTable, row =>
        {
            if (min is not null) return;
            min = ReadInt(row, "Min");
            max = ReadInt(row, "Max");
        });
        var elixirs = new Dictionary<int, ReformElixirEntry>();
        LoadTable(EquipReformTables.ElixirTable, row => elixirs.TryAdd(ReadInt(row, "ID"),
            new ReformElixirEntry(ReadInt(row, "ID"), ReadInt(row, "minShift"), ReadInt(row, "maxShift"))));
        float? discount = null;
        LoadTable(EquipReformTables.DiscountTable, row => discount ??= ReadFloat(row, "ratio"));
        var accepted = discount is >= 0 and < 1 ? discount.Value : 0;
        return new EquipReformTables(materials, curves, min ?? 0, max, elixirs, accepted);
    }

    public PetSpeechTables LoadPetSpeechTables()
    {
        var parameters = new Dictionary<int, PetSpeechParam>();
        LoadTable(PetSpeechTables.ParamTable, row =>
        {
            var chrType = ReadInt(row, "ID");
            if (chrType < PetSpeechTables.FirstChrType || chrType > PetSpeechTables.LastChrType) return;
            parameters.TryAdd(chrType, new PetSpeechParam(ReadString(row, "Speech").Trim(), ReadInt(row, "Voice") != 0, ReadInt(row, "SpeechColor")));
        });
        var tables = new Dictionary<string, Dictionary<int, IReadOnlyList<PetSpeechLine>>>(StringComparer.Ordinal);
        LoadTablesWithPrefix(PetSpeechTables.TablePrefix, (tableName, row) =>
        {
            if (!tables.TryGetValue(tableName, out var byType))
            {
                byType = new Dictionary<int, IReadOnlyList<PetSpeechLine>>();
                tables[tableName] = byType;
            }
            var lines = new List<PetSpeechLine>(PetSpeechTables.LinesPerType);
            var threshold = 0;
            for (var slot = 0; slot < PetSpeechTables.LinesPerType; slot++)
            {
                var text = ReadString(row, "Speech" + slot);
                if (string.IsNullOrWhiteSpace(text)) break;
                threshold += (int)(ReadFloat(row, "Ratio" + slot) * PetSpeechTables.RatioScale);
                lines.Add(new PetSpeechLine(text, ReadString(row, "Voice" + slot).Trim(), threshold));
            }
            byType.TryAdd(ReadInt(row, "Type"), lines);
        });
        return new PetSpeechTables(
            parameters,
            tables.ToDictionary(table => table.Key, table => (IReadOnlyDictionary<int, IReadOnlyList<PetSpeechLine>>)table.Value, StringComparer.Ordinal));
    }

    public PetRetrainTables LoadPetRetrainTables()
    {
        var materials = new Dictionary<int, PetRetrainMaterialEntry>();
        LoadTable(PetRetrainTables.MaterialTable, row => materials.TryAdd(ReadInt(row, "matItemId"), new PetRetrainMaterialEntry(
            ReadInt(row, "matItemId"), ReadInt(row, "matItemCnt"), ReadInt(row, "matToLockItemCnt"),
            ReadInt(row, "randomStatMinPercent"), ReadInt(row, "randomStatMaxPercent"))));
        var locks = new Dictionary<int, int>();
        LoadTable(PetRetrainTables.LockItemTable, row => locks.TryAdd(ReadInt(row, "ItemId"), ReadInt(row, "ItemCnt")));
        IReadOnlyList<int> curve = Array.Empty<int>();
        LoadTable(EquipReformTables.CurveTable, row =>
        {
            if (string.Equals(ReadString(row, "Name"), PetRetrainTables.CurveName, StringComparison.Ordinal)) curve = CurveSteps.Select(step => ReadInt(row, step)).ToArray();
        });
        return new PetRetrainTables(materials, locks, curve);
    }

    public CharacterPetReinforceTables LoadCharacterPetReinforceTables()
    {
        var rows = new Dictionary<(int, int), CharacterPetReinforceEntry>();
        LoadTable(CharacterPetReinforceTables.TableName, row =>
        {
            var entry = new CharacterPetReinforceEntry(
                ReadInt(row, "ResultId"),
                ReadInt(row, "PetReinforceItemId"),
                ReadBool(row, "ChrRareType"),
                ReadInt(row, "ChrBasicId"),
                CharacterPetReinforceTables.StatColumns.Select(column => ReadInt(row, column)).ToArray());
            rows.TryAdd((entry.ReinforceItemId, entry.BasicId), entry);
        });
        return new CharacterPetReinforceTables(rows);
    }

    public PetLevelUpTables LoadPetLevelUpTables()
    {
        var rows = new Dictionary<int, PetLevelUpEntry>();
        LoadTable(PetLevelUpTables.TableName, row => rows.TryAdd(ReadInt(row, "SrcId"), new PetLevelUpEntry(
            ReadInt(row, "SrcId"), ReadInt(row, "ResultId"), ReadInt(row, "CatalyItemId"), ReadInt(row, "CatalyItemCnt"), ReadInt(row, "Evolve") != 0)));
        return new PetLevelUpTables(rows);
    }

    public PetDigTables LoadPetDigTables()
    {
        var pets = new Dictionary<int, PetDigPetEntry>();
        LoadTable(PetDigTables.PetTable, row => pets.TryAdd(ReadInt(row, "RefineIndex"), new PetDigPetEntry(
            ReadInt(row, "RefineIndex"), ReadInt(row, "PetInvenSize"), ReadInt(row, "PetDigRate"))));
        var foods = new Dictionary<int, PetDigFoodEntry>();
        LoadTable(PetDigTables.FoodTable, row => foods.TryAdd(ReadInt(row, "ID"), new PetDigFoodEntry(
            ReadInt(row, "ID"), ReadInt(row, "Speed"), ReadInt(row, "MaxItem"), ReadInt(row, "PlusRate"))));
        var exItems = new Dictionary<int, PetDigExEntry>();
        LoadTable(PetDigTables.ExTable, row => exItems.TryAdd(ReadInt(row, "ID"), new PetDigExEntry(
            ReadInt(row, "ID"),
            ReadInt(row, "ExItemDigRate"),
            Enumerable.Range(0, PetDigTables.ExItemColumns)
                .Select(column => new PetDigExItem(ReadInt(row, $"Item{column}"), ReadInt(row, $"Rate{column}"), ReadInt(row, $"Cnt{column}")))
                .Where(item => item.ItemId != 0 && item.Rate > 0 && item.Count > 0)
                .ToArray())));
        var areas = new Dictionary<int, PetDigAreaEntry>();
        LoadTable(PetDigTables.AreaTable, row => areas.TryAdd(ReadInt(row, "Area"), new PetDigAreaEntry(
            ReadInt(row, "Area"), ReadInt(row, "MinLevel"), ReadInt(row, "MaxLevel"))));
        var zones = new Dictionary<int, PetDigZone>();
        LoadTable(PetDigTables.ZoneTable, row => zones.TryAdd(ReadInt(row, "ID"), new PetDigZone(
            ReadInt(row, "WMRegion"), ReadInt(row, "SubType"), ReadInt(row, "MinItemDepth"), Math.Max(ReadInt(row, "MinItemDepth"), ReadInt(row, "MaxItemDepth")))));
        var depthBands = new List<PetDigDepthBand>();
        LoadTable(PetDigTables.DepthTable, row => depthBands.Add(new PetDigDepthBand(
            ReadInt(row, "MinDeep"), ReadInt(row, "MaxDeep"), ReadInt(row, "Variable"))));
        return new PetDigTables(pets, foods, exItems, areas, zones, depthBands);
    }

    public FiestaTables LoadFiestaTables()
    {
        var slots = new List<FiestaSlotEntry>();
        var ports = new Dictionary<int, LevelPortEntry>();
        LoadTablesNamed(FiestaTables.TableNames, (table, row) =>
        {
            if (table == FiestaTables.SlotTable)
            {
                slots.Add(new FiestaSlotEntry(
                    ReadInt(row, "ID"), ReadInt(row, "ZoneID"), ReadInt(row, "PortalIndex"), ReadInt(row, "PortalOpenMinute"),
                    ReadInt(row, "In_Year"), ReadInt(row, "In_Month"), ReadInt(row, "In_Day"),
                    ReadInt(row, "In_Hour") * 60 + ReadInt(row, "In_Minute"),
                    ReadInt(row, "Out_Hour") * 60 + ReadInt(row, "Out_Minute")));
                return;
            }
            var bands = Enumerable.Range(0, FiestaTables.LevelPortBands)
                .Select(band => new LevelPortBand(ReadInt(row, $"ZoneID_{band}"), ReadInt(row, $"MinLevel_{band}"), ReadInt(row, $"MaxLevel_{band}")))
                .Where(band => band.ZoneId != 0)
                .ToArray();
            ports.TryAdd(ReadInt(row, "ID"), new LevelPortEntry(ReadInt(row, "ID"), ReadString(row, "Notice"), ReadInt(row, "portType"), bands));
        });
        return new FiestaTables(slots, ports);
    }

    public TreasureMapTables LoadTreasureMapTables()
    {
        var teleports = new Dictionary<int, TreasureTeleportEntry>();
        LoadTable(TreasureMapTables.TeleportTable, row => teleports.TryAdd(ReadInt(row, "ID"), new TreasureTeleportEntry(
            ReadInt(row, "ID"),
            ReadInt(row, "Time"),
            Enumerable.Range(0, TreasureMapTables.SpotMapColumns).Select(column => ReadInt(row, $"SpotMap{column}")).Where(zone => zone != 0).ToArray(),
            Enumerable.Range(0, TreasureMapTables.DestinationColumns)
                .Select(column => new TreasureTeleportDestination(ReadInt(row, $"TeleportMap{column}"), ReadFloat(row, $"TeleportRatio{column}"), ReadInt(row, $"PosId{column}")))
                .Where(destination => destination.ZoneId != 0)
                .ToArray())));
        var items = new Dictionary<int, TreasureItemEntry>();
        LoadTable(TreasureMapTables.ItemTable, row => items.TryAdd(ReadInt(row, "ID"), new TreasureItemEntry(
            ReadInt(row, "ID"),
            ReadInt(row, "Time"),
            Enumerable.Range(0, TreasureMapTables.SpotMapColumns).Select(column => ReadInt(row, $"SpotMap{column}")).Where(zone => zone != 0).ToArray(),
            ReadFlags(row, "Option"),
            ReadInt(row, "Count"),
            Enumerable.Range(0, TreasureMapTables.LootSlots)
                .Select(slot => new TreasureLootSlot(ReadInt(row, $"TItem{slot}"), ReadFloat(row, $"TItemRatio{slot}"), ReadInt(row, $"TItemCount{slot}")))
                .ToArray(),
            ReadInt(row, "SideItem"),
            ReadInt(row, "SideItemMin"),
            ReadInt(row, "SideItemMax"))));
        var randomSpots = new Dictionary<int, RandomSpotEntry>();
        LoadTable(TreasureMapTables.RandomSpotTable, row => randomSpots.TryAdd(ReadInt(row, "ID"), new RandomSpotEntry(
            ReadInt(row, "ID"),
            Enumerable.Range(0, TreasureMapTables.RandomSpotMonsterColumns).Select(column => (ReadInt(row, $"MonsterID_{column}"), ReadFloat(row, $"MonsterRatio_{column}"))).ToArray(),
            ReadInt(row, "TreasureID"),
            ReadFloat(row, "TreasureRatio"),
            ReadInt(row, "Monster_DeadTimer"))));
        var addedRatio = 0f;
        LoadTable(TreasureMapTables.RandomSpotEventTable, row => addedRatio = ReadFloat(row, "AddedRatio"));
        var zoneRegions = new Dictionary<int, int>();
        LoadTable("MapInfoEx", row => zoneRegions.TryAdd(ReadInt(row, "ID"), ReadInt(row, "WMRegion")));
        return new TreasureMapTables(teleports, items, randomSpots, zoneRegions, addedRatio);
    }

    public MissionTables LoadMissionTables()
    {
        var missions = new Dictionary<int, MissionInfoEntry>();
        LoadTable(MissionTables.InfoTable, row => missions.TryAdd(ReadInt(row, "ID"), new MissionInfoEntry(
            ReadInt(row, "ID"), ReadString(row, "Name"), ReadInt(row, "MinLv"), ReadInt(row, "MaxLv"), ReadInt(row, "MissionTime"),
            ReadInt(row, "MissionPlayMapID"), ReadInt(row, "MissionType"), ReadInt(row, "TypelimitValue"), ReadInt(row, "MissionMonsterType"),
            ReadString(row, "MissionMonsterTable"))));
        var waves = new Dictionary<string, List<MissionWave>>(StringComparer.Ordinal);
        LoadTablesWithPrefix(MissionTables.WaveTablePrefix, (table, row) =>
        {
            if (!waves.TryGetValue(table, out var list))
            {
                list = new List<MissionWave>();
                waves[table] = list;
            }
            list.Add(new MissionWave(
                ReadInt(row, "MonsterCount"),
                Enumerable.Range(0, MissionTables.WaveColumns)
                    .Select(column => (ReadInt(row, $"MonsterID_{column}"), ReadInt(row, $"Region_{column}")))
                    .Where(monster => monster.Item1 != 0)
                    .ToArray()));
        });
        return new MissionTables(
            missions,
            waves.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MissionWave>)pair.Value.OrderBy(wave => wave.Wave).ToArray(), StringComparer.Ordinal));
    }

    public PuzzleTables LoadPuzzleTables()
    {
        var maps = new Dictionary<int, PuzzleMapEntry>();
        var memory = new Dictionary<int, MemoryPuzzleEntry>();
        var objects = new Dictionary<string, List<(int, int)>>(StringComparer.Ordinal);
        var slide = new Dictionary<int, SlidePuzzleEntry>();
        var blocks = new Dictionary<string, List<SlidePuzzleBlock>>(StringComparer.Ordinal);
        LoadTablesMatching(
            name => name is PuzzleTables.MapTable or PuzzleTables.MemoryTable or PuzzleTables.SlideTable
                || name.StartsWith(PuzzleTables.MemoryObjectPrefix, StringComparison.Ordinal)
                || name.StartsWith(PuzzleTables.SlideBlockPrefix, StringComparison.Ordinal),
            (table, row) =>
            {
                switch (table)
                {
                    case PuzzleTables.MapTable:
                        maps.TryAdd(ReadInt(row, "MapID"), new PuzzleMapEntry(ReadInt(row, "PuzzleUID"), ReadInt(row, "MapID"), ReadInt(row, "PuzzleType"), ReadInt(row, "PuzzleID")));
                        return;
                    case PuzzleTables.MemoryTable:
                        memory.TryAdd(ReadInt(row, "ID"), new MemoryPuzzleEntry(
                            ReadInt(row, "ID"), ReadInt(row, "TurmTime"), ReadString(row, "ObjectTable").Trim(), ReadInt(row, "ObjectCount"),
                            ReadInt(row, "TimeLimit"), ReadInt(row, "PotalMap"), PuzzleRewards(row)));
                        return;
                    case PuzzleTables.SlideTable:
                        slide.TryAdd(ReadInt(row, "ID"), new SlidePuzzleEntry(
                            ReadInt(row, "ID"), ReadString(row, "BlockTable").Trim(), ReadInt(row, "SizeX"), ReadInt(row, "SizeY"),
                            ReadInt(row, "TimeLimit"), ReadInt(row, "PotalMap"), PuzzleRewards(row)));
                        return;
                }
                if (table.StartsWith(PuzzleTables.SlideBlockPrefix, StringComparison.Ordinal))
                {
                    if (!blocks.TryGetValue(table, out var blockList))
                    {
                        blockList = new List<SlidePuzzleBlock>();
                        blocks[table] = blockList;
                    }
                    blockList.Add(new SlidePuzzleBlock(ReadInt(row, "ID"), ReadInt(row, "Bundle")));
                    return;
                }
                if (!objects.TryGetValue(table, out var list))
                {
                    list = new List<(int, int)>();
                    objects[table] = list;
                }
                list.Add((ReadInt(row, "PositionID"), ReadInt(row, "MonID")));
            });
        return new PuzzleTables(
            maps,
            memory,
            objects.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<(int, int)>)pair.Value, StringComparer.Ordinal),
            slide,
            blocks.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<SlidePuzzleBlock>)pair.Value.OrderBy(block => block.Id).ToArray(), StringComparer.Ordinal));
    }

    private static (int ItemId, int Count)[] PuzzleRewards(IReadOnlyDictionary<string, string> row) =>
        Enumerable.Range(0, PuzzleTables.RewardColumns)
            .Select(column => (ReadInt(row, $"Item{column}"), ReadInt(row, $"ItemCount{column}")))
            .Where(reward => reward.Item1 != 0 && reward.Item2 > 0)
            .ToArray();

    public GuildGameTypeTables LoadGuildGameTypeTables()
    {
        var rows = new Dictionary<int, GuildGameTypeEntry>();
        LoadTable(GuildGameTypeTables.Table, row =>
        {
            var maps = Enumerable.Range(0, GuildGameTypeTables.MapColumns).Select(index => ReadInt(row, $"Map{index}")).ToArray();
            rows.TryAdd(ReadInt(row, "Type"), new GuildGameTypeEntry(ReadInt(row, "Type"), ReadInt(row, "TimeLimit"), maps));
        });
        return new GuildGameTypeTables(rows);
    }

    public HarconDefTables LoadHarconDefTables()
    {
        var stages = new Dictionary<int, HarconStageEntry>();
        var drops = new Dictionary<int, HarconDropEntry>();
        var points = HarconPointRule.Empty;
        var shop = new Dictionary<(int, int), HarconShopEntry>();
        var waves = new Dictionary<string, List<HarconWave>>(StringComparer.Ordinal);
        var defended = new Dictionary<string, List<(int MonsterId, int RegionId, int Count)>>(StringComparer.Ordinal);
        var shopOfNpc = new Dictionary<int, int>();
        LoadTablesMatching(
            name => name is HarconDefTables.StageTable or HarconDefTables.DropTable or HarconDefTables.PointTable
                || name.StartsWith(HarconDefTables.ShopTablePrefix, StringComparison.Ordinal)
                || name.StartsWith(HarconDefTables.NpcTalkTablePrefix, StringComparison.Ordinal)
                || name.StartsWith(HarconDefTables.WaveTablePrefix, StringComparison.Ordinal)
                || name.StartsWith(HarconDefTables.MonsterTablePrefix, StringComparison.Ordinal),
            (table, row) =>
            {
                switch (table)
                {
                    case HarconDefTables.StageTable:
                        stages.TryAdd(ReadInt(row, "ID"), new HarconStageEntry(
                            ReadInt(row, "ID"), ReadString(row, "Name"), ReadInt(row, "Level"), ReadInt(row, "Item"), ReadInt(row, "UnlockID"),
                            ReadInt(row, "MinP"), ReadInt(row, "MaxP"), ReadInt(row, "WaitMap"), ReadInt(row, "PlayMap"),
                            ReadString(row, "LinkSrc").Trim(), ReadInt(row, "DropItem"), ReadInt(row, "Category")));
                        return;
                    case HarconDefTables.DropTable:
                        drops.TryAdd(ReadInt(row, "ID"), new HarconDropEntry(
                            ReadInt(row, "ID"),
                            Enumerable.Range(1, HarconDefTables.RankedCoins).Select(rank => ReadInt(row, $"Win_{rank}")).ToArray(),
                            ReadInt(row, "Other"), ReadInt(row, "MinScore"), ReadInt(row, "HarconTitle")));
                        return;
                    case HarconDefTables.PointTable:
                        points = new HarconPointRule(ReadInt(row, "ATK_OneOnKill"), ReadInt(row, "EV_WaveAce"), ReadInt(row, "EV_NoDeath"), ReadInt(row, "EV_Death"));
                        return;
                }
                if (table.StartsWith(HarconDefTables.NpcTalkTablePrefix, StringComparison.Ordinal))
                {
                    var npcShop = ReadString(row, HarconDefTables.NpcShopColumn).Trim();
                    if (npcShop.StartsWith(HarconDefTables.ShopTablePrefix, StringComparison.Ordinal)
                        && int.TryParse(npcShop.AsSpan(HarconDefTables.ShopTablePrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var npcShopIndex))
                    {
                        shopOfNpc.TryAdd(ReadInt(row, "Type"), npcShopIndex);
                    }
                    return;
                }
                if (table.StartsWith(HarconDefTables.ShopTablePrefix, StringComparison.Ordinal))
                {
                    if (!int.TryParse(table.AsSpan(HarconDefTables.ShopTablePrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var shopIndex)) return;
                    var itemId = ReadInt(row, "ChangeItemId");
                    shop.TryAdd((shopIndex, itemId), new HarconShopEntry(shopIndex, ReadInt(row, "ID"), ReadInt(row, "Category"), itemId, ReadInt(row, "ChangeItemCnt"), ReadInt(row, "CoinCnt")));
                    return;
                }
                if (table.StartsWith(HarconDefTables.MonsterTablePrefix, StringComparison.Ordinal))
                {
                    if (!defended.TryGetValue(table, out var spawns))
                    {
                        spawns = new List<(int MonsterId, int RegionId, int Count)>();
                        defended[table] = spawns;
                    }
                    spawns.Add((ReadInt(row, "MonsterID"), ReadInt(row, "Region"), ReadInt(row, "Max")));
                    return;
                }
                if (!waves.TryGetValue(table, out var list))
                {
                    list = new List<HarconWave>();
                    waves[table] = list;
                }
                list.Add(new HarconWave(
                    ReadInt(row, "ID"), ReadInt(row, "Type"), ReadInt(row, "Time"), ReadInt(row, "Alarm"),
                    Enumerable.Range(1, HarconDefTables.SpawnGroups)
                        .Select(group => new HarconSpawnGroup(ReadInt(row, $"MonId{group}"), ReadInt(row, $"MaxNum{group}"), ReadInt(row, $"Rpos{group}"), ReadInt(row, $"Lump{group}"), ReadInt(row, $"Delay{group}")))
                        .Where(group => group.MonsterId != 0 && group.Total > 0)
                        .ToArray()));
            });
        return new HarconDefTables(
            stages,
            waves.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<HarconWave>)pair.Value.OrderBy(wave => wave.Id).ToArray(), StringComparer.Ordinal),
            drops,
            points,
            shop,
            defended.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<(int MonsterId, int RegionId, int Count)>)pair.Value, StringComparer.Ordinal))
        {
            ShopOfNpc = shopOfNpc,
        };
    }

    public PkInfoTables LoadPkInfo()
    {
        var info = PkInfoTables.Empty;
        LoadTable(PkInfoTables.Table, row => info = new PkInfoTables(
            ReadInt(row, "ButtonCoolTime"), ReadInt(row, "SuperTime"), ReadInt(row, "TodayPKMax"),
            ReadInt(row, "TotalPKMax"), ReadInt(row, "TotalDeathMax")));
        return info;
    }

    public CoupleTables LoadCoupleTables()
    {
        var exchange = new List<MarriageExchangeEntry>();
        var titles = new List<MarriageTitleEntry>();
        var rings = new Dictionary<int, WeddingRingEntry>();
        var dresses = new Dictionary<int, WeddingDressEntry>();
        var lines = new List<MarriageOfficiateLine>();
        var halls = new HashSet<int>();
        var quests = new List<int>();
        var skills = new List<int>();
        var eventPoints = new Dictionary<int, CoupleEventPoints>();
        static CouplePointRange Points(IReadOnlyDictionary<string, string> row) => new(
            ReadInt(row, "Min_Relation"), ReadInt(row, "Max_Relation"),
            ReadInt(row, "Min_Love"), ReadInt(row, "Max_Love"),
            ReadInt(row, "Min_Friend"), ReadInt(row, "Max_Friend"));
        LoadTablesNamed(CoupleTables.TableNames, (table, row) =>
        {
            switch (table)
            {
                case CoupleTables.StatusTable:
                    eventPoints.TryAdd(ReadInt(row, "EventType"), new CoupleEventPoints(ReadInt(row, "RelationPoint"), ReadInt(row, "LovePoint"), ReadInt(row, "FriendPoint")));
                    break;
                case CoupleTables.ExchangeTable:
                    exchange.Add(new MarriageExchangeEntry(
                        ReadInt(row, "ResID"), ReadString(row, "Name"), ReadInt(row, "ResNum"),
                        ReadInt(row, "ReqID0"), ReadInt(row, "ReqNum0"), Points(row), ReadInt(row, "Fee"), ReadInt(row, "Category")));
                    break;
                case CoupleTables.TitleTable:
                    titles.Add(new MarriageTitleEntry(ReadInt(row, "Index"), ReadString(row, "String"), Points(row)));
                    break;
                case CoupleTables.RingTable:
                    rings.TryAdd(ReadInt(row, "ItemID"), new WeddingRingEntry(ReadInt(row, "ItemID"), ReadInt(row, "SkillIdMale"), ReadInt(row, "SkillIdFemale")));
                    break;
                case CoupleTables.DressTable:
                    dresses.TryAdd(ReadInt(row, "ItemID"), new WeddingDressEntry(ReadInt(row, "ItemID"), ReadInt(row, "DressMale"), ReadInt(row, "DressFemale")));
                    break;
                case CoupleTables.OfficiateTable:
                    lines.Add(new MarriageOfficiateLine(ReadInt(row, "Index"), ReadString(row, "String"), ReadInt(row, "Scene")));
                    break;
                case CoupleTables.HallTable:
                    halls.Add(ReadInt(row, "ZoneID"));
                    break;
                case CoupleTables.DeleteTable:
                    var ids = Enumerable.Range(0, CoupleTables.DeleteIdColumns)
                        .Select(column => ReadInt(row, $"ID{column:D2}"))
                        .Where(id => id != 0);
                    var type = ReadString(row, "Type");
                    if (string.Equals(type, CoupleTables.DeleteQuestType, StringComparison.Ordinal)) quests.AddRange(ids);
                    else if (string.Equals(type, CoupleTables.DeleteSkillType, StringComparison.Ordinal)) skills.AddRange(ids);
                    break;
            }
        });
        lines.Sort((a, b) => a.Index.CompareTo(b.Index));
        return new CoupleTables(exchange, titles, rings, dresses, lines, halls, quests, skills, eventPoints);
    }

    public SlotExtendTables LoadSlotExtendTables()
    {
        var materials = new Dictionary<int, SlotExtendMaterialEntry>();
        LoadTable(SlotExtendTables.MaterialTable, row =>
        {
            var id = ReadInt(row, "ID");
            materials[id] = new SlotExtendMaterialEntry(
                id,
                Enumerable.Range(1, SlotExtendTables.SlotColumns).Select(n => ReadFloat(row, $"Slot_Ratio_{n}")).ToArray(),
                ReadFlags(row, "UsableFlag"));
        });
        var subItems = new Dictionary<int, SlotExtendSubItemEntry>();
        LoadTable(SlotExtendTables.SubItemTable, row =>
        {
            var id = ReadInt(row, "ID");
            subItems[id] = new SlotExtendSubItemEntry(
                id,
                ReadInt(row, "SubItem_Type"),
                Enumerable.Range(1, SlotExtendTables.SlotColumns).Select(n => ReadFloat(row, $"Slot_RatioAdd_{n}")).ToArray());
        });
        return new SlotExtendTables(materials, subItems);
    }

    public IReadOnlyList<RecycleChangeEntry> LoadRecycleChanges()
    {
        var entries = new List<RecycleChangeEntry>(24);
        LoadTable(RecycleChangeEntry.TableName, row => entries.Add(new RecycleChangeEntry(
            ReadInt(row, "ExchangeRes"), ReadInt(row, "ExchangeResCnt"), ReadInt(row, "ExchangeSrc"), ReadInt(row, "ExchangeSrcCnt"))));
        return entries;
    }

    public IReadOnlyList<RecycleShopEntry> LoadRecycleShop()
    {
        var entries = new List<RecycleShopEntry>(40);
        LoadTable(RecycleShopEntry.TableName, row => entries.Add(new RecycleShopEntry(
            ReadInt(row, "RecycleSrc"), ReadInt(row, "RecycleChangeCnt"), ReadInt(row, "RecycleRes"))));
        return entries;
    }

    public IReadOnlySet<int> LoadRecycleExceptItems()
    {
        var items = new HashSet<int>();
        LoadTable(RecycleShopEntry.ExceptTableName, row => items.Add(ReadInt(row, "ItemID")));
        return items;
    }

    public IReadOnlySet<int> LoadRecycleExceptMyCampItems()
    {
        var items = new HashSet<int>();
        LoadTable(RecycleShopEntry.MyCampExceptTableName, row => items.Add(ReadInt(row, "ItemID")));
        return items;
    }

    public IReadOnlyList<TransScrollEntry> LoadTransScrolls()
    {
        var entries = new List<TransScrollEntry>(90);
        LoadTable(TransScrollEntry.TableName, row => entries.Add(new TransScrollEntry(ReadInt(row, "ID"), ReadInt(row, "TransChrType"))));
        return entries;
    }

    public IReadOnlyList<GunParamEntry> LoadGunParams()
    {
        var entries = new List<GunParamEntry>(600);
        LoadTable("Item_GunParam2", row => entries.Add(new GunParamEntry(
            ReadInt(row, "ID"),
            ReadInt(row, "GunType"),
            ReadInt(row, "GaugeSpeed"),
            ReadInt(row, "PowRange"),
            ReadInt(row, "PowStartPoint"),
            ReadInt(row, "MinAttackRange"),
            ReadInt(row, "MaxAttackRange"))));
        return entries;
    }

    public IReadOnlyList<MapInfoEntry> LoadMapInfo()
    {
        var entries = new List<MapInfoEntry>(700);
        LoadTable("MapInfoEx", row =>
        {
            var id = ReadInt(row, "ID");
            if (id is < 0 or > ushort.MaxValue) return;
            var file = ReadString(row, "FileName").Replace('\\', '/');
            entries.Add(new MapInfoEntry
            {
                Id = (ushort)id,
                Name = Translate("MapInfoEx", id, "Name", ReadString(row, "Name")),
                Md3FileName = file[(file.LastIndexOf('/') + 1)..],
                NpcTable = ReadString(row, "NpcTable"),
                MonsterTable = ReadString(row, "MonsterTable"),
                BossMonsterTable = ReadString(row, "BossMonsterTable"),
                ItemTable = ReadString(row, "ItemTable"),
                ShopTable = ReadString(row, "ShopTable"),
                SkillShopTable = ReadString(row, "SkillShopTable"),
                MonsterAvrNum = ReadInt(row, "MonsterAvrNum"),
                MaxDepth = ReadInt(row, "MaxDepth"),
                BossMonsterCnt = ReadInt(row, "BossMonsterCnt"),
                BossMonCheckTime = ReadInt(row, "BossMonCheckTime"),
            });
        });
        return entries;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<MonsterPoolRow>> LoadAllMonsterPoolTables()
    {
        var tables = new Dictionary<string, List<MonsterPoolRow>>(StringComparer.Ordinal);
        LoadTablesWithPrefix("Mon_", (tableName, row) =>
        {
            if (!tableName.StartsWith("Mon_", StringComparison.Ordinal)) return;
            if (!tables.TryGetValue(tableName, out var rows))
            {
                rows = new List<MonsterPoolRow>();
                tables[tableName] = rows;
            }
            rows.Add(new MonsterPoolRow(
                ReadInt(row, "MonsterID"),
                ReadInt(row, "Avr"),
                ReadInt(row, "Max"),
                ReadDouble(row, "Ratio"),
                ReadInt(row, "Region")));
        });
        return tables.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MonsterPoolRow>)pair.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<RebirthItemEntry> LoadRebirthItems()
    {
        var entries = new List<RebirthItemEntry>(16);
        LoadTable("Item_RebirthParam", row => entries.Add(new RebirthItemEntry(
            ReadInt(row, "ItemId"),
            ReadFloat(row, "fHpRestoreRatio"),
            ReadFloat(row, "fMpRestoreRatio"),
            ReadFloat(row, "fExpRestoreRatio"),
            ReadFloat(row, "fTmxpRestoreRatio"))));
        return entries;
    }

    public IReadOnlyDictionary<int, IReadOnlyList<(int MonsterId, int Count)>> LoadNpcSummonSets(string tableName, int columns)
    {
        var sets = new Dictionary<int, IReadOnlyList<(int MonsterId, int Count)>>();
        LoadTable(tableName, row => sets.TryAdd(ReadInt(row, "ID"), Enumerable.Range(0, columns)
            .Select(column => (ReadInt(row, $"SrcMonster{column}"), ReadInt(row, $"SummonCnt{column}")))
            .Where(entry => entry.Item1 != 0 && entry.Item2 > 0)
            .ToArray()));
        return sets;
    }

    public IReadOnlyList<CardSpecialSetEntry> LoadCardSpecialSets()
    {
        var sets = new List<CardSpecialSetEntry>(40);
        LoadTable(CardSpecialSetEntry.TableName, row =>
        {
            var cards = Enumerable.Range(0, CardSpecialSetEntry.CardColumns).Select(column => ReadInt(row, $"CardId{column}")).ToArray();
            if (cards.All(card => card == 0)) return;
            var presents = Enumerable.Range(0, CardSpecialSetEntry.PresentColumns)
                .Select(column => (ReadInt(row, $"PresentItem{column}"), ReadFloat(row, $"ItemRate{column}")))
                .ToArray();
            sets.Add(new CardSpecialSetEntry(ReadInt(row, "Id"), ReadInt(row, "Grade"), cards, presents));
        });
        return sets;
    }

    public IReadOnlyList<(int ItemId, int MinLevel, int MaxLevel, bool CashPet)> LoadPetComposingPresents(string tableName)
    {
        var rows = new List<(int ItemId, int MinLevel, int MaxLevel, bool CashPet)>();
        LoadTable(tableName, row => rows.Add((ReadInt(row, "ItemId"), ReadInt(row, "PetMinLv"), ReadInt(row, "PetMaxLv"), ReadInt(row, "CMPetType") != 0)));
        return rows;
    }

    public IReadOnlyDictionary<int, int> LoadKeyedItems(string tableName, string keyColumn, string itemColumn)
    {
        var rows = new Dictionary<int, int>();
        LoadTable(tableName, row => rows.TryAdd(ReadInt(row, keyColumn), ReadInt(row, itemColumn)));
        return rows;
    }

    public PartyZoneTables LoadPartyZoneTables()
    {
        var reEnter = new Dictionary<int, int>();
        LoadTable(PartyZoneTables.InfoTable, row => reEnter.TryAdd(ReadInt(row, "ID"), ReadInt(row, "limitMinutesReEnter")));
        var bossTables = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        LoadTable("MapInfoEx", row =>
        {
            var table = ReadString(row, "BossMonsterTable").Trim();
            if (table.Length == 0) return;
            if (!bossTables.TryGetValue(table, out var zones))
            {
                zones = new List<int>();
                bossTables[table] = zones;
            }
            zones.Add(ReadInt(row, "ID"));
        });
        var bosses = new Dictionary<int, HashSet<int>>();
        LoadTablesMatching(bossTables.ContainsKey, (table, row) =>
        {
            foreach (var zone in bossTables[table])
            {
                if (!bosses.TryGetValue(zone, out var ids))
                {
                    ids = new HashSet<int>();
                    bosses[zone] = ids;
                }
                ids.Add(ReadInt(row, "BossMonID"));
            }
        });
        return new PartyZoneTables(reEnter, bosses.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<int>)pair.Value));
    }

    public IReadOnlyList<MapMonsterTableEntry> LoadMapMonsterTables()
    {
        var entries = new List<MapMonsterTableEntry>(700);
        LoadTable("MapInfoEx", row => entries.Add(new MapMonsterTableEntry(
            ReadInt(row, "ID"),
            ReadString(row, "MonsterTable").Trim(),
            ReadInt(row, "MonsterAvrNum"))));
        return entries;
    }

    public IReadOnlyList<(int Level, int NextTmExp)> LoadTmLevels()
    {
        var rows = new List<(int Level, int NextTmExp)>(601);
        LoadTable("TM_LEVEL2", row => rows.Add((ReadInt(row, "Level"), ReadInt(row, "NextTmxp"))));
        return rows;
    }

    public IReadOnlyDictionary<string, string> LoadNameValueTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        LoadTable(tableName, row =>
        {
            var name = ReadString(row, "Name").Trim();
            if (name.Length > 0) values.TryAdd(name, Translate(tableName, name, "Value", ReadString(row, "Value")));
        });
        return values;
    }

    public IReadOnlyDictionary<string, string> LoadNameStringTable(string tableName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        LoadTable(tableName, row =>
        {
            var name = ReadString(row, "Name").Trim();
            if (name.Length > 0) strings.TryAdd(name, Translate(tableName, name, "String", ReadString(row, "String")));
        });
        return strings;
    }

    public BeginnerRebirthInfo? LoadBeginnerRebirthInfo()
    {
        BeginnerRebirthInfo? first = null;
        LoadTable("BeginnerRebirthInfo", row => first ??= new BeginnerRebirthInfo(
            ReadInt(row, "Level"),
            ReadFloat(row, "RestoreHpRatio"),
            ReadFloat(row, "RestoreMpRatio"),
            ReadFloat(row, "RestoreExpRatio"),
            ReadFloat(row, "RestoreTmxpRatio")));
        return first;
    }

    public IReadOnlyList<NpcActionScriptEntry> LoadNpcActionScript()
        => LoadMotionTable("NPCMotionInfo");

    public IReadOnlyList<NpcActionScriptEntry> LoadShopNpcActionScript()
        => LoadMotionTable("ShopNPCMotionInfo");

    public IReadOnlyList<NpcActionScriptEntry> LoadSkillShopNpcActionScript()
        => LoadMotionTable("SkillShopNPCMotionInfo");

    private List<NpcActionScriptEntry> LoadMotionTable(string tableName)
    {
        var entries = new List<NpcActionScriptEntry>(200);
        LoadTable(tableName, row =>
        {
            var type = ReadInt(row, "Type");
            var maxCnt = ReadInt(row, "MaxCnt");
            var steps = new List<NpcActionScriptStep>(Math.Max(0, maxCnt));
            for (var i = 0; i < maxCnt; i++)
            {
                if (!row.ContainsKey($"Action_{i}")) break;
                steps.Add(new NpcActionScriptStep(
                    ReadInt(row, $"Action_{i}"),
                    ReadInt(row, $"Loop_{i}"),
                    ReadInt(row, $"AniTime_{i}"),
                    Translate(tableName, type, $"Speech_{i}", ReadString(row, $"Speech_{i}"))));
            }
            entries.Add(new NpcActionScriptEntry
            {
                Type = type,
                MaxCnt = maxCnt,
                Steps = steps,
            });
        });
        return entries;
    }

    public IReadOnlyList<LevelUpGiftEntry> LoadLevelUpGifts()
    {
        var gifts = new List<LevelUpGiftEntry>(40);
        LoadTable("PostBoxLevelUpGiftInfo", row =>
        {
            var items = new List<(int ItemId, int Count)>();
            for (var i = 0; row.ContainsKey($"ItemId_{i:D2}"); i++)
            {
                var itemId = ReadInt(row, $"ItemId_{i:D2}");
                var count = ReadInt(row, $"ItemCount_{i:D2}");
                if (itemId != 0 && count > 0) items.Add((itemId, count));
            }
            gifts.Add(new LevelUpGiftEntry(
                ReadInt(row, "Id"),
                ReadInt(row, "Level"),
                ReadFlags(row, "ChrType"),
                ReadString(row, "Sender").Trim(),
                ReadString(row, "Title").Trim(),
                ReadString(row, "Message").Trim(),
                items));
        });
        return gifts;
    }

    public IReadOnlyList<SetItemEntry> LoadSetItems()
    {
        string[] bonusColumns = { "APPlus", "ACPlus", "DXPlus", "MaxMPPlus", "MAPlus", "MDPlus", "MaxWTPlus", "DAPlus", "LKPlus", "MaxHPPlus", "DPPlus", "HVPlus" };
        var sets = new List<SetItemEntry>(256);
        LoadTable("SetItemParam", row => sets.Add(new SetItemEntry(
            ReadInt(row, "ID"),
            Enumerable.Range(0, 7).Select(i => ReadInt(row, $"ItemID{i}")).Where(id => id != 0).ToArray(),
            bonusColumns.Select(column => ReadInt(row, column)).ToArray(),
            ReadString(row, "SetEffect").Trim())));
        return sets;
    }

    public IReadOnlyDictionary<int, int> LoadPostSetup()
    {
        var values = new Dictionary<int, int>();
        LoadTable("PostSetupInfo", row => values[ReadInt(row, "SetupType")] = ReadInt(row, "Value"));
        return values;
    }

    public IReadOnlyList<TutorialSystemEntry> LoadTutorialSystem()
    {
        var entries = new List<TutorialSystemEntry>(32);
        LoadTable("TutorialSystem", row => entries.Add(new TutorialSystemEntry
        {
            Id = ReadInt(row, "ID"),
            OpenType = ReadInt(row, "OpenType"),
            QuestId = ReadInt(row, "QuestID"),
            StartNpcID = ReadInt(row, "StartNpcID"),
            EndNpcID = ReadInt(row, "EndNpcID"),
            StartMapID = ReadInt(row, "StartMapID"),
            MonsterMapID = ReadInt(row, "MonsterMapID"),
            EndMapID = ReadInt(row, "EndMapID"),
            DlgTable = ReadString(row, "DlgTable"),
            DlgIndex = ReadInt(row, "DlgIndex"),
            UIOpen = ReadInt(row, "UIOpen"),
            UIOpenType = ReadInt(row, "UIOpenType"),
        }));
        return entries;
    }

    public IReadOnlyList<MonsterQuestInfoEntry> LoadMonsterQuestInfo()
    {
        var entries = new List<MonsterQuestInfoEntry>(400);
        LoadTable("MonsterQuestInfo", row =>
        {
            var id = ReadInt(row, "Id");
            entries.Add(new MonsterQuestInfoEntry
        {
            Id = id,
            Name = Translate("MonsterQuestInfo", id, "Name", ReadString(row, "Name")),
            Client = Translate("MonsterQuestInfo", id, "Client", ReadString(row, "Client")),
            Pos = Translate("MonsterQuestInfo", id, "Pos", ReadString(row, "Pos")),
            Comment = Translate("MonsterQuestInfo", id, "Comment", ReadString(row, "Comment")),
            MinLevel = ReadInt(row, "MinLevel"),
            MaxLevel = ReadInt(row, "MaxLevel"),
            SrcMonster0 = ReadInt(row, "SrcMonster0"),
            Cnt0 = ReadInt(row, "Cnt0"),
            Time0 = ReadInt(row, "Time0"),
            MinLv0 = ReadInt(row, "MinLv0"),
            MaxLv0 = ReadInt(row, "MaxLv0"),
            Exp0 = ReadInt(row, "Exp0"),
            Tmxp0 = ReadInt(row, "Tmxp0"),
            MapId0 = ReadInt(row, "MapId0"),
            SrcMonster1 = ReadInt(row, "SrcMonster1"),
            Cnt1 = ReadInt(row, "Cnt1"),
            Time1 = ReadInt(row, "Time1"),
            MinLv1 = ReadInt(row, "MinLv1"),
            MaxLv1 = ReadInt(row, "MaxLv1"),
            Exp1 = ReadInt(row, "Exp1"),
            Tmxp1 = ReadInt(row, "Tmxp1"),
            MapId1 = ReadInt(row, "MapId1"),
            SrcMonster2 = ReadInt(row, "SrcMonster2"),
            Cnt2 = ReadInt(row, "Cnt2"),
            Time2 = ReadInt(row, "Time2"),
            MinLv2 = ReadInt(row, "MinLv2"),
            MaxLv2 = ReadInt(row, "MaxLv2"),
            Exp2 = ReadInt(row, "Exp2"),
            Tmxp2 = ReadInt(row, "Tmxp2"),
            MapId2 = ReadInt(row, "MapId2"),
            SrcMonster3 = ReadInt(row, "SrcMonster3"),
            Cnt3 = ReadInt(row, "Cnt3"),
            Time3 = ReadInt(row, "Time3"),
            MinLv3 = ReadInt(row, "MinLv3"),
            MaxLv3 = ReadInt(row, "MaxLv3"),
            Exp3 = ReadInt(row, "Exp3"),
            Tmxp3 = ReadInt(row, "Tmxp3"),
            MapId3 = ReadInt(row, "MapId3"),
            SrcMonster4 = ReadInt(row, "SrcMonster4"),
            Cnt4 = ReadInt(row, "Cnt4"),
            Time4 = ReadInt(row, "Time4"),
            MinLv4 = ReadInt(row, "MinLv4"),
            MaxLv4 = ReadInt(row, "MaxLv4"),
            Exp4 = ReadInt(row, "Exp4"),
            Tmxp4 = ReadInt(row, "Tmxp4"),
            MapId4 = ReadInt(row, "MapId4"),
            ResultTable = ReadString(row, "ResultTable"),
            MaxCount = ReadInt(row, "MaxCount"),
            ReturnMapID = ReadInt(row, "ReturnMapID"),
            });
        });
        return entries;
    }

    public IReadOnlyList<QuestItemInfoEntry> LoadQuestItemInfo()
    {
        var entries = new List<QuestItemInfoEntry>();
        LoadTable("QuestItemInfo", row => entries.Add(new QuestItemInfoEntry
        {
            Id = ReadInt(row, "ID"),
            QuestId = ReadInt(row, "QID"),
            ItemId = ReadInt(row, "ItemId"),
            RefineLevel = ReadInt(row, "RefnLev"),
        }));
        return entries;
    }

    public IReadOnlyList<ItemParamEntry> LoadItemParamExtended()
    {
        var entries = new List<ItemParamEntry>(1100);
        LoadTable("ItemParamEx", row => entries.Add(MapItemParamRow(row)));
        return entries;
    }

    public IReadOnlyList<ItemParamEntry> LoadItemParamEx2()
    {
        var entries = new List<ItemParamEntry>(2600);
        LoadTable("ItemParamEx2", row => entries.Add(MapItemParamRow(row)));
        return entries;
    }

    public IReadOnlyList<ItemParamEntry> LoadItemParamCm2()
    {
        var entries = new List<ItemParamEntry>(6200);
        LoadTable("ItemParamCM2", row => entries.Add(MapItemParamRow(row)));
        return entries;
    }

    public IReadOnlyList<ItemParamEntry> LoadItemParam2()
    {
        var entries = new List<ItemParamEntry>(6500);
        LoadTable("ItemParam2", row => entries.Add(MapItemParamRow(row)));
        return entries;
    }

    private static ItemParamEntry MapItemParamRow(IReadOnlyDictionary<string, string> row) => new()
    {
        Id = ReadInt(row, "ID"),
        Class = ReadInt(row, "Class"),
        Type = ReadInt(row, "Type"),
        SubType = ReadInt(row, "SubType"),
        Name = ReadString(row, "Name"),
        NameEng = ReadString(row, "Name_Eng"),
        MinLevel = ReadInt(row, "MinLevel"),
        Weight = ReadInt(row, "Weight"),
        Value = ReadInt(row, "Value"),
        FileName = ReadString(row, "FileName"),
        BundleNum = ReadInt(row, "BundleNum"),
        InvFileName = ReadString(row, "InvFileName"),
        InvBundleNum = ReadInt(row, "InvBundleNum"),
        Effect = ReadInt(row, "Effect"),
        ExistType = ReadInt(row, "ExistType"),
        Hp = ReadInt(row, "HP"),
        HpCon = ReadInt(row, "HPCon"),
        MpCon = ReadInt(row, "MPCon"),
        Ap = ReadInt(row, "AP"),
        HpRecoveryRate = ReadFloat(row, "HPRecoveryRate"),
        MpRecoveryRate = ReadFloat(row, "MPRecoveryRate"),
        RefineType = ReadInt(row, "RefineType"),
        RefineIndex = ReadInt(row, "RefineIndex"),
        CompoundSlot = ReadInt(row, "CompoundSlot"),
        Mp = ReadInt(row, "MP"),
        Options = ReadFlags(row, "Options"),
        Life = ReadInt(row, "Life"),
        CardNum = ReadInt(row, "CardNum"),
        ItemFType = ReadInt(row, "ItemFType"),
        ExtendedOptions = ReadFlags(row, "OptionsEx"),
        CharacterTypeFlags = ReadFlags(row, "ChrTypeFlags"),
        CharacterGroupFlags = ReadFlags(row, "ChrFTypeFlag"),
        CharacterGender = ReadInt(row, "ChrGender"),
        MinStatType = ReadInt(row, "MinStatType"),
        MinStatLevel = ReadInt(row, "MinStatLv"),
        StatBonuses = ItemStatColumns.Select(column => ReadInt(row, column)).ToArray(),
        GroupId = ReadInt(row, "GroupId"),
        ReformCount = ReadInt(row, "ReformCount"),
    };

    private static readonly string[] ItemStatColumns =
    {
        "APPlus", "ACPlus", "DXPlus", "MaxMPPlus", "MAPlus", "MDPlus",
        "MaxWTPlus", "DAPlus", "LKPlus", "MaxHPPlus", "DPPlus", "HVPlus",
    };

    public IReadOnlyList<SkillCardEntry> LoadSkillCards()
    {
        var entries = new List<SkillCardEntry>(400);
        LoadTable("Item_SkillCardParam", row => entries.Add(new SkillCardEntry
        {
            Id = ReadInt(row, "ID"),
            Name = ReadString(row, "Name"),
            CardType = ReadInt(row, "SCardType"),
            TmLevel = ReadInt(row, "TMLV"),
            SubCardType = ReadInt(row, "SubSCardType"),
            SubTmLevel = ReadInt(row, "SubTMLV"),
            AffectionPoint = ReadInt(row, "AffectionPoint"),
            AffectionSkill = ReadInt(row, "AffectionSkill"),
            RegisterLevel = ReadInt(row, "RegisterLevel"),
            PreSkills = Enumerable.Range(1, 3)
                .Select(n => new SkillRequirement(ReadInt(row, $"PreSkill_{n}"), ReadInt(row, $"PreSkillLv_{n}")))
                .Where(requirement => requirement.SkillId != 0)
                .ToArray(),
            Job = ReadString(row, "Job"),
            Options = ReadFlags(row, "Options"),
            JobType1 = ReadFlags(row, "JobType1"),
            JobType2 = ReadFlags(row, "JobType2"),
        }));
        return entries;
    }

    public IReadOnlyList<SkillParam2Entry> LoadSkillParam2()
    {
        var entries = new List<SkillParam2Entry>(320);
        LoadTable("SkillParam2", row => entries.Add(new SkillParam2Entry
        {
            Id = ReadInt(row, "ID"),
            Type = ReadInt(row, "Type"),
            Name = ReadString(row, "Name"),
            SkillOption = ReadFlags(row, "SkillOption"),
            AffectionRel = ReadFlags(row, "AffectionRel"),
            InvocationCondition = ReadInt(row, "ESAIC"),
            TargetInput = ReadInt(row, "ESAUI"),
            DebuffBitCode = ReadFlags(row, "DebuffBitCode"),
            EState = ReadInt(row, "EState"),
            EsClass = ReadInt(row, "ESClass"),
            SkillExp = ReadInt(row, "SkillExp"),
            SkillCardId = ReadInt(row, "SkillCardID"),
            LevelUp = ReadBool(row, "bLevelUp"),
            MaxSkillLevel = ReadInt(row, "MaxSKLV"),
            MasterTmPoint = ReadInt(row, "MasterTMPoint"),
            EsAction = ReadInt(row, "ESAction"),
            EsaTable = ReadString(row, "ESATable"),
            EsaiiTable = ReadString(row, "ESAIITable"),
            SpellType = ReadInt(row, "SpellType"),
            SkillSymbolType = ReadInt(row, "eSkillSymbolType"),
            Element = ReadInt(row, "Element"),
            Support = ReadBool(row, "Surpport"),
            Grade = ReadInt(row, "Grade"),
            UseStyle = ReadFlags(row, "UseStyle"),
            StateType = ReadInt(row, "StateType"),
            PSAbility = ReadInt(row, "PSAbility"),
            ItemInvokeType = ReadInt(row, "ItemInvokeType"),
        }));
        return entries;
    }

    public IReadOnlyList<MonItemDropInfoEntry> LoadMonItemDropInfo()
    {
        var entries = new List<MonItemDropInfoEntry>(800);
        LoadTable("MonItemDropInfo", row =>
        {
            var items = new List<DropItem>(10);
            for (var i = 0; i < 10; i++)
            {
                var itemId = ReadInt(row, $"Item{i}");
                var rate = ReadFloat(row, $"Rate{i}");
                var cnt = ReadInt(row, $"Cnt{i}");
                if (itemId > 0 && rate > 0)
                {
                    items.Add(new DropItem(itemId, rate, Math.Max(1, cnt)));
                }
            }
            var sub = new List<DropTableRef>(5);
            for (var i = 0; i < 5; i++)
            {
                var tableId = ReadInt(row, $"Table{i}");
                var tableRate = ReadFloat(row, $"TableRate{i}");
                if (tableId > 0 && tableRate > 0)
                {
                    sub.Add(new DropTableRef(tableId, tableRate));
                }
            }
            entries.Add(new MonItemDropInfoEntry
            {
                Id = ReadInt(row, "ID"),
                Items = items,
                SubTables = sub,
            });
        });
        return entries;
    }

    public IReadOnlyList<MonItemDropInfoEntry> LoadMonsterDropRows()
    {
        var entries = new List<MonItemDropInfoEntry>(1100);
        LoadTable("MonItemDropInfo", row =>
        {
            var items = new List<DropItem>(10);
            for (var i = 0; i < 10; i++)
            {
                var itemId = ReadInt(row, $"Item{i}");
                if (itemId == 0) continue;
                var count = ReadInt(row, $"Cnt{i}");
                items.Add(new DropItem(itemId, ReadFloat(row, $"Rate{i}"), count == 0 ? 1 : count));
            }
            var tables = new List<DropTableRef>(5);
            for (var i = 0; i < 5; i++)
            {
                var tableId = ReadInt(row, $"Table{i}");
                if (tableId == 0) continue;
                tables.Add(new DropTableRef(tableId, ReadFloat(row, $"TableRate{i}")));
            }
            entries.Add(new MonItemDropInfoEntry
            {
                Id = ReadInt(row, "ID"),
                Items = items,
                SubTables = tables,
            });
        });
        return entries;
    }

    public IReadOnlyList<MonItemTableDropEntry> LoadMonsterDropTables()
    {
        var entries = new List<MonItemTableDropEntry>(160);
        LoadTable("MonItemTable_DropInfo", row =>
        {
            var slots = new DropItem[MonItemTableDropEntry.SlotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                var count = ReadInt(row, $"Cnt{i}");
                slots[i] = new DropItem(ReadInt(row, $"Item{i}"), ReadFloat(row, $"Rate{i}"), count == 0 ? 1 : count);
            }
            entries.Add(new MonItemTableDropEntry
            {
                Id = ReadInt(row, "ID"),
                Slots = slots,
            });
        });
        return entries;
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>> LoadMonsterProtections(IEnumerable<string> tableNames)
    {
        ArgumentNullException.ThrowIfNull(tableNames);
        var wanted = tableNames.Select(name => name.Trim()).Where(name => name.Length > 0).ToHashSet(StringComparer.Ordinal);
        var ratios = wanted.ToDictionary(name => name, _ => new Dictionary<int, int>(), StringComparer.Ordinal);
        LoadTablesNamed(wanted, (tableName, row) =>
        {
            var skillId = ReadInt(row, ProtectSkillColumn);
            var ratio = ReadInt(row, ProtectRatioColumn);
            if (skillId == 0 || ratio <= 0) return;
            var table = ratios[tableName];
            table[skillId] = table.GetValueOrDefault(skillId) + ratio;
        });
        return ratios.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<int, int>)pair.Value, StringComparer.Ordinal);
    }

    private const string ProtectSkillColumn = "ProtectID";
    private const string ProtectRatioColumn = "Ratio";

    public IReadOnlyList<MonsterParamEntry> LoadMonsterParamEx2()
    {
        var entries = new List<MonsterParamEntry>(1500);
        LoadTable("MonsterParamEx2", row =>
        {
            var id = ReadInt(row, "ID");
            entries.Add(new MonsterParamEntry
        {
            Id = id,
            Type = ReadInt(row, "Type"),
            Name = Translate("MonsterParamEx2", id, "Name", ReadString(row, "Name")),
            Level = ReadInt(row, "Level"),
            MaxHpLv = ReadInt(row, "MaxHpLv"),
            MaxMpLv = ReadInt(row, "MaxMpLv"),
            ApLv = ReadInt(row, "ApLv"),
            AcLv = ReadInt(row, "AcLv"),
            DxLv = ReadInt(row, "DxLv"),
            DpLv = ReadInt(row, "DpLv"),
            MaLv = ReadInt(row, "MaLv"),
            MdLv = ReadInt(row, "MdLv"),
            MaxWtLv = ReadInt(row, "MaxWtLv"),
            DaLv = ReadInt(row, "DaLv"),
            LkLv = ReadInt(row, "LkLv"),
            HvLv = ReadInt(row, "HvLv"),
            Exp = ReadInt(row, "EXP"),
            Tm = ReadInt(row, "TM"),
            GMin = ReadInt(row, "GMin"),
            GMax = ReadInt(row, "GMax"),
            GelderRate = ReadDouble(row, "GelderRate"),
            ItemDropCnt = ReadInt(row, "ItemDropCnt"),
            ItemDropIdx = ReadInt(row, "ItemDropIdx"),
            HpRegen = ReadInt(row, "HpRegen"),
            MpRegen = ReadInt(row, "MpRegen"),
            IsBoss = ReadBool(row, "IsBoss"),
            WalkSpeed = ReadFloat(row, "WalkSpeed"),
            RunSpeed = ReadFloat(row, "RunSpeed"),
            MaxStay = ReadFloat(row, "MaxStay"),
            MinStay = ReadFloat(row, "MinStay"),
            ChaseTime = ReadFloat(row, "ChaseTime"),
            AiViewRange = ReadInt(row, "aiViewRange"),
            AiAttackDispos = ReadInt(row, "aiAttackDispos"),
            AiTargetSetting1 = ReadInt(row, "aiTargetSetting1"),
            AiTargetSetting2 = ReadInt(row, "aiTargetSetting2"),
            AiDamageResponse = ReadInt(row, "aiDamageResponse"),
            AiDamageSpecial = ReadInt(row, "aiDamageSpecial"),
            AiAttackLater = ReadInt(row, "aiAtackLater"),
            AiGroupPolicy = ReadInt(row, "aiGroupPolicy"),
            AiHelpResponse = ReadInt(row, "aiHelpResponse"),
            AiRetreatKind = ReadInt(row, "aiRetreatKind"),
            AiMoveArea = ReadInt(row, "aiMoveArea"),
            AiMoveKind = ReadInt(row, "aiMoveKind"),
            AiRetreatArea = ReadInt(row, "aiRetreatArea"),
            AiRetreatHp = ReadInt(row, "aiRetreatHp"),
            Speech = ReadString(row, "Speech"),
            DoesCntMonRetr = ReadBool(row, "DoesCntMonRetr"),
            RetrMonNum = ReadInt(row, "RetrMonNum"),
            TacticsTable = ReadString(row, "TacticsTable"),
            FireR = ReadInt(row, "FireR"),
            WaterR = ReadInt(row, "WaterR"),
            WindR = ReadInt(row, "WindR"),
            EarthR = ReadInt(row, "EarthR"),
            ElecR = ReadInt(row, "ElecR"),
            LightR = ReadInt(row, "LightR"),
            DarkR = ReadInt(row, "DarkR"),
            NoPropR = ReadInt(row, "NoPropR"),
            PhysicalR = ReadInt(row, "PhysicalR"),
            GunR = ReadInt(row, "GunR"),
            ShadowR = ReadInt(row, "ShadowR"),
            MonFtype = ReadInt(row, "MonFtype"),
            PropertyInfo = ReadString(row, "PropertyInfo"),
            PropertyType = ReadInt(row, "PropertyType"),
            PropertyRatio = ReadInt(row, "PropertyRatio"),
            ProtectTable = ReadString(row, "ProtectTable"),
            AccFormula = ReadBool(row, "AccFormula"),
            DebuffSet = ReadInt(row, "DebuffSet"),
            DamageSkill = ReadInt(row, "DamageSkill"),
            DamageSkillEffect = ReadInt(row, "DamageSkillEffect"),
            });
        });
        return entries;
    }

    public IReadOnlyList<RegionInfoEntry> LoadAllRegionInfoTables()
    {
        var entries = new List<RegionInfoEntry>(800);
        LoadTablesWithPrefix("RegionInfo_", (tableName, row) =>
        {
            var mapName = tableName.StartsWith("RegionInfo_", StringComparison.Ordinal)
                ? tableName["RegionInfo_".Length..]
                : tableName;
            entries.Add(new RegionInfoEntry
            {
                MapName = mapName,
                RegionId = ReadInt(row, "ZoneID"),
                LTcoordX = ReadInt(row, "LTcoordX"),
                LTcoordY = ReadInt(row, "LTcoordY"),
                RBcoordX = ReadInt(row, "RBcoordX"),
                RBcoordY = ReadInt(row, "RBcoordY"),
            });
        });
        return entries;
    }

    public IReadOnlyList<MonZoneEntry> LoadAllMonZoneTables()
    {
        var entries = new List<MonZoneEntry>(2000);
        LoadTablesWithPrefix("Mon_", (tableName, row) =>
        {
            if (!tableName.StartsWith("Mon_", StringComparison.Ordinal)) return;
            var mapName = tableName["Mon_".Length..];
            entries.Add(new MonZoneEntry
            {
                MapName = mapName,
                MonsterId = ReadInt(row, "MonsterID"),
                Ratio = ReadFloat(row, "Ratio"),
                Avr = ReadInt(row, "Avr"),
                Max = ReadInt(row, "Max"),
                TimeTable = ReadString(row, "TimeTable"),
                Region = ReadInt(row, "Region"),
                RetrMonNumType = ReadInt(row, "RetrMonNumType"),
                RetrMonNum = ReadInt(row, "RetrMonNum"),
            });
        });
        return entries;
    }

    public IReadOnlyList<MonsterDebuffResistEntry> LoadMonsterDebuffResists()
    {
        var entries = new List<MonsterDebuffResistEntry>(8);
        LoadTable("MonsterDebuffResist", row => entries.Add(new MonsterDebuffResistEntry(
            ReadInt(row, "ID"), MonsterDebuffResistEntry.Columns.Select(column => ReadInt(row, column)).ToArray())));
        return entries;
    }

    public IReadOnlyList<MonsterTacticEntry> LoadMonsterTactics()
    {
        var entries = new List<MonsterTacticEntry>(1800);
        LoadTable("Monster_TacticsEX", row => entries.Add(new MonsterTacticEntry
        {
            Id = ReadInt(row, "ID"),
            TacticsName = ReadString(row, "TacticsName").Trim(),
            FactorType = ReadInt(row, "FactorType"),
            FactorValue = ReadInt(row, "FactorValue"),
            ActionType = ReadInt(row, "ActionType"),
            SkillId = ReadInt(row, "Skill_ID"),
            SkillLevel = ReadInt(row, "SkillLevel"),
            TimeSeconds = ReadInt(row, "Time"),
            ActionRatio = ReadInt(row, "ActionRatio"),
            Option = ReadInt(row, "Option"),
            RandomWeight = ReadInt(row, "RandomWeight"),
            Speech = ReadString(row, "Speech"),
        }));
        return entries;
    }

    public IReadOnlyList<TacticsMonEntry> LoadAllTacticsMonTables()
    {
        var entries = new List<TacticsMonEntry>(800);
        LoadTablesWithPrefix("Tactics_Mon", (tableName, row) =>
        {
            if (!tableName.StartsWith("Tactics_Mon", StringComparison.Ordinal)) return;
            var idStr = tableName["Tactics_Mon".Length..];
            if (!int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var monsterId)) return;
            entries.Add(new TacticsMonEntry
            {
                MonsterId = monsterId,
                TacticsId = ReadInt(row, "TacticsID"),
                Probability = ReadInt(row, "Probability"),
                MinHpRatio = ReadInt(row, "MinHpRatio"),
                MaxHpRatio = ReadInt(row, "MaxHpRatio"),
                Action = ReadString(row, "Action"),
                Param1 = ReadInt(row, "Param1"),
                Param2 = ReadInt(row, "Param2"),
                Param3 = ReadInt(row, "Param3"),
            });
        });
        return entries;
    }

    public IReadOnlyList<MonAITacticsEntry> LoadAllMonAITacticsTables()
    {
        var entries = new List<MonAITacticsEntry>(800);
        LoadTablesWithPrefix("MonAI_Tactics_Mon", (tableName, row) =>
        {
            if (!tableName.StartsWith("MonAI_Tactics_Mon", StringComparison.Ordinal)) return;
            var idStr = tableName["MonAI_Tactics_Mon".Length..];
            if (!int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var monsterId)) return;
            entries.Add(new MonAITacticsEntry
            {
                MonsterId = monsterId,
                StateId = ReadInt(row, "StateID"),
                Action = ReadString(row, "Action"),
                Param1 = ReadInt(row, "Param1"),
                Param2 = ReadInt(row, "Param2"),
                Param3 = ReadInt(row, "Param3"),
            });
        });
        return entries;
    }

    public IReadOnlyList<ShopZoneEntry> LoadAllShopZoneTables()
    {
        var entries = new List<ShopZoneEntry>(200);
        LoadTablesWithPrefix("Shop_", (tableName, row) =>
        {
            if (!tableName.StartsWith("Shop_", StringComparison.Ordinal)) return;
            if (tableName.StartsWith("ShopItem_", StringComparison.Ordinal)) return;
            var mapName = tableName["Shop_".Length..];
            entries.Add(new ShopZoneEntry
            {
                MapName = mapName,
                NpcType = ReadInt(row, "NpcType"),
                PosId = ReadInt(row, "PosId"),
                PurchaseRate = ReadFloat(row, "PurchaseRate"),
                SellingRate = ReadFloat(row, "SellingRate"),
                ItemTable = ReadString(row, "ItemTable"),
                Script = ReadString(row, "Script"),
            });
        });
        return entries;
    }

    public IReadOnlyList<ShopGoodsEntry> LoadShopGoods(IReadOnlySet<string> tableNames)
    {
        ArgumentNullException.ThrowIfNull(tableNames);
        var entries = new List<ShopGoodsEntry>(2000);
        LoadTablesNamed(tableNames, (tableName, row) => entries.Add(new ShopGoodsEntry
        {
            Table = tableName,
            ItemId = ReadInt(row, "ItemId"),
            PurchaseRate = ReadFloat(row, "PurchaseRate"),
            SellingRate = ReadFloat(row, "SellingRate"),
            OutOfStock = ReadInt(row, "OutOfStock") != 0,
        }));
        return entries;
    }

    public IReadOnlyList<(int MapId, float ShopPriceRate)> LoadShopPriceRates()
    {
        var entries = new List<(int MapId, float ShopPriceRate)>(700);
        LoadTable("MapInfoEx", row => entries.Add((ReadInt(row, "ID"), ReadFloat(row, "ShopPriceRate"))));
        return entries;
    }

    public IReadOnlyList<SkillShopZoneEntry> LoadAllSkillShopZoneTables()
    {
        var entries = new List<SkillShopZoneEntry>(100);
        LoadTablesWithPrefix("SkillShopTalk_", (tableName, row) =>
        {
            if (!tableName.StartsWith("SkillShopTalk_", StringComparison.Ordinal)) return;
            var mapName = tableName["SkillShopTalk_".Length..];
            entries.Add(new SkillShopZoneEntry
            {
                MapName = mapName,
                NpcType = ReadInt(row, "Type"),
                PosId = ReadInt(row, "PosId"),
                JobQuestId = ReadInt(row, "JobQuestID"),
                LimitJobType = ReadInt(row, "LimitJobType"),
                ChangeType = ReadInt(row, "ChangeType"),
                ChangeJob = ReadInt(row, "ChangeJob"),
                PurchaseRate = ReadFloat(row, "PurchaseRate"),
                SellingRate = ReadFloat(row, "SellingRate"),
                ItemTable = ReadString(row, "ItemTable"),
                LimitSkillMaster = ReadString(row, "LimitSkillMaster"),
                Pages = Enumerable.Range(0, SkillShopPageCount)
                    .Select(page => (ReadInt(row, $"NpcSType{page}"),
                        Translate(tableName, ReadInt(row, "Type"), $"Message{page}", ReadString(row, $"Message{page}"))))
                    .ToArray(),
            });
        });
        return entries;
    }

    private const int SkillShopPageCount = 21;

    private static readonly string[] CompoundRecipeTables = { "Compound_Potion", "Compound_Rare", "Compound_Throw" };

    public IReadOnlyList<CompoundRecipe> LoadCompoundRecipes()
    {
        var recipes = new List<CompoundRecipe>(400);
        for (var type = 0; type < CompoundRecipeTables.Length; type++)
        {
            var recipeType = type;
            LoadTable(CompoundRecipeTables[type], row =>
            {
                var results = new List<int>(CompoundRecipe.ResultColumns);
                for (var column = 1; column <= CompoundRecipe.ResultColumns; column++)
                {
                    var result = ReadInt(row, $"ResID{column}");
                    if (result == 0) break;
                    results.Add(result);
                }
                var requirements = new List<CompoundRequirement>(CompoundRecipe.RequirementColumns);
                for (var column = 1; column <= CompoundRecipe.RequirementColumns; column++)
                {
                    var required = ReadInt(row, $"ReqID{column}");
                    if (required == 0) break;
                    requirements.Add(new CompoundRequirement(required, ReadInt(row, $"ReqNum{column}")));
                }
                recipes.Add(new CompoundRecipe
                {
                    Id = ReadInt(row, "CompoundID"),
                    Type = recipeType,
                    Results = results,
                    Requirements = requirements,
                    Probability = ReadInt(row, "Probability"),
                    Fee = ReadInt(row, "Fee"),
                    WasteItem = ReadInt(row, "WasteItem"),
                });
            });
        }
        return recipes;
    }

    public const int FirstDrillZone = 1;
    public const int LastDrillZone = 999;
    public const int EventRateTypes = 4;

    private static readonly (string Table, string Rate)[] MapItemCategoryColumns =
    {
        ("SpecialT", "S_Rate"), ("EquipT", "E_Rate"), ("Normal1T", "N1_Rate"), ("Normal2T", "N2_Rate"), ("Normal3T", "N3_Rate"),
    };

    private static readonly string[] RandomStatNames = { "AP", "AC", "DX", "MP", "MA", "MD", "WT", "DA", "LK", "HP", "DP", "HV", "GunAp" };
    private static readonly string[] CurveSteps = { "Step1", "Step10", "Step20", "Step30", "Step40", "Step50", "Step60", "Step70", "Step80", "Step90", "Step100" };
    private static readonly string[] AttributeColumns =
    {
        "Fire", "Water", "Wind", "Earth", "Elec", "Light", "Dark", "NoProp", null!, null!, "Shadow",
        "FireR", "WaterR", "WindR", "EarthR", "ElecR", "LightR", "DarkR", "NoPropR", "GunR", "PhysicalR", "ShadowR",
    };

    public const string RefineSeedRule = "RefineSeed";

    public IReadOnlyDictionary<int, ItemStatCheckEntry> LoadItemStatChecks()
    {
        var checks = new Dictionary<int, ItemStatCheckEntry>();
        LoadTable("ItemStatInfo", row => checks.TryAdd(ReadInt(row, "ID"), new ItemStatCheckEntry(
            ReadInt(row, "ItemIndex"),
            ItemStatCheckColumns.Select(column => ReadInt(row, column)).Select(value => value < 1 ? ItemStatCheckEntry.NoRequirement : value).ToArray())));
        return checks;
    }

    private static readonly string[] ItemStatCheckColumns =
    {
        "AP", "AC", "DX", "MaxMP", "MA", "MD", "MaxWT", "DA", "LK", "MaxHP", "DP", "HV",
    };

    public IReadOnlyDictionary<int, IReadOnlyList<(int SkillId, int Level)>> LoadSkillChecks()
    {
        var checks = new Dictionary<int, IReadOnlyList<(int SkillId, int Level)>>();
        LoadTable("SkillCheckInfo", row => checks.TryAdd(ReadInt(row, "ID"),
            Enumerable.Range(0, NpcSkillCheck.Slots).Select(i => (ReadInt(row, $"Skill_ID{i}"), ReadInt(row, $"Skill_Lv{i}"))).ToArray()));
        return checks;
    }

    public IReadOnlyList<NpcMoveEntry> LoadNpcMoves()
    {
        var entries = new List<NpcMoveEntry>(400);
        LoadTable("NpcMoveInfo", row =>
        {
            var zone = ReadInt(row, "Zone");
            if (zone < NpcMoveEntry.FirstZone || zone > NpcMoveEntry.LastZone) return;
            var items = new List<(int ItemId, int Count)>(NpcMoveEntry.ItemColumns);
            for (var column = 0; column < NpcMoveEntry.ItemColumns; column++)
            {
                var itemId = ReadInt(row, $"SrcItem{column}");
                if (itemId != 0) items.Add((itemId, ReadInt(row, $"Cnt{column}")));
            }
            var min = Math.Max(0, ReadInt(row, "MinLevel"));
            var max = Math.Max(0, ReadInt(row, "MaxLevel"));
            if (min > max) (min, max) = (0, 0);
            entries.Add(new NpcMoveEntry
            {
                Id = ReadInt(row, "Id"),
                Zone = zone,
                DestId = ReadInt(row, "DestId"),
                Items = items,
                MinLevel = min,
                MaxLevel = max,
                FType = ReadInt(row, "FType"),
                CharacterType = ReadInt(row, "ChrType"),
                TakeItem = ReadInt(row, "TakeItem"),
            });
        });
        return entries;
    }

    public PetGrowthTables LoadPetGrowthTables()
    {
        var properties = new Dictionary<int, EquipPropertyRow>();
        LoadTable(PetGrowthTables.PropertyTable, row => properties.TryAdd(ReadInt(row, "ItemId"), new EquipPropertyRow(
            ReadInt(row, "ItemId"),
            ReadFloat(row, "ApplyRatio"),
            Enumerable.Range(1, GrowthTables.EnablePropertyColumns).Select(n => ReadInt(row, $"EnableProperty{n}")).ToArray())));
        var materials = new Dictionary<int, MaturingCompoundRow>();
        LoadTable(PetGrowthTables.MaterialTable, row => materials.TryAdd(ReadInt(row, "ItemId"), new MaturingCompoundRow(
            ReadInt(row, "ItemId"), ReadInt(row, "EnableProperty"), ReadInt(row, "MinAbility"), ReadInt(row, "MaxAbility"),
            ReadInt(row, "ItemNum"), ReadInt(row, "EquipLv"), ReadString(row, "Import"))));
        var aids = new Dictionary<int, MaturingHelpRow>();
        LoadTable(PetGrowthTables.AidTable, row => aids.TryAdd(ReadInt(row, "ItemId"), new MaturingHelpRow(
            ReadInt(row, "ItemId"), ReadFloat(row, "applyRatio"), ReadFloat(row, "minAbility"), ReadFloat(row, "maxAbility"))));
        return new PetGrowthTables { PetProperties = properties, Materials = materials, Aids = aids };
    }

    public GrowthTables LoadGrowthTables()
    {
        var equip = new Dictionary<int, EquipPropertyRow>();
        LoadTable("Equip_Property", row => equip.TryAdd(ReadInt(row, "ItemId"), new EquipPropertyRow(
            ReadInt(row, "ItemId"),
            ReadFloat(row, "ApplyRatio"),
            Enumerable.Range(1, GrowthTables.EnablePropertyColumns).Select(n => ReadInt(row, $"EnableProperty{n}")).ToArray())));
        var materials = new Dictionary<int, MaturingCompoundRow>();
        LoadTable("Maturing_Compound", row => materials.TryAdd(ReadInt(row, "ItemId"), new MaturingCompoundRow(
            ReadInt(row, "ItemId"), ReadInt(row, "EnableProperty"), ReadInt(row, "MinAbility"), ReadInt(row, "MaxAbility"),
            ReadInt(row, "ItemNum"), ReadInt(row, "EquipLv"), ReadString(row, "Import"))));
        var aids = new Dictionary<int, MaturingHelpRow>();
        LoadTable("Maturing_Compound_Help", row => aids.TryAdd(ReadInt(row, "ItemId"), new MaturingHelpRow(
            ReadInt(row, "ItemId"), ReadFloat(row, "applyRatio"), ReadFloat(row, "minAbility"), ReadFloat(row, "maxAbility"))));
        float? discount = null;
        LoadTable("Maturing_Compound_CostDiscount", row => discount ??= ReadFloat(row, "ratio"));
        var ratios = new Dictionary<int, float>();
        LoadTable("ESAction_CompoundMaster", row => ratios.TryAdd(ReadInt(row, "Level"), ReadFloat(row, "CompoundRatio")));
        var accepted = discount is >= 0 and < 1 ? discount.Value : 0;
        return new GrowthTables { EquipProperties = equip, Materials = materials, Aids = aids, CostDiscount = accepted, CompoundMasterRatios = ratios };
    }

    public RefineTables LoadRefineTables()
    {
        var levels = new Dictionary<int, RefineLevelRow>();
        LoadTable("RefineLevelTable", row =>
        {
            var accumulation = Enumerable.Range(1, RefineTables.Levels).Select(n => ReadInt(row, $"AccumulationLv{n}")).ToArray();
            var ratio = Enumerable.Range(1, RefineTables.Levels).Select(n => ReadFloat(row, $"RatioLv{n}")).ToArray();
            levels.TryAdd(ReadInt(row, "RefineIndex"), new RefineLevelRow(ReadInt(row, "RefineIndex"), accumulation, ratio));
        });
        var materials = new List<IReadOnlyList<RefineMaterialRow>>(RefineTables.Types);
        for (var type = 0; type < RefineTables.Types; type++)
        {
            var rows = new List<RefineMaterialRow>();
            LoadTable($"ItemRefineTable{type}", row => rows.Add(new RefineMaterialRow(
                ReadInt(row, "MinLv"),
                ReadInt(row, "MaxLv"),
                Enumerable.Range(1, RefineTables.Levels).Select(n => ReadInt(row, $"ItemIdLv{n}")).ToArray(),
                Enumerable.Range(1, RefineTables.Levels).Select(n => ReadInt(row, $"ItemCntLv{n}")).ToArray())));
            materials.Add(rows);
        }
        var supports = new Dictionary<int, RefineSupportRow>();
        LoadTable("RefineSupportItemLevel", row => supports.TryAdd(ReadInt(row, "ID"), new RefineSupportRow(
            ReadInt(row, "ID"), ReadInt(row, "MinRefineLevel"), ReadInt(row, "MaxRefineLevel"),
            ReadInt(row, "ItemId"), ReadInt(row, "ItemCount"), ReadFloat(row, "Prob"))));
        float? discount = null;
        LoadTable("ItemRefineCostDiscount", row => discount ??= ReadFloat(row, "ratio"));
        var seed = 0f;
        LoadTable("FortuneRuleParam", row =>
        {
            if (string.Equals(ReadString(row, "RuleName"), RefineSeedRule, StringComparison.Ordinal)) seed = ReadFloat(row, "Value");
        });
        return new RefineTables { LevelRows = levels, Materials = materials, Supports = supports, CostDiscount = discount ?? 0, RefineSeed = seed };
    }

    public ItemPropertyTables LoadItemPropertyTables()
    {
        var randomStats = new Dictionary<int, RandomStatRow>();
        LoadTable("RandomStatValueTable", row =>
        {
            var stats = RandomStatNames.Select(name => ReadRange(row, name)).ToArray();
            randomStats.TryAdd(ReadInt(row, "ItemId"), new RandomStatRow { ItemId = ReadInt(row, "ItemId"), Stats = stats, MixSlot = ReadRange(row, "MixSlot") });
        });
        var curves = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        LoadTable("RandomStatImportTable", row => curves.TryAdd(ReadString(row, "Name"), CurveSteps.Select(step => ReadInt(row, step)).ToArray()));
        var attributes = new Dictionary<int, IReadOnlyList<(int Index, int Value)>>();
        LoadTable("Item_Attribute", row =>
        {
            var values = new List<(int Index, int Value)>();
            for (var index = 0; index < AttributeColumns.Length; index++)
            {
                if (AttributeColumns[index] is not { } column) continue;
                var value = ReadInt(row, column);
                if (value != 0) values.Add((index, value));
            }
            attributes.TryAdd(ReadInt(row, "ID"), values);
        });
        return new ItemPropertyTables { RandomStats = randomStats, Curves = curves, Attributes = attributes };
    }

    private static RandomStatRange? ReadRange(IReadOnlyDictionary<string, string> row, string name)
    {
        var curve = ReadString(row, name + "Import");
        return curve.Length == 0 ? null : new RandomStatRange(ReadInt(row, name + "Min"), ReadInt(row, name + "Max"), curve);
    }

    public DrillTables LoadDrillTables()
    {
        var drills = new Dictionary<int, DrillParamEntry>();
        LoadTable("Item_DrillParam", row => drills.TryAdd(ReadInt(row, "Id"), new DrillParamEntry(
            ReadInt(row, "Id"), ReadInt(row, "Grade"), ReadInt(row, "GroundNature"), ReadInt(row, "DigDepth"),
            ReadInt(row, "BarEffArea"), ReadInt(row, "BarSpeed"), ReadInt(row, "ScoopMass"), ReadInt(row, "DrillLife"), ReadFloat(row, "RandomSpot_Ratio"))));

        var zones = new Dictionary<int, DrillZoneEntry>();
        LoadTable("MapInfoEx", row =>
        {
            var id = ReadInt(row, "ID");
            if (id < FirstDrillZone || id > LastDrillZone) return;
            zones.TryAdd(id, new DrillZoneEntry(id, ReadInt(row, "MinItemDepth"), ReadInt(row, "MaxItemDepth"), ReadFloat(row, "ItemPercentage")));
        });

        var zoneItems = new Dictionary<int, MapItemZoneRow>();
        LoadTable("MapItem_All", row =>
        {
            var tables = MapItemCategoryColumns
                .Select(column => new MapItemCategory(ReadString(row, column.Table), ReadInt(row, column.Rate)))
                .ToList();
            zoneItems.TryAdd(ReadInt(row, "Id"), new MapItemZoneRow { ZoneId = ReadInt(row, "Id"), Tables = tables, Items = ReadChances(row) });
        });

        var itemTables = new Dictionary<string, List<MapItemChance>>(StringComparer.Ordinal);
        LoadTable("MapItem_ETC_Table", row =>
        {
            var name = ReadString(row, "Id");
            if (!itemTables.TryGetValue(name, out var list)) itemTables[name] = list = new List<MapItemChance>();
            list.AddRange(ReadChances(row));
        });

        var eventRateAdd = new float[EventRateTypes];
        LoadTable("MapItem_Event_Add", row =>
        {
            var type = ReadInt(row, "Type");
            if (type >= 0 && type < EventRateTypes) eventRateAdd[type] = ReadFloat(row, "Rate_Add");
        });

        var gradeExpRates = new List<float>();
        LoadTable("DrillGradeExpRate", row => gradeExpRates.Add(ReadFloat(row, "ExpRate")));

        var natures = new Dictionary<int, int>();
        LoadTable("MapAttr", row => natures.TryAdd(ReadInt(row, "Type"), ReadInt(row, "Attr")));

        var autoDrillPets = new Dictionary<int, int>();
        LoadTable("AutoDrillPetInfo", row => autoDrillPets.TryAdd(ReadInt(row, "ItemID"), ReadInt(row, "CoolTime")));

        return new DrillTables
        {
            Drills = drills,
            Zones = zones,
            ZoneItems = zoneItems,
            ItemTables = itemTables.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MapItemChance>)pair.Value, StringComparer.Ordinal),
            EventRateAdd = eventRateAdd,
            GradeExpRates = gradeExpRates,
            GroundNatures = natures,
            AutoDrillPets = autoDrillPets,
        };
    }

    private static List<MapItemChance> ReadChances(IReadOnlyDictionary<string, string> row)
    {
        var chances = new List<MapItemChance>(MapItemZoneRow.ItemColumns);
        for (var column = 0; column < MapItemZoneRow.ItemColumns; column++)
        {
            var itemId = ReadInt(row, $"Item{column}");
            var rate = ReadInt(row, $"Rate{column}");
            if (itemId == 0 || rate == 0) continue;
            chances.Add(new MapItemChance(itemId, rate));
        }
        return chances;
    }

    public IReadOnlyList<VipShopOffer> LoadVipShopOffers()
    {
        var offers = new List<VipShopOffer>(128);
        LoadTable("VIPShop", row => offers.Add(new VipShopOffer(
            ReadInt(row, "ID"), ReadInt(row, "Category"), ReadInt(row, "ChangeItemId"), ReadInt(row, "ChangeItemCnt"), ReadInt(row, "RItemCnt"))));
        return offers;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<int>> LoadVipShopLists(IReadOnlySet<string> tableNames)
    {
        ArgumentNullException.ThrowIfNull(tableNames);
        var lists = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        LoadTablesNamed(tableNames, (tableName, row) =>
        {
            if (!lists.TryGetValue(tableName, out var list)) lists[tableName] = list = new List<int>();
            list.Add(ReadInt(row, "VIPShopID"));
        });
        return lists.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<ExchangeOffer> LoadExchangeOffers()
    {
        var offers = new List<ExchangeOffer>(2100);
        LoadTable("ExchangeShopContents", row =>
        {
            var results = new List<int>(CompoundRecipe.ResultColumns);
            for (var column = 1; column <= CompoundRecipe.ResultColumns; column++)
            {
                var result = ReadInt(row, $"ResID{column}");
                if (result == 0) break;
                results.Add(result);
            }
            var requirements = new List<CompoundRequirement>(CompoundRecipe.RequirementColumns);
            for (var column = 1; column <= CompoundRecipe.RequirementColumns; column++)
            {
                var required = ReadInt(row, $"ReqID{column}");
                if (required == 0) break;
                requirements.Add(new CompoundRequirement(required, ReadInt(row, $"ReqNum{column}")));
            }
            offers.Add(new ExchangeOffer { Id = ReadInt(row, "ExchangeID"), Results = results, Requirements = requirements, Fee = ReadInt(row, "Fee") });
        });
        return offers;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<int>> LoadExchangeLists(IReadOnlySet<string> tableNames)
    {
        ArgumentNullException.ThrowIfNull(tableNames);
        var lists = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        LoadTablesNamed(tableNames, (tableName, row) =>
        {
            if (!lists.TryGetValue(tableName, out var list)) lists[tableName] = list = new List<int>();
            list.Add(ReadInt(row, "ExchangeID"));
        });
        return lists.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<CompounderList> LoadCompounderLists()
    {
        var heads = new List<(string Name, int Grade)>(CompounderList.MaxLists);
        LoadTable("Compounder_Info", row =>
        {
            if (heads.Count < CompounderList.MaxLists) heads.Add((ReadString(row, "ListName"), ReadInt(row, "Grade")));
        });
        var entries = new Dictionary<string, List<CompounderEntry>>(StringComparer.Ordinal);
        LoadTablesNamed(new HashSet<string>(heads.Select(head => head.Name), StringComparer.Ordinal), (tableName, row) =>
        {
            if (!entries.TryGetValue(tableName, out var list)) entries[tableName] = list = new List<CompounderEntry>();
            list.Add(new CompounderEntry(ReadInt(row, "CompoundID"), ReadInt(row, "Hidden") != 0));
        });
        return heads
            .Select(head => new CompounderList
            {
                Name = head.Name,
                Grade = head.Grade,
                Entries = entries.TryGetValue(head.Name, out var list) ? list : new List<CompounderEntry>(),
            })
            .ToList();
    }

    public IReadOnlyList<TeleportEntry> LoadTeleportInfo()
    {
        var entries = new List<TeleportEntry>(256);
        LoadTable("TeleportInfo", row =>
        {
            var destinations = new List<TeleportDestination>(TeleportEntry.DestinationColumns);
            for (var column = 0; column < TeleportEntry.DestinationColumns; column++)
            {
                var zone = ReadInt(row, $"Zone{column}");
                if (zone == 0) continue;
                destinations.Add(new TeleportDestination(zone, ReadInt(row, $"Cost{column}"), ReadString(row, $"LoadImg{column}")));
            }
            entries.Add(new TeleportEntry { Id = ReadInt(row, "Id"), MinLevel = ReadInt(row, "MinLevel"), Destinations = destinations });
        });
        return entries;
    }

    public IReadOnlyList<SkillScrollEntry> LoadSkillScrolls()
    {
        var entries = new List<SkillScrollEntry>(100);
        LoadTable("SkillScroll_Info", row => entries.Add(new SkillScrollEntry(
            ReadInt(row, "ItemId"), ReadInt(row, "SkillId"), ReadInt(row, "Level"))));
        return entries;
    }

    public IReadOnlyList<SkillMasterEntry> LoadSkillMasterInfo()
    {
        var entries = new List<SkillMasterEntry>(200);
        LoadTable("SkillMasterInfo", row => entries.Add(new SkillMasterEntry(
            ReadInt(row, "SkillId"), ReadInt(row, "ItemId"), ReadInt(row, "ItemCnt"))));
        return entries;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<int>> LoadSkillIdLists(IReadOnlySet<string> tableNames)
    {
        ArgumentNullException.ThrowIfNull(tableNames);
        var lists = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        LoadTablesNamed(tableNames, (tableName, row) =>
        {
            if (!lists.TryGetValue(tableName, out var list)) lists[tableName] = list = new List<int>();
            list.Add(ReadInt(row, "SkillID"));
        });
        return lists.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<MapItemZoneEntry> LoadAllMapItemZoneTables()
    {
        var entries = new List<MapItemZoneEntry>(2500);
        LoadTablesWithPrefix("MapItem_", (tableName, row) =>
        {
            if (!tableName.StartsWith("MapItem_", StringComparison.Ordinal)) return;
            var mapName = tableName["MapItem_".Length..];
            entries.Add(new MapItemZoneEntry
            {
                MapName = mapName,
                ItemId = ReadInt(row, "ItemID"),
                MonsterId = ReadInt(row, "MonsterID"),
                MinDepth = ReadInt(row, "MinDepth"),
                MaxDepth = ReadInt(row, "MaxDepth"),
                Avr = ReadInt(row, "Avr"),
                Max = ReadInt(row, "Max"),
                Priority = ReadInt(row, "Priority"),
                TimeTable = ReadString(row, "TimeTable"),
            });
        });
        return entries;
    }

    public IReadOnlyList<UniqueItemCountEntry> LoadUniqueItemCountTable()
    {
        var entries = new List<UniqueItemCountEntry>(20);
        LoadTable("UniqueItemCountTable", row => entries.Add(new UniqueItemCountEntry
        {
            MinLevel = ReadInt(row, "minLevel"),
            MaxLevel = ReadInt(row, "maxLevel"),
            Count = ReadFloat(row, "count"),
        }));
        return entries;
    }

    public IReadOnlyList<LegendItemGroupEntry> LoadLegendItemGroups()
    {
        var entries = new List<LegendItemGroupEntry>(16);
        LoadTable(LegendItemGroupEntry.Table, row => entries.Add(new LegendItemGroupEntry(
            ReadInt(row, "monID"),
            Enumerable.Range(0, LegendItemGroupEntry.Slots)
                .Select(slot => (ReadInt(row, $"item{slot}"), ReadFloat(row, $"ratio{slot}")))
                .Where(item => item.Item1 != 0)
                .ToArray())));
        return entries;
    }

    public IReadOnlyList<LegendItemListEntry> LoadLegendItemList()
    {
        var entries = new List<LegendItemListEntry>(80);
        LoadTable("LegendItemList", row => entries.Add(new LegendItemListEntry
        {
            ItemId = ReadInt(row, "ItemId"),
        }));
        return entries;
    }

    public IReadOnlyList<EsActionEntry> LoadAllEsActionTables()
    {
        var entries = new List<EsActionEntry>(2000);
        LoadTablesWithPrefix("ESAction_", (tableName, row) =>
        {
            if (!tableName.StartsWith("ESAction_", StringComparison.Ordinal)) return;
            var skillName = tableName["ESAction_".Length..];
            entries.Add(new EsActionEntry
            {
                SkillName = skillName,
                Level = ReadInt(row, "Level"),
                Ratio1 = ReadInt(row, "Ratio1"),
                Ratio2 = ReadInt(row, "Ratio2"),
                ApplyRatio = ReadFloat(row, "ApplyRatio"),
            });
        });
        return entries;
    }

    public IReadOnlyList<SkillTableRow> LoadSkillInvocationRows() => LoadSkillTableRows("ESAII_");

    public IReadOnlyList<SkillTableRow> LoadSkillActionRows() => LoadSkillTableRows("ESAction_");

    private List<SkillTableRow> LoadSkillTableRows(string tableNamePrefix)
    {
        var rows = new List<SkillTableRow>(3000);
        LoadTablesWithPrefix(tableNamePrefix, (tableName, row) => rows.Add(new SkillTableRow
        {
            Table = tableName,
            Level = ReadInt(row, "Level"),
            Columns = row,
        }));
        return rows;
    }

    public (int MinLevel, int MinGelder)? LoadGuildSetupRow(string tableName, string server)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentException.ThrowIfNullOrEmpty(server);
        (int MinLevel, int MinGelder)? found = null;
        LoadTable(tableName, row =>
        {
            if (string.Equals(ReadString(row, "Server"), server, StringComparison.Ordinal)) found = (ReadInt(row, "MinLv"), ReadInt(row, "MinGelder"));
        });
        return found;
    }

    public IReadOnlyList<double> LoadDailyTimeMinutes(string tableName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        var phases = new SortedDictionary<int, double>();
        LoadTable(tableName, row => phases.TryAdd(ReadInt(row, "Value"), ReadFloat(row, "Minutes")));
        return phases.Values.ToList();
    }

    public IReadOnlyList<RandomStatValueEntry> LoadRandomStatValueTable()
    {
        var entries = new List<RandomStatValueEntry>(3000);
        LoadTable("RandomStatValueTable", row => entries.Add(new RandomStatValueEntry
        {
            Id = ReadInt(row, "ID"),
            StatType = ReadInt(row, "StatType"),
            MinValue = ReadInt(row, "MinValue"),
            MaxValue = ReadInt(row, "MaxValue"),
            Probability = ReadInt(row, "Probability"),
        }));
        return entries;
    }

    private void LoadTablesWithPrefix(string tableNamePrefix,
        Action<string, IReadOnlyDictionary<string, string>> rowHandler)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = false,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Ignore,
        };

        using var reader = OpenXmlReader(settings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;
            if (reader.Name != "TABLE") continue;

            var tableName = reader.GetAttribute("name") ?? string.Empty;
            if (!tableName.StartsWith(tableNamePrefix, StringComparison.Ordinal)) continue;

            var tableElement = (XElement)XNode.ReadFrom(reader);
            foreach (var rowElement in tableElement.Elements("ROW"))
            {
                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var fieldElement in rowElement.Elements())
                {
                    fields[fieldElement.Name.LocalName] = fieldElement.Value;
                }
                rowHandler(tableName, fields);
            }
        }
    }

    private void LoadTablesNamed(IReadOnlySet<string> tableNames, Action<string, IReadOnlyDictionary<string, string>> rowHandler) =>
        LoadTablesMatching(tableNames.Contains, rowHandler);

    private void LoadTablesMatching(Func<string, bool> wanted, Action<string, IReadOnlyDictionary<string, string>> rowHandler)
    {
        var settings = new XmlReaderSettings
        {
            IgnoreWhitespace = false,
            IgnoreComments = true,
            DtdProcessing = DtdProcessing.Ignore,
        };

        using var reader = OpenXmlReader(settings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element) continue;
            if (reader.Name != "TABLE") continue;

            var tableName = reader.GetAttribute("name") ?? string.Empty;
            if (!wanted(tableName)) continue;

            var tableElement = (XElement)XNode.ReadFrom(reader);
            foreach (var rowElement in tableElement.Elements("ROW"))
            {
                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var fieldElement in rowElement.Elements())
                {
                    fields[fieldElement.Name.LocalName] = fieldElement.Value;
                }
                rowHandler(tableName, fields);
            }
        }
    }

    private string Translate(string tableName, int rowId, string fieldName, string sourceValue)
        => _overrides.Apply(tableName, rowId, fieldName, sourceValue);

    private string Translate(string tableName, string rowKey, string fieldName, string sourceValue)
        => _overrides.Apply(tableName, rowKey, fieldName, sourceValue);

    private static int ReadInt(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }
        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }

    private static int ReadFlags(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0;
        var flags = 0;
        foreach (var part in raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag)) flags |= flag;
        }
        return flags;
    }

    private static string ReadString(IReadOnlyDictionary<string, string> row, string field)
    {
        return row.TryGetValue(field, out var v) ? v.Trim() : string.Empty;
    }

    private static float ReadFloat(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0f;
        return float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0.0;
        return double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw)) return false;
        var s = raw.Trim();
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (s.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return s != "0" && !string.IsNullOrEmpty(s);
    }

    public IReadOnlyList<MapFileEntry> LoadMapFiles()
    {
        var entries = new List<MapFileEntry>(700);
        LoadTable("MapInfoEx", row =>
        {
            var id = ReadInt(row, "ID");
            entries.Add(new MapFileEntry(id, Translate("MapInfoEx", id, "Name", ReadString(row, "Name")), ReadString(row, "FileName").Trim()));
        });
        return entries;
    }

    public IReadOnlyDictionary<string, string> LoadStringTable()
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        LoadTable("StringTable", row =>
        {
            var name = ReadString(row, "Name").Trim();
            if (name.Length > 0) strings.TryAdd(name, Translate("StringTable", name, "String", ReadString(row, "String")));
        });
        return strings;
    }
}
