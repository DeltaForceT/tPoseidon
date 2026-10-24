using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.World;

public delegate Task SendTickFrame(Opcode opcode, byte[] payload, byte randKey, CancellationToken ct);

public sealed record TickSession(
    ulong SessionId,
    ushort PlayerX,
    ushort PlayerY,
    SendTickFrame Send);

public sealed class WorldTickBroadcaster
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly MonsterManager _monsters;
    private readonly MonsterAITicker _aiTicker;
    private readonly WorldEntityStore _npcs;
    private readonly WorldAreaOfInterest _aoi;
    private readonly Func<MonsterEntity, byte[]?>? _buildMobSpawn;
    private readonly Func<NpcEntity, byte[]?>? _buildNpcSpawn;
    private readonly Random _randKeySource = new();
    private readonly object _membershipGate = new();
    private readonly Dictionary<ulong, HashSet<ushort>> _visibleBySession = new();
    private bool _diagnosticLogged;

    public WorldTickBroadcaster(
        MonsterManager monsters,
        MonsterAITicker aiTicker,
        WorldEntityStore npcs,
        WorldAreaOfInterest? aoi = null,
        Func<MonsterEntity, byte[]?>? buildMobSpawn = null,
        Func<NpcEntity, byte[]?>? buildNpcSpawn = null)
    {
        _monsters = monsters;
        _aiTicker = aiTicker;
        _npcs = npcs;
        _aoi = aoi ?? new WorldAreaOfInterest();
        _buildMobSpawn = buildMobSpawn;
        _buildNpcSpawn = buildNpcSpawn;
    }

    public void ForgetSession(ulong sessionId)
    {
        lock (_membershipGate)
        {
            _visibleBySession.Remove(sessionId);
        }
    }

    public void PruneSessionsExcept(IReadOnlySet<ulong> liveSessionIds)
    {
        lock (_membershipGate)
        {
            var stale = _visibleBySession.Keys.Where(id => !liveSessionIds.Contains(id)).ToArray();
            foreach (var id in stale)
            {
                _visibleBySession.Remove(id);
            }
        }
    }

    public async Task BroadcastMapTickAsync(
        ushort mapId,
        IReadOnlyList<TickSession> sessions,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (sessions.Count == 0) return;

        var mobs = _monsters.GetByMap(mapId);
        var npcs = _npcs.InMap(mapId);
        if (mobs.Count == 0 && npcs.Count == 0) return;

        var moves = _aiTicker.Tick(mapId, now);
        var movedById = new Dictionary<ushort, MonsterMove>(moves.Count);
        foreach (var move in moves)
        {
            movedById[move.EntityId] = move;
        }

        foreach (var session in sessions)
        {
            if (ct.IsCancellationRequested) return;

            var visible = GetVisibleSet(session.SessionId);

            if (!_diagnosticLogged && mobs.Count > 0)
            {
                _diagnosticLogged = true;
                var m0 = mobs.First();
                var ddx = session.PlayerX - m0.CurrentX;
                var ddy = session.PlayerY - m0.CurrentY;
                var dist = Math.Sqrt(((double)ddx * ddx) + ((double)ddy * ddy));
                Console.WriteLine(
                    $"[AOI-DIAG] player=({session.PlayerX},{session.PlayerY}) " +
                    $"mob0 eid=0x{m0.EntityId:X4} cur=({m0.CurrentX},{m0.CurrentY}) " +
                    $"spawn=({m0.SpawnX},{m0.SpawnY}) dist={dist:F0} radius={_aoi.Radius} " +
                    $"mobsOnMap={mobs.Count}");
            }

            foreach (var mob in mobs)
            {
                if (ct.IsCancellationRequested) return;

                if (!_aoi.IsInRange(session.PlayerX, session.PlayerY, mob.CurrentX, mob.CurrentY))
                {
                    visible.Remove(mob.EntityId);
                    continue;
                }

                if (visible.Add(mob.EntityId) && _buildMobSpawn is not null)
                {
                    var spawnBytes = _buildMobSpawn(mob);
                    if (spawnBytes is not null)
                    {
                        await Send(session, Opcode.NPC_SPAWN, spawnBytes, ct).ConfigureAwait(false);
                    }
                }

                if (movedById.TryGetValue(mob.EntityId, out var move))
                {
                    var moveBytes = new EntityMovementPacket
                    {
                        EntityId = move.EntityId,
                        FromX = move.FromX,
                        FromY = move.FromY,
                        ToX = move.ToX,
                        ToY = move.ToY,
                        Flag = MonsterAITicker.WalkMovType,
                    }.EncodePayload();
                    await Send(session, Opcode.ENTITY_MOVE, moveBytes, ct).ConfigureAwait(false);
                }

                if (mob.TryAnnouncePathComplete(now))
                {
                    var pathDone = new Packet0010Tbd
                    {
                        EntityId = mob.EntityId,
                    }.EncodePayload();
                    await Send(session, Opcode.S2C_ENTITY_DESPAWN, pathDone, ct).ConfigureAwait(false);

                    var stopCommit = new Packet0019Tbd
                    {
                        EntityId = mob.EntityId,
                        WorldX = mob.CurrentX,
                        WorldY = mob.CurrentY,
                        StateFlag = Packet0019Tbd.StateFlagHalt,
                    }.EncodePayload();
                    await Send(session, Opcode.S2C_OP_0019_TBD, stopCommit, ct).ConfigureAwait(false);
                }

                var mobPing = new EntityTickPingPacket { EntityId = mob.EntityId }.EncodePayload();
                await Send(session, Opcode.S2C_OP_003F_TBD, mobPing, ct).ConfigureAwait(false);
            }

            foreach (var npc in npcs)
            {
                if (ct.IsCancellationRequested) return;

                var npcX = (ushort)Math.Clamp((int)Math.Round(npc.Spawn.X), 0, ushort.MaxValue);
                var npcY = (ushort)Math.Clamp((int)Math.Round(npc.Spawn.Y), 0, ushort.MaxValue);

                if (!_aoi.IsInRange(session.PlayerX, session.PlayerY, npcX, npcY))
                {
                    visible.Remove(npc.EntityId);
                    continue;
                }

                if (visible.Add(npc.EntityId) && _buildNpcSpawn is not null)
                {
                    var spawnBytes = _buildNpcSpawn(npc);
                    if (spawnBytes is not null)
                    {
                        await Send(session, Opcode.NPC_SPAWN, spawnBytes, ct).ConfigureAwait(false);
                    }
                }

                var npcPing = new EntityTickPingPacket { EntityId = npc.EntityId }.EncodePayload();
                await Send(session, Opcode.S2C_OP_003F_TBD, npcPing, ct).ConfigureAwait(false);
            }

            StoreVisibleSet(session.SessionId, visible);
        }
    }

    private HashSet<ushort> GetVisibleSet(ulong sessionId)
    {
        lock (_membershipGate)
        {
            return _visibleBySession.TryGetValue(sessionId, out var set)
                ? new HashSet<ushort>(set)
                : new HashSet<ushort>();
        }
    }

    private void StoreVisibleSet(ulong sessionId, HashSet<ushort> set)
    {
        lock (_membershipGate)
        {
            _visibleBySession[sessionId] = set;
        }
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
}
