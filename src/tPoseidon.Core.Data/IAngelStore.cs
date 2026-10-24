namespace tPoseidon.Core.Data;

public sealed record AngelRelation(ulong AngelCharacterId, string Name);

public sealed record AngelResult(int GraduationCount, DateTimeOffset LatestGraduation);

public sealed record AngelGraduation(bool Graduated, IReadOnlyList<ulong> Angels, int Gifts);

public enum AngelStoreError
{
    None = 0,
    InsertFailed = 101,
    ResultInitFailed = 102,
    AlreadyListed = 103,
}

public interface IAngelStore
{
    IReadOnlyList<AngelRelation> Angels(ulong newbieCharacterId);

    AngelStoreError Add(ulong newbieCharacterId, ulong angelCharacterId);

    bool Remove(ulong newbieCharacterId, ulong angelCharacterId);

    AngelResult? Result(ulong angelCharacterId);

    AngelGraduation Graduate(ulong newbieCharacterId, uint newbieAccountId, DateTimeOffset at);
}
