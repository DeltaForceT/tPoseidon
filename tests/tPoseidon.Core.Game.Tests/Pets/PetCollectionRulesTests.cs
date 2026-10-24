using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Pets;

public sealed class PetCollectionRulesTests
{
    private const int CoraBeachMap = 3;
    private const int CoraBeachArea = 10;
    private const int RelicsMap = 2;
    private const int RelicsArea = 3;
    private const int ClosedMap = 99;
    private const int MineMap = 196;
    private const int DeepMap = 118;

    private static readonly ItemParamEntry Nox = new() { Id = 72429, Class = 2, Type = PetRules.PetItemType, MinLevel = 40, RefineIndex = 56 };
    private static readonly ItemParamEntry Food = new() { Id = 19854, Class = 1, Type = 14 };

    private static readonly PetDigTables Tables = new(
        new Dictionary<int, PetDigPetEntry> { [56] = new(56, 2, 8000) },
        new Dictionary<int, PetDigFoodEntry> { [19854] = new(19854, 15, 200, 100) },
        new Dictionary<int, PetDigExEntry>
        {
            [72429] = new(72429, 2000, new[] { new PetDigExItem(106, 2000, 1), new PetDigExItem(112, 1500, 3) }),
        },
        new Dictionary<int, PetDigAreaEntry>
        {
            [CoraBeachArea] = new(CoraBeachArea, 1, 400),
            [RelicsArea] = new(RelicsArea, 50, 400),
        },
        new Dictionary<int, PetDigZone>
        {
            [CoraBeachMap] = new(CoraBeachArea, 0, 0, 0),
            [RelicsMap] = new(RelicsArea, 0, 20, 40),
            [MineMap] = new(CoraBeachArea, PetDigZone.MineSubType, 10, 20),
            [DeepMap] = new(CoraBeachArea, 0, 160, 200),
        },
        new[] { new PetDigDepthBand(0, 9, 0), new PetDigDepthBand(160, 199, 1500), new PetDigDepthBand(200, 249, 1500) });

    private static PetCollectionStartRequest Request() => new(
        PetCollectionState.Idle, 60, false,
        new PetCollectionSlot(Nox, false, false, 1),
        new PetCollectionSlot(Food, false, false, 10),
        5, CoraBeachMap, 0);

