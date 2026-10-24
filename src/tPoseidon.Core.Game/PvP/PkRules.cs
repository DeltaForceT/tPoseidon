namespace tPoseidon.Core.Game.PvP;

public readonly record struct PkZoneEntry(byte PvpMode, byte RecordMode, bool Protected);

public static class PkRules
{
    public const byte NoPvp = 0;
    public const byte PkZoneMode = 1;
    public const byte RecordOff = 0;
    public const byte RecordSkirmish = 1;
    public const byte RecordPeace = 2;

    public const int PkZoneType = 7;
    public const int DuelZoneType = 9;
    public const int PkMapOption = 0x40;

    public static PkZoneEntry ZoneEntry(int zoneType, int mapOption)
    {
        var flagged = (mapOption & PkMapOption) != 0;
        if (zoneType != PkZoneType && !flagged) return new PkZoneEntry(NoPvp, RecordOff, false);
        return new PkZoneEntry(PkZoneMode, flagged ? RecordPeace : RecordOff, true);
    }

    public static byte Toggle(bool on) => on ? RecordSkirmish : RecordPeace;

    public static bool LosesExpOnDeath(byte pvpMode) => pvpMode == NoPvp;
}
