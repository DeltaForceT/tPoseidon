using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed record WeddingOrder(ulong? CharacterId, ushort? Zone, ushort OrderType, byte[] Data);

public sealed class WeddingOutbox
{
    public List<CoupleMessage> Messages { get; } = new();
    public List<WeddingOrder> Orders { get; } = new();
    public List<string> Notes { get; } = new();
}

public sealed class WeddingHallService
{
    private sealed class Hall
    {
        public required ulong Wdid { get; init; }
        public required ulong MasterId { get; init; }
        public required uint MapId { get; init; }
        public required string Title { get; init; }
        public required string Password { get; init; }
        public bool Started { get; set; }
        public string Note { get; set; } = string.Empty;
        public List<WeddingHallMember> Members { get; } = new();

        public bool Has(ulong characterId) => Members.Any(member => member.CharacterId == characterId);
    }

    private readonly object _gate = new();
    private readonly Dictionary<ulong, Hall> _halls = new();
    private readonly Dictionary<ulong, ulong> _hallOf = new();
    private readonly Dictionary<ulong, ushort> _lastZone = new();
    private readonly ICoupleRulesStore _store;
    private readonly Func<ulong, bool> _isOpen;
    private readonly Func<ulong, IReadOnlyList<CoupleMessage>> _refresh;
    private readonly Encoding _text;
    private readonly TimeProvider _time;
    private readonly bool _weddingTest;

