using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.Game.Wedding;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

internal sealed class CoupleMaking
{
    public CoupleMaking(InGameSessionState partner, byte mark)
    {
        Partner = partner;
        Mark = mark;
    }

    public InGameSessionState Partner { get; }
    public byte Mark { get; }
    public byte Ready { get; set; }
    public bool Accepted { get; set; }
}

internal sealed class CoupleController
{
    private const int GroomSayScene = 2;
    private const int BrideSayScene = 3;
    private static readonly TimeSpan LoverMoveDelay = TimeSpan.FromMilliseconds(500);

    private readonly ICoupleRulesStore _store;
    private readonly Func<CoupleTables> _tables;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly SystemServerLink _link;
    private readonly Func<ushort, ushort, InGameSessionState?> _playerOnMap;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly WeddingHallManager _halls = new();
    private readonly Func<ulong, InGameSessionState?> _playerById;
    private readonly Func<ushort, MapKindEntry?> _mapKind;
    private readonly DisguiseController _disguises;
    private readonly List<(OfficerOrder Order, WeddingJoinOrder Join, DateTimeOffset Until)> _pendingJoins = new();
    private readonly object _gate = new();
    private readonly Random _rng = new();

    public CoupleController(
        ICoupleRulesStore store, Func<CoupleTables> tables, IItemStore items, IItemParamLookup catalog, SystemServerLink link,
        Func<ushort, ushort, InGameSessionState?> playerOnMap, Func<ushort, Opcode, byte[], Task> sendToMap, Encoding text, string logTag,
        Func<ulong, InGameSessionState?> playerById, Func<ushort, MapKindEntry?> mapKind, DisguiseController disguises)
    {
        _playerById = playerById ?? throw new ArgumentNullException(nameof(playerById));
        _mapKind = mapKind ?? throw new ArgumentNullException(nameof(mapKind));
        _disguises = disguises ?? throw new ArgumentNullException(nameof(disguises));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _playerOnMap = playerOnMap ?? throw new ArgumentNullException(nameof(playerOnMap));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
    }

