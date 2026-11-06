using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class MonsterTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<IReadOnlyList<MonsterParamEntry>> _monsterParamEx2;
    private readonly Lazy<IReadOnlyList<MonItemDropInfoEntry>> _monItemDropInfo;
    private readonly Lazy<IReadOnlyList<MonItemDropInfoEntry>> _monsterDropRows;
    private readonly Lazy<IReadOnlyList<MonItemTableDropEntry>> _monsterDropTables;
    private readonly Lazy<IReadOnlyList<MonsterDebuffResistEntry>> _monsterDebuffResists;
    private readonly Lazy<IReadOnlyList<MonsterTacticEntry>> _monsterTactics;
    private readonly Lazy<IReadOnlyList<TacticsMonEntry>> _tacticsMonTables;
    private readonly Lazy<IReadOnlyList<MonAITacticsEntry>> _monAITacticsTables;
    private readonly Lazy<IReadOnlyList<MonsterQuestInfoEntry>> _monsterQuestInfo;
    private readonly Lazy<IReadOnlyList<MonsterQuestResultEntry>> _monsterQuestResultTables;
    private readonly Lazy<IReadOnlyList<BossGiveItemEntry>> _bossGiveItems;
    private readonly Lazy<IReadOnlyList<BossLifeEntry>> _bossLives;
    private readonly Lazy<MonsterMatchTables> _monsterMatchTables;
    private readonly Lazy<ShadowAiTable> _shadowAi;
    private readonly Lazy<IReadOnlyList<ShadowTimerEntry>> _shadowTimers;
    private readonly Lazy<ShadowEventTables> _shadowEventTables;

    public MonsterTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _monsterParamEx2 = new Lazy<IReadOnlyList<MonsterParamEntry>>(reader.LoadMonsterParamEx2);
        _monItemDropInfo = new Lazy<IReadOnlyList<MonItemDropInfoEntry>>(reader.LoadMonItemDropInfo);
        _monsterDropRows = new Lazy<IReadOnlyList<MonItemDropInfoEntry>>(reader.LoadMonsterDropRows);
        _monsterDropTables = new Lazy<IReadOnlyList<MonItemTableDropEntry>>(reader.LoadMonsterDropTables);
        _monsterDebuffResists = new Lazy<IReadOnlyList<MonsterDebuffResistEntry>>(reader.LoadMonsterDebuffResists);
        _monsterTactics = new Lazy<IReadOnlyList<MonsterTacticEntry>>(reader.LoadMonsterTactics);
        _tacticsMonTables = new Lazy<IReadOnlyList<TacticsMonEntry>>(reader.LoadAllTacticsMonTables);
        _monAITacticsTables = new Lazy<IReadOnlyList<MonAITacticsEntry>>(reader.LoadAllMonAITacticsTables);
        _monsterQuestInfo = new Lazy<IReadOnlyList<MonsterQuestInfoEntry>>(reader.LoadMonsterQuestInfo);
        _monsterQuestResultTables = new Lazy<IReadOnlyList<MonsterQuestResultEntry>>(reader.LoadMonsterQuestResultTables);
        _bossGiveItems = new Lazy<IReadOnlyList<BossGiveItemEntry>>(reader.LoadBossGiveItems);
        _bossLives = new Lazy<IReadOnlyList<BossLifeEntry>>(reader.LoadBossLives);
        _monsterMatchTables = new Lazy<MonsterMatchTables>(reader.LoadMonsterMatchTables);
        _shadowAi = new Lazy<ShadowAiTable>(reader.LoadShadowAi);
        _shadowTimers = new Lazy<IReadOnlyList<ShadowTimerEntry>>(reader.LoadShadowTimers);
        _shadowEventTables = new Lazy<ShadowEventTables>(reader.LoadShadowEventTables);
    }

    public IReadOnlyList<MonsterParamEntry> MonsterParamEx2 => _monsterParamEx2.Value;

    public IReadOnlyList<MonItemDropInfoEntry> MonItemDropInfo => _monItemDropInfo.Value;

    public IReadOnlyList<MonItemDropInfoEntry> MonsterDropRows => _monsterDropRows.Value;

    public IReadOnlyList<MonItemTableDropEntry> MonsterDropTables => _monsterDropTables.Value;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, int>> MonsterProtections(IEnumerable<string> tableNames) => _reader.LoadMonsterProtections(tableNames);

    public IReadOnlyList<MonsterDebuffResistEntry> MonsterDebuffResists => _monsterDebuffResists.Value;

    public IReadOnlyList<MonsterTacticEntry> MonsterTactics => _monsterTactics.Value;

    public IReadOnlyList<TacticsMonEntry> TacticsMonTables => _tacticsMonTables.Value;

    public IReadOnlyList<MonAITacticsEntry> MonAITacticsTables => _monAITacticsTables.Value;

    public IReadOnlyList<MonsterQuestInfoEntry> MonsterQuestInfo => _monsterQuestInfo.Value;

    public IReadOnlyList<MonsterQuestResultEntry> MonsterQuestResultTables => _monsterQuestResultTables.Value;

    public IReadOnlyList<BossGiveItemEntry> BossGiveItems => _bossGiveItems.Value;

    public IReadOnlyList<BossLifeEntry> BossLives => _bossLives.Value;

    public MonsterMatchTables MonsterMatchTables => _monsterMatchTables.Value;

    public ShadowAiTable ShadowAi => _shadowAi.Value;

    public IReadOnlyList<ShadowTimerEntry> ShadowTimers => _shadowTimers.Value;

    public ShadowEventTables ShadowEventTables => _shadowEventTables.Value;
}
