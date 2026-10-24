using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.GameServer;

public sealed record LoginTitleFrames(byte[] HonorTitleList, byte[] ProfileTitle);

public static class EnterWorldBatchSynthesizer
{
    private const uint EquippedSlotFlag = 1;

    private static bool EverywhereInView(int x, int y) => true;

    public static async Task SendBatchAsync(
        S2SessionContext ctx,
        Character character,
        ushort entityId,
        INpcDialogService dialogService,
        QuestLoginLists questLists,
        WorldEntityStore? worldEntities = null,
        ICharacterInfoLookup? characterInfoCatalog = null,
        IMonsterEntityProvider? monsterProvider = null,
        IItemStore? itemStore = null,
        ISkillStore? skillStore = null,
        ServerRates? rates = null,
        ShopManager? shops = null,
        SkillShopManager? skillShops = null,
        NpcActivation? activation = null,
        Func<Item, IReadOnlyList<ItemProperty>>? itemOptions = null,
        IReadOnlyList<tPoseidon.Core.Game.Items.TimedItem>? timedItems = null,
        Func<int, int, bool>? inView = null,
        LoginTitleFrames? titles = null,
        byte[]? fortune = null,
        uint fortuneTitleId = 0,
        byte[]? abilityPercents = null,
        byte[]? petBag = null,
        CancellationToken ct = default)
    {
        await SendPhase1Async(ctx, character, entityId, questLists, itemStore, skillStore, rates, itemOptions, timedItems, titles, fortune, fortuneTitleId, abilityPercents, petBag, ct).ConfigureAwait(false);
        await SendPhase2Async(
            ctx, character, entityId, dialogService,
            worldEntities, characterInfoCatalog,
            monsterProvider, itemStore, rates, shops, skillShops, inView, activation, ct).ConfigureAwait(false);
    }

