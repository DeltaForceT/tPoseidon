using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.World;

public enum SignKind : byte
{
    None = 0,
    HeadMessage = 2,
    CardBattleTitle = 3,
    PartyBoardNotice = 7,
    Microphone = 8,
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
    public const ushort MicrophoneRequest = 0x0320;
    public const ushort MicrophoneChat = 0x0320;
    public const ushort MicrophoneBubbleShown = 0x032F;
    public const ushort MicrophoneBubbleCleared = 0x0330;
    public const int TextBytes = 50;
    public const int MicrophoneTextBytes = 0xA1;

    private static readonly ushort[] MicrophoneClearingActions =
        [0x0016, 0x0018, 0x001D, 0x0021, 0x002C, 0x0036, 0x013B, 0x014B, 0x01A1, 0x01D4, 0x01DD, 0x02C9, 0x0317, 0x0320];

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

    public IReadOnlyList<SignFrame> ShowPartyBoardNotice(ushort entityId, byte noticeKind, string text, Encoding encoding)
    {
        var frames = new List<SignFrame>(3);
        ClearHeadMessage(entityId, frames);
        ClearCardBattleTitle(entityId, frames);
        Kind = SignKind.PartyBoardNotice;
        frames.Add(new SignFrame(PartyBoardFrames.HeadNoticeShown, PartyBoardFrames.HeadNoticeShownFor(entityId, noticeKind, text, encoding)));
        return frames;
    }

    public IReadOnlyList<SignFrame> ClearPartyBoardNotice(ushort entityId)
    {
        if (Kind != SignKind.PartyBoardNotice) return Array.Empty<SignFrame>();
        Kind = SignKind.None;
        return new[] { new SignFrame(PartyBoardFrames.HeadNoticeCleared, Entity(entityId)) };
    }

    public IReadOnlyList<SignFrame> ClearOnAction(ushort entityId)
    {
        var frames = new List<SignFrame>(3);
        ClearHeadMessage(entityId, frames);
        ClearCardBattleTitle(entityId, frames);
        ClearMicrophone(entityId, frames);
        return frames;
    }

    public static bool ClearsMicrophone(ushort clientOpcode) => MicrophoneClearingActions.Contains(clientOpcode);

    public IReadOnlyList<SignFrame> ClearMicrophoneOnAction(ushort entityId)
    {
        var frames = new List<SignFrame>(1);
        ClearMicrophone(entityId, frames);
        return frames;
    }

    public IReadOnlyList<SignFrame> ShowMicrophone(ushort entityId, string name, string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var frames = new List<SignFrame>(3);
        ClearHeadMessage(entityId, frames);
        ClearMicrophone(entityId, frames);
        var chat = new PacketWriter(sizeof(ushort) + name.Length + text.Length + 2);
        chat.WriteUInt16(entityId);
        chat.WriteNulTerminated(name, encoding);
        chat.WriteNulTerminated(text, encoding);
        frames.Insert(0, new SignFrame(MicrophoneChat, chat.ToArray()));
        Kind = SignKind.Microphone;
        frames.Add(new SignFrame(MicrophoneBubbleShown, Text(entityId, text, encoding, MicrophoneTextBytes)));
        return frames;
    }

    public static byte[] Text(ushort entityId, string text, Encoding encoding) => Text(entityId, text, encoding, TextBytes);

    public static byte[] Text(ushort entityId, string text, Encoding encoding, int limit)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var bytes = encoding.GetBytes(text);
        var w = new PacketWriter(sizeof(ushort) + limit + 1);
        w.WriteUInt16(entityId);
        w.WriteBytes(bytes.AsSpan(0, Math.Min(bytes.Length, limit)));
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

    private void ClearMicrophone(ushort entityId, List<SignFrame> frames)
    {
        if (Kind != SignKind.Microphone) return;
        Kind = SignKind.None;
        frames.Add(new SignFrame(MicrophoneBubbleCleared, Entity(entityId)));
    }

    private void ClearCardBattleTitle(ushort entityId, List<SignFrame> frames)
    {
        if (Kind != SignKind.CardBattleTitle) return;
        Kind = SignKind.None;
        frames.Add(new SignFrame(CardBattleTitleCleared, Entity(entityId)));
    }
}