    public async Task<int> NpcFunctionAsync(InGameSessionState st, int functionId, Func<int, bool> learnSkill)
    {
        ArgumentNullException.ThrowIfNull(st);
        ArgumentNullException.ThrowIfNull(learnSkill);
        if (st.Character is not { } character) return NpcScript.Refused;
        var couple = _store.LoadForCharacter(character.CharacterId);
        var state = CoupleRules.StateOf(couple);
        var partner = couple?.Partner(character.CharacterId);
        switch (functionId)
        {
            case CoupleRules.MakeCouple:
                if (!_link.IsConnected) return 1;
                if (!CoupleRules.CanMakeCouple(state, LevelOf(st))) return 1;
                await SendAsync(st, Opcode.C2S_TRIGGER_75).ConfigureAwait(false);
                st.CoupleSelecting = true;
                await SendAsync(st, Opcode.S2C_COUPLE_SELECT_LOVER).ConfigureAwait(false);
                Log(st, "opens the lover selection");
                return NpcScript.Silent;
            case CoupleRules.BrokenCouple:
                if (partner is null) return 1;
                await SendAsync(st, Opcode.C2S_TRIGGER_75).ConfigureAwait(false);
                await SendAsync(st, Opcode.S2C_COUPLE_BREAK_ASK, CoupleFrames.PartnerName(partner.Name, _text)).ConfigureAwait(false);
                return NpcScript.Silent;
            case CoupleRules.CheckMarriageState:
                return CoupleRules.MarriageStateResult(state);
            case CoupleRules.ExchangeMarriage:
                await SendAsync(st, Opcode.C2S_TRIGGER_75).ConfigureAwait(false);
                st.MarriageExchange = true;
                var points = couple is null ? default : CouplePoints.Of(couple);
                var tables = _tables();
                var rows = CoupleRules.ExchangeList(tables, points, itemId => HeldCount(character.CharacterId, itemId));
                var titles = CoupleRules.TitleList(tables, points);
                await SendAsync(st, Opcode.S2C_MARRIAGE_EXCHANGE_LIST, CoupleFrames.ExchangeList(rows, titles, _text)).ConfigureAwait(false);
                Log(st, $"opens the marriage exchange: {rows.Count} items, {titles.Count} titles");
                return NpcScript.Silent;
            case CoupleRules.OpenMarriageRoom:
                await SendAsync(st, Opcode.S2C_WEDDING_HALL_WINDOW).ConfigureAwait(false);
                return NpcScript.Silent;
            case CoupleRules.OpenMarriageBoard:
                await SendAsync(st, Opcode.S2C_WEDDING_BOARD_WINDOW).ConfigureAwait(false);
                return NpcScript.Silent;
            case CoupleRules.BrokenMarriage:
                if (partner is null) return 1;
                await SendAsync(st, Opcode.S2C_DIVORCE_ASK, CoupleFrames.PartnerName(partner.Name, _text)).ConfigureAwait(false);
                return NpcScript.Silent;
            case CoupleRules.CheckCoupleSkill:
                if (couple is null || CoupleRules.CoupleSkillOf(_tables(), couple.RingItemIndex, CoupleRules.IsFemale(character.Species)) is not { } skill)
                {
                    return NpcScript.Refused;
                }
                if (st.Skills.ContainsKey(skill)) return CoupleRules.SkillAlreadyKnown;
                if (!learnSkill(skill)) return NpcScript.Refused;
                _store.RecordCoupleSkill(couple.CoupleCode, character.CharacterId, CoupleRules.IsFemale(character.Species), skill, DateTimeOffset.UtcNow);
                Log(st, $"learns couple skill {skill} from ring {couple.RingItemIndex}");
                await SendAsync(st, Opcode.S2C_COUPLE_SKILL_LEARNED, CoupleFrames.SkillLearned((ushort)couple.RingItemIndex)).ConfigureAwait(false);
                return NpcScript.Silent;
            case CoupleRules.CheckMarriageOption:
                if (CoupleRules.MarriageOptionResult(state) is { } optionResult) return optionResult;
                Log(st, $"marriage option check in couple state {state}: dialog closed");
                await SendAsync(st, Opcode.C2S_TRIGGER_75).ConfigureAwait(false);
                return NpcScript.Silent;
            default:
                return NpcScript.Refused;
        }
    }

    public async Task ProposeAsync(InGameSessionState st, ReadOnlyMemory<byte> payload)
    {
        if (st.Character is not { } character || !CoupleFrames.TryReadTarget(payload.Span, out var targetId)) return;
        if (!st.CoupleSelecting)
        {
            Log(st, $"proposes to 0x{targetId:X4} outside the lover selection: dropped");
            return;
        }
        var target = _playerOnMap(st.MapId, targetId);
        if (target is null)
        {
            Log(st, $"proposes to 0x{targetId:X4}: no such player");
            await EndMakingAsync(st, CoupleRules.EndTargetMissing).ConfigureAwait(false);
            return;
        }
        if (target.Character is not { } targetCharacter || ReferenceEquals(target, st))
        {
            Log(st, $"proposes to 0x{targetId:X4}: no such player");
            return;
        }
        if (target.Trade is not null || target.IsDead)
        {
            Log(st, $"proposes to 0x{targetId:X4}: the target is busy");
            st.CoupleSelecting = false;
            await SendAsync(st, Opcode.S2C_COUPLE_TARGET_UNAVAILABLE).ConfigureAwait(false);
            return;
        }
        var pending = false;
        lock (_gate)
        {
            if (st.CoupleMaking is not null || target.CoupleMaking is not null) pending = true;
            else
            {
                    st.CoupleMaking = new CoupleMaking(target, CoupleRules.ProposerMark);
                target.CoupleMaking = new CoupleMaking(st, CoupleRules.TargetMark);
            }
        }
        if (pending)
        {
            Log(st, $"proposes to 0x{targetId:X4}: a proposal is pending");
            await SendAsync(st, Opcode.S2C_COUPLE_REQUEST_PENDING).ConfigureAwait(false);
            return;
        }
        target.CoupleSelecting = true;
        await SendAsync(st, Opcode.S2C_COUPLE_PROPOSED, CoupleFrames.Proposed(CoupleRules.ProposerRole, targetCharacter.Name, _text)).ConfigureAwait(false);
        await SendAsync(target, Opcode.S2C_COUPLE_PROPOSED, CoupleFrames.Proposed(CoupleRules.TargetRole, character.Name, _text)).ConfigureAwait(false);
        Log(st, $"proposes to 0x{targetId:X4} '{targetCharacter.Name}'");
    }

