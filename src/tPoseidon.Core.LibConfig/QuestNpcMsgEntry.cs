namespace tPoseidon.Core.LibConfig;

public sealed record QuestNpcMsgEntry(int QuestId, string NpcTable)
{
    public const string TableName = "Quest_NpcMsg";
}
