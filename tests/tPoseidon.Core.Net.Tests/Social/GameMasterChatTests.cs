using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GameMasterChatTests
{
    private static readonly Dictionary<string, string> Keywords = new()
    {
        ["Login"] = "login",
        ["Logout"] = "Logout",
        ["Kick"] = "kick",
        ["TeleportChrToZone"] = "teleportchrtozone",
        ["TeleportSelfToChr"] = "teleportselftochr",
        ["Speed"] = "Speed",
        ["Immortal"] = "immortal",
        ["SearchMonster"] = "searchmonster",
    };

    private static readonly Dictionary<string, string> Results = new()
    {
        ["GM_Kick"] = "%s is forced to kick out",
        ["GM_GMFlagSet"] = "GM flag is set in game server",
    };

    private static readonly string[] KickArguments = { "Bad", "spamming the square" };

    private static GameMasterChat Chat => new(Keywords, Results);

    [Fact]
    public void KeywordsFromTheTableAreCommandsAndPlainChatIsNot()
    {
        Assert.True(Chat.IsCommand("/login secret"));
        Assert.True(Chat.IsCommand("/LOGIN secret"));
        Assert.False(Chat.IsCommand("login secret"));
        Assert.False(Chat.IsCommand("/hello there"));
    }

    [Fact]
    public void KeywordsWithoutABuiltCommandStayPlainChat()
    {
        Assert.False(Chat.IsCommand("/searchmonster Wolf"));
    }

    [Fact]
    public void QuotedArgumentsStayTogether()
    {
        var command = Chat.Parse("/kick Bad \"spamming the square\"");

        Assert.NotNull(command);
        Assert.Equal("Kick", command!.Name);
        Assert.Equal(KickArguments, command.Arguments);
    }

    [Fact]
    public void ResultsFillTheNamePlaceholder()
    {
        Assert.Equal("Bad is forced to kick out", Chat.Result("GM_Kick", "Bad"));
        Assert.Equal("GM flag is set in game server", Chat.Result("GM_GMFlagSet", "Keeper"));
        Assert.Null(Chat.Result("GM_Missing", "Keeper"));
    }

    [Fact]
    public void ResultsFillEveryPlaceholderInOrder()
    {
        var chat = new GameMasterChat(Keywords, new Dictionary<string, string>
        {
            ["GM_SpeedSet"] = "Speed is set to %d.",
            ["GM_KickReason"] = "%s is forced to kick out after 5 seconds for'%s'.",
            ["GM_BlockReason"] = "Block is applied to %s for % d perid for reason of '%s'",
        });

        Assert.Equal("Speed is set to 3.", chat.Result("GM_SpeedSet", "3"));
        Assert.Equal("Bad is forced to kick out after 5 seconds for'spam'.", chat.Result("GM_KickReason", "Bad", "spam"));
        Assert.Equal("Block is applied to Bad for 7 perid for reason of 'spam'", chat.Result("GM_BlockReason", "Bad", "7", "spam"));
    }

    [Fact]
    public void SwitchWordsAreOnAndOff()
    {
        Assert.True(GameMasterChat.TryParseSwitch("ON", out var on) && on);
        Assert.True(GameMasterChat.TryParseSwitch("off", out var off) && !off);
        Assert.False(GameMasterChat.TryParseSwitch("maybe", out _));
    }

    [Fact]
    public void PasswordBodyIsLengthPrefixed()
    {
        Assert.Equal(new byte[] { 3, (byte)'a', (byte)'b', (byte)'c' }, GameMasterChat.Password("abc", Encoding.Latin1));
        Assert.Empty(GameMasterChat.Password(new string('x', GameMasterFrames.PasswordBufferSize), Encoding.Latin1));
    }

    [Fact]
    public void EveryUsageRowIsACommandAndKeywordsAreLookedUpByName()
    {
        var keywords = GameMasterChat.Commands.ToDictionary(command => command.Name, command => "kw" + command.Name.ToLowerInvariant());
        var chat = new GameMasterChat(keywords, Results);

        Assert.Equal(GameMasterChat.Commands.Count, GameMasterChat.Commands.Select(command => command.Name).Distinct().Count());
        foreach (var command in GameMasterChat.Commands)
        {
            Assert.Equal(command.Name, chat.Parse("/kw" + command.Name.ToLowerInvariant())!.Name);
            Assert.Equal("kw" + command.Name.ToLowerInvariant(), chat.KeywordOf(command.Name));
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
        }

        Assert.Equal("login", Chat.KeywordOf(GameMasterChat.Login));
        Assert.Null(Chat.KeywordOf("SearchMonster"));
    }

    [Fact]
    public void FailureRepliesAreRefusalsAndSuccessRepliesAreNot()
    {
        Assert.True(GameMasterToolConnection.IsRefusal(new ToolReply(GameMasterFrames.KickFail, [])));
        Assert.True(GameMasterToolConnection.IsRefusal(new ToolReply(GameMasterEventFrames.ExpReserveRefused, [])));
        Assert.False(GameMasterToolConnection.IsRefusal(new ToolReply(GameMasterFrames.KickOk, [])));
        Assert.False(GameMasterToolConnection.IsRefusal(new ToolReply(GameMasterFrames.LoginOk, [])));
    }
}