    public static async Task SendPhase1Async(
        S2SessionContext ctx,
        Character character,
        ushort entityId,
        QuestLoginLists questLists,
        IItemStore? itemStore = null,
        ISkillStore? skillStore = null,
        ServerRates? rates = null,
        Func<Item, IReadOnlyList<ItemProperty>>? itemOptions = null,
        IReadOnlyList<tPoseidon.Core.Game.Items.TimedItem>? timedItems = null,
        LoginTitleFrames? titles = null,
        byte[]? fortune = null,
        uint fortuneTitleId = 0,
        byte[]? abilityPercents = null,
        byte[]? petBag = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(questLists);

        var charData = new GameCharDataPacket
        {
            CharacterId = character.CharacterId,
            UserId = character.OwnerAccountId,
            Slot = character.Slot,
            Name = character.Name,
            Species = (ushort)character.Species,
            FTypeIndex = (byte)tPoseidon.Core.Game.Progression.StatGrowth.TypeIndexOf(character.FType),
            StatA = character.PtsP,
            StatB = character.PtsM,
            StatC = character.PtsS,
            StatD = character.PtsC,
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        await SendFrameAsync(ctx, Opcode.GAME_CHAR_DATA, randKey: 0x8A, charData.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_BATCH_HEADER, randKey: 0x59,
            new GameBatchHeaderPacket { EntityIdMirror = entityId, SpawnXTbd = (ushort)character.PositionX, SpawnYTbd = (ushort)character.PositionY }.EncodePayload(), ct);

        var itemList = BuildItemListFromStore(character, itemStore, itemOptions);
        await SendFrameAsync(ctx, Opcode.GAME_ENTITY_LIST, randKey: 0x76, itemList.EncodePayload(), ct);

        var charStats = BuildCharInfo(character, itemStore);
        await SendFrameAsync(ctx, Opcode.GAME_CHAR_STATS, randKey: 0x2D, charStats.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_UNK_EA, randKey: 0xD0, questLists.Normal, ct);
        await SendFrameAsync(ctx, Opcode.GAME_MONSTER_QUEST, randKey: 0xC2, questLists.Monster, ct);
        await SendFrameAsync(ctx, Opcode.GAME_SKILL_QUEST, randKey: 0xC9, questLists.JobChange, ct);

        var skillList = BuildStarterSkillList(character, skillStore);
        await SendFrameAsync(ctx, Opcode.GAME_SKILL_LIST, randKey: 0xCD, skillList.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_ITEM_EXPIRY, randKey: 0x68, tPoseidon.Core.Net.World.TimedItemFrames.Login(DateTime.UtcNow, timedItems ?? Array.Empty<tPoseidon.Core.Game.Items.TimedItem>()), ct);

        await SendFrameAsync(ctx, Opcode.S2C_ACTIVE_QUEST_RESTORE, randKey: 0xD4, questLists.Scenario, ct);

        if (titles is not null)
        {
            await SendFrameAsync(ctx, Opcode.S2C_HONOR_TITLE_LIST, randKey: 0x5E, titles.HonorTitleList, ct);
        }

        if (petBag is not null)
        {
            await SendFrameAsync(ctx, Opcode.S2C_PET_COLLECTION_LOGIN_BAG, randKey: 0x7E, petBag, ct);
        }

        await SendFrameAsync(ctx, Opcode.GAME_PHASE_2, randKey: 0x49, new byte[] { 0x00 }, ct);

        if (abilityPercents is not null)
        {
            await SendFrameAsync(ctx, (Opcode)GameMasterEventFrames.AbilityPercents, randKey: 0x4F, abilityPercents, ct);
        }

        await SendFrameAsync(ctx, Opcode.GAME_ENTITY_ANNOUNCE_3D1, randKey: 0x6A, tPoseidon.Core.Net.World.FortuneFrames.TitleAnnounce(entityId, fortuneTitleId), ct);

        await SendFrameAsync(ctx, Opcode.GAME_BATCH_CONST_377, randKey: 0x79, GameBatchFloatArrayPacket.ForBonusRateKr2014(TmBonusRate(rates)).EncodePayload(), ct);
        await SendFrameAsync(ctx, Opcode.GAME_BATCH_CONST_376, randKey: 0x25, GameBatchFloatArrayPacket.ForBonusRateKr2014(ExpBonusRate(rates)).EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.S2C_FORTUNE_APPLIED, randKey: 0x08, fortune ?? BuildBatchCounter3D7().EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_BATCH_CONST_2A4, randKey: 0x61, ListCountPacket.Empty.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_BATCH_CONST_3E4, randKey: 0x40, new SlotCountPacket { Count = 3 }.EncodePayload(), ct);

        var profileTitle = titles?.ProfileTitle ?? new EntityIdAnnouncePacket { EntityId = entityId, PadBytes = 11 }.EncodePayload();
        await SendFrameAsync(ctx, Opcode.S2C_PROFILE_TITLE, randKey: 0x14, profileTitle, ct);

        await SendFrameAsync(ctx, Opcode.GAME_BATCH_CONST_3EA, randKey: 0xB1, ListCountPacket.Empty.EncodePayload(), ct);
    }

    public static async Task SendPhase2Async(
        S2SessionContext ctx,
        Character character,
        ushort entityId,
        INpcDialogService dialogService,
        WorldEntityStore? worldEntities = null,
        ICharacterInfoLookup? characterInfoCatalog = null,
        IMonsterEntityProvider? monsterProvider = null,
        IItemStore? itemStore = null,
        ServerRates? rates = null,
        ShopManager? shops = null,
        SkillShopManager? skillShops = null,
        Func<int, int, bool>? inView = null,
        NpcActivation? activation = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(dialogService);
        inView ??= EverywhereInView;

        await SendFrameAsync(ctx, Opcode.S2C_OP_2E29_TBD, randKey: 0x3B, AngelPairingStatusPacket.NoPairing.EncodePayload(), ct);

        if (worldEntities is not null && characterInfoCatalog is not null)
        {
            byte rk = 0x6A;
            foreach (var npc in worldEntities.InMap(character.MapId))
            {
                if (!inView((int)npc.Spawn.X, (int)npc.Spawn.Y) || activation?.IsActive(npc.EntityId) == false) continue;
                await SendFrameAsync(ctx, Opcode.NPC_SPAWN, randKey: rk, NpcSpawn(npc, characterInfoCatalog), ct);
                rk = (byte)(rk + 0x3B);
            }

            await SendShopSpawnsAsync(ctx, character.MapId, shops, skillShops, characterInfoCatalog, inView, activation, rk, ct).ConfigureAwait(false);
        }

        _ = monsterProvider;

        var playerSpawn = Kr2014.Wire.PlayerSpawnLoginPacket.FromCharacter(character, entityId, itemStore);
        await SendFrameAsync(ctx, Opcode.PLAYER_SPAWN_LOGIN, randKey: 0x11, playerSpawn.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_UNK_135, randKey: 0x28, EmptyPacket.EncodePayload(), ct);
        await SendFrameAsync(ctx, Opcode.S2C_PVP_MODE_OFF, randKey: 0xC1, EmptyPacket.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_BATCH_CONST_377, randKey: 0x8C, GameBatchFloatArrayPacket.ForBonusRateKr2014(TmBonusRate(rates)).EncodePayload(), ct);
        await SendFrameAsync(ctx, Opcode.GAME_BATCH_CONST_376, randKey: 0xD6, GameBatchFloatArrayPacket.ForBonusRateKr2014(ExpBonusRate(rates)).EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_UNK_3A6, randKey: 0xA9, new HeadingVectorPacket().EncodePayload(), ct);

        var entityEcho = new EntityIdEchoPacket { EntityId = entityId };
        await SendFrameAsync(ctx, Opcode.ENTITY_ID_ECHO, randKey: 0x0B, entityEcho.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_UNK_325, randKey: 0x87, new ColorOrFlagPacket0325().EncodePayload(), ct);

        var hp = HealthStatusPacket.Of(character.CurrentHp);
        await SendFrameAsync(ctx, Opcode.S2C_HEALTH_STATUS, randKey: 0x2F, hp.EncodePayload(), ct);

        var mp = ManaStatusPacket.Of(character.CurrentMp);
        await SendFrameAsync(ctx, Opcode.S2C_MANA_STATUS, randKey: 0xF1, mp.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_UNK_477, randKey: 0x15, new TimestampedSparsePacket0477().EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.S2C_MAP_HANDOFF_ACK_4CC, randKey: 0x1D, new MapHandoffAckPacket().EncodePayload(), ct);

        var dialogs = dialogService.GetSpawnDialogs(character);
        foreach (var pair in dialogs)
        {
            await SendFrameAsync(ctx, Opcode.S2C_NPC_DIALOG, randKey: 0x97,
                pair.Opener.EncodePayload(), ct);
            await SendFrameAsync(ctx, Opcode.S2C_NPC_DIALOG_FOLLOWUP, randKey: 0x95,
                pair.Followup.EncodePayload(), ct);
        }

        var speedFrame = new EntitySpeedPacket
        {
            EntityId = entityId,
            SpeedQuarter = EntitySpeedPacket.RunSpeedQuarter,
        };
        await SendFrameAsync(ctx, Opcode.S2C_ENTITY_SPEED, randKey: 0x4D,
            speedFrame.EncodePayload(), ct);
    }

    public static BatchCounterPacket3D7 BuildBatchCounter3D7() => new()
    {
        CounterValue = 1,
        MagicConstant = 0xD62E5000u,
        UnknownU32At8 = 65,
        Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
    };

    public static tPoseidon.Kr2014.Wire.CharInfoPacket BuildCharInfo(Character character, IItemStore? itemStore = null)
    {
        ArgumentNullException.ThrowIfNull(character);

        var slots = new tPoseidon.Kr2014.Wire.CharInfoPacket.EquipSlot[tPoseidon.Kr2014.Wire.CharInfoPacket.EquipSlotCount];
        if (itemStore is not null)
        {
            var equipped = itemStore.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment);
            foreach (var item in equipped)
            {
                if (item.SlotPosition < slots.Length)
                {
                    slots[item.SlotPosition] = new tPoseidon.Kr2014.Wire.CharInfoPacket.EquipSlot(
                        (uint)(item.ItemUid & 0xFFFFFFFFu), EquippedSlotFlag);
                }
            }
        }

        return new tPoseidon.Kr2014.Wire.CharInfoPacket
        {
            EquipSlots = slots,
            Stats = new tPoseidon.Core.Net.World.CharacterStatBlock(
                tPoseidon.Core.Game.Progression.StatusValueChart.PointsOf(character),
                character.AttrPoints,
                character.TmPoint,
                character.TmLevel,
                character.Level,
                tPoseidon.Core.Net.World.CharacterStatBlock.LoginMarkAsStored),
            MapId = character.MapId,
            PositionX = character.PositionX,
            PositionY = character.PositionY,
            ReturnMapId = tPoseidon.Core.Game.Warp.RestartArrival.NormalizeReturnMap(character.Home),
            Money = tPoseidon.Core.Common.WireLimits.Int32FieldUnsigned(character.Money),
            Hp = tPoseidon.Core.Common.WireLimits.Int32Field(character.CurrentHp),
            Mp = tPoseidon.Core.Common.WireLimits.Int32Field(character.CurrentMp),
            TmExp = tPoseidon.Core.Common.WireLimits.Int32FieldUnsigned(character.TmExp),
            Exp = tPoseidon.Core.Common.WireLimits.Int32FieldUnsigned(character.Exp),
            Hair = (byte)(character.Hair & 0xFF),
            WarehouseSlots = character.WarehouseSlots,
        };
    }

