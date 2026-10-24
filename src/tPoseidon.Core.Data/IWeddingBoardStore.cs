namespace tPoseidon.Core.Data;

public sealed record WeddingArticle(
    ulong CoupleCode,
    byte CardType,
    int DressType,
    byte MaleHair,
    byte FemaleHair,
    byte MaleJob,
    byte FemaleJob,
    string CardMessage,
    string Title,
    string MaleName,
    string FemaleName);

public enum WeddingArticleSave
{
    Failed,
    Created,
    Updated,
}

public interface IWeddingBoardStore
{
    IReadOnlyList<WeddingArticle> Articles();

    bool HasArticle(ulong coupleCode);

    bool DeleteArticle(ulong coupleCode);

    WeddingArticleSave SaveArticle(WeddingArticle article, DateTimeOffset at);
}
