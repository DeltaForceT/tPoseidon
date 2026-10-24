using System.Globalization;

namespace tPoseidon.Core.Common.Security;

public sealed class OpcodeAllowance
{
    public string Opcode { get; set; } = "";

    public string Name { get; set; } = "";

    public int Burst { get; set; }

    public double PerSecond { get; set; }

    public static List<OpcodeAllowance> Defaults() =>
    [
        Make(0x0018, "movement", 240, 40),
        Make(0x039F, "npc dialog", 120, 20),
        Make(0x0036, "say", 120, 20),
        Make(0x01A1, "chat bar", 120, 20),
        Make(0x00B6, "head message", 120, 20),
        Make(0x00B3, "trade chat", 120, 20),
        Make(0x1777, "whisper", 120, 20),
        Make(0x1774, "instant chat room", 120, 20),
    ];

    public bool TryParseOpcode(out ushort opcode)
    {
        var text = (Opcode ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ushort.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out opcode);
        }

        return ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out opcode);
    }

    public string? Problem()
    {
        if (!TryParseOpcode(out _))
        {
            return $"opcodeAllowances entry '{Opcode}' is not an opcode like 0x0018.";
        }

        if (Burst <= 0 || !(PerSecond > 0))
        {
            return $"opcodeAllowances entry '{Opcode}' needs a burst and perSecond above zero.";
        }

        return null;
    }

    private static OpcodeAllowance Make(ushort opcode, string name, int burst, double perSecond) => new()
    {
        Opcode = string.Create(CultureInfo.InvariantCulture, $"0x{opcode:X4}"),
        Name = name,
        Burst = burst,
        PerSecond = perSecond,
    };
}
