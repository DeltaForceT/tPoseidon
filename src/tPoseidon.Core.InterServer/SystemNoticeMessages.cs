using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class SystemNoticeMessages
{
    public const int FieldBytes = 160;

    public static byte[] Encode(SystemNotice notice, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(notice.Prefix);
        ArgumentNullException.ThrowIfNull(notice.Text);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(notice.Prefix.Length + notice.Text.Length + 2);
        w.WriteNulTerminated(notice.Prefix, text);
        w.WriteNulTerminated(notice.Text, text);
        return w.ToArray();
    }

    public static SystemNotice Decode(ReadOnlySpan<byte> body, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var r = new PacketReader(body);
        return new SystemNotice(r.ReadNulTerminated(text), r.ReadNulTerminated(text));
    }

    public static string Line(SystemNotice notice, string islandName, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(islandName);
        ArgumentNullException.ThrowIfNull(text);
        return $"[{Clip(notice.Prefix, text)}] {islandName} - {Clip(notice.Text, text)}";
    }

    private static string Clip(string value, Encoding text)
    {
        var end = value.Length;
        while (end > 0 && text.GetByteCount(value.AsSpan(0, end)) > FieldBytes) end--;
        return value[..end];
    }
}

public sealed class MasterSystemNotice
{
    private readonly MasterChannel _channel;
    private readonly Encoding _text;

    public MasterSystemNotice(Func<MasterServerClient?> client, ushort selfIndex, Encoding text, Action<string> log)
    {
        _channel = new MasterChannel(client, selfIndex, "system notice", log);
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public Task SendAsync(SystemNotice notice, CancellationToken ct) =>
        _channel.SendAsync(0, InterServerOpcode.CcmdSysMegaphone, SystemNoticeMessages.Encode(notice, _text), ct);
}
