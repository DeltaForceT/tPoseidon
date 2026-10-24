using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Pets;

public static class PetRules
{
    public const int PetItemType = 13;
    public const int FollowRadius = 80;
    public const int TargetRings = 3;
    public const int IdleEmoteMinMs = 8000;
    public const int IdleEmoteMaxMs = 20000;
    public const byte IdleEmote = 0;

    public static readonly TimeSpan FollowPeriod = TimeSpan.FromMilliseconds(300);

    private const int FirstLookBoundary = 0x3DB8;
    private const int FirstLookOffset = 0x9C4;
    private const int SecondLookBoundary = 70000;
    private const int SecondLookOffset = 0x2260;
    private const int ThirdLookBoundary = 90000;
    private const int ThirdLookOffset = 0xF550;
    private const int FourthLookOffset = 0x14DFC;

    public static int CharacterType(int lookItemId) => lookItemId switch
    {
        < FirstLookBoundary => lookItemId + FirstLookOffset,
        < SecondLookBoundary => lookItemId - SecondLookOffset,
        < ThirdLookBoundary => lookItemId - ThirdLookOffset,
        _ => lookItemId - FourthLookOffset,
    };

    public static bool IsPet(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Type == PetItemType;
    }

    public static bool IsTooFar(int petX, int petY, int ownerX, int ownerY)
    {
        long dx = petX - ownerX;
        long dy = petY - ownerY;
        return dx * dx + dy * dy > (long)FollowRadius * FollowRadius;
    }

    public static (int X, int Y) FollowTarget(ZoneGeometry? geometry, int petX, int petY, int ownerX, int ownerY)
    {
        if (geometry is null) return (ownerX, ownerY);
        var ownerCellX = ZoneGeometry.CellOf(ownerX);
        var ownerCellY = ZoneGeometry.CellOf(ownerY);
        for (var ring = 1; ring <= TargetRings; ring++)
        {
            (int X, int Y)? best = null;
            var bestDistance = long.MaxValue;
            for (var cellY = ownerCellY - ring; cellY <= ownerCellY + ring; cellY++)
            {
                for (var cellX = ownerCellX - ring; cellX <= ownerCellX + ring; cellX++)
                {
                    if (Math.Max(Math.Abs(cellX - ownerCellX), Math.Abs(cellY - ownerCellY)) != ring) continue;
                    if (!geometry.ContainsCell(cellX, cellY) || !geometry.IsFreeDestination(cellX, cellY)) continue;
                    var x = ZoneGeometry.CentreOf(cellX);
                    var y = ZoneGeometry.CentreOf(cellY);
                    if (IsTooFar(x, y, ownerX, ownerY)) continue;
                    long dx = x - petX;
                    long dy = y - petY;
                    var distance = dx * dx + dy * dy;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = (x, y);
                }
            }
            if (best is { } found) return found;
        }
        return (ownerX, ownerY);
    }

    public static TimeSpan NextIdleEmote(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return TimeSpan.FromMilliseconds(rng.Next(IdleEmoteMinMs, IdleEmoteMaxMs + 1));
    }

    public static RecoveryAmounts WithRecoveryBonus(RecoveryAmounts amounts, PetRecovery bonus) => new(
        amounts.Hp + amounts.Hp * bonus.HpRate,
        amounts.Mp + amounts.Mp * bonus.MpRate);
}

public readonly record struct PetRecovery(int HpRate, int MpRate);