    [Fact]
    public void StartChecksRunInOrder()
    {
        var ok = Request();
        Assert.Equal(PetCollectionRules.NotRegistrable, PetCollectionRules.CheckStart(ok with { State = PetCollectionState.Collecting }, Tables, out _));
        Assert.Equal(PetCollectionRules.NotRegistrable, PetCollectionRules.CheckStart(ok with { State = PetCollectionState.Returned }, Tables, out _));
        Assert.Equal(PetCollectionRules.Unknown, PetCollectionRules.CheckStart(ok with { Fainted = true }, Tables, out _));
        Assert.Equal(PetCollectionRules.CharacterTooLow, PetCollectionRules.CheckStart(ok with { CharacterLevel = 29 }, Tables, out _));
        Assert.Equal(PetCollectionRules.NoPet, PetCollectionRules.CheckStart(ok with { Pet = null }, Tables, out _));
        Assert.Equal(PetCollectionRules.Equipped, PetCollectionRules.CheckStart(ok with { Pet = ok.Pet! with { Equipped = true } }, Tables, out _));
        Assert.Equal(PetCollectionRules.Expired, PetCollectionRules.CheckStart(ok with { Pet = ok.Pet! with { Expired = true } }, Tables, out _));
        Assert.Equal(PetCollectionRules.NotCollector, PetCollectionRules.CheckStart(ok with { Pet = ok.Pet! with { Definition = Nox with { RefineIndex = 30 } } }, Tables, out _));
        Assert.Equal(PetCollectionRules.NotCollector, PetCollectionRules.CheckStart(ok with { Pet = ok.Pet! with { Definition = Nox with { Type = 2 } } }, Tables, out _));
        Assert.Equal(PetCollectionRules.PetAboveCharacter, PetCollectionRules.CheckStart(ok with { CharacterLevel = 39 }, Tables, out _));
        Assert.Equal(PetCollectionRules.NotFood, PetCollectionRules.CheckStart(ok with { Food = null }, Tables, out _));
        Assert.Equal(PetCollectionRules.Equipped, PetCollectionRules.CheckStart(ok with { Food = ok.Food! with { Equipped = true } }, Tables, out _));
        Assert.Equal(PetCollectionRules.Expired, PetCollectionRules.CheckStart(ok with { Food = ok.Food! with { Expired = true } }, Tables, out _));
        Assert.Equal(PetCollectionRules.NotFood, PetCollectionRules.CheckStart(ok with { Food = ok.Food! with { Definition = Food with { Id = 19900 } } }, Tables, out _));
        Assert.Equal(PetCollectionRules.Unknown, PetCollectionRules.CheckStart(ok with { FoodCount = 0 }, Tables, out _));
        Assert.Equal(PetCollectionRules.Unknown, PetCollectionRules.CheckStart(ok with { FoodCount = 11 }, Tables, out _));
        Assert.Equal(PetCollectionRules.AreaClosed, PetCollectionRules.CheckStart(ok with { MapId = ClosedMap }, Tables, out _));
        Assert.Equal(PetCollectionRules.AreaLevel, PetCollectionRules.CheckStart(ok with { MapId = RelicsMap }, Tables, out _));
        Assert.Equal(PetCollectionRules.BagFull, PetCollectionRules.CheckStart(ok with { BagCount = 2 }, Tables, out _));
        Assert.Equal(PetCollectionRules.Ok, PetCollectionRules.CheckStart(ok with { State = PetCollectionState.Stopped, BagCount = 1 }, Tables, out _));
        Assert.Equal(PetCollectionRules.Ok, PetCollectionRules.CheckStart(ok, Tables, out var plan));
        Assert.Equal(72429, plan!.PetItemId);
        Assert.Equal(40, plan.PetLevel);
        Assert.Equal(TimeSpan.FromSeconds(15), plan.TickPeriod);
        Assert.NotNull(plan.ExRow);
    }

    [Fact]
    public void FoodCountIsCappedByMaxItem()
    {
        var small = Tables with { Foods = new Dictionary<int, PetDigFoodEntry> { [19854] = new(19854, 15, 4, 100) } };
        Assert.Equal(PetCollectionRules.Unknown, PetCollectionRules.CheckStart(Request(), small, out _));
        Assert.Equal(PetCollectionRules.Ok, PetCollectionRules.CheckStart(Request() with { FoodCount = 4 }, small, out _));
    }

    [Fact]
    public void FailedBaseRollDrawsNothing()
    {
        var plan = Plan();
        var rolls = Rolls(8000);
        Assert.Null(PetCollectionRules.Draw(plan, 0, _ => rolls.Dequeue(), () => (500, 1)));
        Assert.Empty(rolls);
    }

    [Fact]
    public void ExRollAddsTheFoodPlusRateAndPicksByWeight()
    {
        var plan = Plan();
        var rolls = Rolls(7999, 2099, 2000);
        var asked = new List<int>();
        var draw = PetCollectionRules.Draw(plan, 0, bound => { asked.Add(bound); return rolls.Dequeue(); }, () => (500, 1));
        Assert.Equal(new PetCollectionDraw(112, 3, true), draw);
        Assert.Equal(ExBounds, asked);
    }

    [Fact]
    public void MissedExRollFallsBackToTheGroundItem()
    {
        var plan = Plan();
        var rolls = Rolls(0, 2100);
        Assert.Equal(new PetCollectionDraw(500, 1, false), PetCollectionRules.Draw(plan, 0, _ => rolls.Dequeue(), () => (500, 1)));
        var empty = Rolls(0, 2100);
        Assert.Null(PetCollectionRules.Draw(plan, 0, _ => empty.Dequeue(), () => (PetCollectionRules.NothingDrawn, 0)));
    }

    [Fact]
    public void PetsWithoutAnExRowOnlyRollGroundItems()
    {
        var plan = Plan() with { ExRow = null };
        var rolls = Rolls(0);
        Assert.Equal(new PetCollectionDraw(501, 4, false), PetCollectionRules.Draw(plan, 0, _ => rolls.Dequeue(), () => (501, 4)));
        Assert.Empty(rolls);
    }