    public async Task AcceptAsync(InGameSessionState st)
    {
        if (st.Character is null || st.CoupleMaking is not { Mark: CoupleRules.TargetMark, Accepted: false } making) return;
        var proposer = making.Partner;
        if (proposer.CoupleMaking is not { } proposerMaking || !ReferenceEquals(proposerMaking.Partner, st) || proposer.Character is null)
        {
            await EndMakingAsync(st, CoupleRules.EndProposerMissing).ConfigureAwait(false);
            return;
        }
        var fits = new[] { st, proposer }.All(side =>
            side.Character is { } sideCharacter
            && CoupleRules.CanMakeCouple(CoupleRules.StateOf(_store.LoadForCharacter(sideCharacter.CharacterId)), LevelOf(side)));
        if (!fits)
        {
            Log(st, "accepts, but a side already has a couple or is below the level: ended");
            await EndMakingAsync(st, CoupleRules.EndConditionFailed).ConfigureAwait(false);
            return;
        }
        making.Accepted = true;
        proposerMaking.Accepted = true;
        await SendStateAsync(st, CoupleRules.AcceptedFlagForAcceptor).ConfigureAwait(false);
        await SendStateAsync(proposer, CoupleRules.AcceptedFlagForProposer).ConfigureAwait(false);
        Log(st, $"accepts the proposal of 0x{proposer.PlayerEntityId:X4}");
    }

    public async Task RefuseAsync(InGameSessionState st)
    {
        if (st.CoupleMaking is not null) Log(st, "ends the couple making");
        await EndMakingAsync(st, CoupleRules.EndRefused).ConfigureAwait(false);
    }

    public async Task ReadyAsync(InGameSessionState st, ReadOnlyMemory<byte> payload)
    {
        if (st.Character is not { } character || !CoupleFrames.TryReadReady(payload.Span, out var ready)) return;
        if (st.CoupleMaking is not { Accepted: true } making || making.Partner.CoupleMaking is not { } partnerMaking
            || making.Partner.Character is not { } partnerCharacter)
        {
            return;
        }
        if (partnerCharacter.OwnerAccountId == character.OwnerAccountId)
        {
            Log(st, "ready with a character of the same account: ended (Player Same)");
            await EndMakingAsync(st, CoupleRules.EndRefused).ConfigureAwait(false);
            return;
        }
        making.Ready = ready;
        await SendStateAsync(st, partnerMaking.Mark).ConfigureAwait(false);
        await SendStateAsync(making.Partner, making.Mark).ConfigureAwait(false);
        if (making.Ready == 0 || partnerMaking.Ready == 0) return;
        var (male, female) = CoupleRules.Order(
            character.CharacterId, CoupleRules.IsFemale(character.Species),
            partnerCharacter.CharacterId, CoupleRules.IsFemale(partnerCharacter.Species));
        await _link.SendAsync(CoupleSocialFrames.LinkMakeCouple, CoupleSocialFrames.MakeCouple(male, female), default).ConfigureAwait(false);
        Log(st, $"MakeCouple Success: male 0x{male:X}, female 0x{female:X} sent to the SystemServer");
        var partner = making.Partner;
        End(st);
        st.CoupleSelecting = false;
        partner.CoupleSelecting = false;
    }

