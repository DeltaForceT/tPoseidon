using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GameMasterChatTests
{
    private static readonly Dictionary<string, string> Results = new()
    {
        ["GM_Kick"] = "%s is forced to kick out",
        ["GM_GMFlagSet"] = "GM flag is set in game server",
    };

    private static readonly string[] KickArguments = { "Bad", "spamming the square" };

    private static GameMasterChat Chat => new(Results);

    [Fact]
    public void KeywordsFromTheTableAreCommandsAndPlainChatIsNot()
    {
        Assert.True(GameMasterChat.IsCommand("@login secret"));
        Assert.True(GameMasterChat.IsCommand("@LOGIN secret"));
        Assert.True(GameMasterChat.IsCommand("  @login secret"));
        Assert.False(GameMasterChat.IsCommand("login secret"));
        Assert.False(GameMasterChat.IsCommand("/login secret"));
        Assert.False(GameMasterChat.IsCommand("@hello there"));
    }

    [Fact]
    public void KeywordsWithoutABuiltCommandStayPlainChat()
    {
        Assert.False(GameMasterChat.IsCommand("@searchmonster Wolf"));
    }

    [Fact]
    public void QuotedArgumentsStayTogether()
    {
        var command = GameMasterChat.Parse("@kick Bad \"spamming the square\"");

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
        var chat = new GameMasterChat(new Dictionary<string, string>
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
    public void EveryUsageRowIsACommandWithItsLowerCaseNameAsKeyword()
    {
        Assert.Equal(GameMasterChat.Commands.Count, GameMasterChat.Commands.Select(command => command.Name).Distinct().Count());
        Assert.Equal(GameMasterChat.Commands.Count, GameMasterChat.Keywords.Count);
        foreach (var command in GameMasterChat.Commands)
        {
            Assert.Equal(command.Name.ToLowerInvariant(), command.Keyword);
            Assert.Equal(command.Name, GameMasterChat.Parse("@" + command.Keyword)!.Name);
            Assert.Contains(command.Keyword, GameMasterChat.Keywords);
            Assert.False(string.IsNullOrWhiteSpace(command.Description));
        }
    }

    [Theory]
    [InlineData("@login secret", GameMasterChat.Login)]
    [InlineData("@logout", GameMasterChat.Logout)]
    [InlineData("@kick Bad", GameMasterChat.Kick)]
    [InlineData("@speed 3", GameMasterChat.Speed)]
    [InlineData("@teleportchrtozone Bad 23", GameMasterChat.TeleportChrToZone)]
    [InlineData("@summonmonster 2001 3", GameMasterChat.SummonMonster)]
    [InlineData("@commandhelp", GameMasterChat.CommandHelp)]
    public void EachGameMasterLineIsRoutedToItsCommand(string line, string name)
    {
        Assert.True(GameMasterChat.IsCommand(line));
        Assert.Equal(name, GameMasterChat.Parse(line)!.Name);
    }

    [Fact]
    public void ALineWithTheWrongArgumentsIsStillAGameMasterLineSoItIsNeverBroadcast()
    {
        Assert.True(GameMasterChat.IsCommand("@login"));
        Assert.True(GameMasterChat.IsCommand("@login wrong password with spaces"));
        Assert.Empty(GameMasterChat.Parse("@login")!.Arguments);
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
