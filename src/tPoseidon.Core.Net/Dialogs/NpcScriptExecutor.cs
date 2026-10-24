using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record ZoneChangeRequest(ushort Zone, byte ChangeType, int? PointId, TimeSpan Delay, (ushort X, ushort Y)? Position = null);

public sealed record NpcScriptCatalogs
{
    public required IQuestInfoLookup Quests { get; init; }
    public required IQuestResultTables QuestResults { get; init; }
    public required IMonsterQuestInfoLookup MonsterQuests { get; init; }
    public required IMonsterQuestResultTables MonsterQuestResults { get; init; }
    public IMonsterQuestResultTables PartyQuestResults { get; init; } = new InMemoryMonsterQuestResultLookup(Array.Empty<MonsterQuestResultEntry>());
    public required INpcMsgLookup NpcMessages { get; init; }
    public required IItemParamLookup Items { get; init; }
    public IReadOnlyDictionary<int, TeleportEntry> Teleports { get; init; } = new Dictionary<int, TeleportEntry>();
    public IReadOnlyDictionary<int, NpcMoveEntry> NpcMoves { get; init; } = new Dictionary<int, NpcMoveEntry>();
    public IReadOnlyDictionary<int, IReadOnlyList<(int SkillId, int Level)>> SkillChecks { get; init; } = new Dictionary<int, IReadOnlyList<(int SkillId, int Level)>>();
    public IReadOnlyDictionary<int, ItemStatCheckEntry> ItemStatChecks { get; init; } = new Dictionary<int, ItemStatCheckEntry>();
    public Func<ItemPropertyRules>? ItemProperties { get; init; }
    public IItemPropertyStore? StoredProperties { get; init; }
    public Func<int, string?>? MonsterName { get; init; }
    public GuildSetupRequirement? GuildSetup { get; init; }
    public PartyZoneTables PartyZones { get; init; } = PartyZoneTables.Empty;
}

public sealed record NpcScriptStores
{
    public required IQuestStore Quests { get; init; }
    public required IQuestHistoryStore History { get; init; }
    public required IItemStore Items { get; init; }
    public required IMonsterHuntStore Hunts { get; init; }
}

public sealed record NpcScriptPlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
    public required Func<int> MaxWeight { get; init; }
    public required Func<IReadOnlyDictionary<int, int>> Skills { get; init; }
    public required Func<long, long, CancellationToken, Task> GrantProgress { get; init; }
    public required Func<ServerRates> Rates { get; init; }
    public required Func<CancellationToken, Task<int>> OpenBank { get; init; }
    public required Func<CancellationToken, Task<int>> OpenWarehouse { get; init; }
    public required Func<string, CancellationToken, Task<int>> OpenCompound { get; init; }
    public required Func<string, CancellationToken, Task<int>> OpenExchangeShop { get; init; }
    public required Func<string, CancellationToken, Task<int>> OpenVipShop { get; init; }
    public required Func<CancellationToken, Task<int>> OpenRefine { get; init; }
    public required Func<bool, CancellationToken, Task<int>> OpenGrowth { get; init; }
    public Func<byte, CancellationToken, Task<bool>>? SetJobQuestState { get; init; }
    public Func<CancellationToken, Task<int>>? OpenCashBox { get; init; }
    public Func<QuestClear, CancellationToken, Task>? QuestCleared { get; init; }
    public Func<CancellationToken, Task<int>>? CheckAwaken { get; init; }
    public Func<CancellationToken, Task<int>>? AwakenComplete { get; init; }
    public Func<CancellationToken, Task<int>>? EnterFortuneHouse { get; init; }
    public Func<int?>? DailyPhase { get; init; }
    public Func<bool>? SoulSeedPlanted { get; init; }
    public Func<CancellationToken, Task<int>>? OpenLookChange { get; init; }
    public Func<CancellationToken, Task<int>>? AwakenReset { get; init; }
    public Func<CancellationToken, Task<int>>? OpenItemRecycle { get; init; }
    public Func<ushort, CancellationToken, Task<int>>? TakeIssuedItem { get; init; }
    public Func<CancellationToken, Task<int>>? OpenMyCampRecycle { get; init; }
    public Func<CancellationToken, Task<int>>? OpenCouponExchange { get; init; }
    public Func<CancellationToken, Task<int>>? OpenSlotExtend { get; init; }
    public Func<CancellationToken, Task<int>>? OpenMelting { get; init; }
    public Func<bool, CancellationToken, Task<int>>? OpenGhostExchange { get; init; }
    public Func<CancellationToken, Task<int>>? OpenEquipComposing { get; init; }
    public Func<CancellationToken, Task<int>>? OpenPetComposing { get; init; }
    public Func<CancellationToken, Task<int>>? OpenDecompose { get; init; }
    public Func<CancellationToken, Task<int>>? OpenEquipMerge { get; init; }
    public Func<CancellationToken, Task<int>>? OpenChargeTimer { get; init; }
    public Func<CancellationToken, Task<int>>? OpenPhantomRefine { get; init; }
    public Func<CancellationToken, Task<int>>? OpenPetRetrain { get; init; }
    public Func<CancellationToken, Task<int>>? OpenPetLevelUp { get; init; }
    public Func<CancellationToken, Task<int>>? OpenPetReinforce { get; init; }
    public Func<CancellationToken, Task<int>>? OpenPetReinforceReset { get; init; }
    public Func<int, CancellationToken, Task<int>>? CoupleFunction { get; init; }
    public Func<IReadOnlyList<ushort>, CancellationToken, Task<int>>? MissionCheck { get; init; }
    public Func<IReadOnlyList<ushort>, CancellationToken, Task<int>>? HarconDefEnter { get; init; }
    public Func<int, CancellationToken, Task<int>>? OpenHarconShop { get; init; }
    public Func<int, CancellationToken, Task>? SummonMonsters { get; init; }
    public Func<int, CancellationToken, Task<int>>? StartNpcSkill { get; init; }
    public Func<ushort, CancellationToken, Task<int?>>? ZoneUserCount { get; init; }
    public Func<CancellationToken, Task<IReadOnlyList<BossKillRecord>?>>? LatestBossKills { get; init; }

    public Func<int, IReadOnlyList<ushort>, NpcEntity?, CancellationToken, Task<int>>? HarconPlay { get; init; }
    public Func<int, CancellationToken, Task<int>>? OpenSoulWindow { get; init; }
    public Func<int, CancellationToken, Task<int>>? OpenEquipReform { get; init; }
}

public sealed record QuestLoginLists(byte[] Normal, byte[] Monster, byte[] JobChange, byte[] Scenario);

public interface IJobQuests
{
    Task<int> CheckAsync(SkillShopEntity shop, int tier, CancellationToken ct);

    Task<int> AcceptAsync(SkillShopEntity shop, IReadOnlyList<ushort> args, Func<ushort, CancellationToken, Task> sendPage, CancellationToken ct);

    Task<int> CompleteAsync(SkillShopEntity shop, IReadOnlyList<ushort> args, Func<ushort, CancellationToken, Task> sendPage, CancellationToken ct);
}

public static class JobQuestRules
{
    public const int SecondJobQuest = 0;
    public const int ThirdJobQuest = 1;
    public const int SecondJobLevel = 60;
    public const int SecondJobTmLevel = 50;
    public const int ThirdJobLevel = 130;
    public const int ThirdJobTmLevel = 120;
    public const int ActiveSlots = 1;

    public static bool HoldsJobFor(int tier, Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return tier switch
        {
            SecondJobQuest => character.JobState < OccupationRules.SecondJobTier,
            ThirdJobQuest => character.JobState == OccupationRules.SecondJobTier,
            _ => false,
        };
    }

    public static bool LevelsFit(int tier, int level, int tmLevel) => tier switch
    {
        SecondJobQuest => level >= SecondJobLevel && tmLevel >= SecondJobTmLevel,
        ThirdJobQuest => level >= ThirdJobLevel && tmLevel >= ThirdJobTmLevel,
        _ => false,
    };
}

public sealed partial class NpcScriptExecutor : IJobQuests
{
    public const int MonsterQuestStateArgs = 3;
    public const int QuestStateArgs = 2;
    public const int HistoryCap = 99;
    public const int WithEquippedItemCheck = 5;

    private readonly S2SessionContext _ctx;
    private readonly NpcScriptCatalogs _catalogs;
    private readonly NpcScriptStores _stores;
    private readonly NpcScriptPlayer _player;
    private readonly ItemManager _itemManager;
    private readonly Random _rng;
    private readonly string _logTag;
    private readonly object _huntGate = new();
    private readonly MonsterHunt _hunt = new();
    private MonsterQuestEntry? _monsterEntry;
    private (MonsterQuestEntry Entry, MonsterHuntSnapshot Hunt)? _savedAtDeath;
    private bool _leftZone;
    private NpcEntity? _npc;
    private NpcTalkEntry? _talk;
    private ushort _npcEntityId;