    public Task LeftAsync(InGameSessionState st)
    {
        ArgumentNullException.ThrowIfNull(st);
        End(st);
        st.CoupleSelecting = false;
        st.MarriageExchange = false;
        return Task.CompletedTask;
    }

    public async Task OptionAsync(InGameSessionState st, ReadOnlyMemory<byte> payload)
    {
        if (st.Character is not { } character || st.Stats is not { } stats) return;
        if (!CoupleFrames.TryReadOption(payload.Span, out var request))
        {
            Log(st, $"marriage option request of {payload.Length} bytes not read");
            return;
        }
        if (!st.MarriageExchange)
        {
            Log(st, "marriage option outside the exchange window: dropped");
            return;
        }
        st.MarriageExchange = false;
        if (_store.LoadForCharacter(character.CharacterId) is not { } couple)
        {
            Log(st, "marriage option without a couple");
            await SendAsync(st, Opcode.S2C_MARRIAGE_OPTION_REFUSED, CoupleFrames.OptionRefused(CoupleRules.RefusalCode(MarriageOptionRefusal.NotCloseEnough))).ConfigureAwait(false);
            return;
        }
        var (refusal, choice) = CoupleRules.Choose(
            _tables(), couple.State, CouplePoints.Of(couple), request, itemId => HeldCount(character.CharacterId, itemId), stats.Money);
        if (choice is null)
        {
            Log(st, $"marriage option ring {request.RingItemId} dress {request.DressItemId} title {request.TitleIndex} refused: {refusal}");
            await SendAsync(st, Opcode.S2C_MARRIAGE_OPTION_REFUSED, CoupleFrames.OptionRefused(CoupleRules.RefusalCode(refusal))).ConfigureAwait(false);
            return;
        }
        var dressItemId = choice.Dress?.ItemId ?? 0;
        var ringCoupon = choice.RingCoupon is { } ringTake ? Take(character.CharacterId, ringTake.ItemId, ringTake.Count) : [];
        var dressCoupon = choice.DressCoupon is { } dressTake ? Take(character.CharacterId, dressTake.ItemId, dressTake.Count) : [];
        if (choice.Fee > 0) stats.GrantMoney(-choice.Fee, tPoseidon.Core.Game.Stats.StatChangeSource.Marriage);
        if (!_store.SetMarriageOptions(couple.CoupleCode, choice.Ring.ItemId, dressItemId, (short)choice.Title.Index, DateTimeOffset.UtcNow))
        {
            Log(st, $"marriage options of couple {couple.CoupleCode} not stored");
            return;
        }
        await SendAsync(st, Opcode.S2C_MARRIAGE_OPTION_DONE, CoupleFrames.OptionDone(FirstOperation(ringCoupon), FirstOperation(dressCoupon))).ConfigureAwait(false);
        var otherStacks = ringCoupon.Skip(1).Concat(dressCoupon.Skip(1)).ToList();
        if (otherStacks.Count > 0) await SendAsync(st, Opcode.S2C_ITEM_ADDED, QuestResultFrames.ItemOperations(otherStacks)).ConfigureAwait(false);
        await _link.SendAsync(CoupleSocialFrames.LinkMarriageOption,
            CoupleSocialFrames.MarriageOption(couple.CoupleCode, (ushort)choice.Ring.ItemId, (ushort)dressItemId, (byte)choice.Title.Index), default).ConfigureAwait(false);
        Log(st, $"couple {couple.CoupleCode} options: ring {choice.Ring.ItemId}, dress {dressItemId}, title {choice.Title.Index}, fee {choice.Fee} (coupon 0x{request.CouponWireUid:X})");
    }

