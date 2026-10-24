using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters;

public interface IZoneAggroScanner
{
    ushort FindNearestHostile(MonsterEntity scanner, int radius);
}
