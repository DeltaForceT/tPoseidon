using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters.Strategies;

namespace tPoseidon.Core.Game.Monsters;

public sealed class AggroDetector
{
    public event Action<MonsterEntity, ushort>? TargetAcquired;

    public void Tick(MonsterEntity mob, IMonsterStrategy strategy, IZoneAggroScanner scanner, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(mob);
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(scanner);

        if (strategy is not AggressiveMonsterStrategy aggressive) return;
        if (now < aggressive.NextScanAt) return;

        aggressive.OnTick(mob, now);

        if (mob.AiViewRange <= 0) return;
        var targetId = scanner.FindNearestHostile(mob, mob.AiViewRange);
        if (targetId == 0) return;

        if (mob.AddAttacker(targetId))
        {
            TargetAcquired?.Invoke(mob, targetId);
        }
    }
}