    public async Task EnteredZoneAsync(InGameSessionState st)
    {
        if (st.Character is not { } character) return;
        var tables = _tables();
        if (!tables.HallZones.Contains(st.MapId))
        {
            await _disguises.RemoveDressAsync(st).ConfigureAwait(false);
            return;
        }
        if (_halls.Find(st.MapId) is not { } hall) return;
        await JoinHallAsync(st, hall).ConfigureAwait(false);
    }

    public async Task TickAsync(DateTimeOffset now)
    {
        foreach (var hall in _halls.Halls)
        {
            await DispatchAsync(hall, hall.Ceremony.Tick(now)).ConfigureAwait(false);
        }
        List<(OfficerOrder Order, WeddingJoinOrder Join, DateTimeOffset Until)> due;
        lock (_gate)
        {
            due = _pendingJoins.ToList();
            _pendingJoins.Clear();
        }
        foreach (var pending in due)
        {
            if (_playerById(pending.Join.MasterId) is { } master)
            {
                await OpenHallAsync(pending.Order, pending.Join, master).ConfigureAwait(false);
            }
            else if (now >= pending.Until)
            {
                Console.WriteLine($"{_logTag} [couple] hall {pending.Join.Wdid}: master 0x{pending.Join.MasterId:X} did not arrive");
                await ReportEnterAsync(pending.Order, WeddingEnterResult.RequesterGone, 0, pending.Join.PreviousZone).ConfigureAwait(false);
            }
            else
            {
                lock (_gate) _pendingJoins.Add(pending);
            }
        }
    }

    private async Task OrderAsync(OfficerOrder order, CancellationToken ct)
    {
        switch (order.OrderType)
        {
            case WeddingHallFrames.OrderWarpMaster when WeddingHallFrames.TryReadWarpMaster(order.Data, out var mapId):
                if (_playerById(order.CharacterId) is not { } master) return;
                if (!_tables().HallZones.Contains((int)mapId))
                {
                    Log(master, $"wedding warp to map {mapId} refused: not a wedding hall map");
                    return;
                }
                Log(master, $"goes to the wedding hall map {mapId}");
                await WarpAsync(master, (ushort)mapId).ConfigureAwait(false);
                return;
            case WeddingHallFrames.OrderJoinMember when WeddingHallFrames.ReadJoinMember(order.Data, _text) is { } join:
                await JoinMemberAsync(order, join).ConfigureAwait(false);
                return;
            case WeddingHallFrames.OrderWarpMember when WeddingHallFrames.ReadWarpMember(order.Data, _text) is { } warp:
                foreach (var memberId in warp.Members)
                {
                    if (_playerById(memberId) is { } guest) await WarpAsync(guest, (ushort)warp.Zone).ConfigureAwait(false);
                }
                Console.WriteLine($"{_logTag} [couple] {warp.Members.Count} hall members called to zone {warp.Zone}");
                return;
            case WeddingHallFrames.OrderDress when WeddingHallFrames.ReadDress(order.Data) is { } dress:
                await DressAsync(dress).ConfigureAwait(false);
                return;
            case CoupleSocialFrames.OrderLoverPosition when CoupleSocialFrames.TryReadLoverPositionOrder(order.Data, out var loverId, out var requesterId):
                var lover = _playerById(loverId);
                var loverMapRefuses = lover is not null && _mapKind(lover.MapId) is { AllowsLoverMove: false };
                var spot = lover is null || loverMapRefuses
                    ? CoupleSocialFrames.LoverSpot(requesterId, 0, 0, 0)
                    : CoupleSocialFrames.LoverSpot(requesterId, lover.MapId, (short)lover.PlayerX, (short)lover.PlayerY);
                Console.WriteLine($"{_logTag} [couple] lover 0x{loverId:X} position for 0x{requesterId:X}: {(lover is null ? "not on this server" : loverMapRefuses ? $"zone {lover.MapId} refuses the move" : $"zone {lover.MapId} ({lover.PlayerX},{lover.PlayerY})")}");
                await _link.ReportAsync(order with { Data = spot }, ct).ConfigureAwait(false);
                return;
            case CoupleSocialFrames.OrderMoveToLover when CoupleSocialFrames.TryReadLoverSpot(order.Data, out var movedId, out var loverZone, out var loverX, out var loverY):
                if (_playerById(movedId) is not { } moved) return;
                if (loverZone == 0 || moved.RequestZoneChange is not { } toLover)
                {
                    Log(moved, "move to the lover cancelled: the lover was not found");
                    await SendAsync(moved, Opcode.S2C_LOVER_MOVE_CANCELLED).ConfigureAwait(false);
                    return;
                }
                Log(moved, $"moves to the lover: zone {loverZone} ({loverX},{loverY})");
                await toLover(new ZoneChangeRequest((ushort)loverZone, TeleportArrival.ChangeZoneType, null, LoverMoveDelay, ((ushort)loverX, (ushort)loverY)), ct).ConfigureAwait(false);
                return;
            case WeddingHallFrames.OrderWarpMaster or WeddingHallFrames.OrderJoinMember or WeddingHallFrames.OrderWarpMember or WeddingHallFrames.OrderDress:
                Console.WriteLine($"{_logTag} [couple] ORDER 0x{order.OrderType:X2} of {order.Data.Length} B not read");
                return;
        }
    }

