namespace tPoseidon.Core.Net.Social;

public static class GameMasterChatBlocks
{
    public const int ReleaseValue = 0;
    public const int PermanentValue = -1;
    public const uint MinutesPerDay = 24 * 60;
    public const uint PermanentDays = 15;
    public const uint PermanentMinutes = PermanentDays * MinutesPerDay;

    public const string ReleaseBlockText = "GM_ReleaseBlock";
    public const string BlockReasonText = "GM_BlockReason";
    public const string BlockNoReasonText = "GM_BlockNoReason";
    public const string PermanentBlockNoReasonText = "GM_PermBlockNoReason";
    public const string ListLineText = "GM_TypeSearchListOff";
    public const string ListCountText = "GM_TypeSearchResult";

    public sealed record TypeTexts(string Timed, string Permanent, string Released);

    public static byte? TypeOf(string command) => command switch
    {
        GameMasterChat.ChatBlock => GameMasterBlocks.Chat,
        GameMasterChat.TradeBlock => GameMasterBlocks.Trade,
        GameMasterChat.MoveBlock => GameMasterBlocks.Move,
        GameMasterChat.BlockMyCamp => GameMasterBlocks.MyCamp,
        GameMasterChat.BlockMyShop => GameMasterBlocks.PersonalShop,
        GameMasterChat.BlockMyChat => GameMasterBlocks.ChatRoom,
        _ => null,
    };

    public static (byte Type, string Header)? ListOf(string command) => command switch
    {
        GameMasterChat.ChatBlockList => (GameMasterBlocks.Chat, "GM_GetAllUserChatBlock"),
        GameMasterChat.MoveBlockList => (GameMasterBlocks.Move, "GM_GetAllUserMoveBlock"),
        GameMasterChat.TradeBlockList => (GameMasterBlocks.Trade, "GM_GetAllUserTradeBlock"),
        _ => null,
    };

    public static TypeTexts TextsOf(byte type) => type switch
    {
        GameMasterBlocks.Chat => new("GM_ChatBlock", "GM_PermChatBlock", "GM_ReleaseChatBlock"),
        GameMasterBlocks.Trade => new("GM_BlockTrade", "GM_PermBlockTrade", "GM_ReleaseBlockTrade"),
        GameMasterBlocks.Move => new("GM_BlockMove", "GM_PermBlockMove", "GM_ReleaseBlockMove"),
        GameMasterBlocks.MyCamp => new("GM_BlockMyCamp", "GM_PermBlockMyCamp", "GM_ReleaseBlockMyCamp"),
        GameMasterBlocks.PersonalShop => new("GM_BlockMyShop", "GM_PermBlockMyShop", "GM_ReleaseBlockMyShop"),
        GameMasterBlocks.ChatRoom => new("GM_BlockMyChat", "GM_PermBlockMyChat", "GM_ReleaseBlockMyChat"),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a timed block type."),
    };
}
