using tPoseidon.Core.Game.HarconDefence;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.HarconDefence;

public sealed class HarconDefenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly HarconStageEntry Fantasy1 = new(1, "Fantasy 1", 1, 0, 0, 1, 6, 672, 663, "Sc_HarconDef_Fantasy1", 1, 1);
    private static readonly HarconStageEntry Fantasy2 = new(2, "Fantasy 2", 2, 0, 1, 1, 6, 672, 663, "Sc_HarconDef_Fantasy2", 2, 1);
    private static readonly HarconStageEntry Boss = new(26, "Boss", 1, 64784, 0, 1, 20, 677, 668, "Sc_HarconDef_Boss", 14, 3);
    private static readonly HarconPointRule Points = new(4, 15, 10, 0);

    private static readonly HarconWave[] Waves =
    [
        new(1, 0, 0, 0, [new HarconSpawnGroup(3057, 3, 10, 1, 3000)]),
        new(2, 1, 30, 0, [new HarconSpawnGroup(3058, 2, 11, 2, 0)]),
    ];

    [Fact]
    public void ContributionIsTheShareOfTheRoomScoreInPercent()
    {
        var placings = new[] { new HarconPlacing(1, 30, 1, 0), new HarconPlacing(2, 10, 2, 0), new HarconPlacing(3, -5, 3, 0) };

        Assert.Equal(75f, HarconRun.Contribution(30, placings));
        Assert.Equal(0f, HarconRun.Contribution(-5, placings));
        Assert.Equal(0f, HarconRun.Contribution(0, Array.Empty<HarconPlacing>()));
    }

    private static HarconDefTables Tables() => new(
        new Dictionary<int, HarconStageEntry> { [1] = Fantasy1, [2] = Fantasy2, [26] = Boss },
        new Dictionary<string, IReadOnlyList<HarconWave>> { [Fantasy1.WaveTable] = Waves, [Fantasy2.WaveTable] = Waves, [Boss.WaveTable] = Waves },
        new Dictionary<int, HarconDropEntry>(),
        Points,
        new Dictionary<(int, int), HarconShopEntry>(),
        new Dictionary<string, IReadOnlyList<(int MonsterId, int RegionId, int Count)>>());

    [Fact]
    public void TheCoinShopOfferFollowsTheNpcsShopTableAndTheItem()
    {
        var offer = new HarconShopEntry(2, 1, 3, 64784, 1, 100);
        var tables = Tables() with
        {
            Shop = new Dictionary<(int, int), HarconShopEntry> { [(2, 64784)] = offer },
            ShopOfNpc = new Dictionary<int, int> { [1395] = 1, [1411] = 2 },
        };

        Assert.Same(offer, tables.Offer(1411, 64784));
        Assert.Null(tables.Offer(1395, 64784));
        Assert.Null(tables.Offer(77, 64784));
        Assert.Equal(300, HarconRules.ShopPrice(100, 3));
        Assert.Equal(tPoseidon.Core.Game.Quests.NpcScriptOperation.OpenHarconShop, tPoseidon.Core.Game.Quests.NpcScript.Classify(HarconRules.ShopFunction).Operation);
    }

    [Fact]
    public void EventStagesKeepNoRecords()
    {
        var eagle = new HarconStageEntry(27, "Eagle", 1, 64785, 0, 1, 10, 662, 664, "Sc_HarconDef_EagleEv", 15, HarconRules.EventCategory);
        Assert.False(HarconRules.KeepsRecords(eagle));
        Assert.True(HarconRules.KeepsRecords(Fantasy1));
        Assert.True(HarconRules.KeepsRecords(Boss));
    }

    [Theory]
    [InlineData(HarconRules.CannonFunction)]
    [InlineData(HarconRules.SkillFunction)]
    [InlineData(HarconRules.CannonCheckFunction)]
    public void InPlayNpcFunctionsReachTheHarconController(int function)
    {
        var call = tPoseidon.Core.Game.Quests.NpcScript.Classify((ushort)function);
        Assert.Equal(tPoseidon.Core.Game.Quests.NpcScriptOperation.HarconPlay, call.Operation);
        Assert.Equal(function, call.Slot);
    }

    [Fact]
    public void OnlyTheSealedHarconIsATarget()
    {
        Assert.True(HarconRules.IsTarget(HarconRules.SealedHarconMonsterType));
        Assert.False(HarconRules.IsTarget(14));
        Assert.False(HarconRules.IsTarget(0));
    }

    [Fact]
    public void CreateFollowsTheErrorOrder()
    {
        var lobby = new HarconLobby();
        var none = new HashSet<int>();
        Assert.Equal(HarconRules.CreateInvalidStage, lobby.CheckCreate(Tables(), new HarconCreate(99, "room", 4), 1, none, _ => true));
        Assert.Equal(HarconRules.CreateNotCleared, lobby.CheckCreate(Tables(), new HarconCreate(2, "room", 4), 1, none, _ => true));
        Assert.Equal(HarconRules.CreateItemMissing, lobby.CheckCreate(Tables(), new HarconCreate(26, "room", 4), 1, none, _ => false));
        Assert.Equal(HarconRules.CreateInvalidTitle, lobby.CheckCreate(Tables(), new HarconCreate(1, " ", 4), 1, none, _ => true));
        Assert.Equal(HarconRules.CreateInvalidMaxPlayers, lobby.CheckCreate(Tables(), new HarconCreate(1, "room", 7), 1, none, _ => true));
        Assert.Null(lobby.CheckCreate(Tables(), new HarconCreate(2, "room", 6), 1, new HashSet<int> { 1 }, _ => true));
    }

    [Fact]
    public void RoomsCapAtSixty()
    {
        var lobby = new HarconLobby();
        for (var index = 0; index < HarconRules.MaxRooms; index++)
        {
            lobby.Create(Fantasy1, new HarconCreate(1, "room", 6), new HarconMember((ulong)index + 1, (ushort)(index + 1), $"p{index}"));
        }
        Assert.Equal(HarconRules.CreateFailed, lobby.CheckCreate(Tables(), new HarconCreate(1, "room", 6), 1000, new HashSet<int>(), _ => true));
    }

    [Fact]
    public void JoinLeaveAndMasterHandOver()
    {
        var lobby = new HarconLobby();
        var room = lobby.Create(Fantasy1, new HarconCreate(1, "room", 2), new HarconMember(1, 0x10, "a"));
        Assert.Null(lobby.CheckJoin(room.Key, 2));
        lobby.Join(room.Key, new HarconMember(2, 0x11, "b"));
        Assert.Equal(HarconRules.EnterFull, lobby.CheckJoin(room.Key, 3));
        Assert.Equal(HarconRules.EnterInvalidPlayerState, lobby.CheckJoin(room.Key, 2));
        Assert.Equal("a", lobby.Leave(1, out _, out var closed)!.Name);
        Assert.False(closed);
        Assert.Equal(2UL, room.Master!.CharacterId);
        lobby.Leave(2, out _, out closed);
        Assert.True(closed);
        Assert.Null(lobby.Find(room.Key));
    }

    [Fact]
    public void StartNeedsTheMasterAndEveryoneReady()
    {
        var lobby = new HarconLobby();
        var room = lobby.Create(Fantasy1, new HarconCreate(1, "room", 6), new HarconMember(1, 0x10, "a"));
        lobby.Join(room.Key, new HarconMember(2, 0x11, "b"));
        Assert.Equal(HarconRules.StartNotMaster, HarconLobby.CheckStart(room, 2, _ => true));
        Assert.Null(HarconLobby.CheckStart(room, 1, _ => true));
        room.State = HarconRoomState.Readying;
        Assert.False(room.AllReady);
        Assert.Null(HarconLobby.CheckReady(room, 2));
        room.Member(2)!.ReadyState = HarconRules.Ready;
        Assert.True(room.AllReady);
        Assert.Equal(HarconRules.ReadyInvalidPlayerState, HarconLobby.CheckReady(room, 1));
    }

    [Fact]
    public void WavesSpawnByDelayAndAdvance()
    {
        var run = new HarconRun(Waves, Points, [1UL, 2UL]);
        Assert.Equal(1, run.Start(Now)!.Id);
        Assert.Equal([new HarconSpawnOrder(3057, 10, 1)], run.DueSpawns(Now));
        Assert.Empty(run.DueSpawns(Now.AddSeconds(2)));
        Assert.Single(run.DueSpawns(Now.AddSeconds(3)));
        Assert.Null(run.NextWaveIfDone(Now.AddSeconds(4), 0));
        Assert.Single(run.DueSpawns(Now.AddSeconds(6)));
        Assert.Null(run.NextWaveIfDone(Now.AddSeconds(7), 1));
        Assert.Equal(2, run.NextWaveIfDone(Now.AddSeconds(8), 0)!.Id);
        Assert.Equal([new HarconSpawnOrder(3058, 11, 2)], run.DueSpawns(Now.AddSeconds(8)));
        Assert.Null(run.NextWaveIfDone(Now.AddSeconds(20), 2));
        Assert.Null(run.NextWaveIfDone(Now.AddSeconds(38), 2));
        Assert.True(run.LastWaveOver);
        Assert.False(run.Won(2));
        Assert.True(run.Won(0));
    }

    [Fact]
    public void ScoresRankAndCoins()
    {
        var run = new HarconRun(Waves, Points, [1UL, 2UL]);
        run.Start(Now);
        run.Killed(1);
        run.Killed(1);
        run.Killed(2);
        run.Died(2);
        foreach (var second in new[] { 0, 3, 6 }) run.DueSpawns(Now.AddSeconds(second));
        Assert.NotNull(run.NextWaveIfDone(Now.AddSeconds(6), 0));
        var drop = new HarconDropEntry(1, [3, 3, 3, 2, 2, 1, 1, 1, 1, 1], 1, 1, 1);
        var placings = run.Finish(true, drop);
        Assert.Equal(1UL, placings[0].CharacterId);
        Assert.Equal(8 + 15 + 10, placings[0].Score);
        Assert.Equal(4, placings[1].Score);
        Assert.Equal(3, placings[0].Coins);
        Assert.Equal(1, HarconRun.Coins(drop, 11, 5));
        Assert.Equal(0, HarconRun.Coins(drop, 1, 0));
        Assert.All(new HarconRun(Waves, Points, [1UL]).Finish(false, drop), placing => Assert.Equal(0, placing.Coins));
    }

    [Fact]
    public void DailyEntriesResetEachDay()
    {
        var today = new DateOnly(2026, 10, 19);
        Assert.Equal(6, HarconRules.EntriesToday(6, today, today));
        Assert.Equal(0, HarconRules.EntriesToday(6, today.AddDays(-1), today));
        Assert.False(HarconRules.CanEnter(30, 6));
        Assert.False(HarconRules.CanEnter(29, 0));
        Assert.True(HarconRules.CanEnter(30, 5));
    }
}
