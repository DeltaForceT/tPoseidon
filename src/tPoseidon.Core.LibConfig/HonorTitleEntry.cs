namespace tPoseidon.Core.LibConfig;

public sealed record HonorTitleEntry(int Id, int TitleType, int QuestType, int QuestId, int ClearCount, int Time, int HuntCount)
{
    public const string TableName = "HonorTitleInfo";
}
