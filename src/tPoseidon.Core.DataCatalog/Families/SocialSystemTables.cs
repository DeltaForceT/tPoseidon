using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class SocialSystemTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<GuildRankBonusTable> _guildRankBonusTable;
    private readonly Lazy<GuildGameTypeTables> _guildGameTypeTables;
    private readonly Lazy<GuildGameRewardTables> _guildGameRewardTables;
    private readonly Lazy<PkInfoTables> _pkInfo;
    private readonly Lazy<CoupleTables> _coupleTables;
    private readonly Lazy<BeginnerRebirthInfo?> _beginnerRebirthInfo;
    private readonly Lazy<IReadOnlyList<LevelUpGiftEntry>> _levelUpGifts;
    private readonly Lazy<IReadOnlyDictionary<int, int>> _postSetup;
    private readonly Lazy<IReadOnlyList<(int Level, int NextTmExp)>> _tmLevels;

    public SocialSystemTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _guildRankBonusTable = new Lazy<GuildRankBonusTable>(reader.LoadGuildRankBonusTable);
        _guildGameTypeTables = new Lazy<GuildGameTypeTables>(reader.LoadGuildGameTypeTables);
        _guildGameRewardTables = new Lazy<GuildGameRewardTables>(reader.LoadGuildGameRewardTables);
        _pkInfo = new Lazy<PkInfoTables>(reader.LoadPkInfo);
        _coupleTables = new Lazy<CoupleTables>(reader.LoadCoupleTables);
        _beginnerRebirthInfo = new Lazy<BeginnerRebirthInfo?>(reader.LoadBeginnerRebirthInfo);
        _levelUpGifts = new Lazy<IReadOnlyList<LevelUpGiftEntry>>(reader.LoadLevelUpGifts);
        _postSetup = new Lazy<IReadOnlyDictionary<int, int>>(reader.LoadPostSetup);
        _tmLevels = new Lazy<IReadOnlyList<(int Level, int NextTmExp)>>(reader.LoadTmLevels);
    }

    public GuildRankBonusTable GuildRankBonusTable => _guildRankBonusTable.Value;

    public GuildGameTypeTables GuildGameTypeTables => _guildGameTypeTables.Value;

    public GuildGameRewardTables GuildGameRewardTables => _guildGameRewardTables.Value;

    public (int MinLevel, int MinGelder)? GuildSetupRow(string tableName, string server) => _reader.LoadGuildSetupRow(tableName, server);

    public PkInfoTables PkInfo => _pkInfo.Value;

    public CoupleTables CoupleTables => _coupleTables.Value;

    public BeginnerRebirthInfo? BeginnerRebirthInfo => _beginnerRebirthInfo.Value;

    public IReadOnlyList<LevelUpGiftEntry> LevelUpGifts => _levelUpGifts.Value;

    public IReadOnlyDictionary<int, int> PostSetup => _postSetup.Value;

    public IReadOnlyList<(int Level, int NextTmExp)> TmLevels => _tmLevels.Value;

    public IReadOnlyList<double> DailyTimeMinutes(string tableName) => _reader.LoadDailyTimeMinutes(tableName);
}
