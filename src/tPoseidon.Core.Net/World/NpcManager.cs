using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed class NpcManager
{
    private readonly INpcTalkLookup _npcTalk;
    private readonly INpcPointProvider _points;
    private readonly WorldEntityStore _world;
    private readonly IdAllocator _ids;
    private readonly Action<string>? _log;
    private readonly IReadOnlyDictionary<(ushort MapId, ushort PosId), ushort>? _pinnedEntityIds;

        public const int RegularNpcMinChrType = 1000;
    public const int RegularNpcMaxChrType = 1499;
    public const int TimerNpcMinChrType = 17000;
    public const int TimerNpcMaxChrType = 17998;

    public NpcManager(
        INpcTalkLookup npcTalk,
        INpcPointProvider points,
        WorldEntityStore world,
        IdAllocator ids,
        Action<string>? log = null,
        IReadOnlyDictionary<(ushort MapId, ushort PosId), ushort>? pinnedEntityIds = null)
    {
        _npcTalk = npcTalk ?? throw new ArgumentNullException(nameof(npcTalk));
        _points = points ?? throw new ArgumentNullException(nameof(points));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _ids = ids ?? throw new ArgumentNullException(nameof(ids));
        _log = log;
        _pinnedEntityIds = pinnedEntityIds;
    }

        public static IReadOnlyDictionary<(ushort MapId, ushort PosId), ushort> CoralTownPinnedIds()
        => new Dictionary<(ushort, ushort), ushort>
        {
            { ((ushort)148, (ushort)2), (ushort)0x0153 },
            { ((ushort)148, (ushort)3), (ushort)0x014C },
        };

        public IReadOnlyList<NpcEntity> PreloadMap(ushort mapId, string libconfigMapName)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigMapName);

        var rows = _npcTalk.GetAllByMap(libconfigMapName);
        var created = new List<NpcEntity>(rows.Count);
        var missingPoints = 0;
        foreach (var row in rows)
        {
            var npc = CreateNpc(mapId, row, quietMissingPoint: true);
            if (npc is not null)
            {
                created.Add(npc);
                if (npc.Spawn.X == 0 && npc.Spawn.Y == 0) missingPoints++;
            }
        }
        if (missingPoints > 0)
        {
            _log?.Invoke($"NpcManager: map {mapId} ({libconfigMapName}) — {missingPoints}/{rows.Count} NPCs fell back to Origin (missing point catalog entries)");
        }
        return created;
    }

        public NpcEntity? CreateNpc(ushort mapId, NpcTalkEntry row, bool quietMissingPoint = false)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (!IsValidChrType(row.Type))
        {
            _log?.Invoke($"NpcManager: invalid chrtype {row.Type} (map {mapId} posId {row.PosId}); skipped");
            return null;
        }

        if (!_points.TryGet(mapId, (ushort)row.PosId, out var point))
        {
            if (!quietMissingPoint)
            {
                _log?.Invoke(
                    $"NpcManager: not found NPC Point, MapId={mapId}, MapName={row.MapName}, PointId={row.PosId} — fallback to Origin");
            }
            point = NpcPoint.Origin();
        }



        ushort entityId;
        if (_pinnedEntityIds is not null
            && _pinnedEntityIds.TryGetValue((mapId, (ushort)row.PosId), out var pinned))
        {
            entityId = pinned;
        }
        else
        {
            entityId = _ids.NewId();
        }

        var npc = new NpcEntity
        {
            EntityId = entityId,
            MapId = mapId,
            ChrType = row.Type,
            PosId = (ushort)row.PosId,
            Spawn = point,
            HeadMarkType = row.HeadMarkType,
            Source = row,
        };
        _world.Install(npc);
        return npc;
    }

    public NpcEntity? SummonNpc(ushort mapId, int chrType, NpcPoint spawn, IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (!IsValidChrType(chrType))
        {
            _log?.Invoke($"NpcManager: summon with invalid chrtype {chrType} (map {mapId}); skipped");
            return null;
        }

        var npc = new NpcEntity
        {
            EntityId = _ids.NewId(),
            MapId = mapId,
            ChrType = chrType,
            PosId = 0,
            Spawn = spawn,
            Source = lines,
        };
        _world.Install(npc);
        return npc;
    }

        public static bool IsValidChrType(int chrType)
        => (chrType >= RegularNpcMinChrType && chrType <= RegularNpcMaxChrType)
        || (chrType >= TimerNpcMinChrType && chrType <= TimerNpcMaxChrType);
}
