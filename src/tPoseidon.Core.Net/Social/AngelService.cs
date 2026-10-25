using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Angel;

namespace tPoseidon.Core.Net.Social;

public sealed record AngelOrder(ulong CharacterId, ushort OrderType, byte[] Data);

public sealed class AngelOutbox
{
    public List<CoupleMessage> Messages { get; } = new();

    public List<AngelOrder> Orders { get; } = new();

    public List<CoupleMessage> Direct { get; } = new();

    public List<ulong> GraduationMemos { get; } = new();

    public bool Graduated { get; set; }

    public List<string> Notes { get; } = new();

    public void Send(ulong to, ushort opcode, byte[] payload) => Messages.Add(new CoupleMessage(to, opcode, payload));
}

public sealed class AngelService
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, Record> _angels = new();
    private readonly List<ulong> _rotation = new();
    private readonly HashSet<ulong> _newbies = new();
    private readonly Dictionary<ulong, (int Count, DateTimeOffset At)> _rude = new();
    private readonly IAngelStore _store;
    private readonly Encoding _text;
    private readonly TimeProvider _time;
    private int _cursor;

    public AngelService(IAngelStore store, Encoding text, TimeProvider time)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public AngelOutbox LoggedIn(ulong characterId, int level)
    {
        var outbox = new AngelOutbox();
        lock (_gate)
        {
            if (AngelRules.IsAngel(level) && _angels.TryAdd(characterId, new Record()))
            {
                _rotation.Add(characterId);
            }
        }
        if (AngelRules.IsNewbie(level)) return OpenSession(characterId);
        return outbox;
    }

    public void LoggedOut(ulong characterId)
    {
        lock (_gate)
        {
            if (_angels.Remove(characterId)) _rotation.Remove(characterId);
            _newbies.Remove(characterId);
        }
    }

    public AngelOutbox LevelChanged(ulong characterId, uint accountId, int level)
    {
        var outbox = new AngelOutbox();
        if (!AngelRules.Graduates(level)) return outbox;
        var graduation = _store.Graduate(characterId, accountId, _time.GetUtcNow());
        if (!graduation.Graduated)
        {
            if (graduation.Angels.Count == 0) return outbox;
            outbox.Notes.Add($"graduation of 0x{characterId:X} failed: no gift rows");
            return outbox;
        }
        lock (_gate) _newbies.Remove(characterId);
        outbox.Graduated = true;
        foreach (var angel in graduation.Angels)
        {
            outbox.Orders.Add(new AngelOrder(angel, AngelFrames.OrderAngelReward, AngelFrames.Character(angel)));
            outbox.GraduationMemos.Add(angel);
        }
        outbox.Notes.Add($"0x{characterId:X} graduated at level {level}: {graduation.Gifts} gift rows, {graduation.Angels.Count} angels rewarded");
        return outbox;
    }

    public AngelOutbox OpenSession(ulong characterId)
    {
        var outbox = new AngelOutbox();
        lock (_gate) _newbies.Add(characterId);
        outbox.Send(characterId, AngelFrames.SessionOpen, Array.Empty<byte>());
        return outbox;
    }

    public AngelOutbox List(ulong newbieId)
    {
        var outbox = new AngelOutbox();
        if (!IsNewbie(newbieId)) return outbox;
        outbox.Send(newbieId, AngelFrames.List, AngelFrames.ListOf(_store.Angels(newbieId), _text));
        return outbox;
    }

    public AngelOutbox Call(ulong newbieId, string newbieName, int newbieLevel, ushort newbieZone, AngelCall call)
    {
        ArgumentNullException.ThrowIfNull(newbieName);
        ArgumentNullException.ThrowIfNull(call);
        var outbox = new AngelOutbox();
        if (!IsNewbie(newbieId))
        {
            outbox.Notes.Add("no newbie session");
            return outbox;
        }
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (_rude.TryGetValue(newbieId, out var rude) && AngelRules.RemainingRudeWait(rude.Count, rude.At, now) is { } wait)
            {
                outbox.Send(newbieId, AngelFrames.Refused, AngelFrames.RudeWait(wait));
                outbox.Notes.Add($"rude wait {wait.TotalSeconds:0} s");
                return outbox;
            }
            ulong angelId;
            if (call.Type == AngelRules.RequestListed)
            {
                if (!_angels.TryGetValue(call.AngelCharacterId, out var listed))
                {
                    outbox.Send(newbieId, AngelFrames.Refused, AngelFrames.Code(AngelRules.Unreachable));
                    return outbox;
                }
                if (AngelRules.RefusalFor(listed.State, listed.At, now) is { } refusal)
                {
                    outbox.Send(newbieId, AngelFrames.Refused, AngelFrames.Code(refusal));
                    return outbox;
                }
                angelId = call.AngelCharacterId;
            }
            else
            {
                if (NextFree(newbieId, now) is not { } free)
                {
                    outbox.Send(newbieId, AngelFrames.Refused, AngelFrames.Code(AngelRules.AllBusy));
                    return outbox;
                }
                angelId = free;
            }
            var record = _angels[angelId];
            record.State = AngelRequestState.Requested;
            record.Partner = newbieId;
            record.At = now;
            var listedAlready = _store.Angels(newbieId).Any(angel => angel.AngelCharacterId == angelId);
            outbox.Send(angelId, AngelFrames.CallToAngel,
                AngelFrames.CallFor(newbieName, (ushort)newbieLevel, newbieZone, listedAlready, call.Message, _text));
            outbox.Notes.Add($"angel 0x{angelId:X} called");
        }
        return outbox;
    }

    public AngelOutbox Dismiss(ulong angelId, bool rude)
    {
        var outbox = new AngelOutbox();
        lock (_gate)
        {
            if (!_angels.TryGetValue(angelId, out var record) || record.Partner == 0) return outbox;
            var newbieId = record.Partner;
            _store.Remove(newbieId, angelId);
            outbox.Send(newbieId, AngelFrames.Refused, AngelFrames.Code(AngelRules.RemovedByAngel));
            if (rude) Report(newbieId, outbox);
            record.Clear();
            outbox.Notes.Add($"newbie 0x{newbieId:X} dismissed{(rude ? " as rude" : string.Empty)}");
        }
        return outbox;
    }

    public AngelOutbox Accept(ulong angelId, ushort zone, ushort x, ushort y)
    {
        var outbox = new AngelOutbox();
        lock (_gate)
        {
            if (!_angels.TryGetValue(angelId, out var record) || record.Partner == 0 || !_newbies.Contains(record.Partner))
            {
                outbox.Send(angelId, AngelFrames.AcceptRefused, AngelFrames.Code(AngelRules.AcceptFailed));
                return outbox;
            }
            record.At = _time.GetUtcNow();
            record.State = AngelRequestState.Accepted;
            record.Zone = zone;
            record.X = x;
            record.Y = y;
            outbox.Orders.Add(new AngelOrder(record.Partner, AngelFrames.OrderNewbiePosition, AngelFrames.Pair(record.Partner, angelId)));
            outbox.Notes.Add($"accepted newbie 0x{record.Partner:X} from zone {zone} ({x},{y})");
        }
        return outbox;
    }

    public AngelOutbox NewbieSpot(AngelSpot spot, Func<ulong, string?> nameOf)
    {
        ArgumentNullException.ThrowIfNull(spot);
        ArgumentNullException.ThrowIfNull(nameOf);
        var outbox = new AngelOutbox();
        lock (_gate)
        {
            if (!_angels.TryGetValue(spot.AngelCharacterId, out var record) || record.State != AngelRequestState.Accepted) return outbox;
            var newbieId = record.Partner;
            if (spot.Zone == 0)
            {
                outbox.Send(spot.AngelCharacterId, AngelFrames.AcceptRefused, AngelFrames.Code(AngelRules.AcceptFailed));
                outbox.Send(newbieId, AngelFrames.Refused, AngelFrames.Code(AngelRules.Unreachable));
                outbox.Notes.Add($"newbie 0x{newbieId:X} cannot be reached");
                return outbox;
            }
            var angelName = nameOf(spot.AngelCharacterId) ?? string.Empty;
            outbox.Orders.Add(new AngelOrder(spot.AngelCharacterId, AngelFrames.OrderAngelMove, AngelFrames.Spot(spot)));
            outbox.Send(spot.AngelCharacterId, AngelFrames.AngelMoving, Array.Empty<byte>());
            outbox.Send(newbieId, AngelFrames.AngelComing, AngelFrames.Named(angelName, _text));
            var listed = _store.Angels(newbieId);
            if (listed.Count < AngelRules.ListCapacity && listed.All(angel => angel.AngelCharacterId != spot.AngelCharacterId)
                && _store.Add(newbieId, spot.AngelCharacterId) == AngelStoreError.None)
            {
                outbox.Send(newbieId, AngelFrames.Listed, AngelFrames.ListedAngel(spot.AngelCharacterId, angelName, _text));
            }
            outbox.Notes.Add($"angel moves to zone {spot.Zone} ({spot.X},{spot.Y})");
        }
        return outbox;
    }

    public AngelOutbox Portal(ulong angelId, bool goBack, bool rude)
    {
        var outbox = new AngelOutbox();
        lock (_gate)
        {
            if (!_angels.TryGetValue(angelId, out var record) || record.Partner == 0) return outbox;
            if (rude)
            {
                _store.Remove(record.Partner, angelId);
                outbox.Send(record.Partner, AngelFrames.Refused, AngelFrames.Code(AngelRules.RemovedByAngel));
                Report(record.Partner, outbox);
            }
            if (goBack && record.Zone != 0)
            {
                outbox.Direct.Add(new CoupleMessage(angelId, AngelFrames.Portal, AngelFrames.ReturnMessage(angelId, record.X, record.Y, record.Zone)));
            }
            outbox.Notes.Add($"portal closed{(goBack ? $", back to zone {record.Zone} ({record.X},{record.Y})" : string.Empty)}");
            record.End(_time.GetUtcNow());
        }
        return outbox;
    }

    public AngelOutbox RemoveListed(ulong newbieId, ulong angelId)
    {
        var outbox = new AngelOutbox();
        if (!IsNewbie(newbieId)) return outbox;
        if (_store.Remove(newbieId, angelId)) outbox.Send(newbieId, AngelFrames.RemoveListed, AngelFrames.Character(angelId));
        return outbox;
    }

    public AngelOutbox Busy(ulong angelId)
    {
        var outbox = new AngelOutbox();
        lock (_gate)
        {
            if (_angels.TryGetValue(angelId, out var record) && record.Partner != 0)
            {
                outbox.Send(record.Partner, AngelFrames.Refused, AngelFrames.Code(AngelRules.CannotRespond));
            }
        }
        return outbox;
    }

    private bool IsNewbie(ulong characterId)
    {
        lock (_gate) return _newbies.Contains(characterId);
    }

    private ulong? NextFree(ulong newbieId, DateTimeOffset now)
    {
        for (var tried = 0; tried < _rotation.Count; tried++)
        {
            _cursor = (_cursor + 1) % _rotation.Count;
            var candidate = _rotation[_cursor];
            if (candidate != newbieId && _angels[candidate] is var record && AngelRules.IsFree(record.State, record.At, now)) return candidate;
        }
        return null;
    }

    private void Report(ulong newbieId, AngelOutbox outbox)
    {
        var now = _time.GetUtcNow();
        var count = _rude.TryGetValue(newbieId, out var previous) ? previous.Count + 1 : 1;
        _rude[newbieId] = (count, now);
        outbox.Send(newbieId, AngelFrames.Refused, AngelFrames.RudeWait(AngelRules.RudeWait(count)));
    }

    private sealed class Record
    {
        public ulong Partner { get; set; }
        public DateTimeOffset At { get; set; }
        public AngelRequestState State { get; set; }
        public ushort Zone { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }

        public void Clear()
        {
            Partner = 0;
            State = AngelRequestState.None;
            Zone = X = Y = 0;
        }

        public void End(DateTimeOffset at)
        {
            State = AngelRequestState.Ended;
            At = at;
            Zone = X = Y = 0;
        }
    }
}