    private async Task JoinMemberAsync(OfficerOrder order, WeddingJoinOrder join)
    {
        if (!_tables().HallZones.Contains((int)join.MapId))
        {
            await ReportEnterAsync(order, WeddingEnterResult.NotWeddingZone, 0, join.PreviousZone).ConfigureAwait(false);
            return;
        }
        if (join.Members.Count > WeddingHallFrames.MaxJoinOrderMembers)
        {
            await ReportEnterAsync(order, WeddingEnterResult.TooManyMembers, 0, join.PreviousZone).ConfigureAwait(false);
            return;
        }
        if (_playerById(join.MasterId) is { } master)
        {
            await OpenHallAsync(order, join, master).ConfigureAwait(false);
            return;
        }
        lock (_gate) _pendingJoins.Add((order, join, DateTimeOffset.UtcNow.AddSeconds(WeddingHallFrames.MasterArrivalSeconds)));
    }

    private async Task OpenHallAsync(OfficerOrder order, WeddingJoinOrder join, InGameSessionState master)
    {
        var zone = (ushort)join.MapId;
        var couple = _store.Load(join.Wdid);
        var groom = couple?.Members.FirstOrDefault(member => !member.Female);
        var bride = couple?.Members.FirstOrDefault(member => member.Female);
        WeddingHall? hall = null;
        if (groom is not null && bride is not null)
        {
            var lengths = _tables().OfficiateLines.Select(line => _text.GetByteCount(line.Text)).ToArray();
            hall = new WeddingHall(
                zone, join.Wdid, join.MasterId,
                new WeddingPartner(groom.CharacterId, groom.Name),
                new WeddingPartner(bride.CharacterId, bride.Name),
                join.Members,
                new WeddingCeremony(lengths));
        }
        if (hall is null || !_halls.TryOpen(hall))
        {
            Log(master, $"wedding hall {join.Wdid} on map {zone} not created ({(hall is null ? "couple not found" : "map busy")})");
            await ReportEnterAsync(order, WeddingEnterResult.CreateFailed, 0, join.PreviousZone).ConfigureAwait(false);
            await WarpAsync(master, WeddingHallFrames.WarpMemberZone).ConfigureAwait(false);
            return;
        }
        await JoinHallAsync(master, hall).ConfigureAwait(false);
        await ReportEnterAsync(order, WeddingEnterResult.Entered, zone, join.PreviousZone).ConfigureAwait(false);
        Log(master, $"opens wedding hall {join.Wdid} on map {zone} for {join.Members.Count} members");
    }

