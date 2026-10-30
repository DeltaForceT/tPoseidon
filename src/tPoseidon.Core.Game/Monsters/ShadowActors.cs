namespace tPoseidon.Core.Game.Monsters;

public static class ShadowActors
{
    public const int FirstMonsterId = 16000;
    public const int LastMonsterId = 16999;
    public const int FirstNpcId = 17000;
    public const int LastNpcId = 17999;

    public static bool IsMonster(int monsterId) => monsterId is >= FirstMonsterId and <= LastMonsterId;

    public static bool IsNpc(int npcType) => npcType is >= FirstNpcId and <= LastNpcId;
}
