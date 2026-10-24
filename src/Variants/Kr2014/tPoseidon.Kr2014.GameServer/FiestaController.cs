using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Fiesta;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

internal sealed class FiestaController
{
    private static readonly TimeSpan WarpDelay = TimeSpan.FromMilliseconds(200);

    private readonly Func<FiestaTables> _tables;
    private readonly IFiestaEntryStore _store;
    private readonly Func<ushort, int> _mapOption;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly object _gate = new();
    private readonly Dictionary<int, (DateTime SlotStart, bool PortalOpened)> _active = new();
    private readonly HashSet<InGameSessionState> _evicting = new();
    private readonly Random _rng = new();

    public FiestaController(
        Func<FiestaTables> tables, IFiestaEntryStore store, Func<ushort, int> mapOption, Func<IReadOnlyCollection<InGameSessionState>> sessions,
        Func<ushort, Opcode, byte[], Task> sendToMap, Encoding text, string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _mapOption = mapOption ?? throw new ArgumentNullException(nameof(mapOption));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<WarpTicketUse?> TicketAsync(InGameSessionState st, ItemParamEntry ticket)
    {
        ArgumentNullException.ThrowIfNull(st);
        ArgumentNullException.ThrowIfNull(ticket);
        if (st.Character is not { } character || st.RequestZoneChange is not { } requestZoneChange) return null;
        var tables = _tables();
        if (!tables.LevelPorts.TryGetValue(ticket.Id, out var port))
        {
            Log(st, $"ticket {ticket.Id} has no {FiestaTables.LevelPortTable} row");
            return null;
        }
        var now = DateTime.Now;
        var level = st.Stats?.Level ?? character.Level;
        var (refusal, band) = FiestaRules.Ticket(port, level, _mapOption(st.MapId), tables.Slots, EntryOf(st, character.CharacterId), now);
        var code = refusal switch
        {
            FiestaTicketRefusal.MapRefuses => FiestaRules.MapRefusedCode,
            FiestaTicketRefusal.Closed => FiestaRules.ClosedCode,
            FiestaTicketRefusal.DailyLimit => FiestaRules.DailyLimitCode,
            _ => (uint?)null,
        };
        if (code is { } refusedCode)
        {
            var w = new PacketWriter(4);
            w.WriteUInt32(refusedCode);
            await SendAsync(st, Opcode.S2C_WARP_ITEM_REFUSED, w.ToArray()).ConfigureAwait(false);
        }
        if (band is null)
        {
            Log(st, $"ticket {ticket.Id} refused: {refusal} (level {level}, map {st.MapId})");
            return null;
        }
        st.FiestaReturnZone = st.MapId;
        Log(st, $"ticket {ticket.Id} sends level {level} to zone {band.ZoneId}");
        return new WarpTicketUse(port.Notice, _text, ct =>
            requestZoneChange(new ZoneChangeRequest((ushort)band.ZoneId, TeleportArrival.ChangeZoneType, null, WarpDelay), ct));
    }

    public async Task EnteredZoneAsync(InGameSessionState st)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (st.Character is not { } character) return;
        var now = DateTime.Now;
        if (FiestaRules.OpenInZone(_tables().Slots, st.MapId, now) is not { } window) return;
        var w = new PacketWriter(8);
        w.WriteUInt32((uint)window.Duration.TotalMilliseconds);
        w.WriteUInt32((uint)Math.Max(0, window.Elapsed.TotalMilliseconds));
        await SendAsync(st, Opcode.S2C_FIESTA_TIMER, w.ToArray()).ConfigureAwait(false);
        var before = EntryOf(st, character.CharacterId);
        var after = FiestaRules.Entered(before, window.SlotStart, now);
        st.FiestaEntry = after;
        if (after != before)
        {
            _store.Save(character.CharacterId, after.Count, after.Slot);
            if (after.Count > FiestaRules.DailyEntries) Log(st, $"FiestaOverDayCount {after.Count}");
        }
        Log(st, $"entered Fiesta zone {st.MapId} (row {window.Row.Id}), entry {after.Count} today");
    }

    public async Task TickAsync()
    {
        var now = DateTime.Now;
        var slots = _tables().Slots;
        foreach (var zone in slots.Select(row => row.ZoneId).Distinct())
        {
            if (FiestaRules.OpenInZone(slots, zone, now) is { } window)
            {
                bool announce;
                lock (_gate)
                {
                    var known = _active.TryGetValue(zone, out var state) && state.SlotStart == window.SlotStart;
                    var opened = known && state.PortalOpened;
                    announce = !opened && FiestaRules.PortalOpens(window);
                    _active[zone] = (window.SlotStart, opened || announce);
                }
                if (announce)
                {
                    await _sendToMap((ushort)zone, Opcode.S2C_FIESTA_STATE, new[] { FiestaRules.PortalOpenedState }).ConfigureAwait(false);
                    Console.WriteLine($"{_logTag} [fiesta] zone {zone}: portal {window.Row.PortalIndex} open (row {window.Row.Id})");
                }
                continue;
            }
            lock (_gate) _active.Remove(zone);
            foreach (var guest in _sessions().Where(guest => guest.MapId == zone && guest.ClientReady && guest.Character is not null).ToArray())
            {
                lock (_gate)
                {
                    if (!_evicting.Add(guest)) continue;
                }
                await EvictAsync(guest).ConfigureAwait(false);
            }
        }
        lock (_gate) _evicting.RemoveWhere(guest => !slots.Any(row => row.ZoneId == guest.MapId));
    }

    private async Task EvictAsync(InGameSessionState guest)
    {
        if (guest.Character is not { } character || guest.RequestZoneChange is not { } requestZoneChange) return;
        var home = RestartArrival.NormalizeReturnMap(character.Home);
        var zone = (ushort)FiestaRules.ReturnZone(guest.FiestaReturnZone, home);
        Log(guest, $"Fiesta zone {guest.MapId} closed: back to zone {zone}");
        await requestZoneChange(new ZoneChangeRequest(zone, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), default).ConfigureAwait(false);
    }

    private FiestaEntryRecord EntryOf(InGameSessionState st, ulong characterId)
    {
        if (st.FiestaEntry is { } cached) return cached;
        var (count, slot) = _store.Load(characterId);
        var record = new FiestaEntryRecord(count, slot);
        st.FiestaEntry = record;
        return record;
    }

    private void Log(InGameSessionState st, string what) =>
        Console.WriteLine($"{_logTag} [fiesta] 0x{st.PlayerEntityId:X4} {what}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
