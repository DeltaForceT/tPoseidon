namespace tPoseidon.Core.Game.Monsters;

public static class TrialGateMaps
{
    public const ushort FirstMapId = 247;
    public const ushort LastMapId = 262;

    public static bool Contains(int mapId) => mapId >= FirstMapId && mapId <= LastMapId;
}