    private static GameItemListPacket BuildItemListFromStore(
        Character character, IItemStore? itemStore, Func<Item, IReadOnlyList<ItemProperty>>? itemOptions)
    {
        if (itemStore is null)
        {
            return new GameItemListPacket { Records = Array.Empty<GameItemListPacket.ItemRecord>() };
        }

        var equipment = itemStore.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment);
        var inventory = itemStore.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var records = new List<GameItemListPacket.ItemRecord>(equipment.Count + inventory.Count);

        foreach (var item in equipment)
        {
            records.Add(new GameItemListPacket.ItemRecord
            {
                Class = item.ItemClass,
                ItemIndex = (uint)item.ItemIndex,
                ItemUid = (uint)(item.ItemUid & 0xFFFFFFFFu),
                EquipmentCount = item.Stack,
                RefineType = (byte)item.RefineType,
                RefineLevel = item.RefineLevel,
                RefineState = item.RefineState,
                MixSlot = item.ItemSlot,
                Options = item.ItemClass == ItemParamEntry.RareClass && itemOptions is not null ? itemOptions(item) : Array.Empty<ItemProperty>(),
            });
        }

        foreach (var item in inventory)
        {
            records.Add(new GameItemListPacket.ItemRecord
            {
                Class = item.ItemClass,
                ItemIndex = (uint)item.ItemIndex,
                ItemUid = (uint)(item.ItemUid & 0xFFFFFFFFu),
                StackCount = item.Stack,
                EquipmentCount = item.Stack,
                RefineType = (byte)item.RefineType,
                RefineLevel = item.RefineLevel,
                RefineState = item.RefineState,
                MixSlot = item.ItemSlot,
                Options = item.ItemClass == ItemParamEntry.RareClass && itemOptions is not null ? itemOptions(item) : Array.Empty<ItemProperty>(),
            });
        }

