namespace tPoseidon.Core.LibConfig;

public sealed record LevelUpGiftEntry(int Id, int Level, int CharacterTypeMask, string Sender, string Title, string Message, IReadOnlyList<(int ItemId, int Count)> Items)
{
    public const int MaskedTypes = 24;

    public bool Fits(int level, int characterType) =>
        level == Level && characterType >= 1 && characterType <= MaskedTypes && (CharacterTypeMask & (1 << (characterType - 1))) != 0;
}
