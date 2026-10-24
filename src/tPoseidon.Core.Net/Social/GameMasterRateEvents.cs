using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public interface IGameMasterTimedEvent
{
    long StartTime { get; }

    long EndTime { get; }
}

public readonly record struct GameMasterScheduleStep<T>(T? Expired, T? Applied, bool ListsChanged) where T : struct;

public sealed class GameMasterSchedule<T> where T : struct, IGameMasterTimedEvent, IEquatable<T>
{
    private readonly object _gate = new();
    private readonly List<T> _waiting = new();
    private readonly List<T> _running = new();
    private T _current;

    public T Current
    {
        get { lock (_gate) return _current; }
    }

    public IReadOnlyList<T> Running
    {
        get { lock (_gate) return _running.ToArray(); }
    }

    public IReadOnlyList<T> Waiting
    {
        get { lock (_gate) return _waiting.ToArray(); }
    }

    public void Reserve(T entry)
    {
        lock (_gate) _waiting.Add(entry);
    }

    public void Restore(IEnumerable<T> running, IEnumerable<T> waiting)
    {
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(waiting);
        lock (_gate)
        {
            _running.Clear();
            _running.AddRange(running);
            _waiting.Clear();
            _waiting.AddRange(waiting);
            _current = default;
        }
    }

    public bool Delete(T entry)
    {
        lock (_gate)
        {
            var running = _running.IndexOf(entry);
            if (running >= 0)
            {
                _running.RemoveAt(running);
                return true;
            }
            var waiting = _waiting.IndexOf(entry);
            if (waiting < 0) return false;
            _waiting.RemoveAt(waiting);
            return true;
        }
    }

    public GameMasterScheduleStep<T> Tick(long now)
    {
        lock (_gate)
        {
            var due = _waiting.FindIndex(entry => entry.StartTime <= now);
            if (due >= 0)
            {
                _running.Add(_waiting[due]);
                _waiting.RemoveAt(due);
            }
            T? expired = null;
            var over = _running.FindIndex(entry => entry.EndTime <= now);
            if (over >= 0)
            {
                expired = _running[over];
                _running.RemoveAt(over);
            }
            var next = _running.Count > 0 ? _running[0] : default;
            T? applied = null;
            if (!next.Equals(_current))
            {
                _current = next;
                applied = next;
            }
            return new GameMasterScheduleStep<T>(expired, applied, due >= 0 || over >= 0);
        }
    }
}

public readonly record struct GameMasterExpEvent(ushort ExpPercent, ushort TmPercent, long StartTime, long EndTime) : IGameMasterTimedEvent
{
    public const int Length = sizeof(ushort) * 2 + sizeof(long) * 2;

    public static bool TryRead(ReadOnlySpan<byte> body, out GameMasterExpEvent entry)
    {
        entry = default;
        if (body.Length < Length) return false;
        var r = new PacketReader(body);
        entry = new GameMasterExpEvent(r.ReadUInt16(), r.ReadUInt16(), r.ReadInt64(), r.ReadInt64());
        return true;
    }

    public void WriteTo(PacketWriter w)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteUInt16(ExpPercent);
        w.WriteUInt16(TmPercent);
        w.WriteInt64(StartTime);
        w.WriteInt64(EndTime);
    }
}

public readonly record struct GameMasterAbilityEvent(byte Stat, ushort Percent, long StartTime, long EndTime) : IGameMasterTimedEvent
{
    public const int Length = sizeof(byte) + sizeof(ushort) + sizeof(long) * 2;

    public static bool TryRead(ReadOnlySpan<byte> body, out GameMasterAbilityEvent entry)
    {
        entry = default;
        if (body.Length < Length) return false;
        var r = new PacketReader(body);
        entry = new GameMasterAbilityEvent(r.ReadByte(), r.ReadUInt16(), r.ReadInt64(), r.ReadInt64());
        return true;
    }

    public void WriteTo(PacketWriter w)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteByte(Stat);
        w.WriteUInt16(Percent);
        w.WriteInt64(StartTime);
        w.WriteInt64(EndTime);
    }
}

public sealed record GameMasterLinkMessage(ushort Opcode, byte[] Body);

public sealed record GameMasterScheduleList<T>(DateTime? Time, T? Current, IReadOnlyList<T> Running, IReadOnlyList<T> Waiting) where T : struct;

