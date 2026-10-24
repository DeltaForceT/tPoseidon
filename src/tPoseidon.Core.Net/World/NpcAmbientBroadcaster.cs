using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.World;

public sealed class NpcAmbientBroadcaster
{
    private const int AniTimeMillisecondsScale = 1000;

    private static readonly TimeSpan DefaultReemitPeriod = TimeSpan.FromMilliseconds(10000);

    private readonly WorldEntityStore _npcs;
    private readonly INpcActionScriptLookup _scripts;
    private readonly WorldAreaOfInterest _aoi;
    private readonly Random _randKeySource = new();
    private readonly object _gate = new();
    private readonly Dictionary<ushort, NpcAmbientCursor> _cursorByEntity = new();
    private bool _diagnosticLogged;
    private int _advanceLogCount;

    public NpcAmbientBroadcaster(
        WorldEntityStore npcs,
        INpcActionScriptLookup scripts,
        WorldAreaOfInterest? aoi = null)
    {
        ArgumentNullException.ThrowIfNull(npcs);
        ArgumentNullException.ThrowIfNull(scripts);
        _npcs = npcs;
        _scripts = scripts;
        _aoi = aoi ?? new WorldAreaOfInterest();
    }

    public async Task BroadcastMapAmbientAsync(
        ushort mapId,
        IReadOnlyList<TickSession> sessions,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (sessions.Count == 0) return;

        var npcs = _npcs.InMap(mapId);
        if (npcs.Count == 0) return;

        foreach (var npc in npcs)
        {
            if (ct.IsCancellationRequested) return;

            var script = _scripts.GetByType(npc.ChrType);
            if (script is null || script.Steps.Count == 0) continue;

            if (!_diagnosticLogged)
            {
                _diagnosticLogged = true;
                var steps = string.Join(",", script.Steps.Select(s =>
                    $"A{s.Action}/L{s.Loop}/T{s.AniTime}s/sp{(s.Speech.Length > 0 ? 1 : 0)}"));
                Console.WriteLine(
                    $"[AMBIENT-DIAG] npc eid=0x{npc.EntityId:X4} chrType={npc.ChrType} " +
                    $"MaxCnt={script.MaxCnt} steps={script.Steps.Count} [{steps}] " +
                    $"reemit0={ReemitDelayFor(script.Steps[0]).TotalMilliseconds:F0}ms");
            }

            if (!TryAdvance(npc.EntityId, script, now, out var step)) continue;

            if (_advanceLogCount < 20)
            {
                _advanceLogCount++;
                Console.WriteLine(
                    $"[AMBIENT-FIRE] t={now:HH:mm:ss.fff} eid=0x{npc.EntityId:X4} " +
                    $"step A{step.Action}/T{step.AniTime}s npcsOnMap={npcs.Count}");
            }

            var npcX = (ushort)Math.Clamp((int)Math.Round(npc.Spawn.X), 0, ushort.MaxValue);
            var npcY = (ushort)Math.Clamp((int)Math.Round(npc.Spawn.Y), 0, ushort.MaxValue);

            var routineBytes = new Packet01CFTbd
            {
                EntityId = npc.EntityId,
                Action = (byte)step.Action,
                AniTimeSeconds = (ushort)step.AniTime,
                Loop = (byte)step.Loop,
            }.EncodePayload();

            byte[]? speechBytes = null;
            if (step.Speech.Length != 0)
            {
                speechBytes = new Packet01D0Tbd
                {
                    EntityId = npc.EntityId,
                    Speech = step.Speech,
                }.EncodePayload();
            }

            foreach (var session in sessions)
            {
                if (ct.IsCancellationRequested) return;
                if (!_aoi.IsInRange(session.PlayerX, session.PlayerY, npcX, npcY)) continue;

                await Send(session, Opcode.S2C_OP_01CF_TBD, routineBytes, ct).ConfigureAwait(false);
                if (speechBytes is not null)
                {
                    await Send(session, Opcode.S2C_OP_01D0_TBD, speechBytes, ct).ConfigureAwait(false);
                }
            }
        }
    }

    private bool TryAdvance(
        ushort entityId,
        NpcActionScriptEntry script,
        DateTimeOffset now,
        out NpcActionScriptStep step)
    {
        var activeCount = Math.Min(script.MaxCnt, script.Steps.Count);
        if (activeCount <= 0)
        {
            step = default;
            return false;
        }

        lock (_gate)
        {
            if (!_cursorByEntity.TryGetValue(entityId, out var cursor))
            {
                var phase = TimeSpan.FromTicks(
                    (long)((entityId * 2654435761UL) % (ulong)DefaultReemitPeriod.Ticks));
                cursor = new NpcAmbientCursor
                {
                    StepIndex = 0,
                    NextEmit = now + phase,
                };
            }

            if (cursor.StepIndex >= activeCount)
            {
                cursor.StepIndex = 0;
            }

            if (now < cursor.NextEmit)
            {
                _cursorByEntity[entityId] = cursor;
                step = default;
                return false;
            }

            step = script.Steps[cursor.StepIndex];
            cursor.NextEmit = now + ReemitDelayFor(step);
            cursor.StepIndex = (cursor.StepIndex + 1) % activeCount;
            _cursorByEntity[entityId] = cursor;
            return true;
        }
    }

    private static TimeSpan ReemitDelayFor(NpcActionScriptStep step)
    {
        if (step.AniTime <= 0)
        {
            return DefaultReemitPeriod;
        }
        return TimeSpan.FromMilliseconds(step.AniTime * AniTimeMillisecondsScale);
    }

    private async Task Send(
        TickSession session,
        Opcode opcode,
        byte[] payload,
        CancellationToken ct)
    {
        byte rk;
        lock (_randKeySource) rk = (byte)_randKeySource.Next(1, 256);
        try
        {
            await session.Send(opcode, payload, rk, ct).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private struct NpcAmbientCursor
    {
        public int StepIndex;
        public DateTimeOffset NextEmit;
    }
}
