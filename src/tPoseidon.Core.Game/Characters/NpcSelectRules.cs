namespace tPoseidon.Core.Game.Characters;

public static class NpcSelectRules
{
    public const int Range = 300;

    public static bool Accepts(bool npcKnown, int playerX, int playerY, int npcX, int npcY)
    {
        if (!npcKnown) return false;
        var dx = (long)npcX - playerX;
        var dy = (long)npcY - playerY;
        return dx * dx + dy * dy <= (long)Range * Range;
    }
}