public static class GameMasterEventFrames
{
    public const ushort ExpApply = 0x14EE;
    public const ushort ExpQuery = 0x14F1;
    public const ushort ExpQueryReply = 0x14F2;
    public const ushort ExpList = 0x14FC;
    public const ushort ExpListReply = 0x14FD;
    public const ushort ExpListRefused = 0x14FE;
    public const ushort ExpReserve = 0x14FF;
    public const ushort ExpReserved = 0x1500;
    public const ushort ExpReserveRefused = 0x1501;
    public const ushort ExpDelete = 0x1502;
    public const ushort ExpDeleted = 0x1503;
    public const ushort ExpDeleteRefused = 0x1504;
    public const ushort AbilityApply = 0x151F;
    public const ushort AbilityList = 0x1520;
    public const ushort AbilityListReply = 0x1521;
    public const ushort AbilityListRefused = 0x1522;
    public const ushort AbilityReserve = 0x1523;
    public const ushort AbilityReserved = 0x1524;
    public const ushort AbilityReserveRefused = 0x1525;
    public const ushort AbilityDelete = 0x1526;
    public const ushort AbilityDeleted = 0x1527;
    public const ushort AbilityDeleteRefused = 0x1528;
    public const ushort AbilityPercents = 0x04EF;

    public const int SystemTimeLength = sizeof(ushort) * 8;
    public const int ExpPairLength = sizeof(ushort) * 2;
    public const int AbilityChangeLength = sizeof(byte) + sizeof(ushort);

    public static void WriteSystemTime(PacketWriter w, DateTime local)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteUInt16((ushort)local.Year);
        w.WriteUInt16((ushort)local.Month);
        w.WriteUInt16((ushort)local.DayOfWeek);
        w.WriteUInt16((ushort)local.Day);
        w.WriteUInt16((ushort)local.Hour);
        w.WriteUInt16((ushort)local.Minute);
        w.WriteUInt16((ushort)local.Second);
        w.WriteUInt16((ushort)local.Millisecond);
    }

    public static DateTime? ReadSystemTime(ref PacketReader r)
    {
        var year = r.ReadUInt16();
        var month = r.ReadUInt16();
        r.ReadUInt16();
        var day = r.ReadUInt16();
        var hour = r.ReadUInt16();
        var minute = r.ReadUInt16();
        var second = r.ReadUInt16();
        var millisecond = r.ReadUInt16();
        var valid = year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(Math.Max((int)year, 1), Math.Clamp((int)month, 1, 12))
            && hour < 24 && minute < 60 && second < 60 && millisecond < 1000;
        return valid ? new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Local) : null;
    }

    public static byte[] ExpPair(ushort expPercent, ushort tmPercent)
    {
        var w = new PacketWriter(ExpPairLength);
        w.WriteUInt16(expPercent);
        w.WriteUInt16(tmPercent);
        return w.ToArray();
    }

    public static bool TryReadExpPair(ReadOnlySpan<byte> body, out ushort expPercent, out ushort tmPercent)
    {
        expPercent = 0;
        tmPercent = 0;
        if (body.Length < ExpPairLength) return false;
        var r = new PacketReader(body);
        expPercent = r.ReadUInt16();
        tmPercent = r.ReadUInt16();
        return true;
    }

    public static byte[] ExpListBody(DateTime? local, GameMasterExpEvent? current, IReadOnlyList<GameMasterExpEvent> running, IReadOnlyList<GameMasterExpEvent> waiting) =>
        ListBody(local, current, running, waiting, GameMasterExpEvent.Length, static (entry, w) => entry.WriteTo(w));

    public static bool TryReadExpList(ReadOnlySpan<byte> body, bool withTime, bool withCurrent, out GameMasterScheduleList<GameMasterExpEvent> list) =>
        TryReadList(body, withTime, withCurrent, GameMasterExpEvent.Length, GameMasterExpEvent.TryRead, out list);

    public static byte[] AbilityListBody(DateTime? local, IReadOnlyList<GameMasterAbilityEvent> running, IReadOnlyList<GameMasterAbilityEvent> waiting) =>
        ListBody<GameMasterAbilityEvent>(local, null, running, waiting, GameMasterAbilityEvent.Length, static (entry, w) => entry.WriteTo(w));

    public static bool TryReadAbilityList(ReadOnlySpan<byte> body, bool withTime, out GameMasterScheduleList<GameMasterAbilityEvent> list) =>
        TryReadList(body, withTime, withCurrent: false, GameMasterAbilityEvent.Length, GameMasterAbilityEvent.TryRead, out list);

    public static byte[] AbilityChange(byte stat, ushort percent)
    {
        var w = new PacketWriter(AbilityChangeLength);
        w.WriteByte(stat);
        w.WriteUInt16(percent);
        return w.ToArray();
    }

    public static bool TryReadAbilityChange(ReadOnlySpan<byte> body, out byte stat, out ushort percent)
    {
        stat = 0;
        percent = 0;
        if (body.Length < AbilityChangeLength) return false;
        var r = new PacketReader(body);
        stat = r.ReadByte();
        percent = r.ReadUInt16();
        return true;
    }

    private delegate bool EntryReader<T>(ReadOnlySpan<byte> body, out T entry);

    private static byte[] ListBody<T>(DateTime? local, T? current, IReadOnlyList<T> running, IReadOnlyList<T> waiting, int length, Action<T, PacketWriter> write) where T : struct
    {
        ArgumentNullException.ThrowIfNull(running);
        ArgumentNullException.ThrowIfNull(waiting);
        var w = new PacketWriter(SystemTimeLength + length * (running.Count + waiting.Count + 1) + sizeof(ushort) * 2);
        if (local is { } time) WriteSystemTime(w, time);
        if (current is { } head) write(head, w);
        WriteEntries(w, running, write);
        WriteEntries(w, waiting, write);
        return w.ToArray();
    }

    private static bool TryReadList<T>(ReadOnlySpan<byte> body, bool withTime, bool withCurrent, int length, EntryReader<T> read, out GameMasterScheduleList<T> list) where T : struct
    {
        list = new GameMasterScheduleList<T>(null, null, Array.Empty<T>(), Array.Empty<T>());
        var need = (withTime ? SystemTimeLength : 0) + (withCurrent ? length : 0) + sizeof(ushort);
        if (body.Length < need) return false;
        var r = new PacketReader(body);
        DateTime? time = withTime ? ReadSystemTime(ref r) : null;
        T? current = null;
        if (withCurrent && read(body[r.Position..], out var head))
        {
            current = head;
            r.Skip(length);
        }
        if (ReadEntries(body, ref r, length, read) is not { } running || ReadEntries(body, ref r, length, read) is not { } waiting) return false;
        list = new GameMasterScheduleList<T>(time, current, running, waiting);
        return true;
    }

    private static void WriteEntries<T>(PacketWriter w, IReadOnlyList<T> entries, Action<T, PacketWriter> write)
    {
        w.WriteUInt16((ushort)entries.Count);
        foreach (var entry in entries) write(entry, w);
    }

    private static T[]? ReadEntries<T>(ReadOnlySpan<byte> body, ref PacketReader r, int length, EntryReader<T> read)
    {
        if (r.Remaining < sizeof(ushort)) return null;
        var count = r.ReadUInt16();
        if (r.Remaining < count * length) return null;
        var entries = new T[count];
        for (var i = 0; i < count; i++)
        {
            read(body[r.Position..], out entries[i]);
            r.Skip(length);
        }
        return entries;
    }
}