    public NpcScriptExecutor(
        S2SessionContext ctx,
        NpcScriptCatalogs catalogs,
        NpcScriptStores stores,
        NpcScriptPlayer player,
        ItemManager itemManager,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsEngaged => _talk is not null;

    public Social.PartyQuestBook? PartyQuests { get; set; }

    public static readonly TimeSpan ReturnWarpDelay = TimeSpan.FromMilliseconds(500);

    private readonly HashSet<byte> _historyLoaded = new();

    public async Task SendQuestHistoryAsync(byte type, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || HistoryTypeOf(type) is not byte stored) return;
        bool alreadySent;
        lock (_historyLoaded) alreadySent = !_historyLoaded.Add(type);
        if (alreadySent)
        {
            await _ctx.SendFrameAsync(QuestFrames.HistoryAlreadySent, new[] { type }, NextRandKey(), ct).ConfigureAwait(false);
            return;
        }
        var rows = _stores.History.GetForCharacter(characterId).Where(row => row.QuestType == stored).ToArray();
        await _ctx.SendFrameAsync(QuestFrames.HistoryList, QuestFrames.HistoryListFor(type, rows), NextRandKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   quest history type {type}: {rows.Length} rows");
    }

    private static byte? HistoryTypeOf(byte type) => type switch
    {
        QuestFrames.HistoryOfNormal => QuestTypes.Normal,
        QuestFrames.HistoryOfMonster => QuestTypes.Monster,
        QuestFrames.HistoryOfScenario => QuestTypes.Scenario,
        QuestFrames.HistoryOfParty => PartyQuestRules.HistoryQuestType,
        _ => null,
    };

    private bool HistoryLoaded(byte type)
    {
        lock (_historyLoaded) return _historyLoaded.Contains(type);
    }

    public async Task ReturnWarpAsync(uint type, CancellationToken ct)
    {
        var zone = ReturnZone(type);
        if (zone is not ushort destination || ZoneChangeRequested is not { } move)
        {
            await _ctx.SendFrameAsync(QuestFrames.ReturnWarpRefused, Array.Empty<byte>(), NextRandKey(), ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_ctx.Remote}   return warp type {type} refused");
            return;
        }
        await _ctx.SendFrameAsync(QuestFrames.ReturnWarpAccepted, Array.Empty<byte>(), NextRandKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   return warp type {type} to zone {destination}");
        await move(new ZoneChangeRequest(destination, NpcMoveRules.ChangeZoneType, null, ReturnWarpDelay), ct).ConfigureAwait(false);
    }

    private ushort? ReturnZone(uint type)
    {
        int returnMap;
        switch (type)
        {
            case QuestFrames.ReturnFromMonsterQuest:
                lock (_huntGate)
                {
                    if (!_hunt.Active || _monsterEntry is not { } entry) return null;
                    if (_catalogs.MonsterQuests.GetById(entry.QuestId) is not { } quest) return null;
                    returnMap = quest.ReturnMapID;
                }
                break;
            case QuestFrames.ReturnFromPartyQuest:
                if (CharacterId is not ulong characterId || PartyQuests?.Held(characterId) is not { Success: true } item) return null;
                if (PartyQuests.Catalog.GetById((int)item.QuestId) is not { } partyQuest) return null;
                returnMap = partyQuest.ReturnMapId;
                break;
            default:
                return null;
        }
        return returnMap is >= 1 and <= QuestFrames.LastReturnMap ? (ushort)returnMap : null;
    }

    public Social.PartyQuestPlayer? AsPartyQuestPlayer()
    {
        if (_player.Character() is not { } character) return null;
        bool hunting;
        lock (_huntGate) hunting = _hunt.Active;
        return new Social.PartyQuestPlayer(_ctx, character.CharacterId, character.Name, character.MapId, Level, hunting);
    }

    public event Func<ZoneChangeRequest, CancellationToken, Task>? ZoneChangeRequested;

    public async Task<bool> OpenAsync(NpcEntity npc, ushort entityId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (npc.Source is not NpcTalkEntry talk || string.IsNullOrWhiteSpace(talk.MsgTable)) return false;
        if (CharacterId is not ulong characterId) return false;
        _npc = npc;
        _talk = talk;
        _npcEntityId = entityId;
        _eventTalk = null;

        var index = GreetingIndex(talk, characterId);
        var row = _catalogs.NpcMessages.GetRow(talk.MsgTable, index);
        var dialog = new NpcDialogPacket
        {
            EntityId = entityId,
            PortraitType = (ushort)npc.ChrType,
            NpcSType = (byte)Math.Min(_catalogs.NpcMessages.GetRowCount(talk.MsgTable), byte.MaxValue),
            Text = row?.Message ?? string.Empty,
        };
        await SendAsync(Opcode.S2C_NPC_DIALOG, dialog.EncodePayload(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   NPC {npc.ChrType} greets with {talk.MsgTable} message {index} (pre-check {talk.PreCheckType})");
        return true;
    }

    public void Close()
    {
        _eventTalk = null;
        _npc = null;
        _talk = null;
        _npcEntityId = 0;
    }

    public Task ShowMessageAsync(ushort entityId, int index, CancellationToken ct)
    {
        if (!IsEngagedWith(entityId)) return Task.CompletedTask;
        return SendMessageAsync(index, QuestInSlot(0) ?? 0, ct);
    }

    public async Task CallAsync(ushort entityId, uint functionId, IReadOnlyList<ushort> args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!IsEngagedWith(entityId))
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   script function {functionId} for 0x{entityId:X4} ignored: not talking to it");
            return;
        }
        var call = NpcScript.Classify(functionId);
        var questId = QuestInSlot(call.Operation is NpcScriptOperation.QuestActive or NpcScriptOperation.ScenarioQuestActive ? 0 : call.Slot) ?? 0;
        Console.WriteLine($"{_logTag} {_ctx.Remote}   script function {functionId} ({call.Operation}, slot {call.Slot}, quest {questId}) args [{string.Join(",", args)}]");
        int result;
        switch (call.Operation)
        {
            case NpcScriptOperation.Exit:
                await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
                Close();
                return;
            case NpcScriptOperation.Unhandled:
                Console.WriteLine($"{_logTag} {_ctx.Remote}   script function {functionId} is not implemented; dialog closed");
                await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
                Close();
                return;
            case NpcScriptOperation.QuestCheck:
                result = await QuestCheckAsync(call.Slot, args, QuestTypes.Normal, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.ScenarioQuestCheck:
                result = await QuestCheckAsync(call.Slot, args, QuestTypes.Scenario, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.QuestAccept:
            case NpcScriptOperation.QuestContinue:
                result = await QuestAcceptAsync(call.Slot, args, QuestTypes.Normal, oncePerDay: false, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.QuestAcceptOncePerDay:
                result = await QuestAcceptAsync(call.Slot, args, QuestTypes.Normal, oncePerDay: true, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.ScenarioQuestAccept:
            case NpcScriptOperation.ScenarioQuestContinue:
                result = await QuestAcceptAsync(call.Slot, args, QuestTypes.Scenario, oncePerDay: false, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.QuestComplete:
            case NpcScriptOperation.QuestCompleteWithoutHistory:
                result = await QuestCompleteAsync(call.Slot, args, QuestTypes.Normal, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.ScenarioQuestComplete:
                result = await QuestCompleteAsync(call.Slot, args, QuestTypes.Scenario, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.QuestQuit:
            case NpcScriptOperation.MonsterQuestQuit:
                result = 1;
                break;
            case NpcScriptOperation.PartyQuestCheck:
                result = await PartyQuestCheckAsync(call.Slot, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.PartyQuestAccept:
                result = await PartyQuestAcceptAsync(call.Slot, args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.PartyQuestComplete:
                result = await PartyQuestCompleteAsync(call.Slot, args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.PartyQuestSuccess:
                result = NpcScript.Refused;
                break;
            case NpcScriptOperation.MonsterQuestCheck:
                result = await MonsterQuestCheckAsync(call.Slot, args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.MonsterQuestAccept:
            case NpcScriptOperation.MonsterQuestContinue:
                result = await MonsterQuestAcceptAsync(call.Slot, args, oncePerDay: false, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.MonsterQuestAcceptOncePerDay:
                result = await MonsterQuestAcceptAsync(call.Slot, args, oncePerDay: true, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.MonsterQuestComplete:
            case NpcScriptOperation.MonsterQuestCompleteWithoutHistory:
                result = await MonsterQuestCompleteAsync(call.Slot, args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.MonsterQuestState:
                result = await MonsterQuestStateAsync(args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.QuestActive:
                result = IsNormalListActive(ActiveQuestId(call.Slot)) ? 1 : 2;
                break;
            case NpcScriptOperation.ScenarioQuestActive:
                result = QuestOf(ActiveQuestId(call.Slot), QuestTypes.Scenario) is not null && IsActive(QuestTypes.Scenario, ActiveQuestId(call.Slot)) ? 1 : 2;
                break;
            case NpcScriptOperation.QuestState:
                result = await QuestStateAsync(call.Slot, args, QuestTypes.Normal, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.ScenarioQuestState:
                result = await QuestStateAsync(call.Slot, args, QuestTypes.Scenario, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.HasItem:
                result = HasItemResult(args);
                break;
            case NpcScriptOperation.ItemCount:
                result = ItemCountResult(_talk!.CheckItemId0, _talk.CheckItemCnt0);
                break;
            case NpcScriptOperation.SecondItemCount:
                result = ItemCountResult(_talk!.CheckItemId1, _talk.CheckItemCnt1);
                break;
            case NpcScriptOperation.GiveItem:
                result = await GiveNpcItemAsync(args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.TakeItem:
                result = await TakeNpcItemAsync(args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.RandomMessage:
                if (args.Count < NpcScript.RandomMessageArgs)
                {
                    result = NpcScript.Refused;
                    break;
                }
                lock (_rng) result = _rng.Next(NpcScript.RandomMessageArgs) + 1;
                break;
            case NpcScriptOperation.CheckGender:
                result = _player.Character() is { } gendered
                    ? NpcScript.GenderResult((int)gendered.Species, args.Count)
                    : NpcScript.Refused;
                break;
            case NpcScriptOperation.FTypeOnly:
                result = _player.Character() is { } typed
                    ? NpcScript.FTypeResult(StatGrowth.TypeIndexOf(typed.FType), call.Slot)
                    : NpcScript.Refused;
                break;
            case NpcScriptOperation.OpenBank:
            case NpcScriptOperation.OpenWarehouse:
            case NpcScriptOperation.OpenRefine:
            case NpcScriptOperation.OpenGrowth:
            case NpcScriptOperation.OpenResetMix:
            case NpcScriptOperation.OpenCashBox:
                result = call.Operation switch
                {
                    NpcScriptOperation.OpenCashBox => _player.OpenCashBox is { } openCashBox ? await openCashBox(ct).ConfigureAwait(false) : NpcScript.Refused,
                    NpcScriptOperation.OpenBank => await _player.OpenBank(ct).ConfigureAwait(false),
                    NpcScriptOperation.OpenWarehouse => await _player.OpenWarehouse(ct).ConfigureAwait(false),
                    NpcScriptOperation.OpenGrowth => await _player.OpenGrowth(false, ct).ConfigureAwait(false),
                    NpcScriptOperation.OpenResetMix => await _player.OpenGrowth(true, ct).ConfigureAwait(false),
                    _ => await _player.OpenRefine(ct).ConfigureAwait(false),
                };
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.OpenCompound:
            case NpcScriptOperation.OpenExchangeShop:
            case NpcScriptOperation.OpenVipShop:
                result = call.Operation switch
                {
                    NpcScriptOperation.OpenCompound => await _player.OpenCompound(_talk!.CompounderTable, ct).ConfigureAwait(false),
                    NpcScriptOperation.OpenExchangeShop => await _player.OpenExchangeShop(_talk!.ExchangeShopTable, ct).ConfigureAwait(false),
                    _ => await _player.OpenVipShop(_talk!.VipShopTable, ct).ConfigureAwait(false),
                };
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.MoveCheck:
                result = MoveCheck();
                break;
            case NpcScriptOperation.TakeIssuedItem:
                result = _player.TakeIssuedItem is { } takeIssued ? await takeIssued(_npcEntityId, ct).ConfigureAwait(false) : NpcScript.Refused;
                break;
            case NpcScriptOperation.CheckAwaken:
                result = _player.CheckAwaken is { } checkAwaken ? await checkAwaken(ct).ConfigureAwait(false) : NpcScript.Refused;
                break;
            case NpcScriptOperation.AwakenComplete:
                result = _player.AwakenComplete is { } awaken ? await awaken(ct).ConfigureAwait(false) : NpcScript.Refused;
                break;
            case NpcScriptOperation.OpenItemRecycle:
                result = _player.OpenItemRecycle is { } openRecycle ? await openRecycle(ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.OpenMyCampRecycle:
                result = _player.OpenMyCampRecycle is { } openCampRecycle ? await openCampRecycle(ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.OpenCouponExchange:
                result = _player.OpenCouponExchange is { } openCoupons ? await openCoupons(ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.OpenSlotExtend:
            case NpcScriptOperation.OpenMelting:
            case NpcScriptOperation.OpenEquipComposing:
            case NpcScriptOperation.OpenPetComposing:
            case NpcScriptOperation.OpenDecompose:
            case NpcScriptOperation.OpenEquipMerge:
            case NpcScriptOperation.OpenChargeTimer:
            case NpcScriptOperation.OpenPhantomRefine:
            case NpcScriptOperation.OpenPetRetrain:
            case NpcScriptOperation.OpenPetLevelUp:
            case NpcScriptOperation.OpenPetReinforce:
            case NpcScriptOperation.OpenPetReinforceReset:
                var openWindow = call.Operation switch
                {
                    NpcScriptOperation.OpenPetReinforceReset => _player.OpenPetReinforceReset,
                    NpcScriptOperation.OpenPetReinforce => _player.OpenPetReinforce,
                    NpcScriptOperation.OpenPetRetrain => _player.OpenPetRetrain,
                    NpcScriptOperation.OpenPetLevelUp => _player.OpenPetLevelUp,
                    NpcScriptOperation.OpenMelting => _player.OpenMelting,
                    NpcScriptOperation.OpenEquipComposing => _player.OpenEquipComposing,
                    NpcScriptOperation.OpenPetComposing => _player.OpenPetComposing,
                    NpcScriptOperation.OpenDecompose => _player.OpenDecompose,
                    NpcScriptOperation.OpenEquipMerge => _player.OpenEquipMerge,
                    NpcScriptOperation.OpenChargeTimer => _player.OpenChargeTimer,
                    NpcScriptOperation.OpenPhantomRefine => _player.OpenPhantomRefine,
                    _ => _player.OpenSlotExtend,
                };
                result = openWindow is { } openUpgrade ? await openUpgrade(ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.OpenSoulWindow:
                result = _player.OpenSoulWindow is { } openSoul ? await openSoul(call.Slot, ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.OpenEquipReform:
                result = _player.OpenEquipReform is { } openReform ? await openReform(call.Slot, ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.OpenGhostExchange:
                result = _player.OpenGhostExchange is { } openGhost ? await openGhost(call.Slot == 1, ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.MissionCheck:
                result = _player.MissionCheck is { } missionCheck ? await missionCheck(args, ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.OpenHarconShop:
                result = _player.OpenHarconShop is { } openHarconShop && _npc is { } shopNpc ? await openHarconShop(shopNpc.ChrType, ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.HarconDefEnter:
                result = _player.HarconDefEnter is { } harconEnter ? await harconEnter(args, ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.HarconPlay:
                result = _player.HarconPlay is { } harconPlay ? await harconPlay(call.Slot, args, _npc, ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.MonsterQuestZoneRank:
                result = await ZoneRankAsync(ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.Couple:
                result = _player.CoupleFunction is { } coupleFunction ? await coupleFunction(call.Slot, ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.AwakenReset:
                result = _player.AwakenReset is { } awakenReset ? await awakenReset(ct).ConfigureAwait(false) : NpcScript.Refused;
                break;
            case NpcScriptOperation.OpenLookChange:
                result = _player.OpenLookChange is { } openLook ? await openLook(ct).ConfigureAwait(false) : NpcScript.Refused;
                break;
            case NpcScriptOperation.EnterFortuneHouse:
                result = _player.EnterFortuneHouse is { } enterFortune ? await enterFortune(ct).ConfigureAwait(false) : NpcScript.Refused;
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.ItemStatCheck:
                result = ItemStatCheck(call.Slot);
                break;
            case NpcScriptOperation.CheckDailyValue:
                result = args.Count == DailyCycle.CheckArgs && _npc is not null && _player.DailyPhase?.Invoke() is { } dailyPhase
                    ? dailyPhase + 1
                    : NpcScript.Refused;
                break;
            case NpcScriptOperation.NotifyCardBattleRank:
                result = await NotifyCardBattleRankAsync(ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.GuildSetupCheck:
                result = await GuildSetupCheckAsync(ct).ConfigureAwait(false);
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.GuildWindow:
                result = await GuildWindowAsync(call.Slot, ct).ConfigureAwait(false);
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.CheckSympathizing:
                result = SympathizingRules.Result(args.Count, _player.SoulSeedPlanted?.Invoke() == true);
                break;
            case NpcScriptOperation.EventTalk:
                result = await EventTalkStartAsync(call.Slot, args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.LimitTeleport:
                result = await LimitTeleportAsync(call.Slot, args, ct).ConfigureAwait(false);
                if (result == NpcScript.Silent) return;
                break;
            case NpcScriptOperation.CheckEnterPartyZone:
                result = await CheckEnterPartyZoneAsync(args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.MonsterSummon:
                result = await MonsterSummonAsync(args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.NpcSkillStart:
                result = await NpcSkillStartAsync(args, ct).ConfigureAwait(false);
                if (result == NpcScript.Silent)
                {
                    Close();
                    return;
                }
                break;
            case NpcScriptOperation.WarpGate:
                result = await WarpGateAsync(call.Slot == 1, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.CheckJob:
                result = _talk is { } jobTalk && _player.Character() is { } jobCharacter
                    ? NpcCheckRules.Job(jobCharacter.JobTier, jobTalk.JobTypeFlag1, jobTalk.JobTypeFlag2)
                    : NpcScript.Refused;
                break;
            case NpcScriptOperation.SkillCheckSlot:
                result = SkillCheckRow(0) is { } slotRow && call.Slot < slotRow.Count
                    ? NpcCheckRules.SkillSlot(slotRow[call.Slot], _player.Skills())
                    : NpcScript.Refused;
                break;
            case NpcScriptOperation.SkillCheckAll:
                result = SkillCheckRow(call.Slot) is { } allRow
                    ? NpcCheckRules.AllSkills(allRow, _player.Skills())
                    : NpcScript.Refused;
                break;
            case NpcScriptOperation.RandomMove:
                result = await RandomMoveAsync(args, ct).ConfigureAwait(false);
                break;
            case NpcScriptOperation.MoveComplete:
                result = await MoveCompleteAsync(args, ct).ConfigureAwait(false);
                if (result == NpcScript.Silent) return;
                break;
            case NpcScriptOperation.TeleportCheck:
                result = TeleportCheck(args);
                break;
            case NpcScriptOperation.TeleportStart:
            case NpcScriptOperation.ImageTeleportStart:
                result = await TeleportStartAsync(args, call.Operation == NpcScriptOperation.ImageTeleportStart, ct).ConfigureAwait(false);
                if (result == NpcScript.Silent) return;
                break;
            default:
                Console.WriteLine($"{_logTag} {_ctx.Remote}   script function {functionId} ({call.Operation}) is not implemented; dialog closed");
                await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
                Close();
                return;
        }
        await FinishAsync(result, args, questId, ct).ConfigureAwait(false);
    }

    public Task<int> CheckAsync(SkillShopEntity shop, int tier, CancellationToken ct) => Task.FromResult(CheckJobQuest(shop, tier));

    private int CheckJobQuest(SkillShopEntity shop, int tier)
    {
        ArgumentNullException.ThrowIfNull(shop);
        if (_player.Character() is not { } character || shop.JobQuestId < 1) return NpcScript.Refused;
        var questId = shop.JobQuestId;
        if (shop.LimitJobType != 0 && shop.LimitJobType != OccupationRules.CurrentJobOf(character)) return 6;
        if (character.ChangeJobQuestState != 0) return character.ChangeJobQuestState == shop.ChangeJob ? 4 : 6;
        if (!JobQuestRules.HoldsJobFor(tier, character)) return 6;
        if (!JobQuestRules.LevelsFit(tier, Level, _player.Stats()?.TmLevel ?? character.TmLevel)) return 5;
        if (QuestOf(questId, QuestTypes.JobChange) is not { } quest) return NpcScript.Refused;
        if (!IsActive(QuestTypes.JobChange, questId)) return 1;
        return HoldsRequiredItems(character.CharacterId, quest, QuestTypes.JobChange) ? 2 : 3;
    }

    public async Task<int> AcceptAsync(SkillShopEntity shop, IReadOnlyList<ushort> args, Func<ushort, CancellationToken, Task> sendPage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(sendPage);
        if (CharacterId is not ulong characterId || QuestOf(shop.JobQuestId, QuestTypes.JobChange) is not { } quest) return NpcScript.Refused;
        var questId = shop.JobQuestId;
        if (IsActive(QuestTypes.JobChange, questId)) return NpcScript.Refused;
        if (_stores.Quests.CountActiveOfType(characterId, QuestTypes.JobChange) >= JobQuestRules.ActiveSlots) return 2;
        if (Arg(args, 1) is not ushort acceptedMessage) return NpcScript.Refused;
        _stores.Quests.AddActive(new Quest
        {
            CharacterId = characterId,
            QuestType = QuestTypes.JobChange,
            QuestId = questId,
            CompletionCount = 1,
        });
        await _ctx.SendFrameAsync(QuestFrames.JobQuestAdded, QuestFrames.ScenarioQuestAdded(questId, 1, TextsOf(quest)), NextRandKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   job quest {questId} accepted at the skill master (job {shop.ChangeJob})");
        await sendPage(acceptedMessage, ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    public async Task<int> CompleteAsync(SkillShopEntity shop, IReadOnlyList<ushort> args, Func<ushort, CancellationToken, Task> sendPage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(sendPage);
        if (CharacterId is not ulong characterId || QuestOf(shop.JobQuestId, QuestTypes.JobChange) is not { } quest) return NpcScript.Refused;
        var questId = shop.JobQuestId;
        if (_stores.Quests.FindActive(characterId, QuestTypes.JobChange, questId, monsterId: 0) is not { } entry) return NpcScript.Refused;
        var row = QuestRules.ResultRow(_catalogs.QuestResults.RowsOf(quest.ResultTable), entry.CompletionCount);
        var required = QuestTurnIn.Required(quest, _catalogs.Items);
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        if (!QuestTurnIn.HoldsAll(bag, required) || RequiredItemEquipped(characterId, required)) return NpcScript.Refused;
        var rewards = Rewards(row?.RowItems(), row?.Type ?? 0);
        if (!Fits(bag, required, rewards)) return 2;
        if (Arg(args, entry.CompletionCount < QuestRules.MaxCountOf(quest) ? 1 : 3) is not ushort message) return NpcScript.Refused;

        var taken = TakeItems(bag, required);
        var operations = new List<BagOperation>();
        operations.AddRange(GrantRewards(characterId, rewards));
        operations.AddRange(taken);
        var rates = _player.Rates();
        var exp = Math.Max(0, rates.ScaleExp(quest.Exp));
        var tmExp = Math.Max(0, rates.ScaleTmxp(quest.Tmxp));
        await _player.GrantProgress(exp, tmExp, ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_QUEST_RESULT, QuestResultFrames.QuestResult(
            operations, rewards.Select(reward => ((uint)reward.ItemIndex, (ushort)reward.Count)).ToList(),
            exp, tmExp, 0, 0, 0, 0, 0), ct).ConfigureAwait(false);
        var stateStored = _player.SetJobQuestState is { } setState
            && await setState((byte)shop.ChangeJob, ct).ConfigureAwait(false);
        RecordCompletion(characterId, QuestTypes.JobChange, questId, entry.CompletionCount, monster: 0, kills: 0, clearTimeMs: 0);
        await sendPage(message, ct).ConfigureAwait(false);
        _stores.Quests.RemoveActive(characterId, QuestTypes.JobChange, questId, monsterId: 0);
        await _ctx.SendFrameAsync(QuestFrames.JobQuestRemoved, QuestFrames.QuestRemoved(questId), NextRandKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   job quest {questId} completed: exp {exp}, TM {tmExp}, job {shop.ChangeJob} unlocked{(stateStored ? string.Empty : " (state NOT stored)")}");
        await ClearedAsync(QuestClear.Uncounted(QuestTypes.JobChange, questId), ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    public async Task GiveUpJobQuestAsync(int questId, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || !IsActive(QuestTypes.JobChange, questId)) return;
        _stores.Quests.RemoveActive(characterId, QuestTypes.JobChange, questId, monsterId: 0);
        await _ctx.SendFrameAsync(QuestFrames.JobQuestRemoved, QuestFrames.QuestRemoved(questId), NextRandKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   job quest {questId} given up");
    }

    public async Task GiveUpQuestAsync(int questId, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId) return;
        var type = IsActive(QuestTypes.Normal, questId) ? QuestTypes.Normal
            : IsActive(QuestTypes.TutorialNormal, questId) ? QuestTypes.TutorialNormal
            : (byte)0;
        if (type == 0) return;
        _stores.Quests.RemoveActive(characterId, type, questId, monsterId: 0);
        await SendAsync(Opcode.S2C_QUEST_REMOVED, QuestFrames.QuestRemoved(questId), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   quest {questId} given up");
    }

    public async Task GiveUpScenarioQuestAsync(int questId, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || !IsActive(QuestTypes.Scenario, questId)) return;
        _stores.Quests.RemoveActive(characterId, QuestTypes.Scenario, questId, monsterId: 0);
        await SendAsync(Opcode.S2C_QUEST_COMPLETED, QuestFrames.QuestRemoved(questId), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   scenario quest {questId} given up");
    }

    public async Task GiveUpMonsterQuestAsync(int questId, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId) return;
        MonsterQuestEntry? removed;
        lock (_huntGate)
        {
            _hunt.Reset();
            removed = _monsterEntry is { } entry && entry.QuestId == questId ? entry : null;
            if (removed is not null) _monsterEntry = null;
        }
        _stores.Hunts.Delete(characterId);
        if (removed is null) return;
        _stores.Quests.RemoveActive(characterId, QuestTypes.Monster, removed.QuestId, removed.Monster);
        await SendAsync(Opcode.S2C_MONSTER_QUEST_REMOVED, QuestFrames.MonsterQuestRemoved(questId), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {questId} given up ({(removed.Success ? "finished" : "not finished")})");
    }

    public void LoadForCharacter(ulong characterId)
    {
        var rows = _stores.Quests.GetActiveForCharacter(characterId)
            .Where(quest => quest.QuestType == QuestTypes.Monster)
            .ToList();
        MonsterQuestEntry? entry = null;
        foreach (var row in rows)
        {
            if (_catalogs.MonsterQuests.GetById(row.QuestId) is not { } quest) continue;
            var grades = MonsterQuests.Grades(quest);
            var grade = MonsterQuests.GradeOfMonster(grades, row.MonsterId);
            if (grade == MonsterQuests.NoGrade) continue;
            entry = new MonsterQuestEntry(
                row.QuestId, row.CompletionCount, grade, row.MonsterId, row.KillCount, row.RecordTime,
                row.KillCount >= grades[grade].Count && row.KillCount > 0);
            break;
        }
        lock (_huntGate)
        {
            _leftZone = false;
            _monsterEntry = entry;
            _savedAtDeath = null;
            _hunt.Reset();
            if (entry is not null && _stores.Hunts.Find(characterId) is { } saved && saved.QuestId == entry.QuestId)
            {
                _hunt.Restore(new MonsterHuntSnapshot(
                    saved.Active, saved.QuestId, saved.Grade, saved.RemainingMs, saved.AccumulatorMs, saved.Kills, saved.Target));
            }
        }
        if (entry is not null)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {entry.QuestId} restored: grade {entry.Grade}, kills {entry.Kills}, {(entry.Success ? "finished" : "running")}, hunt {(_hunt.Active ? $"{_hunt.RemainingMs} ms left" : "stopped")}");
        }
    }

    public QuestLoginLists LoginLists(ulong characterId)
    {
        var active = _stores.Quests.GetActiveForCharacter(characterId);
        var normal = active
            .Where(quest => quest.QuestType is QuestTypes.Normal or QuestTypes.TutorialNormal)
            .Take(QuestRules.Slots)
            .Select(quest => new QuestListEntry(quest.QuestId, quest.CompletionCount))
            .ToList();
        var jobChange = active
            .Where(quest => quest.QuestType == QuestTypes.JobChange)
            .Take(QuestRules.Slots)
            .Select(quest => quest.QuestId)
            .ToList();
        var scenario = active
            .Where(quest => quest.QuestType == QuestTypes.Scenario)
            .Take(QuestRules.Slots)
            .Select(quest => new QuestListEntry(quest.QuestId, quest.CompletionCount))
            .ToList();
        var monster = new List<MonsterQuestListEntry>(MonsterQuests.Slots);
        lock (_huntGate)
        {
            if (_monsterEntry is { } entry) monster.Add(ListEntry(entry));
        }
        return new QuestLoginLists(
            QuestFrames.QuestList(normal),
            QuestFrames.MonsterQuestList(monster),
            QuestFrames.QuestIdList(jobChange),
            QuestFrames.QuestList(scenario));
    }

    public void SaveHunt()
    {
        if (CharacterId is not ulong characterId) return;
        MonsterHuntRecord? record;
        lock (_huntGate)
        {
            if (_leftZone) return;
            record = _savedAtDeath is { } saved ? HuntRecord(characterId, saved.Hunt with { Active = true })
                : _monsterEntry is null ? null
                : HuntRecord(characterId, _hunt.Snapshot());
        }
        if (record is null)
        {
            _stores.Hunts.Delete(characterId);
            return;
        }
        _stores.Hunts.Save(record);
    }

    public void LeaveZone()
    {
        SaveHunt();
        lock (_huntGate)
        {
            _leftZone = true;
            _hunt.Reset();
            _monsterEntry = null;
            _savedAtDeath = null;
        }
        Close();
    }

    public IReadOnlyList<(Opcode Opcode, byte[] Payload)> AdvanceHunt(int elapsedMs)
    {
        var frames = new List<(Opcode Opcode, byte[] Payload)>();
        if (CharacterId is not ulong characterId) return frames;
        MonsterQuestEntry? removed = null;
        MonsterQuestEntry? kept = null;
        lock (_huntGate)
        {
            if (!_hunt.Active) return frames;
            var entry = _monsterEntry is { } held && held.QuestId == _hunt.QuestId ? held : null;
            var tick = _hunt.Advance(elapsedMs);
            if (tick.SendsTimer && entry is not null && LimitOf(entry) is int limit)
            {
                frames.Add((Opcode.S2C_MONSTER_QUEST_TIMER, QuestFrames.MonsterQuestTimer(_hunt.RemainingMs, limit)));
            }
            if (!tick.TimedOut) return frames;

            frames.Add((Opcode.S2C_MONSTER_QUEST_TIMED_OUT, QuestFrames.MonsterQuestTimedOut(entry?.QuestId ?? 0)));
            var questId = _hunt.QuestId;
            var kills = _hunt.Kills;
            _hunt.Stop();
            if (entry is null)
            {
                Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {questId} timed out without an entry");
                return frames;
            }
            entry = entry with { Kills = kills };
            if (!entry.Success)
            {
                _monsterEntry = null;
                removed = entry;
                frames.Add((Opcode.S2C_MONSTER_QUEST_REMOVED, QuestFrames.MonsterQuestRemoved(questId)));
            }
            else
            {
                _monsterEntry = entry;
                kept = entry;
            }
        }
        if (removed is not null)
        {
            _stores.Quests.RemoveActive(characterId, QuestTypes.Monster, removed.QuestId, removed.Monster);
            _stores.Hunts.Delete(characterId);
            Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {removed.QuestId} timed out unfinished ({removed.Kills} kills): removed");
        }
        if (kept is not null)
        {
            SaveProgress(characterId, kept);
            Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {kept.QuestId} timed out after its target: kept for the reward");
        }
        return frames;
    }

    public IReadOnlyList<(Opcode Opcode, byte[] Payload)> CountKill(int monsterType)
    {
        var frames = new List<(Opcode Opcode, byte[] Payload)>();
        if (CharacterId is not ulong characterId) return frames;
        MonsterQuestEntry? changed;
        lock (_huntGate)
        {
            if (_monsterEntry is not { } entry || !_hunt.Active || entry.QuestId != _hunt.QuestId) return frames;
            if (_catalogs.MonsterQuests.GetById(entry.QuestId) is not { } quest) return frames;
            var grades = MonsterQuests.Grades(quest);
            if (entry.Grade < 0 || entry.Grade >= grades.Count || grades[entry.Grade].Monster != monsterType) return frames;

            var reached = _hunt.CountKill();
            entry = entry with { Kills = _hunt.Kills };
            frames.Add((Opcode.S2C_MONSTER_QUEST_KILLS, QuestFrames.MonsterQuestKills(_hunt.Kills, _hunt.Target)));
            if (reached)
            {
                entry = entry with { Success = true, ClearTimeMs = grades[entry.Grade].TimeLimitMs - _hunt.RemainingMs };
                frames.Add((Opcode.S2C_MONSTER_QUEST_SUCCEEDED, QuestFrames.MonsterQuestSucceeded(entry.QuestId, quest.ReturnMapID != 0)));
                Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {entry.QuestId} target reached: {_hunt.Kills} kills in {entry.ClearTimeMs} ms");
            }
            _monsterEntry = entry;
            changed = entry;
        }
        SaveProgress(characterId, changed);
        return frames;
    }

    public IReadOnlyList<(Opcode Opcode, byte[] Payload)> SuspendHuntForDeath()
    {
        var frames = new List<(Opcode Opcode, byte[] Payload)>();
        if (CharacterId is not ulong characterId) return frames;
        MonsterQuestEntry? kept = null;
        lock (_huntGate)
        {
            if (!_hunt.Active) return frames;
            var entry = _monsterEntry is { } held && held.QuestId == _hunt.QuestId ? held : null;
            if (entry is null) return frames;
            _savedAtDeath = (entry, _hunt.Snapshot());
            _hunt.Stop();
            if (!entry.Success)
            {
                _monsterEntry = null;
                frames.Add((Opcode.S2C_MONSTER_QUEST_REMOVED, QuestFrames.MonsterQuestRemoved(entry.QuestId)));
            }
            else
            {
                kept = entry;
            }
        }
        if (kept is not null) SaveProgress(characterId, kept);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest hunt paused by death");
        return frames;
    }

    public IReadOnlyList<(Opcode Opcode, byte[] Payload)> ResumeHuntAfterResurrection()
    {
        var frames = new List<(Opcode Opcode, byte[] Payload)>();
        if (CharacterId is not ulong characterId) return frames;
        MonsterQuestEntry entry;
        lock (_huntGate)
        {
            if (_savedAtDeath is not { } saved) return frames;
            _savedAtDeath = null;
            entry = saved.Entry;
            frames.Add((Opcode.S2C_MONSTER_QUEST_REMOVED, QuestFrames.MonsterQuestRemoved(entry.QuestId)));
            _monsterEntry = entry;
            if (_catalogs.MonsterQuests.GetById(entry.QuestId) is { } quest)
            {
                frames.Add((Opcode.S2C_MONSTER_QUEST_ADDED, QuestFrames.MonsterQuestAdded(ListEntry(entry), TextsOf(quest))));
            }
            _hunt.Start(entry.QuestId, saved.Hunt.RemainingMs);
            if (LimitOf(entry) is int limit)
            {
                frames.Add((Opcode.S2C_MONSTER_QUEST_TIMER, QuestFrames.MonsterQuestTimer(_hunt.RemainingMs, limit)));
            }
            _hunt.SetCounter(saved.Hunt.Kills, saved.Hunt.Target);
            frames.Add((Opcode.S2C_MONSTER_QUEST_KILLS, QuestFrames.MonsterQuestKills(_hunt.Kills, _hunt.Target)));
        }
        SaveProgress(characterId, entry);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {entry.QuestId} hunt resumed after resurrection: {_hunt.RemainingMs} ms left");
        return frames;
    }

    public void DiscardHuntSavedAtDeath()
    {
        if (CharacterId is not ulong characterId) return;
        MonsterQuestEntry? lost;
        lock (_huntGate)
        {
            lost = _savedAtDeath is { } saved && !saved.Entry.Success ? saved.Entry : null;
            _savedAtDeath = null;
        }
        if (lost is null) return;
        _stores.Quests.RemoveActive(characterId, QuestTypes.Monster, lost.QuestId, lost.Monster);
        _stores.Hunts.Delete(characterId);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {lost.QuestId} lost: restarted in town before finishing it");
    }

    private async Task<int> QuestCheckAsync(int slot, IReadOnlyList<ushort> args, byte type, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || QuestInSlot(slot) is not int questId) return NpcScript.Refused;
        if (QuestOf(questId, type) is not { } quest) return NpcScript.Refused;
        var entry = _stores.Quests.FindActive(characterId, type, questId, monsterId: 0);
        if (entry is null)
        {
            if (Money < quest.SrcGelder) return 5;
            if (Arg(args, 1) is not ushort allowedMessage || Arg(args, 4) is not ushort refusedMessage) return NpcScript.Refused;
            var allowed = HistoryCount(characterId, type, questId) < QuestRules.MaxCountOf(quest);
            await SendMessageAsync(allowed ? allowedMessage : refusedMessage, questId, ct).ConfigureAwait(false);
            return NpcScript.Pending;
        }
        if (QuestRules.MaxCountOf(quest) < entry.CompletionCount) return 4;
        if (type == QuestTypes.Normal && quest.SrcSkill0 != 0)
        {
            if (!_player.Skills().TryGetValue(quest.SrcSkill0, out var skillLevel)) return 3;
            if (skillLevel > quest.SrcSkillLv0) return 7;
            if (skillLevel < quest.SrcSkillLv0) return 6;
        }
        return HoldsRequiredItems(characterId, quest, type) ? 2 : 3;
    }

    private async Task<int> QuestAcceptAsync(int slot, IReadOnlyList<ushort> args, byte type, bool oncePerDay, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || QuestInSlot(slot) is not int questId) return NpcScript.Refused;
        if (QuestOf(questId, type) is not { } quest) return NpcScript.Refused;
        if (!QuestRules.IsLevelValid(quest, Level)) return 3;
        if (IsActive(type, questId)) return NpcScript.Refused;
        var held = type == QuestTypes.Scenario
            ? _stores.Quests.CountActiveOfType(characterId, QuestTypes.Scenario)
            : _stores.Quests.CountActiveOfType(characterId, QuestTypes.Normal) + _stores.Quests.CountActiveOfType(characterId, QuestTypes.TutorialNormal);
        if (held >= QuestRules.Slots) return 2;
        if (Arg(args, 1) is not ushort acceptedMessage) return NpcScript.Refused;
        if (oncePerDay)
        {
            if (Arg(args, 4) is not ushort doneTodayMessage) return NpcScript.Refused;
            if (_stores.History.CompletedToday(characterId, type, questId, 0, DateTime.UtcNow))
            {
                await SendMessageAsync(doneTodayMessage, questId, ct).ConfigureAwait(false);
                return NpcScript.Pending;
            }
        }
        var history = HistoryCount(characterId, type, questId);
        if (history >= QuestRules.MaxCountOf(quest)) return NpcScript.Refused;
        var count = Math.Min(history + 1, HistoryCap);
        _stores.Quests.AddActive(new Quest
        {
            CharacterId = characterId,
            QuestType = type,
            QuestId = questId,
            CompletionCount = (byte)count,
        });
        var added = type == QuestTypes.Scenario
            ? (Opcode.S2C_QUEST_ACCEPTED, QuestFrames.ScenarioQuestAdded(questId, count, TextsOf(quest)))
            : (Opcode.S2C_QUEST_ADDED, QuestFrames.QuestAdded(questId, count, TextsOf(quest)));
        await SendAsync(added.Item1, added.Item2, ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   quest {questId} (type {type}) accepted, count {count}");
        await SendMessageAsync(acceptedMessage, questId, ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> QuestCompleteAsync(int slot, IReadOnlyList<ushort> args, byte type, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || QuestInSlot(slot) is not int questId) return NpcScript.Refused;
        if (QuestOf(questId, type) is not { } quest) return NpcScript.Refused;
        if (_stores.Quests.FindActive(characterId, type, questId, monsterId: 0) is not { } entry) return NpcScript.Refused;
        if (type == QuestTypes.Scenario && !QuestRules.IsLevelValid(quest, Level)) return 4;
        var row = QuestRules.ResultRow(_catalogs.QuestResults.RowsOf(quest.ResultTable), entry.CompletionCount);
        if (type == QuestTypes.Normal && (row is { Skill: not 0 } || quest.SrcSkill0 != 0 || quest.Options == QuestRules.UserChoiceOption))
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   quest {questId}: skill or user-choice rewards are not implemented; refused");
            return NpcScript.Refused;
        }
        if (row is not null && row.RandomTables().Any())
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   quest {questId}: random reward tables are not implemented; refused");
            return NpcScript.Refused;
        }
        var required = QuestTurnIn.Required(quest, _catalogs.Items);
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        if (!QuestTurnIn.HoldsAll(bag, required)) return NpcScript.Refused;
        if (RequiredItemEquipped(characterId, required)) return args.Count == WithEquippedItemCheck ? 5 : NpcScript.Refused;

        var rewards = Rewards(row?.RowItems(), row?.Type ?? 0);
        if (!Fits(bag, required, rewards)) return 2;
        if (Arg(args, entry.CompletionCount < QuestRules.MaxCountOf(quest) ? 1 : 3) is not ushort message) return NpcScript.Refused;

        var taken = TakeItems(bag, required);
        var operations = new List<BagOperation>();
        operations.AddRange(GrantRewards(characterId, rewards));
        operations.AddRange(taken);

        var rates = _player.Rates();
        var exp = Math.Max(0, rates.ScaleExp(quest.Exp + (row?.Exp ?? 0)));
        var tmExp = Math.Max(0, rates.ScaleTmxp(quest.Tmxp + (row?.Tmxp ?? 0)));
        var gelder = Math.Max(0, rates.ScaleGalder(row?.Gelder ?? 0));
        var rewardList = rewards.Select(reward => ((uint)reward.ItemIndex, (ushort)reward.Count)).ToList();

        if (type == QuestTypes.Scenario)
        {
            await _player.GrantProgress(exp, tmExp, ct).ConfigureAwait(false);
            await GrantMoneyAsync(gelder, ct).ConfigureAwait(false);
            await SendAsync(Opcode.S2C_QUEST_RESULT_MANIFEST, QuestResultFrames.Result(
                operations, rewardList, exp, tmExp, (uint)questId,
                (ushort)Math.Clamp(row?.Count ?? 0, 0, ushort.MaxValue), gelder), ct).ConfigureAwait(false);
        }
        else
        {
            await GrantMoneyAsync(gelder, ct).ConfigureAwait(false);
            await _player.GrantProgress(exp, tmExp, ct).ConfigureAwait(false);
            await SendAsync(Opcode.S2C_QUEST_RESULT, QuestResultFrames.QuestResult(
                operations, rewardList, exp, tmExp, (ushort)questId, entry.CompletionCount,
                gelder, 0, 0), ct).ConfigureAwait(false);
        }
        RecordCompletion(characterId, type, questId, entry.CompletionCount, monster: 0, kills: 0, clearTimeMs: 0);
        var historyType = type == QuestTypes.Scenario ? QuestFrames.HistoryOfScenario : QuestFrames.HistoryOfNormal;
        if (HistoryLoaded(historyType))
        {
            var firstRow = entry.CompletionCount < QuestFrames.FirstCompletionRow;
            var historyOpcode = (type == QuestTypes.Scenario, firstRow) switch
            {
                (true, true) => QuestFrames.ScenarioHistoryAdded,
                (true, false) => QuestFrames.ScenarioHistoryCounted,
                (false, true) => QuestFrames.NormalHistoryAdded,
                _ => QuestFrames.NormalHistoryCounted,
            };
            await _ctx.SendFrameAsync(historyOpcode, firstRow
                ? QuestFrames.ScenarioQuestAdded(questId, entry.CompletionCount, TextsOf(quest))
                : QuestFrames.HistoryCounted(questId, entry.CompletionCount), NextRandKey(), ct).ConfigureAwait(false);
        }
        _stores.Quests.RemoveActive(characterId, type, questId, monsterId: 0);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   quest {questId} (type {type}) completed: exp {exp}, TM {tmExp}, gelder {gelder}, {operations.Count} bag changes");
        await SendMessageAsync(message, questId, ct).ConfigureAwait(false);
        await SendAsync(
            type == QuestTypes.Scenario ? Opcode.S2C_QUEST_COMPLETED : Opcode.S2C_QUEST_REMOVED,
            QuestFrames.QuestRemoved(questId), ct).ConfigureAwait(false);
        await ClearedAsync(type == QuestTypes.Scenario
            ? QuestClear.Uncounted(type, questId)
            : QuestClear.Counted(type, questId, entry.CompletionCount), ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> QuestStateAsync(int slot, IReadOnlyList<ushort> args, byte type, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || _talk is null || args.Count != QuestStateArgs) return NpcScript.Refused;
        var (questId, threshold) = slot switch
        {
            0 => (_talk.CheckQuestId0, _talk.CheckQuestNum0),
            1 => (_talk.CheckQuestId1, _talk.CheckQuestNum1),
            2 => (_talk.CheckQuestId2, _talk.CheckQuestNum2),
            _ => (0, 0),
        };
        if (threshold < 1 || Arg(args, 1) is not ushort trueMessage || Arg(args, 2) is not ushort falseMessage) return NpcScript.Refused;
        var reached = HistoryCount(characterId, type, questId) >= threshold;
        await SendMessageAsync(reached ? trueMessage : falseMessage, QuestInSlot(slot) ?? 0, ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> MonsterQuestCheckAsync(int slot, IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || QuestInSlot(slot) is not int questId) return NpcScript.Refused;
        int? result = null;
        ushort? sendMessage = null;
        lock (_huntGate)
        {
            if (_hunt.IsRunningAnother(questId) || PartyQuests?.IsRunning(characterId) == true) return 6;
            if (_catalogs.MonsterQuests.GetById(questId) is not { } quest) return NpcScript.Refused;
            var entry = _monsterEntry is { } held && held.QuestId == questId ? held : null;
            if (!_hunt.Active && entry is null && _monsterEntry is not null) return 6;
            var grades = MonsterQuests.Grades(quest);
            if (entry is null)
            {
                var grade = MonsterQuests.GradeForLevel(grades, Level);
                if (grade == MonsterQuests.NoGrade || !MonsterQuests.IsLevelValid(quest, Level)) return 5;
                if (Arg(args, 1) is not ushort allowedMessage || Arg(args, 4) is not ushort refusedMessage) return NpcScript.Refused;
                var allowed = MonsterHistoryCount(characterId, questId, grades[grade].Monster) < MonsterQuests.MaxCountOf(quest);
                sendMessage = allowed ? allowedMessage : refusedMessage;
            }
            else if (MonsterQuests.MaxCountOf(quest) < entry.Count)
            {
                result = 4;
            }
            else if (!entry.Success)
            {
                result = 3;
            }
            else
            {
                var target = entry.Grade < grades.Count ? grades[entry.Grade].Count : int.MaxValue;
                if (_hunt.Active && (_hunt.Kills < 1 || _hunt.Kills < _hunt.Target || _hunt.Kills < target)) result = NpcScript.Refused;
                else if (entry.Kills < 1 || entry.Kills < target) result = NpcScript.Refused;
                else result = 2;
            }
        }
        if (sendMessage is ushort index)
        {
            await SendMessageAsync(index, questId, ct).ConfigureAwait(false);
            return NpcScript.Pending;
        }
        return result ?? NpcScript.Refused;
    }

    private async Task<int> PartyQuestCheckAsync(int slot, CancellationToken ct)
    {
        if (PartyQuests is null || QuestInSlot(slot) is not int questId || AsPartyQuestPlayer() is not { } me) return NpcScript.Refused;
        return await PartyQuests.CheckAsync(me, questId, ct).ConfigureAwait(false);
    }

    private async Task<int> PartyQuestAcceptAsync(int slot, IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (PartyQuests is null || QuestInSlot(slot) is not int questId || AsPartyQuestPlayer() is not { } me) return NpcScript.Refused;
        var refusal = PartyQuests.AcceptRefusal(me, questId);
        if (refusal != 0) return refusal;
        if (Arg(args, 1) is not ushort acceptedMessage) return NpcScript.Refused;
        if (!await PartyQuests.AcceptAsync(me, questId, ct).ConfigureAwait(false)) return NpcScript.Pending;
        await SendMessageAsync(acceptedMessage, questId, ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> PartyQuestCompleteAsync(int slot, IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (PartyQuests is null || QuestInSlot(slot) is not int questId || CharacterId is not ulong characterId) return NpcScript.Refused;
        if (Arg(args, 1) is not ushort completedMessage) return NpcScript.Refused;
        if (PartyQuests.Held(characterId) is not { Success: true } item || item.QuestId != questId) return NpcScript.Refused;
        if (PartyQuests.Catalog.GetById(questId) is not { } quest) return NpcScript.Refused;
        var rows = _catalogs.PartyQuestResults.RowsOf(quest.ResultTable);
        if (rows.Count == 0) return NpcScript.Refused;
        var row = rows[0];
        var rewards = Rewards(row?.RowItems(), row?.Type ?? 0);
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        if (!Fits(bag, Array.Empty<RequiredItem>(), rewards)) return Social.PartyQuestResults.NoRoom;
        if (await PartyQuests.RewardAsync(characterId, item, ct).ConfigureAwait(false) is not int completions)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   party quest {questId} reward refused by the Master");
            return NpcScript.Refused;
        }
        var operations = GrantRewards(characterId, rewards);
        if (HistoryLoaded(QuestFrames.HistoryOfParty))
        {
            await _ctx.SendFrameAsync(completions < QuestFrames.FirstCompletionRow ? QuestFrames.PartyHistoryAdded : QuestFrames.PartyHistoryCounted,
                QuestFrames.PartyHistoryFor((uint)questId, completions), NextRandKey(), ct).ConfigureAwait(false);
        }
        var rates = _player.Rates();
        var exp = Math.Max(0, rates.ScaleExp(PartyQuests.Catalog.RewardFor(quest.Exp, item.MemberCount, item.OwnKills, item.TargetCount, completions)));
        var tmExp = Math.Max(0, rates.ScaleTmxp(PartyQuests.Catalog.RewardFor(quest.Tmxp, item.MemberCount, item.OwnKills, item.TargetCount, completions)));
        await _ctx.SendFrameAsync(Social.PartyQuestFrames.Rewarded,
            QuestResultFrames.PartyQuestResult(operations, exp, tmExp, (uint)questId), NextRandKey(), ct).ConfigureAwait(false);
        await _player.GrantProgress(exp, tmExp, ct).ConfigureAwait(false);
        await PartyQuests.FinishAsync(characterId, (uint)questId, ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   party quest {questId} rewarded: exp {exp}, TM {tmExp}, completion {completions}, {operations.Count} bag changes");
        await SendMessageAsync(completedMessage, questId, ct).ConfigureAwait(false);
        await ClearedAsync(QuestClear.Uncounted(QuestTypes.Party, questId), ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> MonsterQuestAcceptAsync(int slot, IReadOnlyList<ushort> args, bool oncePerDay, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || QuestInSlot(slot) is not int questId) return NpcScript.Refused;
        if (_catalogs.MonsterQuests.GetById(questId) is not { } quest) return NpcScript.Refused;
        var grades = MonsterQuests.Grades(quest);
        int grade;
        lock (_huntGate)
        {
            if (_hunt.IsRunningAnother(questId)) return NpcScript.Refused;
            if (_monsterEntry is { } held && held.QuestId == questId) return NpcScript.Refused;
            grade = MonsterQuests.GradeForLevel(grades, Level);
            if (grade == MonsterQuests.NoGrade || !MonsterQuests.IsLevelValid(quest, Level)) return 3;
            if (_monsterEntry is not null) return 2;
        }
        if (Arg(args, 1) is not ushort acceptedMessage) return NpcScript.Refused;
        var monster = grades[grade].Monster;
        if (oncePerDay)
        {
            if (Arg(args, 4) is not ushort doneTodayMessage) return NpcScript.Refused;
            if (_stores.History.CompletedToday(characterId, QuestTypes.Monster, questId, monster, DateTime.UtcNow))
            {
                await SendMessageAsync(doneTodayMessage, questId, ct).ConfigureAwait(false);
                return NpcScript.Pending;
            }
        }
        var history = MonsterHistoryCount(characterId, questId, monster);
        if (history >= MonsterQuests.MaxCountOf(quest)) return NpcScript.Refused;
        var count = Math.Min(history + 1, HistoryCap);
        _stores.Quests.AddActive(new Quest
        {
            CharacterId = characterId,
            QuestType = QuestTypes.Monster,
            QuestId = questId,
            CompletionCount = (byte)count,
            MonsterId = monster,
        });

        var entry = new MonsterQuestEntry(questId, count, grade, monster, Kills: 0, ClearTimeMs: 0, Success: false);
        var frames = new List<(Opcode Opcode, byte[] Payload)>
        {
            (Opcode.S2C_MONSTER_QUEST_ADDED, QuestFrames.MonsterQuestAdded(ListEntry(entry), TextsOf(quest))),
        };
        lock (_huntGate)
        {
            _monsterEntry = entry;
            _hunt.Start(questId, grades[grade].TimeLimitMs);
            frames.Add((Opcode.S2C_MONSTER_QUEST_TIMER, QuestFrames.MonsterQuestTimer(_hunt.RemainingMs, grades[grade].TimeLimitMs)));
            _hunt.SetCounter(0, grades[grade].Count);
            frames.Add((Opcode.S2C_MONSTER_QUEST_KILLS, QuestFrames.MonsterQuestKills(_hunt.Kills, _hunt.Target)));
            _hunt.Grade = grade;
        }
        SaveProgress(characterId, entry);
        foreach (var (opcode, payload) in frames) await SendAsync(opcode, payload, ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {questId} accepted: grade {grade}, hunt {grades[grade].Count} of monster {monster} in {grades[grade].Minutes} min");
        await SendMessageAsync(acceptedMessage, questId, ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> MonsterQuestCompleteAsync(int slot, IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || QuestInSlot(slot) is not int questId) return NpcScript.Refused;
        if (_catalogs.MonsterQuests.GetById(questId) is not { } quest) return NpcScript.Refused;
        var grades = MonsterQuests.Grades(quest);
        MonsterQuestEntry entry;
        lock (_huntGate)
        {
            if (_hunt.IsRunningAnother(questId)) return NpcScript.Refused;
            if (_monsterEntry is not { } held || held.QuestId != questId) return NpcScript.Refused;
            entry = held;
            if (!entry.Success || entry.Grade < 0 || entry.Grade >= grades.Count)
            {
                Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {questId} completion refused: not finished");
                return NpcScript.Refused;
            }
            var target = grades[entry.Grade].Count;
            if (_hunt.Active && (entry.Grade != _hunt.Grade || _hunt.Kills < 1 || _hunt.Kills < _hunt.Target || _hunt.Kills < target)) return NpcScript.Refused;
            if (entry.Kills < 1 || entry.Kills < target) return NpcScript.Refused;
        }
        var row = MonsterQuests.ResultRow(_catalogs.MonsterQuestResults.RowsOf(quest.ResultTable), entry.Grade);
        if (row is not null && row.RandomTables().Any())
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {questId}: random reward tables are not implemented; refused");
            return NpcScript.Refused;
        }
        var rewards = Rewards(row?.RowItems(), row?.Type ?? 0);
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        if (!Fits(bag, Array.Empty<RequiredItem>(), rewards)) return 2;
        var messageArg = entry.Count < MonsterQuests.MaxCountOf(quest)
            ? MonsterQuests.GradeForLevel(grades, Level) switch
            {
                MonsterQuests.NoGrade => 5,
                var current when current == entry.Grade => 1,
                _ => 4,
            }
            : 3;
        if (Arg(args, messageArg) is not ushort message) return NpcScript.Refused;

        var operations = GrantRewards(characterId, rewards);
        var rates = _player.Rates();
        var grade = grades[entry.Grade];
        var exp = Math.Max(0, rates.ScaleExp(grade.Exp));
        var tmExp = Math.Max(0, rates.ScaleTmxp(Math.Max(0, grade.Tmxp)));
        var gelder = Math.Max(0, rates.ScaleGalder(row?.Gelder ?? 0));
        await GrantMoneyAsync(gelder, ct).ConfigureAwait(false);
        await _player.GrantProgress(exp, tmExp, ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_MONSTER_QUEST_RESULT, QuestResultFrames.MonsterQuestResult(
            operations,
            rewards.Select(reward => ((uint)reward.ItemIndex, (ushort)reward.Count)).ToList(),
            exp, tmExp, (ushort)questId, gelder), ct).ConfigureAwait(false);

        RecordCompletion(characterId, QuestTypes.Monster, questId, entry.Count, entry.Monster, entry.Kills, entry.ClearTimeMs);
        if (HistoryLoaded(QuestFrames.HistoryOfMonster))
        {
            await _ctx.SendFrameAsync(entry.Count < QuestFrames.FirstCompletionRow ? QuestFrames.MonsterHistoryAdded : QuestFrames.MonsterHistoryCounted,
                entry.Count < QuestFrames.FirstCompletionRow
                    ? QuestFrames.MonsterQuestAdded(ListEntry(entry), TextsOf(quest))
                    : QuestFrames.MonsterHistoryCountedFor(ListEntry(entry)), NextRandKey(), ct).ConfigureAwait(false);
        }
        _stores.Quests.RemoveActive(characterId, QuestTypes.Monster, questId, entry.Monster);
        _stores.Hunts.Delete(characterId);
        lock (_huntGate)
        {
            _monsterEntry = null;
            _hunt.Reset();
        }
        Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {questId} completed: exp {exp}, TM {tmExp}, gelder {gelder}, {operations.Count} bag changes");
        await SendMessageAsync(message, questId, ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_MONSTER_QUEST_REMOVED, QuestFrames.MonsterQuestRemoved(questId), ct).ConfigureAwait(false);
        await ClearedAsync(QuestClear.Hunted(QuestTypes.Monster, questId, entry.Count, entry.ClearTimeMs, entry.Kills), ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> MonsterQuestStateAsync(IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || _talk is null || args.Count != MonsterQuestStateArgs) return NpcScript.Refused;
        if (Arg(args, 1) is null || Arg(args, 2) is not ushort trueMessage || Arg(args, 3) is not ushort falseMessage) return NpcScript.Refused;
        var questId = _talk.CheckMonQuestId;
        if (_talk.CheckMonQuestNum < 1)
        {
            lock (_huntGate)
            {
                return _monsterEntry is { Success: true } entry && entry.QuestId == questId ? 1 : 3;
            }
        }
        var reached = MonsterHistoryCount(characterId, questId, monster: null) >= _talk.CheckMonQuestNum;
        await SendMessageAsync(reached ? trueMessage : falseMessage, QuestInSlot(0) ?? 0, ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> GiveNpcItemAsync(IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || _talk is null) return NpcScript.Refused;
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        if (PickupRules.BagCapacity - bag.Count < 1) return 2;
        if (_talk.GiveItemId == 0 || _catalogs.Items.GetById(_talk.GiveItemId) is not { } given) return 2;
        if (given.Weight + bag.Sum(item => WeightOf(item.ItemIndex) * item.WeightUnits) > _player.MaxWeight()) return 2;
        if (args.Count < 2) return NpcScript.Refused;
        var operations = GrantRewards(characterId, new[] { new RewardItem(_talk.GiveItemId, 1) });
        Console.WriteLine($"{_logTag} {_ctx.Remote}   NPC gives item {_talk.GiveItemId}");
        await SendMessageAsync(args[0], QuestInSlot(0) ?? 0, ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_ITEM_ADDED, QuestResultFrames.ItemOperations(operations), ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private async Task<int> TakeNpcItemAsync(IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (CharacterId is not ulong characterId || _talk is null) return NpcScript.Refused;
        if (_talk.TakeItemId == 0) return 2;
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var taken = new[] { new RequiredItem(_talk.TakeItemId, 1) };
        if (!QuestTurnIn.HoldsAll(bag, taken)) return 2;
        if (args.Count < 2) return NpcScript.Refused;
        var operations = TakeItems(bag, taken);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   NPC takes item {_talk.TakeItemId}");
        await SendAsync(Opcode.S2C_ITEM_ADDED, QuestResultFrames.ItemOperations(operations), ct).ConfigureAwait(false);
        await SendMessageAsync(args[0], QuestInSlot(0) ?? 0, ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    private int HasItemResult(IReadOnlyList<ushort> args)
    {
        if (CharacterId is not ulong characterId || _talk is null) return NpcScript.Refused;
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var first = bag.Any(item => item.ItemIndex == _talk.CheckItemId0);
        if (args.Count == 2) return first ? 1 : 2;
        if (args.Count != 4) return NpcScript.Refused;
        var second = bag.Any(item => item.ItemIndex == _talk.CheckItemId1);
        return (first, second) switch
        {
            (true, true) => 1,
            (true, false) => 2,
            (false, true) => 3,
            _ => 4,
        };
    }

    private int ItemCountResult(int itemId, int needed)
    {
        if (CharacterId is not ulong characterId) return NpcScript.Refused;
        var held = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
            .FirstOrDefault(item => item.ItemIndex == itemId);
        if (held is null) return 3;
        var count = _catalogs.Items.GetById(held.ItemIndex)?.ObjectClass == ItemParamEntry.RareClass ? 1 : held.Stack;
        return count < needed ? 2 : 1;
    }

    private Task ClearedAsync(QuestClear clear, CancellationToken ct) =>
        _player.QuestCleared is { } cleared ? cleared(clear, ct) : Task.CompletedTask;

    private async Task FinishAsync(int result, IReadOnlyList<ushort> args, int questId, CancellationToken ct)
    {
        if (result == NpcScript.Refused)
        {
            await SendAsync(Opcode.S2C_NPC_DIALOG_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (result < 1) return;
        if (result > args.Count)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   script result {result} has no message among {args.Count} arguments");
            await SendAsync(Opcode.S2C_NPC_DIALOG_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var index = args[result - 1];
        if (index < 1) return;
        await SendMessageAsync(index, questId, ct).ConfigureAwait(false);
    }

    private async Task SendMessageAsync(int index, int questId, CancellationToken ct)
    {
        if (_talk is null) return;
        var resolved = ResolveMessage(_talk.MsgTable, index, questId);
        if (resolved is { } message)
        {
            await SendAsync(Opcode.S2C_NPC_DIALOG_FOLLOWUP,
                QuestFrames.NpcMessage(_npcEntityId, index, message.Text, message.Kind, questId), ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_ctx.Remote}   NPC message {index} ({message.Text.Length} chars, kind {message.Kind}, quest {questId})");
            return;
        }
        await SendAsync(Opcode.S2C_NPC_DIALOG_REFUSED, QuestFrames.NpcMessageMissing(_npcEntityId, index), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   NPC message {index} not found (quest {questId})");
    }

    private (string Text, int Kind)? ResolveMessage(string msgTable, int index, int questId)
    {
        if (string.IsNullOrWhiteSpace(msgTable)) return null;
        if (_catalogs.NpcMessages.GetRow(msgTable, index) is { } row) return (row.Message, row.NpcSType);
        string? text = null;
        if (index is >= NpcMessages.QuestBase and < NpcMessages.MonsterBase)
        {
            if (_catalogs.Quests.GetById(questId) is { } quest && CharacterId is ulong characterId)
            {
                var count = ActiveCount(characterId, quest, questId);
                text = QuestRules.ResultRow(_catalogs.QuestResults.RowsOf(quest.ResultTable), count)?.Message(index - NpcMessages.QuestBase);
            }
        }
        else if (index is >= NpcMessages.MonsterBase and < NpcMessages.SkillShopBase)
        {
            if (_catalogs.MonsterQuests.GetById(questId) is { } quest)
            {
                int grade;
                lock (_huntGate) grade = _hunt.Grade;
                text = MonsterQuests.ResultRow(_catalogs.MonsterQuestResults.RowsOf(quest.ResultTable), grade)?.Message(index - NpcMessages.MonsterBase);
            }
        }
        return string.IsNullOrEmpty(text) ? null : (text, 0);
    }

    private int ActiveCount(ulong characterId, QuestInfoEntry quest, int questId)
    {
        var type = QuestRules.IsScenario(quest) ? QuestTypes.Scenario : QuestTypes.Normal;
        return _stores.Quests.FindActive(characterId, type, questId, monsterId: 0)?.CompletionCount ?? 0;
    }

    private int GreetingIndex(NpcTalkEntry talk, ulong characterId)
    {
        var argument = talk.PreCheckArg;
        if (talk.PreCheckType == NpcGreeting.Always) return 0;
        if (argument == 0) return talk.PreCheckNo;
        bool? passed = talk.PreCheckType switch
        {
            NpcGreeting.QuestDone => HistoryCount(characterId, QuestTypes.Normal, argument) >= 1,
            NpcGreeting.QuestActive => IsNormalListActive(argument),
            NpcGreeting.ItemHeld => _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).Any(item => item.ItemIndex == argument),
            NpcGreeting.ScenarioQuestDone => HistoryCount(characterId, QuestTypes.Scenario, argument) >= 1,
            NpcGreeting.ScenarioQuestActive => QuestOf(argument, QuestTypes.Scenario) is not null && IsActive(QuestTypes.Scenario, argument),
            _ => null,
        };
        if (passed is null)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   greeting pre-check {talk.PreCheckType} is not implemented; message {talk.PreCheckNo} used");
            return talk.PreCheckNo;
        }
        return passed.Value ? talk.PreCheckYes : talk.PreCheckNo;
    }

    private bool HoldsRequiredItems(ulong characterId, QuestInfoEntry quest, byte type)
    {
        var required = QuestTurnIn.Required(quest, _catalogs.Items);
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        if (type == QuestTypes.Normal && quest.Options == QuestRules.UserChoiceOption)
        {
            return required.Any(item => QuestTurnIn.Holding(bag, item) is not null);
        }
        return QuestTurnIn.HoldsAll(bag, required);
    }

    private bool RequiredItemEquipped(ulong characterId, IReadOnlyList<RequiredItem> required)
    {
        var equipped = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Equipment);
        return required.Any(item =>
            _catalogs.Items.GetById(item.ItemId) is { } definition
            && definition.Class != ItemParamEntry.StackableClass
            && equipped.Any(worn => worn.ItemIndex == item.ItemId));
    }

    private IReadOnlyList<RewardItem> Rewards(IEnumerable<(int ItemIndex, int Count)>? rowItems, int rowType)
    {
        if (rowItems is null) return Array.Empty<RewardItem>();
        var all = QuestRules.RewardItems(rowItems, _catalogs.Items);
        if (rowType == 0 || all.Count < 2) return all;
        lock (_rng) return new[] { all[_rng.Next(all.Count)] };
    }

    private bool Fits(IReadOnlyList<Item> bag, IReadOnlyList<RequiredItem> required, IReadOnlyList<RewardItem> rewards)
    {
        var weight = bag.Sum(item => WeightOf(item.ItemIndex) * item.WeightUnits)
            - required.Sum(item => WeightOf(item.ItemId) * item.Count)
            + rewards.Sum(item => WeightOf(item.ItemIndex) * item.Count);
        return weight <= _player.MaxWeight() && PickupRules.BagCapacity - bag.Count >= rewards.Count;
    }

    private int WeightOf(int itemId) => _catalogs.Items.GetById(itemId)?.Weight ?? 0;

    private List<BagOperation> GrantRewards(ulong characterId, IReadOnlyList<RewardItem> rewards)
    {
        var operations = new List<BagOperation>(rewards.Count);
        foreach (var reward in rewards)
        {
            var definition = _catalogs.Items.GetById(reward.ItemIndex);
            var itemClass = definition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
            var count = (ushort)Math.Clamp(reward.Count, 1, ushort.MaxValue);
            var stack = definition is { IsStackable: true }
                ? _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(held => held.ItemIndex == reward.ItemIndex)
                : null;
            if (stack is not null)
            {
                var total = _stores.Items.AdjustStack(stack.ItemUid, count);
                operations.Add(new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)reward.ItemIndex, BagItemIds.OnWire(stack.ItemUid), total));
            }
            else
            {
                var minted = _itemManager.GrantRewardItem(characterId, reward.ItemIndex, count, itemClassOverride: itemClass);
                operations.Add(new BagOperation(QuestResultFrames.Created, itemClass, (uint)reward.ItemIndex, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot));
            }
        }
        return operations;
    }

    private List<BagOperation> TakeItems(IReadOnlyList<Item> bag, IReadOnlyList<RequiredItem> required)
    {
        var operations = new List<BagOperation>(required.Count);
        foreach (var item in required)
        {
            if (QuestTurnIn.Holding(bag, item) is not { } held) continue;
            var itemClass = _catalogs.Items.GetById(item.ItemId)?.ObjectClass ?? held.ItemClass;
            if (held.Stack > item.Count)
            {
                var left = _stores.Items.AdjustStack(held.ItemUid, -item.Count);
                operations.Add(new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)item.ItemId, BagItemIds.OnWire(held.ItemUid), left));
            }
            else
            {
                _stores.Items.Delete(held.ItemUid);
                operations.Add(new BagOperation(QuestResultFrames.Removed, itemClass, (uint)item.ItemId, BagItemIds.OnWire(held.ItemUid), 0));
            }
        }
        return operations;
    }

    private async Task<int> ZoneRankAsync(CancellationToken ct)
    {
        if (QuestInSlot(0) is not int questId || _catalogs.MonsterQuests.GetById(questId) is not { } quest) return NpcScript.Refused;
        var grades = MonsterQuests.Grades(quest);
        var monsters = grades.Reverse().Select(grade => (grade.Monster, _catalogs.MonsterName?.Invoke(grade.Monster) ?? string.Empty)).ToList();
        await SendAsync(Opcode.S2C_MONSTER_QUEST_RANK_MONSTERS, QuestFrames.ZoneRankMonsters(questId, quest.Name, monsters), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   monster quest {questId} rank window with {grades.Count} monsters");
        return 1;
    }

    private int ItemStatCheck(int column)
    {
        if (_talk is not { } talk || column >= talk.ItemStatIds.Count || CharacterId is not ulong characterId) return NpcScript.Refused;
        if (!_catalogs.ItemStatChecks.TryGetValue(talk.ItemStatIds[column], out var row) || _catalogs.ItemProperties is not { } rules || _catalogs.StoredProperties is not { } stored)
        {
            return NpcScript.Refused;
        }
        var candidates = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
            .Where(item => item.ItemIndex == row.ItemIndex)
            .Take(ItemStatCheckEntry.MaxItems)
            .ToList();
        if (candidates.Count == 0) return ItemStatCheckEntry.ItemMissing;
        foreach (var item in candidates)
        {
            if (_catalogs.Items.GetById(item.ItemIndex) is not { ObjectClass: ItemParamEntry.RareClass } definition) return NpcScript.Refused;
            var properties = stored.ForItem(item.ItemUid);
            var passes = true;
            for (var stat = 0; stat < row.Minimums.Count; stat++)
            {
                if (rules().StatSum(definition, properties, ItemPropertyRules.FirstStatId + stat) < row.Minimums[stat])
                {
                    passes = false;
                    break;
                }
            }
            if (passes) return ItemStatCheckEntry.Passed;
        }
        return ItemStatCheckEntry.TooWeak;
    }

    private IReadOnlyList<(int SkillId, int Level)>? SkillCheckRow(int column) =>
        _talk is { } talk && column < talk.SkillCheckIds.Count && _catalogs.SkillChecks.TryGetValue(talk.SkillCheckIds[column], out var row) ? row : null;

    private async Task<int> RandomMoveAsync(IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (args.Count < NpcCheckRules.RandomMoveZones) return NpcScript.Refused;
        int pick;
        lock (_rng) pick = _rng.Next(NpcCheckRules.RandomMoveZones);
        var zone = args[pick];
        Console.WriteLine($"{_logTag} {_ctx.Remote}   random move to zone {zone}");
        if (TeleportRules.IsZone(zone) && ZoneChangeRequested is { } move)
        {
            await move(new ZoneChangeRequest(zone, tPoseidon.Core.Game.Warp.TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), ct).ConfigureAwait(false);
        }
        return NpcScript.Pending;
    }

    private NpcMoveEntry? MoveRow => _talk is { NpcMoveId: var id } && _catalogs.NpcMoves.TryGetValue(id, out var row) ? row : null;

    private int MoveCheckOf(NpcMoveEntry row, ulong characterId) =>
        _player.Character() is { } character
            ? NpcMoveRules.Check(row, _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory), Level,
                StatGrowth.TypeIndexOf(character.FType), (int)character.Species)
            : NpcMoveRules.ItemsMissing;

    private int MoveCheck()
    {
        if (MoveRow is not { } row || CharacterId is not ulong characterId) return NpcScript.Refused;
        return MoveCheckOf(row, characterId) + 1;
    }

    private async Task<int> MoveCompleteAsync(IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (MoveRow is not { } row || CharacterId is not ulong characterId) return NpcScript.Refused;
        if (MoveCheckOf(row, characterId) != NpcMoveRules.Passed) return NpcScript.Refused;
        var request = new ZoneChangeRequest((ushort)row.Zone, NpcMoveRules.ChangeZoneType, row.DestId, NpcMoveRules.Delay);
        if (row.TakeItem != NpcMoveEntry.TakesItems)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   NPC move {row.Id} to zone {row.Zone} point {row.DestId}");
            if (ZoneChangeRequested is { } move) _ = move(request, ct);
            return 1;
        }
        if (args.Count < 1) return NpcScript.Refused;
        var bag = _stores.Items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var required = NpcMoveRules.Required(row);
        var operations = TakeItems(bag, required);
        await SendAsync(Opcode.S2C_MOVE_ITEMS_TAKEN,
            QuestResultFrames.ItemsTaken(operations, required.Select(item => ((uint)item.ItemId, (ushort)item.Count)).ToList()), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   NPC move {row.Id} to zone {row.Zone} point {row.DestId}, {operations.Count} items taken");
        if (ZoneChangeRequested is { } paid) _ = paid(request, ct);
        if (args[0] >= 1) await SendMessageAsync(args[0], QuestInSlot(0) ?? 0, ct).ConfigureAwait(false);
        Close();
        return NpcScript.Silent;
    }

    private TeleportEntry? TeleportRow => _talk is { TeleportId: var id } && _catalogs.Teleports.TryGetValue(id, out var row) ? row : null;

    private int TeleportCheck(IReadOnlyList<ushort> args)
    {
        if (args.Count < 1) return NpcScript.Refused;
        var result = TeleportRules.Check(TeleportRow, Level, Money, args[0]);
        return result < TeleportRules.Allowed ? result : result + 1;
    }

    private async Task<int> TeleportStartAsync(IReadOnlyList<ushort> args, bool showImage, CancellationToken ct)
    {
        if (args.Count < 1 || TeleportRow is not { } row || _player.Stats() is not { } stats) return NpcScript.Refused;
        var index = args[0];
        if (TeleportRules.Check(row, Level, Money, index) != TeleportRules.Allowed) return NpcScript.Refused;
        var destination = row.Destinations[index];

        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        Close();
        if (destination.Cost != 0)
        {
            stats.GrantMoney(-destination.Cost, StatChangeSource.Teleport);
            await SendAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money), ct).ConfigureAwait(false);
        }
        if (showImage && !string.IsNullOrWhiteSpace(destination.LoadImage))
        {
            await SendAsync(Opcode.S2C_TELEPORT_IMAGE, QuestFrames.Text(destination.LoadImage), ct).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} {_ctx.Remote}   teleport {row.Id} destination {index}: zone {destination.Zone}, cost {destination.Cost}");
        if (TeleportRules.IsZone(destination.Zone) && ZoneChangeRequested is { } teleport)
        {
            await teleport(new ZoneChangeRequest((ushort)destination.Zone, tPoseidon.Core.Game.Warp.TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), ct).ConfigureAwait(false);
        }
        return NpcScript.Silent;
    }

    private async Task GrantMoneyAsync(long gelder, CancellationToken ct)
    {
        if (gelder <= 0 || _player.Stats() is not { } stats) return;
        stats.GrantMoney(gelder, StatChangeSource.Quest);
        await SendAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money), ct).ConfigureAwait(false);
    }

    private void RecordCompletion(ulong characterId, byte type, int questId, int count, int monster, int kills, int clearTimeMs)
    {
        var row = new QuestHistory
        {
            CharacterId = characterId,
            QuestType = type,
            QuestId = questId,
            CompletionCount = (byte)Math.Clamp(count, 1, HistoryCap),
            MonsterId = monster,
            KillCount = (short)Math.Clamp(kills, 0, short.MaxValue),
            RecordTime = clearTimeMs,
            CompletedAt = DateTimeOffset.UtcNow,
        };
        if (_stores.History.Find(characterId, type, questId, monster) is null)
        {
            _stores.History.Insert(row);
        }
        else
        {
            _stores.History.Update(row);
        }
        _stores.History.AppendLog(row);
    }

    private void SaveProgress(ulong characterId, MonsterQuestEntry entry)
    {
        _stores.Quests.UpdateMonsterProgress(
            characterId, entry.QuestId, entry.Monster, (short)Math.Clamp(entry.Kills, 0, short.MaxValue), entry.ClearTimeMs);
        MonsterHuntSnapshot snapshot;
        lock (_huntGate) snapshot = _hunt.Snapshot();
        _stores.Hunts.Save(HuntRecord(characterId, snapshot.QuestId == entry.QuestId ? snapshot : snapshot with { QuestId = entry.QuestId, Active = false }));
    }

    private static MonsterHuntRecord HuntRecord(ulong characterId, MonsterHuntSnapshot snapshot) => new()
    {
        CharacterId = characterId,
        QuestId = snapshot.QuestId,
        Grade = snapshot.Grade,
        Active = snapshot.Active,
        RemainingMs = snapshot.RemainingMs,
        AccumulatorMs = snapshot.AccumulatorMs,
        Kills = snapshot.Kills,
        Target = snapshot.Target,
    };

    private int? LimitOf(MonsterQuestEntry entry)
    {
        if (_catalogs.MonsterQuests.GetById(entry.QuestId) is not { } quest) return null;
        var grades = MonsterQuests.Grades(quest);
        return entry.Grade >= 0 && entry.Grade < grades.Count ? grades[entry.Grade].TimeLimitMs : null;
    }

    private static MonsterQuestListEntry ListEntry(MonsterQuestEntry entry) =>
        new(entry.QuestId, entry.Monster, entry.Count, entry.Grade, entry.Kills, entry.ClearTimeMs);

    private static QuestTexts TextsOf(QuestInfoEntry quest) => new(quest.Name, quest.Client, quest.Pos, quest.Comment);

    private static QuestTexts TextsOf(MonsterQuestInfoEntry quest) => new(quest.Name, quest.Client, quest.Pos, quest.Comment);

    private QuestInfoEntry? QuestOf(int questId, byte type)
    {
        if (questId == 0 || _catalogs.Quests.GetById(questId) is not { } quest) return null;
        return QuestRules.IsScenario(quest) == (type == QuestTypes.Scenario) ? quest : null;
    }

    private bool IsNormalListActive(int questId) =>
        QuestOf(questId, QuestTypes.Normal) is not null
        && (IsActive(QuestTypes.Normal, questId) || IsActive(QuestTypes.TutorialNormal, questId) || IsActive(QuestTypes.JobChange, questId));

    private bool IsActive(byte type, int questId) =>
        questId != 0 && CharacterId is ulong characterId && _stores.Quests.FindActive(characterId, type, questId, monsterId: 0) is not null;

    private int HistoryCount(ulong characterId, byte type, int questId) =>
        _stores.History.Find(characterId, type, questId, monsterId: 0)?.CompletionCount ?? 0;

    private int MonsterHistoryCount(ulong characterId, int questId, int? monster) =>
        _stores.History.GetForCharacter(characterId)
            .Where(row => row.QuestType == QuestTypes.Monster && row.QuestId == questId && (monster is null || row.MonsterId == monster))
            .Select(row => (int)row.CompletionCount)
            .DefaultIfEmpty(0)
            .Max();

    private int? QuestInSlot(int slot)
    {
        if (_talk is null) return null;
        var questId = slot switch
        {
            0 => _talk.QuestId0,
            1 => _talk.QuestId1,
            2 => _talk.QuestId2,
            3 => _talk.QuestId3,
            4 => _talk.QuestId4,
            _ => 0,
        };
        return questId == 0 ? null : questId;
    }

    private int ActiveQuestId(int index) => _talk is null ? 0 : index switch
    {
        0 => _talk.ActiveQuestId0,
        1 => _talk.ActiveQuestId1,
        2 => _talk.ActiveQuestId2,
        _ => 0,
    };

    private static ushort? Arg(IReadOnlyList<ushort> args, int position) =>
        position >= 1 && position <= args.Count && args[position - 1] >= 1 ? args[position - 1] : null;

    private bool IsEngagedWith(ushort entityId) => _talk is not null && _npcEntityId == entityId;

    private ulong? CharacterId => _player.Character()?.CharacterId;

    private int Level => _player.Stats()?.Level ?? _player.Character()?.Level ?? 0;

    private long Money => _player.Stats()?.Money ?? (long)(_player.Character()?.Money ?? 0);

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
