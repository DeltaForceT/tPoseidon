using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class WorldSystemTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<WorldMapTables> _worldMapTables;
    private readonly Lazy<PortalLevelBands> _portalLevelBands;
    private readonly Lazy<TreasureMapTables> _treasureMapTables;
    private readonly Lazy<MissionTables> _missionTables;
    private readonly Lazy<PuzzleTables> _puzzleTables;
    private readonly Lazy<HarconDefTables> _harconDefTables;
    private readonly Lazy<ScenarioTables> _scenarioTables;
    private readonly Lazy<FiestaTables> _fiestaTables;
    private readonly Lazy<PartyZoneTables> _partyZoneTables;
    private readonly Lazy<IReadOnlyList<RegionInfoEntry>> _regionInfoTables;
    private readonly Lazy<IReadOnlyList<TeleportEntry>> _teleportInfo;
    private readonly Lazy<DrillTables> _drillTables;
    private readonly Lazy<SlotExtendTables> _slotExtendTables;

    public WorldSystemTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _worldMapTables = new Lazy<WorldMapTables>(reader.LoadWorldMapTables);
        _portalLevelBands = new Lazy<PortalLevelBands>(reader.LoadPortalLevelBands);
        _treasureMapTables = new Lazy<TreasureMapTables>(reader.LoadTreasureMapTables);
        _missionTables = new Lazy<MissionTables>(reader.LoadMissionTables);
        _puzzleTables = new Lazy<PuzzleTables>(reader.LoadPuzzleTables);
        _harconDefTables = new Lazy<HarconDefTables>(reader.LoadHarconDefTables);
        _scenarioTables = new Lazy<ScenarioTables>(reader.LoadScenarioTables);
        _fiestaTables = new Lazy<FiestaTables>(reader.LoadFiestaTables);
        _partyZoneTables = new Lazy<PartyZoneTables>(reader.LoadPartyZoneTables);
        _regionInfoTables = new Lazy<IReadOnlyList<RegionInfoEntry>>(reader.LoadAllRegionInfoTables);
        _teleportInfo = new Lazy<IReadOnlyList<TeleportEntry>>(reader.LoadTeleportInfo);
        _drillTables = new Lazy<DrillTables>(reader.LoadDrillTables);
        _slotExtendTables = new Lazy<SlotExtendTables>(reader.LoadSlotExtendTables);
    }

    public WorldMapTables WorldMapTables => _worldMapTables.Value;

    public PortalLevelBands PortalLevelBands => _portalLevelBands.Value;

    public TreasureMapTables TreasureMapTables => _treasureMapTables.Value;

    public MissionTables MissionTables => _missionTables.Value;

    public PuzzleTables PuzzleTables => _puzzleTables.Value;

    public HarconDefTables HarconDefTables => _harconDefTables.Value;

    public ScenarioTables ScenarioTables => _scenarioTables.Value;

    public FiestaTables FiestaTables => _fiestaTables.Value;

    public PartyZoneTables PartyZoneTables => _partyZoneTables.Value;

    public IReadOnlyList<RegionInfoEntry> RegionInfoTables => _regionInfoTables.Value;

    public IReadOnlyList<TeleportEntry> TeleportInfo => _teleportInfo.Value;

    public DrillTables DrillTables => _drillTables.Value;

    public SlotExtendTables SlotExtendTables => _slotExtendTables.Value;
}
