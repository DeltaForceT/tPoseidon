using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class NpcTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<IReadOnlyList<NpcTalkEntry>> _npcTalkTables;
    private readonly Lazy<IReadOnlyList<NpcMsgEntry>> _npcMsgTables;
    private readonly Lazy<IReadOnlyList<NpcActionScriptEntry>> _npcActionScript;
    private readonly Lazy<IReadOnlyList<NpcActionScriptEntry>> _shopNpcActionScript;
    private readonly Lazy<IReadOnlyList<NpcActionScriptEntry>> _skillShopNpcActionScript;
    private readonly Lazy<IReadOnlyList<NpcMoveEntry>> _npcMoves;
    private readonly Lazy<IReadOnlyList<NpcTimerEntry>> _npcTimers;
    private readonly Lazy<IReadOnlyList<CharacterInfoEntry>> _characterInfo;
    private readonly Lazy<IReadOnlyList<TutorialSystemEntry>> _tutorialSystem;

    public NpcTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _npcTalkTables = new Lazy<IReadOnlyList<NpcTalkEntry>>(reader.LoadAllNpcTalkTables);
        _npcMsgTables = new Lazy<IReadOnlyList<NpcMsgEntry>>(reader.LoadAllNpcMsgTables);
        _npcActionScript = new Lazy<IReadOnlyList<NpcActionScriptEntry>>(reader.LoadNpcActionScript);
        _shopNpcActionScript = new Lazy<IReadOnlyList<NpcActionScriptEntry>>(reader.LoadShopNpcActionScript);
        _skillShopNpcActionScript = new Lazy<IReadOnlyList<NpcActionScriptEntry>>(reader.LoadSkillShopNpcActionScript);
        _npcMoves = new Lazy<IReadOnlyList<NpcMoveEntry>>(reader.LoadNpcMoves);
        _npcTimers = new Lazy<IReadOnlyList<NpcTimerEntry>>(reader.LoadNpcTimers);
        _characterInfo = new Lazy<IReadOnlyList<CharacterInfoEntry>>(reader.LoadCharacterInfo);
        _tutorialSystem = new Lazy<IReadOnlyList<TutorialSystemEntry>>(reader.LoadTutorialSystem);
    }

    public IReadOnlyList<NpcTalkEntry> NpcTalkTables => _npcTalkTables.Value;

    public IReadOnlyList<NpcMsgEntry> NpcMsgTables => _npcMsgTables.Value;

    public IReadOnlyList<NpcActionScriptEntry> NpcActionScript => _npcActionScript.Value;

    public IReadOnlyList<NpcActionScriptEntry> ShopNpcActionScript => _shopNpcActionScript.Value;

    public IReadOnlyList<NpcActionScriptEntry> SkillShopNpcActionScript => _skillShopNpcActionScript.Value;

    public IReadOnlyList<NpcMoveEntry> NpcMoves => _npcMoves.Value;

    public IReadOnlyList<NpcTimerEntry> NpcTimers => _npcTimers.Value;

    public IReadOnlyDictionary<int, IReadOnlyList<(int MonsterId, int Count)>> NpcSummonSets(string tableName, int columns) => _reader.LoadNpcSummonSets(tableName, columns);

    public IReadOnlyList<CharacterInfoEntry> CharacterInfo => _characterInfo.Value;

    public IReadOnlyList<TutorialSystemEntry> TutorialSystem => _tutorialSystem.Value;
}
