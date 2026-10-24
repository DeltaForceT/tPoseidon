using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Pets;

public sealed class PetRulesTests
{
    private const int GridCells = 10;

    private static ZoneGeometry OpenGrid(params (int X, int Y)[] blocked)
    {
        var cells = new byte[GridCells * GridCells];
        foreach (var (x, y) in blocked) cells[y * GridCells + x] = ZoneGeometry.CellBlocked;
        return new ZoneGeometry
        {
            MinX = 0,
            MinY = 0,
            MaxX = GridCells * ZoneGeometry.CellSize,
            MaxY = GridCells * ZoneGeometry.CellSize,
            CellsX = GridCells,
            CellsY = GridCells,
            CellAttributes = cells,
            Regions = Array.Empty<ZoneRegion>(),
        };
    }

    [Theory]
    [InlineData(0x5DD, 0xFA1)]
    [InlineData(15802, 7002)]
    [InlineData(70000, 70000 - 0xF550)]
    [InlineData(90000, 90000 - 0x14DFC)]
    public void CharacterTypeFollowsTheItemIdBands(int itemId, int expected)
    {
        Assert.Equal(expected, PetRules.CharacterType(itemId));
    }

    [Fact]
    public void FollowStartsOnlyBeyondTheRadius()
    {
        Assert.False(PetRules.IsTooFar(100 + PetRules.FollowRadius, 100, 100, 100));
        Assert.True(PetRules.IsTooFar(101 + PetRules.FollowRadius, 100, 100, 100));
    }

    [Fact]
    public void FollowTargetWithoutGeometryIsTheOwner()
    {
        Assert.Equal((40, 50), PetRules.FollowTarget(null, 0, 0, 40, 50));
    }

    [Fact]
    public void FollowTargetIsTheFreeNeighbourCellNearestThePet()
    {
        var owner = (X: ZoneGeometry.CentreOf(5), Y: ZoneGeometry.CentreOf(5));

        var target = PetRules.FollowTarget(OpenGrid((4, 5)), ZoneGeometry.CentreOf(0), owner.Y, owner.X, owner.Y);

        Assert.Equal(ZoneGeometry.CentreOf(4), target.X);
        Assert.NotEqual(owner.Y, target.Y);
        Assert.False(PetRules.IsTooFar(target.X, target.Y, owner.X, owner.Y));
    }

    [Fact]
    public void RecoveryBonusAddsTheBaseTimesTheRate()
    {
        var amounts = PetRules.WithRecoveryBonus(new RecoveryAmounts(15, 10), new PetRecovery(4, 2));

        Assert.Equal(new RecoveryAmounts(75, 30), amounts);
    }

    [Fact]
    public void IdleEmoteWaitStaysInsideItsRange()
    {
        var rng = new Random(7);
        for (var roll = 0; roll < 200; roll++)
        {
            var wait = PetRules.NextIdleEmote(rng).TotalMilliseconds;
            Assert.InRange(wait, PetRules.IdleEmoteMinMs, PetRules.IdleEmoteMaxMs);
        }
    }

    [Fact]
    public void PetPositionMovesAlongItsWalk()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var pet = new PetActor { EntityId = 1, ItemUid = 2, CharacterType = 7002, Name = "pet", MapId = 3 };

        pet.MoveTo(0, 0, 100, 200, start, TimeSpan.FromSeconds(2));

        Assert.True(pet.IsMoving(start.AddSeconds(1)));
        Assert.Equal((50, 100), pet.PositionAt(start.AddSeconds(1)));
        Assert.Equal((100, 200), pet.PositionAt(start.AddSeconds(3)));
        Assert.False(pet.IsMoving(start.AddSeconds(2)));
    }
}
