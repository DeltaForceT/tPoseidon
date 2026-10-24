using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Missions;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;

internal sealed class MissionController
{
    private readonly Func<MissionTables> _tables;
    private readonly MonsterManager _monsters;
    private readonly IReadOnlyDictionary<ushort, ZoneGeometry> _geometry;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<MonsterEntity, byte[]> _spawnFrame;
    private readonly Func<ushort, byte[]> _despawnFrame;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly string _logTag;
    private readonly Random _rng = new();

    public MissionController(
        Func<MissionTables> tables, MonsterManager monsters, IReadOnlyDictionary<ushort, ZoneGeometry> geometry,
        Func<IReadOnlyCollection<InGameSessionState>> sessions, Func<MonsterEntity, byte[]> spawnFrame, Func<ushort, byte[]> despawnFrame,
        Func<ushort, Opcode, byte[], Task> sendToMap, string logTag)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _spawnFrame = spawnFrame ?? throw new ArgumentNullException(nameof(spawnFrame));
        _despawnFrame = despawnFrame ?? throw new ArgumentNullException(nameof(despawnFrame));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> CheckAsync(InGameSessionState st, IReadOnlyList<ushort> args)
    {
        ArgumentNullException.ThrowIfNull(st);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count < 1 || !_tables().Missions.TryGetValue(args[0], out var mission)) return NpcScript.Refused;
        st.MissionOffer = mission.Id;
        await SendAsync(st, Opcode.S2C_MISSION_GUIDE, new[] { (byte)mission.Id }).ConfigureAwait(false);
        Log(st, $"opens the guide of mission {mission.Id}");
        return NpcScript.Silent;
    }

    public async Task StartAsync(InGameSessionState st)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (st.Character is not { } character || st.RequestZoneChange is not { } requestZoneChange) return;
        var tables = _tables();
        if (st.MissionOffer is not { } offered || !tables.Missions.TryGetValue(offered, out var mission)
            || !tables.Waves.TryGetValue(mission.MonsterTable, out var waves))
        {
            await RefuseAsync(st, "no mission offered").ConfigureAwait(false);
            return;
        }
        var level = st.Stats?.Level ?? character.Level;
        var mapTaken = _sessions().Any(other => other.Mission is not null && other.Mission.Mission.PlayMapId == mission.PlayMapId);
        if (!MissionRules.InBand(mission, level) || st.Mission is not null || st.IsDead || st.Trade is not null || mapTaken)
        {
            await RefuseAsync(st, $"mission {mission.Id} refused (level {level}, map {mission.PlayMapId} {(mapTaken ? "in use" : "free")})").ConfigureAwait(false);
            return;
        }
        st.MissionOffer = null;
        st.Mission = new MissionRun(mission, waves, st.MapId, st.PlayerX, st.PlayerY);
        await SendAsync(st, Opcode.S2C_MISSION_GUIDE_CLOSED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(st, $"starts mission {mission.Id}: {waves.Count} waves on map {mission.PlayMapId} for {mission.MissionMinutes} min");
        await requestZoneChange(new ZoneChangeRequest((ushort)mission.PlayMapId, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), default).ConfigureAwait(false);
    }

    public async Task EnteredZoneAsync(InGameSessionState st)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (st.Mission is not { } run) return;
        if (st.MapId != run.Mission.PlayMapId)
        {
            if (run.Phase != MissionPhase.Travelling)
            {
                await ClearAsync(run).ConfigureAwait(false);
                st.Mission = null;
                Log(st, $"left the mission map: mission {run.Mission.Id} ended");
            }
            return;
        }
        if (run.Start(DateTimeOffset.UtcNow) is not { } wave) return;
        await SendAsync(st, Opcode.S2C_MISSION_STARTED, Array.Empty<byte>()).ConfigureAwait(false);
        await SpawnAsync(run, wave).ConfigureAwait(false);
    }

    public async Task TickAsync(InGameSessionState st, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (st.Mission is not { } run) return;
        if (run.Phase == MissionPhase.Running)
        {
            if (run.TimedOut(now))
            {
                await ClearAsync(run).ConfigureAwait(false);
                await SendAsync(st, Opcode.S2C_MISSION_RESULT, new[] { MissionRules.Failed }).ConfigureAwait(false);
                Log(st, $"mission {run.Mission.Id} timed out at wave {run.WaveIndex + 1}");
                return;
            }
            if (run.WaveMonsters.Count > 0 && run.WaveMonsters.All(id => _monsters.Get(id) is not { IsDead: false }))
            {
                if (run.WaveCleared(now) is { } next)
                {
                    await SpawnAsync(run, next).ConfigureAwait(false);
                }
                else
                {
                    await SendAsync(st, Opcode.S2C_MISSION_RESULT, new[] { MissionRules.Succeeded }).ConfigureAwait(false);
                    Log(st, $"mission {run.Mission.Id} cleared");
                }
            }
            return;
        }
        if (!run.ReadyToLeave(now) || st.RequestZoneChange is not { } requestZoneChange) return;
        st.Mission = null;
        await SendAsync(st, Opcode.S2C_MISSION_UI_OFF, Array.Empty<byte>()).ConfigureAwait(false);
        await requestZoneChange(new ZoneChangeRequest(run.ReturnMap, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, (run.ReturnX, run.ReturnY)), default).ConfigureAwait(false);
    }

    public Task LeftAsync(InGameSessionState st)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (st.Mission is not { Phase: not MissionPhase.Travelling } run) return Task.CompletedTask;
        st.Mission = null;
        return ClearAsync(run);
    }

    private async Task SpawnAsync(MissionRun run, MissionWave wave)
    {
        var mapId = (ushort)run.Mission.PlayMapId;
        _geometry.TryGetValue(mapId, out var geometry);
        var spawned = new List<ushort>();
        foreach (var (monsterId, regionId) in wave.Monsters)
        {
            var region = geometry?.Regions.FirstOrDefault(candidate => candidate.Id == regionId);
            if (region is not { } area)
            {
                Console.WriteLine($"{_logTag} [mission] map {mapId} has no region {regionId} for monster {monsterId}");
                continue;
            }
            var x = (ushort)((area.X1 + area.X2) / 2);
            var y = (ushort)((area.Y1 + area.Y2) / 2);
            if (_monsters.Spawn(monsterId, mapId, x, y, null, null, regionId) is not { } monster) continue;
            spawned.Add(monster.EntityId);
            await _sendToMap(mapId, Opcode.NPC_SPAWN, _spawnFrame(monster)).ConfigureAwait(false);
        }
        run.Spawned(spawned);
        Console.WriteLine($"{_logTag} [mission] mission {run.Mission.Id} wave {wave.Wave}: {spawned.Count} monsters");
    }

    private async Task ClearAsync(MissionRun run)
    {
        var mapId = (ushort)run.Mission.PlayMapId;
        foreach (var id in run.WaveMonsters.ToArray())
        {
            if (!_monsters.Remove(id)) continue;
            await _sendToMap(mapId, Opcode.S2C_ENTITY_DESPAWN, _despawnFrame(id)).ConfigureAwait(false);
        }
    }

    private Task RefuseAsync(InGameSessionState st, string why)
    {
        Log(st, why);
        return SendAsync(st, Opcode.S2C_MISSION_ERROR, new[] { MissionRules.CannotProceed });
    }

    private void Log(InGameSessionState st, string what) =>
        Console.WriteLine($"{_logTag} [mission] 0x{st.PlayerEntityId:X4} {what}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
