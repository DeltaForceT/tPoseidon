using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneInitializer
{
    private readonly ItemManager _itemMgr;
    private readonly MonsterManager _monsterMgr;
    private readonly NpcManager _npcMgr;
    private readonly ActionManager _actionMgr;
    private readonly ShopManager? _shopMgr;
    private readonly SkillShopManager? _skillShopMgr;
    private readonly IRegionInfoLookup? _regions;
    private readonly IMonZoneLookup? _zonePool;
    private readonly string? _mapDirectory;
    private readonly Action<string>? _log;

    public ZoneInitializer(
        ItemManager itemMgr,
        MonsterManager monsterMgr,
        NpcManager npcMgr,
        ActionManager actionMgr,
        ShopManager? shopMgr = null,
        SkillShopManager? skillShopMgr = null,
        IRegionInfoLookup? regions = null,
        IMonZoneLookup? zonePool = null,
        string? mapDirectory = null,
        Action<string>? log = null)
    {
        _itemMgr = itemMgr ?? throw new ArgumentNullException(nameof(itemMgr));
        _monsterMgr = monsterMgr ?? throw new ArgumentNullException(nameof(monsterMgr));
        _npcMgr = npcMgr ?? throw new ArgumentNullException(nameof(npcMgr));
        _actionMgr = actionMgr ?? throw new ArgumentNullException(nameof(actionMgr));
        _shopMgr = shopMgr;
        _skillShopMgr = skillShopMgr;
        _regions = regions;
        _zonePool = zonePool;
        _mapDirectory = mapDirectory;
        _log = log;
    }

    public int LyrLoadCount { get; private set; }
    public int LyrMissingCount { get; private set; }

    public void Initialize(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (zone.IsInitialized) return;

        zone.Stage = new Stage
        {
            MapId = zone.MapId,
            Md3FileName = zone.Md3FileName,
            LyrFileName = ResolveLyrFileName(zone.Md3FileName),
        };
        zone.ShellMsgCaster = new ShellMsgCaster { MapId = zone.MapId };
        zone.ZoneMapAttr = new ZoneMapAttr { MapId = zone.MapId };
        zone.PathFinder = new PathFinder();

        zone.ItemMgr = new ZoneItemMgr(zone, _itemMgr);
        zone.ItemMgr.Init();

        zone.MonsterMgr = new ZoneMonsterMgr(zone, _monsterMgr, _regions, _zonePool);
        zone.MonsterMgr.Init();

        zone.NpcMgr = new ZoneNpcMgr(zone, _npcMgr);
        zone.NpcMgr.Init();

        zone.ShopMgr = new ZoneShopMgr(zone, _shopMgr);
        zone.ShopMgr.Init();

        zone.SkillShopMgr = new ZoneSkillShopMgr(zone, _skillShopMgr);
        zone.SkillShopMgr.Init();

        zone.ActionMgr = new ZoneActionMgr(zone, _actionMgr);
        zone.ActionMgr.Init();

        zone.TrapMgr = new ZoneTrapMgr(zone);
        zone.TrapMgr.Init();

        zone.SystemAgency = new ZoneSystemAgency(zone);
        zone.SystemAgency.Init();

        LoadMap(zone);

        zone.ShopMgr.CreateAll();
        zone.SkillShopMgr.CreateAll();

        zone.IsInitialized = true;
    }

    private static string ResolveLyrFileName(string md3FileName)
    {
        if (string.IsNullOrEmpty(md3FileName)) return string.Empty;
        var ext = Path.GetExtension(md3FileName);
        return string.IsNullOrEmpty(ext)
            ? md3FileName + ".lyr"
            : md3FileName[..^ext.Length] + ".lyr";
    }

    private void LoadMap(Zone zone)
    {
        if (zone.Stage is null || zone.PathFinder is null || zone.ZoneMapAttr is null) return;
        zone.Stage.MarkLoaded();
        zone.PathFinder.AcquireStage(zone.Stage);

        if (_mapDirectory is null) return;
        if (string.IsNullOrEmpty(zone.Stage.LyrFileName)) return;

        var lyrPath = Path.Combine(_mapDirectory, zone.Stage.LyrFileName);
        var groundMap = LyrReader.TryLoad(lyrPath);
        if (groundMap is null)
        {
            LyrMissingCount++;
            return;
        }
        zone.ZoneMapAttr.LoadBitmap(groundMap.Tiles);
        zone.PathFinder.LoadGroundAttribute(groundMap);
        LyrLoadCount++;
    }
}