    private async Task JoinHallAsync(InGameSessionState st, WeddingHall hall)
    {
        if (st.Character is not { } character || !hall.Join(character.CharacterId)) return;
        await SendAsync(st, Opcode.S2C_WEDDING_JOINED, CoupleFrames.Joined(hall.IsMember(character.CharacterId))).ConfigureAwait(false);
        Log(st, $"joins the wedding hall of couple {hall.CoupleCode} in zone {hall.Zone}");
        await DispatchAsync(hall, hall.Ceremony.Open(DateTimeOffset.UtcNow)).ConfigureAwait(false);
    }

    private Task ReportEnterAsync(OfficerOrder order, uint result, uint zone, uint previousZone) =>
        _link.ReportAsync(order with
        {
            OrderType = WeddingHallFrames.ReportEnterMember,
            Data = WeddingHallFrames.EnterReport(new WeddingEnterReport(result, zone, previousZone, string.Empty), _text),
        }, default);

    private async Task DressAsync(WeddingDressOrder order)
    {
        if (_playerById(order.CharacterId) is not { } player) return;
        if (order.On) await WearDressAsync(player, order.DressItem).ConfigureAwait(false);
        else await _disguises.RemoveDressAsync(player).ConfigureAwait(false);
    }

    private Task WearDressAsync(InGameSessionState player, ushort dressItem)
    {
        var female = DisguiseRules.IsFemale(player.PlayerCharacterType);
        var index = _tables().Dresses.TryGetValue(dressItem, out var row) ? (female ? row.DressFemale : row.DressMale) : 0;
        return _disguises.WearDressAsync(player, DisguiseRules.DressType(index, player.PlayerCharacterType));
    }

    private static Task WarpAsync(InGameSessionState st, ushort zone)
    {
        if (st.MapId == zone || st.RequestZoneChange is not { } requestZoneChange) return Task.CompletedTask;
        return requestZoneChange(new ZoneChangeRequest(zone, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), default);
    }

    private async Task DispatchAsync(WeddingHall hall, IReadOnlyList<WeddingEvent> events)
    {
        foreach (var weddingEvent in events)
        {
            switch (weddingEvent.Kind)
            {
                case WeddingEventKind.GatheringTimer:
                    await _sendToMap(hall.Zone, Opcode.S2C_WEDDING_TIMER, CoupleFrames.Stage(weddingEvent.Value)).ConfigureAwait(false);
                    break;
                case WeddingEventKind.Scene:
                    if (!WeddingScene.IsSent(weddingEvent.Value)) break;
                    var lines = _tables().OfficiateLines;
                    await _sendToMap(hall.Zone, Opcode.S2C_WEDDING_SCENE, CoupleFrames.Scene(
                        weddingEvent.Value, hall.Groom.Name, hall.Bride.Name,
                        lines.FirstOrDefault(line => line.Scene == GroomSayScene)?.Text ?? string.Empty,
                        lines.FirstOrDefault(line => line.Scene == BrideSayScene)?.Text ?? string.Empty,
                        _text)).ConfigureAwait(false);
                    break;
                case WeddingEventKind.CeremonyEnded:
                    foreach (var spouse in new[] { hall.Groom.CharacterId, hall.Bride.CharacterId })
                    {
                        if (_playerById(spouse) is { } ceremonyMember) await SendAsync(ceremonyMember, Opcode.S2C_WEDDING_CONTROL_CLOSED).ConfigureAwait(false);
                    }
                    await _link.SendAsync(WeddingHallFrames.LinkEndWedding, WeddingHallFrames.EndWedding(new WeddingEnded(hall.CoupleCode, hall.Guests)), default).ConfigureAwait(false);
                    Console.WriteLine($"{_logTag} [couple] wedding of couple {hall.CoupleCode} ended with {hall.Guests.Count} guests (0x{WeddingHallFrames.LinkEndWedding:X4} to the SystemServer)");
                    break;
                case WeddingEventKind.ClosingTimer:
                    await _sendToMap(hall.Zone, Opcode.S2C_WEDDING_CLOSING_TIMER, CoupleFrames.Stage(weddingEvent.Value)).ConfigureAwait(false);
                    break;
                case WeddingEventKind.HallClosed:
                    _halls.Close(hall);
                    Console.WriteLine($"{_logTag} [couple] wedding hall of couple {hall.CoupleCode} in zone {hall.Zone} closed");
                    break;
            }
        }
    }