public sealed class GameMasterRateControl
{
    private readonly object _gate = new();
    private readonly IGmRateEventStore _store;
    private (ushort Exp, ushort Tm) _direct;
    private (ushort Exp, ushort Tm) _appliedExp;
    private readonly ushort[] _appliedAbilities = new ushort[GameMasterRates.AbilitySlots];

    public GameMasterRateControl(IGmRateEventStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Restore(_store.Load());
    }

    public GameMasterSchedule<GameMasterExpEvent> Exp { get; } = new();

    public GameMasterSchedule<GameMasterAbilityEvent> Abilities { get; } = new();

    public byte[] AbilityListReply(DateTime local) =>
        GameMasterEventFrames.AbilityListBody(local, Abilities.Running, Abilities.Waiting);

    public byte[] ReserveAbility(GameMasterAbilityEvent entry)
    {
        Abilities.Reserve(entry);
        Persist();
        return GameMasterEventFrames.AbilityListBody(null, Abilities.Running, Abilities.Waiting);
    }

    public byte[] DeleteAbility(GameMasterAbilityEvent entry)
    {
        if (Abilities.Delete(entry)) Persist();
        return GameMasterEventFrames.AbilityListBody(null, Abilities.Running, Abilities.Waiting);
    }

    public byte[] ExpListReply(DateTime local) =>
        GameMasterEventFrames.ExpListBody(local, Exp.Current, Exp.Running, Exp.Waiting);

    public byte[] ReserveExp(GameMasterExpEvent entry)
    {
        Exp.Reserve(entry);
        Persist();
        return GameMasterEventFrames.ExpListBody(null, null, Exp.Running, Exp.Waiting);
    }

    public byte[] DeleteExp(GameMasterExpEvent entry)
    {
        if (Exp.Delete(entry)) Persist();
        return GameMasterEventFrames.ExpListBody(null, Exp.Current, Exp.Running, Exp.Waiting);
    }

    public GameMasterLinkMessage SetExpNow(ushort expPercent, ushort tmPercent)
    {
        lock (_gate)
        {
            _direct = (expPercent, tmPercent);
            _appliedExp = (expPercent, tmPercent);
        }
        Persist();
        return new GameMasterLinkMessage(GameMasterEventFrames.ExpApply, GameMasterEventFrames.ExpPair(expPercent, tmPercent));
    }

    public byte[] ExpQueryReply()
    {
        lock (_gate) return GameMasterEventFrames.ExpPair(_direct.Exp, _direct.Tm);
    }

