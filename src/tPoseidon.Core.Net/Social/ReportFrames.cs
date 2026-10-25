using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed record ReportChatLine(string Name, ushort ChatType, string Text);

public sealed record ReportForm(ushort Slot, string Defendant, string Reporter, string Title, string Description, ushort Kind);

public static class ReportFrames
{
    public const ushort SessionOpen = 0x2712;
    public const ushort TryReport = 0x2713;
    public const ushort AttachChat = 0x2714;
    public const ushort FillReport = 0x2715;
    public const ushort SubmitReport = 0x2716;
    public const ushort CancelReport = 0x2717;
    public const ushort CancelQueued = 0x2718;
    public const ushort IsOnline = 0x2728;

    public const ushort ReportedSelf = 0x271A;
    public const ushort AlreadyReported = 0x271B;
    public const ushort NoFreeSlot = 0x271C;
    public const ushort DefendantIsGameMaster = 0x2727;
    public const ushort SlotGranted = 0x271E;
    public const ushort NoSuchCharacter = 0x2720;
    public const ushort Cancelled = 0x2721;
    public const ushort NothingToCancel = 0x2722;
    public const ushort Queued = 0x2723;
    public const ushort Filed = 0x2724;
    public const ushort OnlineAnswer = 0x2729;

    public const int LineBreakBelow = 0x7F;
    public const int ChattingPack = 0x707;

    private static readonly string[] ChatTypeNames =
    {
        "(normal):", "(party):", "(whisper):", "(instant):", "(card battle):", "(channel chat):", "(trade chat):",
        "(head notice):", "(sms):", "(MyCampTitle):", "(ShopTitle):", "(MyCamp):", "(Guild):",
    };

    public static string ChatTypeText(ushort chatType) =>
        chatType >= 1 && chatType <= ChatTypeNames.Length ? ChatTypeNames[chatType - 1] : "(etc):";

    public static string ChatLineText(ReportChatLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = line.Text.Length < LineBreakBelow ? line.Text + "\n" : line.Text;
        return line.Name + ChatTypeText(line.ChatType) + text;
    }

    public static IReadOnlyList<string> PackChatting(IEnumerable<ReportChatLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var rows = new List<string>();
        var current = new StringBuilder();
        foreach (var line in lines)
        {
            var text = ChatLineText(line);
            if (current.Length > 0 && current.Length + text.Length > ChattingPack)
            {
                rows.Add(current.ToString());
                current.Clear();
            }
            current.Append(text);
        }
        if (current.Length > 0) rows.Add(current.ToString());
        return rows;
    }

    public static string ReadName(ReadOnlySpan<byte> body, Encoding text) => new PacketReader(body).ReadNulTerminated(text);

    public static (ushort Slot, IReadOnlyList<ReportChatLine> Lines) ReadChat(ReadOnlySpan<byte> body, Encoding text)
    {
        var r = new PacketReader(body);
        var slot = r.ReadUInt16();
        var count = r.ReadUInt16();
        var lines = new List<ReportChatLine>(count);
        for (var i = 0; i < count; i++)
        {
            var name = r.ReadNulTerminated(text);
            var type = r.ReadUInt16();
            lines.Add(new ReportChatLine(name, type, r.ReadNulTerminated(text)));
        }
        return (slot, lines);
    }

    public static ReportForm ReadForm(ReadOnlySpan<byte> body, Encoding text)
    {
        var r = new PacketReader(body);
        var slot = r.ReadUInt16();
        var defendant = r.ReadNulTerminated(text);
        var reporter = r.ReadNulTerminated(text);
        var title = r.ReadNulTerminated(text);
        var description = r.ReadNulTerminated(text);
        return new ReportForm(slot, defendant, reporter, title, description, r.ReadUInt16());
    }

    public static (ushort Slot, string Name) ReadSlotAndName(ReadOnlySpan<byte> body, Encoding text)
    {
        var r = new PacketReader(body);
        var slot = r.ReadUInt16();
        return (slot, r.ReadNulTerminated(text));
    }

    public static byte[] Name(string name, Encoding text)
    {
        var w = new PacketWriter(24);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] U32AndName(uint value, string name, Encoding text)
    {
        var w = new PacketWriter(28);
        w.WriteUInt32(value);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] SlotAndName(ushort slot, string name, Encoding text)
    {
        var w = new PacketWriter(26);
        w.WriteUInt16(slot);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }
}
