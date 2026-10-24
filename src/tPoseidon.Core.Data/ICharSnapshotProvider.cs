namespace tPoseidon.Core.Data;

public interface ICharSnapshotProvider
{
    CharacterSnapshot? GetSnapshot(ulong characterId);
}

public sealed class LocalCharSnapshotProvider : ICharSnapshotProvider
{
    private readonly CharacterSnapshotLoader _loader;

    public LocalCharSnapshotProvider(CharacterSnapshotLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);
        _loader = loader;
    }

    public CharacterSnapshot? GetSnapshot(ulong characterId) => _loader.Load(characterId);
}
