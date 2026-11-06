using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class GuardianSoulTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<GuardianTables> _guardianTables;
    private readonly Lazy<SoulSeedTables> _soulSeedTables;
    private readonly Lazy<SoulSystemTables> _soulSystemTables;
    private readonly Lazy<GhostExchangeTables> _ghostExchangeTables;
    private readonly Lazy<IReadOnlyList<HonorTitleEntry>> _honorTitles;
    private readonly Lazy<IReadOnlyList<MemoryPortEntry>> _memoryPorts;
    private readonly Lazy<CardIdentifyTables> _cardIdentifyTables;
    private readonly Lazy<IReadOnlyList<CardSpecialSetEntry>> _cardSpecialSets;

    public GuardianSoulTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _guardianTables = new Lazy<GuardianTables>(reader.LoadGuardianTables);
        _soulSeedTables = new Lazy<SoulSeedTables>(reader.LoadSoulSeedTables);
        _soulSystemTables = new Lazy<SoulSystemTables>(reader.LoadSoulSystemTables);
        _ghostExchangeTables = new Lazy<GhostExchangeTables>(reader.LoadGhostExchangeTables);
        _honorTitles = new Lazy<IReadOnlyList<HonorTitleEntry>>(reader.LoadHonorTitles);
        _memoryPorts = new Lazy<IReadOnlyList<MemoryPortEntry>>(reader.LoadMemoryPorts);
        _cardIdentifyTables = new Lazy<CardIdentifyTables>(reader.LoadCardIdentifyTables);
        _cardSpecialSets = new Lazy<IReadOnlyList<CardSpecialSetEntry>>(reader.LoadCardSpecialSets);
    }

    public GuardianTables GuardianTables => _guardianTables.Value;

    public SoulSeedTables SoulSeedTables => _soulSeedTables.Value;

    public SoulSystemTables SoulSystemTables => _soulSystemTables.Value;

    public GhostExchangeTables GhostExchangeTables => _ghostExchangeTables.Value;

    public IReadOnlyList<HonorTitleEntry> HonorTitles => _honorTitles.Value;

    public IReadOnlyList<MemoryPortEntry> MemoryPorts => _memoryPorts.Value;

    public CardIdentifyTables CardIdentifyTables => _cardIdentifyTables.Value;

    public IReadOnlyList<CardSpecialSetEntry> CardSpecialSets => _cardSpecialSets.Value;
}
