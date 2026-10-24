using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Hosting.Tests;

internal sealed class FakeToolTransport : IGmToolTransport
{
    public List<(GameMasterToolRequest Request, bool ExpectsReply, GmLogin Login)> Sent { get; } = [];

    public IReadOnlyList<ToolReply> Replies { get; set; } = [];

    public bool LoginAccepted { get; set; } = true;

    public bool? Listening { get; set; } = true;

    public int Logins { get; private set; }

    public Task<IReadOnlyList<ToolReply>> SendAsync(GmToolEndpoint endpoint, GmLogin login, GameMasterToolRequest request, bool expectsReply, CancellationToken ct)
    {
        Sent.Add((request, expectsReply, login));
        return Task.FromResult(Replies);
    }

    public Task<bool> LoginAsync(GmToolEndpoint endpoint, GmLogin login, CancellationToken ct)
    {
        Logins++;
        return Task.FromResult(LoginAccepted);
    }

    public bool? IsListening(GmToolEndpoint endpoint) => Listening;
}

internal sealed class FakeAccountBook : IGmAccountBook
{
    public List<GmCharacterEntry> Entries { get; } = [];

    public string? ListProblem { get; set; }

    public GmCharacterRegistration? Register(string characterName, string permittedIp)
    {
        if (characterName != "Boss")
        {
            return null;
        }

        Entries.Add(new GmCharacterEntry(7, "owner", "Boss", [permittedIp], DateTimeOffset.UnixEpoch));
        return new GmCharacterRegistration(7, "Boss", permittedIp);
    }

    public GmCharacterEntry? Unregister(string characterName)
    {
        var entry = Entries.FirstOrDefault(gm => string.Equals(gm.CharacterName, characterName, StringComparison.OrdinalIgnoreCase));
        if (entry is not null)
        {
            Entries.Remove(entry);
        }

        return entry;
    }

    public IReadOnlyList<GmCharacterEntry> List() => ListProblem is { } problem ? throw new HostingException(problem) : Entries;

    public IReadOnlyList<CharacterSummary> FindCharacters(string text, int limit) =>
        new[] { new CharacterSummary(0x4002, "Friend", "player1", 30, 23) }.Where(found => found.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(limit).ToList();
}

internal sealed class FakePenaltyBook : IGmPenaltyBook
{
    public List<PenaltyKind> KindList { get; } = [];

    public List<(short Code, short Count, int Hours)> Times { get; } = [];

    public int PenalizeResult { get; set; } = PenaltyResults.Ok;

    public int Released { get; set; }

    public (string Account, short Code, long Gm, string Note)? Penalized { get; private set; }

    public void SetKind(PenaltyKind kind) => KindList.Add(kind);

    public void SetTime(short code, short count, int hours) => Times.Add((code, count, hours));

    public IReadOnlyList<PenaltyKind> Kinds() => KindList;

    public int Penalize(string accountName, short code, long gameMasterAccountId, string note)
    {
        Penalized = (accountName, code, gameMasterAccountId, note);
        return PenalizeResult;
    }

    public int Release(string accountName, string gameMasterName) => Released;

    public IReadOnlyList<RunningPenalty> Running() => [];
}

public sealed class GameMasterTests
{
    private static readonly GmToolEndpoint Endpoint = new("127.0.0.1", 13336);
    private static readonly GmLogin Login = new("Boss", "secret");

    private static GameMasterToolCommand Command(string name) => GameMasterToolCommands.Find(name)!;

    [Fact]
    public void PrepareBuildsTheSameRequestAsTheCommandTable()
    {
        var call = GmTool.Prepare(Command("kick"), ["Friend", "spamming"]);
        var expected = GameMasterToolCommands.Build("kick", ["Friend", "spamming"], GameMasterToolCommands.ClientText())!;
        Assert.Equal(expected.Opcode, call.Request.Opcode);
        Assert.Equal(expected.Body, call.Request.Body);
    }