        return new GameItemListPacket { Records = records };
    }

    private static GameSkillListPacket BuildStarterSkillList(Character character, ISkillStore? skillStore = null)
    {
        var persisted = skillStore?.GetForCharacter(character.CharacterId);
        if (persisted is not null && persisted.Count > 0)
        {
            var entries = new GameSkillListPacket.Entry[persisted.Count];
            for (int i = 0; i < persisted.Count; i++)
            {
                entries[i] = new GameSkillListPacket.Entry
                {
                    SkillId = (ushort)persisted[i].SkillId,
                    Level = (byte)persisted[i].SkillLevel,
                    UnknownByte3 = 0x0C,
                    UnknownByte4 = 0xA4,
                };
            }
            return new GameSkillListPacket { Entries = entries };
        }

        return new GameSkillListPacket
        {
            Entries = SpeciesDefaults.StarterSkillIds(character.Species, character.FType)
                .Select(starterSkill => new GameSkillListPacket.Entry
                {
                    SkillId = starterSkill,
                    Level = 1,
                    UnknownByte3 = 0x0C,
                    UnknownByte4 = 0xA4,
                })
                .ToArray(),
        };
    }

    public static (float Exp, float Tm) FixedBonusRates(ServerRates? rates) => (ExpBonusRate(rates), TmBonusRate(rates));

    private static float ExpBonusRate(ServerRates? rates) =>
        rates is null ? 0f : (float)(rates.ExpHelper.ExpPcRoom + rates.ExpHelper.ExpEvent);

    private static float TmBonusRate(ServerRates? rates) =>
        rates is null ? 0f : (float)(rates.ExpHelper.TmPcRoom + rates.ExpHelper.TmEvent);

    public static byte[] NpcSpawn(NpcEntity npc, ICharacterInfoLookup characterInfoCatalog)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(characterInfoCatalog);
        var headMark = (uint)npc.HeadMarkType;
        var trailer = new byte[18];
        trailer[1] = 0x80;
        trailer[12] = (byte)(npc.PosId & 0xFF);
        trailer[13] = (byte)((npc.PosId >> 8) & 0xFF);
        trailer[14] = (byte)(headMark & 0xFF);
        trailer[15] = (byte)((headMark >> 8) & 0xFF);
        trailer[16] = (byte)((headMark >> 16) & 0xFF);
        trailer[17] = (byte)((headMark >> 24) & 0xFF);
        return new NpcSpawnPacket
        {
            EntityId = npc.EntityId,
            Kind = NpcSpawnPacket.EntityKind.Npc,
            TemplateId = (ushort)npc.ChrType,
            SpawnX = (ushort)npc.Spawn.X,
            SpawnY = (ushort)npc.Spawn.Y,
            CurrentX = (ushort)npc.Spawn.X,
            CurrentY = (ushort)npc.Spawn.Y,
            Name = npc.DisplayName ?? characterInfoCatalog.GetById(npc.ChrType)?.DisplayName ?? "NPC",
            Trailer = trailer,
        }.EncodePayload();
    }

    public static byte[] ShopSpawn(ShopEntity shop, ICharacterInfoLookup characterInfoCatalog) =>
        ShopSpawnFrames.Encode(shop, characterInfoCatalog.GetById(shop.NpcType)?.DisplayName ?? string.Empty);

    public static byte[] SkillShopSpawn(SkillShopEntity skillShop, ICharacterInfoLookup characterInfoCatalog) =>
        ShopSpawnFrames.EncodeSkillShop(skillShop, characterInfoCatalog.GetById(skillShop.NpcType)?.DisplayName ?? string.Empty);

    private static async Task SendShopSpawnsAsync(
        S2SessionContext ctx,
        ushort mapId,
        ShopManager? shops,
        SkillShopManager? skillShops,
        ICharacterInfoLookup characterInfoCatalog,
        Func<int, int, bool> inView,
        NpcActivation? activation,
        byte randKey,
        CancellationToken ct)
    {
        foreach (var shop in shops?.GetByMap(mapId) ?? Array.Empty<ShopEntity>())
        {
            if (!inView(shop.SpawnX, shop.SpawnY) || activation?.IsActive(shop.EntityId) == false) continue;
            await SendFrameAsync(ctx, Opcode.NPC_SPAWN, randKey, ShopSpawn(shop, characterInfoCatalog), ct).ConfigureAwait(false);
            randKey = (byte)(randKey + 0x3B);
        }
        foreach (var skillShop in skillShops?.GetByMap(mapId) ?? Array.Empty<SkillShopEntity>())
        {
            if (!inView(skillShop.SpawnX, skillShop.SpawnY) || activation?.IsActive(skillShop.EntityId) == false) continue;
            await SendFrameAsync(ctx, Opcode.NPC_SPAWN, randKey, SkillShopSpawn(skillShop, characterInfoCatalog), ct).ConfigureAwait(false);
            randKey = (byte)(randKey + 0x3B);
        }
    }

    private static async Task SendFrameAsync(
        S2SessionContext ctx,
        Opcode op,
        byte randKey,
        byte[] payload,
        CancellationToken ct)
    {
        var frame = ctx.Cipher.Encrypt(
            opcode: (ushort)op,
            sequence: 0,
            randKey: randKey,
            flags: PackingFlags.PayloadEncrypted,
            plaintextPayload: payload,
            session: ctx.SessionInfo);
        await ctx.Session.SendAsync(frame, ct).ConfigureAwait(false);
    }

    public static async Task SendWarpLandingBatchAsync(
        S2SessionContext ctx,
        Character character,
        ushort entityId,
        WorldEntityStore? worldEntities,
        ICharacterInfoLookup? characterInfoCatalog,
        tPoseidon.Core.Game.Monsters.MonsterManager? monsterManager,
        IMonsterParamLookup? monsterCatalog,
        IItemStore? itemStore = null,
        ShopManager? shops = null,
        SkillShopManager? skillShops = null,
        Func<int, int, bool>? inView = null,
        NpcActivation? activation = null,
        CancellationToken ct = default)
    {
        inView ??= EverywhereInView;
        var spawnPos = new PlayerSpawnPositionPacket
        {
            EntityId = entityId,
            CharUid = (ulong)character.CharacterId,
        };
        await SendFrameAsync(ctx, Opcode.GAME_PLAYER_SPAWN_POSITION, randKey: 0xC8, spawnPos.EncodePayload(), ct);

        var playerSpawn = Kr2014.Wire.PlayerSpawnLoginPacket.FromCharacter(character, entityId, itemStore);
        await SendFrameAsync(ctx, Opcode.S2C_PLAYER_SPAWN_POST_WARP, randKey: 0x11, playerSpawn.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_PLAYER_PRESENCE_RESET, randKey: 0xCE, PlayerPresenceResetPacket.Zero.EncodePayload(), ct);

        if (worldEntities is not null && characterInfoCatalog is not null)
        {
            byte rk = 0x6A;
            foreach (var npc in worldEntities.InMap(character.MapId))
            {
                if (!inView((int)npc.Spawn.X, (int)npc.Spawn.Y) || activation?.IsActive(npc.EntityId) == false) continue;
                await SendFrameAsync(ctx, Opcode.NPC_SPAWN, randKey: rk, NpcSpawn(npc, characterInfoCatalog), ct);
                rk = (byte)(rk + 0x3B);
            }

            await SendShopSpawnsAsync(ctx, character.MapId, shops, skillShops, characterInfoCatalog, inView, activation, rk, ct).ConfigureAwait(false);
        }

        if (monsterManager is not null)
        {
            var mobs = monsterManager.GetByMap(character.MapId);
            byte rk = 0x42;
            foreach (var mob in mobs)
            {
                if (!inView(mob.CurrentX, mob.CurrentY)) continue;
                var trailer = new byte[23];
                trailer[2] = 0x15;
                trailer[7] = 0x01;
                var def = monsterCatalog?.GetById(mob.MonsterParamId);
                var mobLevel = (ushort)Math.Clamp(def?.Level ?? 1, 1, ushort.MaxValue);
                var spawnPkt = new NpcSpawnPacket
                {
                    EntityId = mob.EntityId,
                    Kind = NpcSpawnPacket.EntityKind.Mob,
                    TemplateId = (ushort)(mob.MonsterParamId & 0xFFFF),
                    UnknownField6 = 0,
                    UnknownField8 = mobLevel,
                    Reserved10 = 0x00000001,
                    SpawnX = mob.SpawnX,
                    SpawnY = mob.SpawnY,
                    CurrentX = mob.CurrentX,
                    CurrentY = mob.CurrentY,
                    Name = mob.Name,
                    Trailer = trailer,
                };
                await SendFrameAsync(ctx, Opcode.NPC_SPAWN, randKey: rk, spawnPkt.EncodePayload(), ct);
                rk = (byte)(rk + 0x2B);
            }
        }

        var entityEcho = new EntityIdEchoPacket { EntityId = entityId };
        await SendFrameAsync(ctx, Opcode.GAME_UNK_3A6, randKey: 0xA9, new HeadingVectorPacket().EncodePayload(), ct);
        await SendFrameAsync(ctx, Opcode.ENTITY_ID_ECHO, randKey: 0x0B, entityEcho.EncodePayload(), ct);
        await SendFrameAsync(ctx, Opcode.GAME_UNK_325, randKey: 0x87, new ColorOrFlagPacket0325().EncodePayload(), ct);

        var hp = HealthStatusPacket.Of(character.CurrentHp);
        await SendFrameAsync(ctx, Opcode.S2C_HEALTH_STATUS, randKey: 0x2F, hp.EncodePayload(), ct);

        var mp = ManaStatusPacket.Of(character.CurrentMp);
        await SendFrameAsync(ctx, Opcode.S2C_MANA_STATUS, randKey: 0xF1, mp.EncodePayload(), ct);

        await SendFrameAsync(ctx, Opcode.GAME_UNK_477, randKey: 0x15, new TimestampedSparsePacket0477().EncodePayload(), ct);
        await SendFrameAsync(ctx, Opcode.S2C_MAP_HANDOFF_ACK_4CC, randKey: 0x1D, new MapHandoffAckPacket().EncodePayload(), ct);

        var speedFrame = new EntitySpeedPacket
        {
            EntityId = entityId,
            SpeedQuarter = EntitySpeedPacket.RunSpeedQuarter,
        };
        await SendFrameAsync(ctx, Opcode.S2C_ENTITY_SPEED, randKey: 0x4D, speedFrame.EncodePayload(), ct);
    }
}