    private async Task SendStateAsync(InGameSessionState recipient, byte flag)
    {
        if (recipient.CoupleMaking is not { } making || making.Partner.Character is not { } partnerCharacter) return;
        var partnerReady = making.Partner.CoupleMaking?.Ready ?? 0;
        await SendAsync(recipient, Opcode.S2C_COUPLE_MAKE_STATE, CoupleFrames.MakeState(
            flag, making.Ready, partnerCharacter.Name, (ushort)making.Partner.PlayerCharacterType, partnerReady, _text)).ConfigureAwait(false);
    }

    private async Task EndMakingAsync(InGameSessionState st, uint code)
    {
        var partner = st.CoupleMaking?.Partner;
        var notify = new List<InGameSessionState>();
        if (st.CoupleSelecting) notify.Add(st);
        if (partner is not null && partner.CoupleSelecting && partner.CoupleMaking is { } other && ReferenceEquals(other.Partner, st)) notify.Add(partner);
        End(st);
        st.CoupleSelecting = false;
        foreach (var side in notify)
        {
            side.CoupleSelecting = false;
            await SendAsync(side, Opcode.S2C_COUPLE_MAKE_ENDED, CoupleFrames.MakeEnded(code)).ConfigureAwait(false);
        }
    }

    private void End(InGameSessionState st)
    {
        lock (_gate)
        {
            if (st.CoupleMaking is not { } making) return;
            st.CoupleMaking = null;
            if (making.Partner.CoupleMaking is { } other && ReferenceEquals(other.Partner, st))
            {
                making.Partner.CoupleMaking = null;
                making.Partner.CoupleSelecting = false;
            }
        }
    }

    private static int LevelOf(InGameSessionState st) => st.Stats?.Level ?? st.Character?.Level ?? 0;

    private int HeldCount(ulong characterId, int itemId) =>
        _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).Where(item => item.ItemIndex == itemId).Sum(item => item.Stack);

    private static BagOperation? FirstOperation(List<BagOperation> operations) => operations.Count > 0 ? operations[0] : null;

    private List<BagOperation> Take(ulong characterId, int itemId, int count)
    {
        var operations = new List<BagOperation>();
        var left = count;
        foreach (var held in _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).Where(item => item.ItemIndex == itemId).ToArray())
        {
            if (left <= 0) break;
            var itemClass = _catalog.GetById(itemId)?.ObjectClass ?? held.ItemClass;
            if (held.Stack > left)
            {
                var remaining = _items.AdjustStack(held.ItemUid, -left);
                operations.Add(new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(held.ItemUid), remaining));
                left = 0;
            }
            else
            {
                _items.Delete(held.ItemUid);
                operations.Add(new BagOperation(QuestResultFrames.Removed, itemClass, (uint)itemId, BagItemIds.OnWire(held.ItemUid), 0));
                left -= held.Stack;
            }
        }
        return operations;
    }

    private void Log(InGameSessionState st, string what) =>
        Console.WriteLine($"{_logTag} [couple] 0x{st.PlayerEntityId:X4} {what}");

    private Task SendAsync(InGameSessionState player, Opcode opcode) => SendAsync(player, opcode, Array.Empty<byte>());

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
