namespace tPoseidon.Core.Game.Warp;

public static class MemoryPortRules
{
    public const int Effect = 34;
    public const int CustomDataOption = 0x8000;
    public const int SaveRefusedMapOption = 0x10;
    public const int WarpRefusedMapOption = 0x80;
    public const uint WarpRefusedReason = 4;
    public const uint NoSuchSlotError = 104;
    public const uint UndeterminedErrorCode = 0;
    public const int UseSlotDataBytes = sizeof(ushort);
    public const byte ChangeZoneType = TeleportArrival.ChangeZoneType;
    public static readonly TimeSpan WarpDelay = TimeSpan.FromMilliseconds(200);

    public static bool RefusesSave(int mapOption) => (mapOption & SaveRefusedMapOption) != 0;

    public static bool RefusesWarp(int mapOption) => (mapOption & WarpRefusedMapOption) != 0;

    public static bool IsMemoryPort(int effect, int options) => effect == Effect && (options & CustomDataOption) != 0;
}
