namespace tPoseidon.Core.Game.Characters;

public static class CharacterNameRules
{
    public const int MinimumBytes = 4;
    public const int MaximumBytes = 16;
    public const uint InvalidNameCode = 1019;

    private const byte LastControl = 0x20;
    private const byte FirstLead = 0x80;
    private const byte Dot = (byte)'.';
    private const byte Quote = (byte)'\'';
    private const byte Slash = (byte)'/';
    private const byte Comma = (byte)',';
    private const byte Backtick = (byte)'`';
    private const byte Filler = 0xFF;
    private const byte SpecialLead = 0xC6;
    private const byte SpecialLeadFirstBadTrail = 0x53;
    private const byte FirstBadTrailForHighLeads = 0x41;
    private const byte FirstUserLead = 0xC9;
    private const byte LastUserLead = 0xFE;
    private const byte FirstBadTrail = 0xA1;
    private const ushort RejectedPair = 0xA4D4;

    public static bool Accepts(ReadOnlySpan<byte> name)
    {
        if (name.Length == 0) return false;
        if (name[0] is Dot or Quote or Slash) return false;
        var at = 0;
        for (; at < name.Length; at++)
        {
            var lead = name[at];
            if (lead <= LastControl || lead is Backtick or Comma or Quote) return false;
            var next = at + 1 < name.Length ? name[at + 1] : (byte)0;
            if (next == 0)
            {
                if (lead is Dot or Quote) return false;
                continue;
            }
            if (lead < FirstLead) continue;
            if (RejectsPair(lead, next)) return false;
            at++;
        }
        return at >= MinimumBytes && at <= MaximumBytes;
    }

    private static bool RejectsPair(byte lead, byte trail)
    {
        if (lead == Filler || trail == Filler) return true;
        if (lead > SpecialLead && trail >= FirstBadTrailForHighLeads && trail < FirstBadTrail) return true;
        if (lead == SpecialLead && trail >= SpecialLeadFirstBadTrail && trail < FirstBadTrail) return true;
        if (lead is FirstUserLead or LastUserLead) return true;
        return ((lead << 8) | trail) == RejectedPair;
    }
}
