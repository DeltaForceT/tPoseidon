namespace tPoseidon.Core.Data;

public interface IGmBlockStore
{
    int BlockCharacter(ulong characterId, byte blockType, ushort minutes, ulong gameMasterId);

    int UnblockCharacter(ulong characterId, byte blockType, ulong gameMasterId);

    int SetUserAuthority(string characterName, byte authority);

    GmRunningBlock? LatestRunningBlock(ulong characterId);

    IReadOnlyList<GmActiveBlock> ActiveBlocks();
}

public readonly record struct GmRunningBlock(byte Type, int MinutesLeft);

public readonly record struct GmActiveBlock(byte Type, ulong CharacterId, string Name, int MinutesLeft, DateTimeOffset From);

public static class GmBlockRules
{
    public const int Ok = 0;
    public const int NoCharacter = 8998;
    public const int NoBlockRow = 8001;
    public const int NoEarlierBlock = 8003;
    public const int NoMasterAnswer = -1;
    public const int MasterCancelled = -2;
    public const byte ByGameMasterCharacter = 1;
    public const byte NoReason = 0;
    public const string GameMasterCharacterDesc = "GM-CHAR";
    public const string GameMasterCharacterNote = "GM_CHAR";
    public const byte NormalUser = 0;
    public const byte SanctionedUser = 1;
    public const int LoginBlockSlots = 7;
    public const byte FirstLoginBlockType = 2;
    public const byte LastLoginBlockType = 5;

    public static ushort[] LoginBlockMinutes(GmRunningBlock? block)
    {
        var minutes = new ushort[LoginBlockSlots];
        if (block is { } running && running.Type is >= FirstLoginBlockType and <= LastLoginBlockType)
        {
            minutes[running.Type - FirstLoginBlockType] = unchecked((ushort)running.MinutesLeft);
        }
        return minutes;
    }
}