    [Fact]
    public void PrepareRefusesWrongArgumentsWithTheCommandLineUsage()
    {
        var error = Assert.Throws<HostingException>(() => GmTool.Prepare(Command("speed"), ["fast"]));
        Assert.Equal("Wrong arguments for 'speed'. Usage: tposeidon gm speed <1..4>", error.Message);
    }

    [Fact]
    public async Task NoReplyMeansRefusedOrUnreachable()
    {
        var transport = new FakeToolTransport();
        var error = await Assert.ThrowsAsync<HostingException>(() => GmTool.RunAsync(transport, Endpoint, Login, GmTool.Prepare(Command("zone-counts"), []), CancellationToken.None));
        Assert.Equal("No answer: GM login was refused or the system server is not reachable at 127.0.0.1:13336.", error.Message);
    }

    [Fact]
    public async Task RepliesAreDescribedAndRefusalsFlagged()
    {
        var transport = new FakeToolTransport { Replies = [new ToolReply(GameMasterFrames.LoginOk, []), new ToolReply(GameMasterFrames.KickFail, [])] };

        var outcome = await GmTool.RunAsync(transport, Endpoint, Login, GmTool.Prepare(Command("kick"), ["Friend"]), CancellationToken.None);

        Assert.True(outcome.Ok);
        Assert.True(outcome.Refused);
        Assert.Equal(["GM login accepted", "kick failed: not online"], outcome.Lines);
        Assert.Equal(["kick failed: not online"], outcome.Results);
        Assert.True(Assert.Single(transport.Sent).ExpectsReply);
    }

    [Fact]
    public async Task ACommandWithoutAnAnswerOnlyReportsTheLogin()
    {
        var transport = new FakeToolTransport { Replies = [new ToolReply(GameMasterFrames.LoginOk, [])] };

        var outcome = await GmTool.RunAsync(transport, Endpoint, Login, GmTool.Prepare(Command("memo"), ["Friend", "Hello"]), CancellationToken.None);

        Assert.True(outcome.Ok);
        Assert.False(outcome.Refused);
        Assert.Empty(outcome.Results);
        Assert.False(Assert.Single(transport.Sent).ExpectsReply);
    }