    public IReadOnlyList<GameMasterLinkMessage> Tick(long now)
    {
        var messages = new List<GameMasterLinkMessage>();
        var expStep = Exp.Tick(now);
        if (expStep.Applied is { } exp)
        {
            lock (_gate) _appliedExp = (exp.ExpPercent, exp.TmPercent);
            messages.Add(new GameMasterLinkMessage(GameMasterEventFrames.ExpApply, GameMasterEventFrames.ExpPair(exp.ExpPercent, exp.TmPercent)));
        }
        var ability = Abilities.Tick(now);
        if (ability.Expired is { } expired) messages.Add(AbilityMessage(expired.Stat, 0));
        if (ability.Applied is { } applied) messages.Add(AbilityMessage(applied.Stat, applied.Percent));
        if (expStep.ListsChanged || ability.ListsChanged) Persist();
        return messages;
    }

    private GameMasterLinkMessage AbilityMessage(byte stat, ushort percent)
    {
        lock (_gate)
        {
            if (stat < _appliedAbilities.Length) _appliedAbilities[stat] = percent;
        }
        return new GameMasterLinkMessage(GameMasterEventFrames.AbilityApply, GameMasterEventFrames.AbilityChange(stat, percent));
    }

    public IReadOnlyList<GameMasterLinkMessage> LinkState()
    {
        var messages = new List<GameMasterLinkMessage>();
        lock (_gate)
        {
            if (_appliedExp != default) messages.Add(new GameMasterLinkMessage(GameMasterEventFrames.ExpApply, GameMasterEventFrames.ExpPair(_appliedExp.Exp, _appliedExp.Tm)));
            for (var stat = 0; stat < _appliedAbilities.Length; stat++)
            {
                if (_appliedAbilities[stat] == 0) continue;
                messages.Add(new GameMasterLinkMessage(GameMasterEventFrames.AbilityApply, GameMasterEventFrames.AbilityChange((byte)stat, _appliedAbilities[stat])));
            }
        }
        return messages;
    }

    public GmRateEventState Snapshot()
    {
        var rows = new List<GmRateEventRow>();
        AddRows(rows, GmRateEventList.Running, Exp.Running.Select(ExpRow));
        AddRows(rows, GmRateEventList.Waiting, Exp.Waiting.Select(ExpRow));
        AddRows(rows, GmRateEventList.Running, Abilities.Running.Select(AbilityRow));
        AddRows(rows, GmRateEventList.Waiting, Abilities.Waiting.Select(AbilityRow));
        lock (_gate) return new GmRateEventState(rows, _direct.Exp, _direct.Tm);
    }

    private void Restore(GmRateEventState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Exp.Restore(Events(state, GmRateEventKind.Exp, GmRateEventList.Running, ToExp), Events(state, GmRateEventKind.Exp, GmRateEventList.Waiting, ToExp));
        Abilities.Restore(Events(state, GmRateEventKind.Ability, GmRateEventList.Running, ToAbility), Events(state, GmRateEventKind.Ability, GmRateEventList.Waiting, ToAbility));
        lock (_gate)
        {
            _direct = (state.DirectExpPercent, state.DirectTmPercent);
            _appliedExp = _direct;
        }
    }

    private void Persist() => _store.Save(Snapshot());

    private static List<T> Events<T>(GmRateEventState state, GmRateEventKind kind, GmRateEventList list, Func<GmRateEventRow, T> convert) =>
        state.Rows.Where(row => row.Kind == kind && row.List == list).OrderBy(row => row.Position).Select(convert).ToList();

    private static void AddRows(List<GmRateEventRow> rows, GmRateEventList list, IEnumerable<Func<GmRateEventList, int, GmRateEventRow>> builders)
    {
        var position = 0;
        foreach (var build in builders) rows.Add(build(list, position++));
    }

    private static Func<GmRateEventList, int, GmRateEventRow> ExpRow(GameMasterExpEvent entry) =>
        (list, position) => new GmRateEventRow(GmRateEventKind.Exp, list, position, 0, entry.ExpPercent, entry.TmPercent, entry.StartTime, entry.EndTime);

    private static Func<GmRateEventList, int, GmRateEventRow> AbilityRow(GameMasterAbilityEvent entry) =>
        (list, position) => new GmRateEventRow(GmRateEventKind.Ability, list, position, entry.Stat, entry.Percent, 0, entry.StartTime, entry.EndTime);

    private static GameMasterExpEvent ToExp(GmRateEventRow row) => new(row.FirstPercent, row.SecondPercent, row.StartTime, row.EndTime);

    private static GameMasterAbilityEvent ToAbility(GmRateEventRow row) => new(row.Stat, row.FirstPercent, row.StartTime, row.EndTime);
}
