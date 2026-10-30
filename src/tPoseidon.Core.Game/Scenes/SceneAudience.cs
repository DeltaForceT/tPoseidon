namespace tPoseidon.Core.Game.Scenes;

public static class SceneAudience
{
    public static bool Shares(ushort firstMap, ushort firstKey, ushort secondMap, ushort secondKey) =>
        firstMap == secondMap && firstKey == secondKey;
}