    [Fact]
    public async Task TheLinkCheckExplainsWhyItCannotConnect()
    {
        var transport = new FakeToolTransport { Listening = false };
        var accounts = new FakeAccountBook();

        Assert.Equal(GmLinkStatus.ServerDown, (await GmLink.CheckAsync(transport, Endpoint, Login, accounts, CancellationToken.None)).Status);
        transport.Listening = true;
        Assert.Equal(GmLinkStatus.NoGameMaster, (await GmLink.CheckAsync(transport, Endpoint, Login, accounts, CancellationToken.None)).Status);
        accounts.Register("Boss", "127.0.0.1");
        var stranger = await GmLink.CheckAsync(transport, Endpoint, Login with { Character = "Friend" }, accounts, CancellationToken.None);
        Assert.Equal(GmLinkStatus.NotRegistered, stranger.Status);
        Assert.Contains("Registered: Boss", stranger.Message, StringComparison.Ordinal);
        transport.LoginAccepted = false;
        Assert.Equal(GmLinkStatus.Refused, (await GmLink.CheckAsync(transport, Endpoint, Login, accounts, CancellationToken.None)).Status);
        transport.LoginAccepted = true;
        Assert.True((await GmLink.CheckAsync(transport, Endpoint, Login with { Character = " boss " }, accounts, CancellationToken.None)).Connected);
        Assert.Equal(GmLinkStatus.NotConnected, (await GmLink.CheckAsync(transport, Endpoint, Login with { Password = "" }, accounts, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task AnUnreadableGmListDoesNotBlockTheLoginButIsReported()
    {
        var transport = new FakeToolTransport { LoginAccepted = false };
        var accounts = new FakeAccountBook { ListProblem = "Nothing answered on 127.0.0.1:5432." };

        var refused = await GmLink.CheckAsync(transport, Endpoint, Login, accounts, CancellationToken.None);

        Assert.Equal(GmLinkStatus.Refused, refused.Status);
        Assert.EndsWith("The GM list could not be read either: Nothing answered on 127.0.0.1:5432.", refused.Message, StringComparison.Ordinal);
        transport.LoginAccepted = true;
        Assert.True((await GmLink.CheckAsync(transport, Endpoint, Login, accounts, CancellationToken.None)).Connected);
    }

    [Fact]
    public void PenaltiesKeepTheCommandLineTexts()
    {
        var book = new FakePenaltyBook();

        Assert.Equal(["Penalty kind 1001 is 'Abusive chat'."], GmPenalties.Run(book, GmPenalties.KindName, ["1001", "Abusive chat"]).Lines);
        Assert.Equal(["Penalty 1001, step 1: 72 hour(s)."], GmPenalties.Run(book, GmPenalties.TimeName, ["1001", "1", "72"]).Lines);
        var penalized = GmPenalties.Run(book, GmPenalties.PenalizeName, ["player1", "1001", "spam"]);
        Assert.Equal((GmOutcome.Done, "Account 'player1' is blocked with penalty 1001."), (penalized.ExitCode, penalized.Lines[0]));
        Assert.Equal(("player1", (short)1001, 0L, "spam"), book.Penalized);
        book.PenalizeResult = PenaltyResults.AlreadyPenalized;
        var again = GmPenalties.Run(book, GmPenalties.PenalizeName, ["player1", "1001"]);
        Assert.Equal((GmOutcome.NotDone, true), (again.ExitCode, again.Refused));
        Assert.Equal(["Account 'player1' has no running penalty."], GmPenalties.Run(book, GmPenalties.ReleaseName, ["player1"]).Lines);
        book.Released = 2;
        Assert.Equal(["Released 2 penalty(ies) of 'player1'."], GmPenalties.Run(book, GmPenalties.ReleaseName, ["player1"]).Lines);
        Assert.Equal(["Penalty kinds:", "    1001  Abusive chat (kind 0)", "No running penalties."], GmPenalties.Run(book, GmPenalties.ListName, []).Lines);
    }

    [Theory]
    [InlineData(GmPenalties.KindName, "x", "name")]
    [InlineData(GmPenalties.TimeName, "1001", "0", "72")]
    [InlineData(GmPenalties.PenalizeName, "player1")]
    [InlineData(GmPenalties.ReleaseName)]
    public void PenaltyUsageErrorsAreRefused(string subcommand, params string[] arguments)
    {
        var error = Assert.Throws<HostingException>(() => GmPenalties.Run(new FakePenaltyBook(), subcommand, arguments));
        Assert.StartsWith($"Usage: tposeidon gm {subcommand} ", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GmAccountsRegisterListAndUnregister()
    {
        var book = new FakeAccountBook();

        Assert.Equal(
            ["'Boss' is now the GM character of account 7.", "GM login is allowed from 127.0.0.1, with the account's password."],
            GmAccounts.Register(book, "Boss", GmAccounts.LocalPermitIp).Lines);
        Assert.Equal("There is no character named 'Nobody'. Create it in game first.", Assert.Throws<HostingException>(() => GmAccounts.Register(book, "Nobody", "127.0.0.1")).Message);
        var listed = GmAccounts.List(book).Lines;
        Assert.Equal("GM characters:", listed[0]);
        Assert.Contains("Boss (account 'owner', 7): GM login from 127.0.0.1", listed[1], StringComparison.Ordinal);
        Assert.True(GmAccounts.Unregister(book, "boss").Ok);
        Assert.True(GmAccounts.Unregister(book, "boss").Refused);
        Assert.Equal(["No GM characters are registered."], GmAccounts.List(book).Lines);
        Assert.False(GmAccounts.IsPermitAddress("my pc"));
    }

    [Fact]
    public void TheCatalogReadsNamesFromTheGameData()
    {
        using var folder = new TempFolder();
        var libconfig = Path.Combine(folder.Path, "libconfig.xml");
        File.WriteAllText(libconfig, CatalogXml);

        var catalog = GameCatalog.Load(libconfig);

        Assert.Equal(["Jelly Bunny", "Big Bunny"], catalog.Monsters.Select(monster => monster.Name));
        Assert.Equal(["Big Bunny"], catalog.FindMonsters("bunny", 10, 99, null).Select(monster => monster.Name));
        Assert.Equal(["Jelly Bunny"], catalog.FindMonsters("", 1, 999, 23).Select(monster => monster.Name));
        Assert.Equal(["Shop keeper"], catalog.Npcs.Select(npc => npc.Name));
        Assert.Equal("Coral Beach", catalog.MapName(23));
        Assert.Equal(["Red potion"], catalog.FindItems("", ItemNames.ItemTable).Select(item => item.Name));
        Assert.Equal([1001, 9001], catalog.FindItems("", null).Select(item => item.Id));
        var login = catalog.ChatCommands.Single(command => command.Name == GameMasterChat.Login);
        Assert.Equal("@login <password>", login.Typed);
        Assert.Equal("kick", catalog.ChatCommands.Single(command => command.Name == GameMasterChat.Kick).Keyword);
    }

    [Fact]
    public void AMissingGameDataFileGivesAnEmptyCatalogThatSaysSo()
    {
        using var folder = new TempFolder();
        var catalog = GameCatalog.For(new HostSettings { ClientFolder = folder.Path });
        Assert.Empty(catalog.Monsters);
        Assert.Contains("was not found", catalog.Problem, StringComparison.Ordinal);
        Assert.Equal(GameMasterChat.Commands.Count, catalog.ChatCommands.Count);
    }

    private const string CatalogXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <INI TableCount="7" TableInfoCount="7">
        <TABLE name="MonsterParamEx2" RowCount="3" TableInfoID="1" FieldCnt="3">
            <ROW><ID>2001</ID><Name><![CDATA[Jelly Bunny]]></Name><Level>3</Level></ROW>
            <ROW><ID>2002</ID><Name><![CDATA[Big Bunny]]></Name><Level>40</Level><IsBoss>1</IsBoss></ROW>
            <ROW><ID>5000</ID><Name><![CDATA[Not summonable]]></Name><Level>40</Level></ROW>
        </TABLE>
        <TABLE name="CharacterInfo" RowCount="2" TableInfoID="2" FieldCnt="2">
            <ROW><ID>1001</ID><CommonName><![CDATA[Shop keeper]]></CommonName></ROW>
            <ROW><ID>1</ID><CommonName><![CDATA[A player kind]]></CommonName></ROW>
        </TABLE>
        <TABLE name="MapInfoEx" RowCount="1" TableInfoID="3" FieldCnt="3">
            <ROW><ID>23</ID><Name><![CDATA[Coral Beach]]></Name><MonsterTable><![CDATA[Mon_Coral]]></MonsterTable></ROW>
        </TABLE>
        <TABLE name="Mon_Coral" RowCount="1" TableInfoID="4" FieldCnt="2">
            <ROW><MonsterID>2001</MonsterID><Avr>5</Avr></ROW>
        </TABLE>
        <TABLE name="ItemParam2" RowCount="1" TableInfoID="5" FieldCnt="2">
            <ROW><ID>1001</ID><Name><![CDATA[Red potion]]></Name></ROW>
        </TABLE>
        <TABLE name="ItemParamCM2" RowCount="1" TableInfoID="6" FieldCnt="2">
            <ROW><ID>9001</ID><Name><![CDATA[Gift box]]></Name></ROW>
        </TABLE>
        </INI>
        """;
}