    public WeddingHallService(
        ICoupleRulesStore store, Func<ulong, bool> isOpen, Func<ulong, IReadOnlyList<CoupleMessage>> refresh, Encoding text, TimeProvider time, bool weddingTest)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _isOpen = isOpen ?? throw new ArgumentNullException(nameof(isOpen));
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _weddingTest = weddingTest;
    }

    public IReadOnlyList<WeddingHallListing> Listings()
    {
        lock (_gate)
        {
            return _halls.Values
                .Select(hall => new WeddingHallListing(hall.Wdid, hall.MasterId, hall.Title, hall.Password.Length > 0, hall.MapId, hall.Started))
                .ToArray();
        }
    }

    public WeddingOutbox Make(ulong characterId, string name, string title, string password, uint mapId)
    {
        var outbox = new WeddingOutbox();
        if (!_isOpen(characterId)) return Fail(outbox, characterId, WeddingHallError.ClientMissing, "no couple session");
        if (_store.LoadForCharacter(characterId) is not { } couple) return Fail(outbox, characterId, WeddingHallError.CoupleMissing, "no couple");
        if (!_weddingTest && couple.State != CoupleStates.OptionsChosen)
        {
            return Fail(outbox, characterId, WeddingHallError.InvalidState, $"couple {couple.CoupleCode} in state {couple.State}");
        }
        lock (_gate)
        {
            if (_hallOf.ContainsKey(characterId)) return Fail(outbox, characterId, WeddingHallError.AlreadyInHall, "already in a hall");
            if (_halls.ContainsKey(couple.CoupleCode)) return Fail(outbox, characterId, WeddingHallError.HallExists, $"hall {couple.CoupleCode} exists");
            var hall = new Hall { Wdid = couple.CoupleCode, MasterId = characterId, MapId = mapId, Title = title, Password = password };
            _halls[hall.Wdid] = hall;
            Enter(outbox, hall, new WeddingHallMember(characterId, name), couple);
            outbox.Messages.Add(new CoupleMessage(characterId, WeddingHallFrames.HallMade, WeddingHallFrames.Hall(hall.Wdid)));
            outbox.Notes.Add($"hall {hall.Wdid} made on map {mapId}");
        }
        return outbox;
    }

    public WeddingOutbox Join(ulong characterId, string name, ulong wdid, string password)
    {
        var outbox = new WeddingOutbox();
        if (!_isOpen(characterId)) return Fail(outbox, characterId, WeddingHallError.ClientMissing, "no couple session");
        lock (_gate)
        {
            if (!_halls.TryGetValue(wdid, out var hall)) return Fail(outbox, characterId, WeddingHallError.JoinHallMissing, $"hall {wdid} missing");
            if (_hallOf.ContainsKey(characterId)) return Fail(outbox, characterId, WeddingHallError.AlreadyInHall, "already in a hall");
            if (!string.Equals(hall.Password, password, StringComparison.Ordinal)) return Fail(outbox, characterId, WeddingHallError.WrongPassword, "wrong password");
            if (hall.Has(characterId)) return Fail(outbox, characterId, WeddingHallError.AlreadyInHall, "already a member");
            if (hall.Members.Count >= WeddingHallFrames.MaxMembers) return Fail(outbox, characterId, WeddingHallError.Full, "hall full");
            if (hall.Started) return Fail(outbox, characterId, WeddingHallError.Started, "ceremony started");
            Enter(outbox, hall, new WeddingHallMember(characterId, name), _store.Load(hall.Wdid));
            outbox.Messages.Add(new CoupleMessage(characterId, WeddingHallFrames.HallJoined, DetailOf(hall, characterId)));
            outbox.Notes.Add($"joined hall {wdid}");
        }
        return outbox;
    }

    public WeddingOutbox Exit(ulong characterId, ulong wdid)
    {
        var outbox = new WeddingOutbox();
        if (!_isOpen(characterId)) return Fail(outbox, characterId, WeddingHallError.ClientMissing, "no couple session");
        lock (_gate)
        {
            if (!_halls.TryGetValue(wdid, out var hall)) return Fail(outbox, characterId, WeddingHallError.HallMissing, $"hall {wdid} missing");
            if (!hall.Has(characterId)) return Fail(outbox, characterId, WeddingHallError.NotMember, "not a member");
            Broadcast(outbox, hall, WeddingHallFrames.MemberExited, WeddingHallFrames.Character(characterId));
            var couple = _store.Load(hall.Wdid);
            if (IsPartner(couple, characterId))
            {
                if (!hall.Started) DressOrders(outbox, couple!, false);
                Close(hall);
                outbox.Notes.Add($"partner left: hall {wdid} expired");
            }
            else
            {
                hall.Members.RemoveAll(member => member.CharacterId == characterId);
                _hallOf.Remove(characterId);
                if (hall.Members.Count == 0) Close(hall);
                outbox.Notes.Add($"left hall {wdid}");
            }
            outbox.Messages.Add(new CoupleMessage(characterId, WeddingHallFrames.HallExited, Array.Empty<byte>()));
        }
        return outbox;
    }

    public WeddingOutbox Start(ulong characterId, ulong wdid)
    {
        var outbox = new WeddingOutbox();
        if (!_isOpen(characterId)) return Fail(outbox, characterId, WeddingHallError.ClientMissing, "no couple session");
        lock (_gate)
        {
            if (!_halls.TryGetValue(wdid, out var hall)) return Fail(outbox, characterId, WeddingHallError.HallMissing, $"hall {wdid} missing");
            if (hall.MasterId != characterId) return Fail(outbox, characterId, WeddingHallError.NotMaster, "not the hall master");
            if (_store.LoadForCharacter(characterId)?.Partner(characterId) is not { } lover) return Fail(outbox, characterId, WeddingHallError.CoupleMissing, "no couple");
            if (!hall.Has(lover.CharacterId)) return Fail(outbox, characterId, WeddingHallError.LoverAbsent, "lover not in the hall");
            if (hall.Started)
            {
                outbox.Notes.Add($"hall {wdid} already started");
                return outbox;
            }
            hall.Started = true;
            outbox.Orders.Add(new WeddingOrder(characterId, null, WeddingHallFrames.OrderWarpMaster, WeddingHallFrames.WarpMaster(hall.MapId)));
            outbox.Notes.Add($"hall {wdid} started: master to map {hall.MapId}");
        }
        return outbox;
    }

    public WeddingOutbox Kick(ulong characterId, ulong wdid, IReadOnlyList<ulong> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var outbox = new WeddingOutbox();
        if (_store.LoadForCharacter(characterId) is not { } couple) return Fail(outbox, characterId, WeddingHallError.CoupleMissing, "no couple");
        lock (_gate)
        {
            if (!_halls.TryGetValue(wdid, out var hall)) return Fail(outbox, characterId, WeddingHallError.HallMissing, $"hall {wdid} missing");
            if (hall.MasterId != characterId) return Fail(outbox, characterId, WeddingHallError.NotMaster, "not the hall master");
            var lover = couple.Partner(characterId)?.CharacterId;
            foreach (var target in targets)
            {
                if (target == characterId) return Fail(outbox, characterId, WeddingHallError.KickMaster, "cannot kick the master");
                if (target == lover) return Fail(outbox, characterId, WeddingHallError.KickLover, "cannot kick the lover");
                if (!hall.Has(target)) return Fail(outbox, characterId, WeddingHallError.NotMember, $"0x{target:X} not a member");
                Broadcast(outbox, hall, WeddingHallFrames.MemberKicked, WeddingHallFrames.Character(target));
                hall.Members.RemoveAll(member => member.CharacterId == target);
                _hallOf.Remove(target);
            }
            outbox.Messages.Add(new CoupleMessage(characterId, WeddingHallFrames.Kicked, Array.Empty<byte>()));
            outbox.Notes.Add($"kicked {targets.Count} from hall {wdid}");
        }
        return outbox;
    }

    public WeddingOutbox Detail(ulong characterId, ulong wdid)
    {
        var outbox = new WeddingOutbox();
        lock (_gate)
        {
            if (!_halls.TryGetValue(wdid, out var hall)) return Fail(outbox, characterId, WeddingHallError.HallMissing, $"hall {wdid} missing");
            outbox.Messages.Add(new CoupleMessage(characterId, WeddingHallFrames.HallDetail, DetailOf(hall, characterId)));
        }
        return outbox;
    }

    public WeddingOutbox EnteredZone(ulong characterId, ushort zone)
    {
        var outbox = new WeddingOutbox();
        lock (_gate)
        {
            var previous = _lastZone.TryGetValue(characterId, out var last) ? last : (ushort)0;
            _lastZone[characterId] = zone;
            if (!WeddingHallFrames.IsHallMap(zone)) return outbox;
            if (!_hallOf.TryGetValue(characterId, out var wdid) || !_halls.TryGetValue(wdid, out var hall)) return outbox;
            if (hall.MasterId != characterId || !hall.Has(characterId)) return outbox;
            if (_store.Load(hall.Wdid)?.Partner(characterId) is not { } lover || !hall.Has(lover.CharacterId)) return outbox;
            var order = new WeddingJoinOrder(hall.Wdid, characterId, hall.MapId, previous, hall.Title, hall.Members.Select(member => member.CharacterId).ToArray());
            outbox.Orders.Add(new WeddingOrder(characterId, null, WeddingHallFrames.OrderJoinMember, WeddingHallFrames.JoinMember(order, _text)));
            outbox.Notes.Add($"master entered map {zone}: hall {hall.Wdid} opens with {hall.Members.Count} members");
        }
        return outbox;
    }

    public WeddingOutbox EnterReported(ulong masterId, WeddingEnterReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var outbox = new WeddingOutbox();
        lock (_gate)
        {
            if (!_hallOf.TryGetValue(masterId, out var wdid) || !_halls.TryGetValue(wdid, out var hall))
            {
                outbox.Notes.Add($"enter report {report.Result} for 0x{masterId:X}: no hall");
                return outbox;
            }
            if (report.Result != WeddingEnterResult.Entered)
            {
                hall.Started = false;
                Broadcast(outbox, hall, WeddingHallFrames.Error, WeddingHallFrames.Code(WeddingHallError.FromEnterReport(report.Result)));
                outbox.Notes.Add($"hall {wdid} could not open: result {report.Result}");
                return outbox;
            }
            hall.Note = report.Note;
            var warp = new WeddingWarpOrder(report.Zone, report.Note, hall.Members.Select(member => member.CharacterId).ToArray());
            outbox.Orders.Add(new WeddingOrder(null, WeddingHallFrames.WarpMemberZone, WeddingHallFrames.OrderWarpMember, WeddingHallFrames.WarpMember(warp, _text)));
            outbox.Notes.Add($"hall {wdid} open in zone {report.Zone}: {hall.Members.Count} members called");
        }
        return outbox;
    }

    public WeddingOutbox Ended(WeddingEnded ended)
    {
        ArgumentNullException.ThrowIfNull(ended);
        var outbox = new WeddingOutbox();
        var couple = _store.Load(ended.Wdid);
        if (couple is null)
        {
            outbox.Notes.Add($"wedding {ended.Wdid} ended: couple not found");
        }
        else if (couple.State == CoupleStates.Married)
        {
            outbox.Notes.Add($"wedding {ended.Wdid} ended: couple already married");
        }
        else
        {
            var stored = _store.CompleteWedding(ended.Wdid, _time.GetUtcNow(), ended.Guests);
            outbox.Notes.Add($"wedding {ended.Wdid} ended with {ended.Guests.Count} guests{(stored ? string.Empty : " (NOT stored)")}");
            foreach (var member in couple.Members) outbox.Messages.AddRange(_refresh(member.CharacterId));
        }
        lock (_gate)
        {
            if (_halls.TryGetValue(ended.Wdid, out var hall)) Close(hall);
        }
        return outbox;
    }

    private void Enter(WeddingOutbox outbox, Hall hall, WeddingHallMember member, CoupleRecord? couple)
    {
        Broadcast(outbox, hall, WeddingHallFrames.MemberJoined, WeddingHallFrames.Member(member, _text));
        hall.Members.Add(member);
        _hallOf[member.CharacterId] = hall.Wdid;
        if (IsPartner(couple, member.CharacterId))
        {
            outbox.Orders.Add(new WeddingOrder(member.CharacterId, null, WeddingHallFrames.OrderDress,
                WeddingHallFrames.Dress(new WeddingDressOrder(member.CharacterId, true, (ushort)couple!.DressItemIndex))));
        }
    }

    private void DressOrders(WeddingOutbox outbox, CoupleRecord couple, bool on)
    {
        foreach (var member in couple.Members.Where(member => _isOpen(member.CharacterId)))
        {
            outbox.Orders.Add(new WeddingOrder(member.CharacterId, null, WeddingHallFrames.OrderDress,
                WeddingHallFrames.Dress(new WeddingDressOrder(member.CharacterId, on, (ushort)couple.DressItemIndex))));
        }
    }

    private void Close(Hall hall)
    {
        foreach (var member in hall.Members) _hallOf.Remove(member.CharacterId);
        hall.Members.Clear();
        _halls.Remove(hall.Wdid);
    }

    private static bool IsPartner(CoupleRecord? couple, ulong characterId) =>
        couple is not null && couple.Members.Any(member => member.CharacterId == characterId);

    private static void Broadcast(WeddingOutbox outbox, Hall hall, ushort opcode, byte[] payload)
    {
        foreach (var member in hall.Members) outbox.Messages.Add(new CoupleMessage(member.CharacterId, opcode, payload));
    }

    private byte[] DetailOf(Hall hall, ulong viewerId)
    {
        var couple = _store.Load(hall.Wdid);
        var groom = couple?.Members.FirstOrDefault(member => !member.Female)?.CharacterId ?? 0;
        var bride = couple?.Members.FirstOrDefault(member => member.Female)?.CharacterId ?? 0;
        var detail = new WeddingHallDetail(hall.Wdid, hall.MapId, groom, bride, hall.Members.ToArray());
        return WeddingHallFrames.Detail(detail, hall.MasterId == viewerId, _text);
    }

    private static WeddingOutbox Fail(WeddingOutbox outbox, ulong characterId, uint code, string reason)
    {
        outbox.Messages.Add(new CoupleMessage(characterId, WeddingHallFrames.Error, WeddingHallFrames.Code(code)));
        outbox.Notes.Add($"refused with 0x{code:X}: {reason}");
        return outbox;
    }
}