    [Fact]
    public void StateMovesLikeTheClientWindow()
    {
        Assert.True(PetCollectionRules.CanStartFrom(PetCollectionState.Idle));
        Assert.True(PetCollectionRules.CanStartFrom(PetCollectionState.Stopped));
        Assert.False(PetCollectionRules.CanStopFrom(PetCollectionState.Stopped));
        Assert.True(PetCollectionRules.CanReturnFrom(PetCollectionState.Stopped));
        Assert.False(PetCollectionRules.CanReturnFrom(PetCollectionState.Idle));
        Assert.Equal(PetCollectionState.Returned, PetCollectionRules.AfterReturn(1));
        Assert.Equal(PetCollectionState.Idle, PetCollectionRules.AfterReturn(0));
        Assert.Equal(PetCollectionState.Idle, PetCollectionRules.AfterBagChange(PetCollectionState.Returned, 0));
        Assert.Equal(PetCollectionState.Returned, PetCollectionRules.AfterBagChange(PetCollectionState.Returned, 1));
        Assert.Equal(PetCollectionState.Stopped, PetCollectionRules.AfterBagChange(PetCollectionState.Stopped, 0));
        Assert.True(PetCollectionRules.StopsAfterTick(0, 0, Tables.Pets[56]));
        Assert.True(PetCollectionRules.StopsAfterTick(3, 2, Tables.Pets[56]));
        Assert.False(PetCollectionRules.StopsAfterTick(3, 1, Tables.Pets[56]));
    }

    [Fact]
    public void MineMapsNeedASpecialDigPetAndFood()
    {
        var mine = Request() with { MapId = MineMap };
        Assert.Equal(PetCollectionRules.MineNeedsSpecialPet, PetCollectionRules.CheckStart(mine, Tables, out _));
        var specialPet = mine with { Pet = mine.Pet! with { Definition = Nox with { SubType = PetDigTables.SpecialDigSubType } } };
        Assert.Equal(PetCollectionRules.MineNeedsSpecialFood, PetCollectionRules.CheckStart(specialPet, Tables, out _));
        var specialFood = specialPet with { Food = specialPet.Food! with { Definition = Food with { SubType = PetDigTables.SpecialDigSubType } } };
        Assert.Equal(PetCollectionRules.Ok, PetCollectionRules.CheckStart(specialFood, Tables, out var plan));
        Assert.True(plan!.PetDigsMines);
        Assert.Equal(PetCollectionRules.MineNeedsSpecialPet, PetCollectionRules.CheckArea(Tables, MineMap, Plan()));
    }

    [Fact]
    public void DepthBandsLowerTheBaseRate()
    {
        Assert.Equal(0, PetCollectionRules.DepthPenalty(Tables, CoraBeachMap, _ => 0));
        Assert.Equal(0, PetCollectionRules.DepthPenalty(Tables, ClosedMap, _ => 0));
        var asked = new List<int>();
        Assert.Equal(1500, PetCollectionRules.DepthPenalty(Tables, DeepMap, bound => { asked.Add(bound); return 4; }));
        Assert.Equal(5, Assert.Single(asked));
        Assert.Equal(1500, PetCollectionRules.DepthPenalty(Tables, DeepMap, _ => 0));
        Assert.Equal(0, PetCollectionRules.DepthPenalty(Tables, RelicsMap, _ => 1));

        var rolls = Rolls(6600);
        Assert.Null(PetCollectionRules.Draw(Plan(), 1500, _ => rolls.Dequeue(), () => (500, 1)));
        var passed = Rolls(6400, 2100);
        Assert.Equal(new PetCollectionDraw(500, 1, false), PetCollectionRules.Draw(Plan(), 1500, _ => passed.Dequeue(), () => (500, 1)));
    }

    private static readonly int[] ExBounds = [PetCollectionRules.RateScale, PetCollectionRules.RateScale, 3500];

    private static Queue<int> Rolls(params int[] values) => new(values);

    private static PetCollectionPlan Plan()
    {
        Assert.Equal(PetCollectionRules.Ok, PetCollectionRules.CheckStart(Request(), Tables, out var plan));
        return plan!;
    }
}
