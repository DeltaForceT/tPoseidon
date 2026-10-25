using System.Text;

namespace tPoseidon.Core.Net.World;

public enum SignKind : byte
{
    None = 0,
    HeadMessage = 2,
    CardBattleTitle = 3,
    FortuneSign = 9,
}

public readonly record struct SignFrame(ushort Opcode, byte[] Payload);

public sealed class PlayerSign
{
    public const ushort HeadMessageShown = 0x00B6;
    public const ushort HeadMessageCleared = 0x00B7;
    public const ushort CardBattleTitleShown = 0x013D;
    public const ushort CardBattleTitleCleared = 0x013E;
    public const ushort FortuneSignShown = 0x03DE;
    public const ushort FortuneSignCleared = 0x03DF;
    public const ushort CardBattleTitleRequest = 0x013D;
    public const ushort CardBattleTitleEndRequest = 0x013E;
    public const ushort FortuneSignRequest = 0x03DE;
    public const ushort FortuneSignEndRequest = 0x03DF;
    public const int TextBytes = 50;

    public SignKind Kind { get; private set; }

    public IReadOnlyList<SignFrame> ShowHeadMessage(ushort entityId, string text, Encoding encoding)
    {
        var frames = new List<SignFrame>(2);
        ClearCardBattleTitle(entityId, frames);
        return Show(frames, SignKind.HeadMessage, HeadMessageShown, entityId, text, encoding);
    }

    public IReadOnlyList<SignFrame> ShowCardBattleTitle(ushort entityId, string text, Encoding encoding)
    {
        var frames = new List<SignFrame>(2);
        ClearHeadMessage(entityId, frames);
        return Show(frames, SignKind.CardBattleTitle, CardBattleTitleShown, entityId, text, encoding);
    }

    public IReadOnlyList<SignFrame> EndCardBattleTitle(ushort entityId)
    {
        if (Kind == SignKind.CardBattleTitle) Kind = SignKind.None;
        return new[] { new SignFrame(CardBattleTitleCleared, Entity(entityId)) };
    }

    public IReadOnlyList<SignFrame> ShowFortuneSign(ushort entityId, string text, Encoding encoding)
    {
        var frames = new List<SignFrame>(3);
        ClearHeadMessage(entityId, frames);
        ClearCardBattleTitle(entityId, frames);
        return Show(frames, SignKind.FortuneSign, FortuneSignShown, entityId, text, encoding);
    }

    public IReadOnlyList<SignFrame> EndFortuneSign(ushort entityId)
    {
        if (Kind == SignKind.FortuneSign) Kind = SignKind.None;
        return new[] { new SignFrame(FortuneSignCleared, Entity(entityId)) };
    }

    public IReadOnlyList<SignFrame> ClearOnAction(ushort entityId)
    {
        var frames = new List<SignFrame>(2);
        ClearHeadMessage(entityId, frames);
        ClearCardBattleTitle(entityId, frames);
        return frames;
    }

    public static byte[] Text(ushort entityId, string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var bytes = encoding.GetBytes(text);
        var w = new PacketWriter(sizeof(ushort) + TextBytes + 1);
        w.WriteUInt16(entityId);
        w.WriteBytes(bytes.AsSpan(0, Math.Min(bytes.Length, TextBytes)));
        w.WriteByte(0);
        return w.ToArray();
    }

    public static byte[] Entity(ushort entityId)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(entityId);
        return w.ToArray();
    }

    private List<SignFrame> Show(List<SignFrame> frames, SignKind kind, ushort opcode, ushort entityId, string text, Encoding encoding)
    {
        Kind = kind;
        frames.Add(new SignFrame(opcode, Text(entityId, text, encoding)));
        return frames;
    }

    private void ClearHeadMessage(ushort entityId, List<SignFrame> frames)
    {
        if (Kind != SignKind.HeadMessage) return;
        Kind = SignKind.None;
        frames.Add(new SignFrame(HeadMessageCleared, Entity(entityId)));
    }

    private void ClearCardBattleTitle(ushort entityId, List<SignFrame> frames)
    {
        if (Kind != SignKind.CardBattleTitle) return;
        Kind = SignKind.None;
        frames.Add(new SignFrame(CardBattleTitleCleared, Entity(entityId)));
    }
}
