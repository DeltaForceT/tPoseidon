using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.PvP;
using tPoseidon.Core.LibConfig;

internal sealed class PkController
{
    private readonly Func<ushort, MapKindEntry?> _mapKind;
    private readonly Func<PkInfoTables> _info;
    private readonly IPkInfoStore _store;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Random _rng = new();

    public PkController(
        Func<ushort, MapKindEntry?> mapKind, Func<PkInfoTables> info, IPkInfoStore store, Func<IReadOnlyCollection<InGameSessionState>> sessions,
        Func<ushort, Opcode, byte[], Task> sendToMap, Encoding text, string logTag)
    {
        _mapKind = mapKind ?? throw new ArgumentNullException(nameof(mapKind));
        _info = info ?? throw new ArgumentNullException(nameof(info));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public Task ScoreAsync(InGameSessionState st, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (payload.Length < 4) return Task.CompletedTask;
        var key = BitConverter.ToUInt32(payload.Span[..4]);
        var owner = _sessions().FirstOrDefault(session => session.Character is { } character && (uint)character.CharacterId == key);
        var w = new tPoseidon.Core.Net.PacketWriter(16);
        w.WriteUInt32(key);
        if (owner?.Character is { } found)
        {
            var record = _store.GetOrCreate(found.CharacterId);
            w.WriteUInt32((uint)Math.Max(0, record.KillTotalCount));
            w.WriteUInt32((uint)Math.Max(0, record.DeathTotalCount));
            w.WriteUInt32(owner.PkRecordMode == PkRules.RecordSkirmish ? 1u : 0u);
        }
        else
        {
            w.WriteZero(12);
        }
        return SendAsync(st, Opcode.PK_SCORE_QUERY, w.ToArray());
    }

    public async Task KillCreditAsync(InGameSessionState victim, InGameSessionState? killer)
    {
        ArgumentNullException.ThrowIfNull(victim);
        if (victim.Character is not { } victimCharacter) return;
        var killerIsPlayer = killer?.Character is not null;
        if (PvpHitRules.CreditsKill(victim.PvpMode, victim.PkRecordMode, killerIsPlayer) && killer?.Character is { } killerCharacter)
        {
            var info = _info();
            var victimRecord = Credit(victimCharacter.CharacterId, counters => PvpHitRules.VictimCredit(counters, info.TotalDeathMax));
            var killerRecord = Credit(killerCharacter.CharacterId, counters => PvpHitRules.KillerCredit(counters, info.TodayPkMax, info.TotalPkMax));
            foreach (var (player, record) in new[] { (victim, victimRecord), (killer, killerRecord) })
            {
                await SendAsync(player, Opcode.GAME_UNK_3A6, RecordFrame(record)).ConfigureAwait(false);
                var today = new tPoseidon.Core.Net.PacketWriter(4);
                today.WriteUInt16(player.PlayerEntityId);
                today.WriteUInt16((ushort)Math.Clamp(record.KillTodayCount, 0, ushort.MaxValue));
                await _sendToMap(player.MapId, Opcode.ENTITY_ID_ECHO, today.ToArray()).ConfigureAwait(false);
            }
            Console.WriteLine($"{_logTag} [pk] 0x{killer.PlayerEntityId:X4} killed 0x{victim.PlayerEntityId:X4}: kills today {killerRecord.KillTodayCount}, deaths today {victimRecord.DeathTodayCount}");
        }
        if (!PvpHitRules.AnnouncesKill(victim.PvpMode, killerIsPlayer) || killer?.Character is not { } announcer) return;
        var notice = new tPoseidon.Core.Net.PacketWriter(40);
        notice.WriteNulTerminated(victimCharacter.Name, _text);
        notice.WriteNulTerminated(announcer.Name, _text);
        await _sendToMap(victim.MapId, Opcode.S2C_PK_KILL_NOTICE, notice.ToArray()).ConfigureAwait(false);
    }

    private PkInfo Credit(ulong characterId, Func<PkCounters, PkCounters> rule)
    {
        var record = _store.GetOrCreate(characterId);
        var counters = rule(new PkCounters(record.KillTodayCount, record.KillTotalCount, record.DeathTodayCount, record.DeathTotalCount));
        return _store.Save(record with
        {
            KillTodayCount = counters.KillToday,
            KillTotalCount = counters.KillTotal,
            DeathTodayCount = counters.DeathToday,
            DeathTotalCount = counters.DeathTotal,
        });
    }

    private static byte[] RecordFrame(PkInfo record)
    {
        var w = new tPoseidon.Core.Net.PacketWriter(12);
        w.WriteUInt16((ushort)Math.Clamp(record.KillTodayCount, 0, ushort.MaxValue));
        w.WriteUInt16((ushort)Math.Clamp(record.DeathTodayCount, 0, ushort.MaxValue));
        w.WriteUInt32((uint)Math.Max(0, record.KillTotalCount));
        w.WriteUInt32((uint)Math.Max(0, record.DeathTotalCount));
        return w.ToArray();
    }

    public async Task EnteredZoneAsync(InGameSessionState st, bool modeOffAlreadySent)
    {
        ArgumentNullException.ThrowIfNull(st);
        var kind = _mapKind(st.MapId);
        if (kind is { Type: PkRules.DuelZoneType })
        {
            Console.WriteLine($"{_logTag} [pk] 0x{st.PlayerEntityId:X4} entered duel zone {st.MapId}: the duel zone path is not built");
        }
        var entry = PkRules.ZoneEntry(kind?.Type ?? 0, kind?.Option ?? 0);
        st.PvpMode = entry.PvpMode;
        st.PkRecordMode = entry.RecordMode;
        if (entry.PvpMode == PkRules.NoPvp)
        {
            if (!modeOffAlreadySent) await SendAsync(st, Opcode.S2C_PVP_MODE_OFF, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        if (entry.Protected) st.ProtectedUntil = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(_info().SuperTime);
        await SendAsync(st, Opcode.S2C_PVP_MODE_ON, new[] { entry.PvpMode, entry.RecordMode }).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [pk] 0x{st.PlayerEntityId:X4} entered PK zone {st.MapId}: record mode {entry.RecordMode}");
    }

    public async Task ToggleAsync(InGameSessionState st, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (payload.Length < 1) return;
        if (st.PvpMode != PkRules.PkZoneMode)
        {
            Console.WriteLine($"{_logTag} [pk] 0x{st.PlayerEntityId:X4} ERROR PK AREA : NOT ATTACK (map {st.MapId})");
            st.PkRecordMode = PkRules.RecordOff;
        }
        st.PkRecordMode = PkRules.Toggle(payload.Span[0] != 0);
        await SendAsync(st, Opcode.S2C_PK_RECORD_MODE, new[] { st.PkRecordMode }).ConfigureAwait(false);
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
