using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Game.Fortune;

namespace tPoseidon.Core.Net.World;

public sealed record FortuneBlock(uint UsesToday, DateTimeOffset AppliedAt, DateTimeOffset Expiry, uint TitleId, FortuneBonus Bonus);

public sealed record FortuneChangeRequest(IReadOnlyList<ulong> JokerUids, IReadOnlyList<uint> Indices);

public readonly record struct FortuneRollRequest(int Power, IReadOnlyList<int> Picks);

public sealed record FortuneRollFrame(bool ThreeOfAKind, IReadOnlyList<FortuneResult> Before, IReadOnlyList<FortuneResult> Final,
    long DurationSeconds, uint TitleId, uint UsesToday, string Award);

public readonly record struct FortunePaidJoker(BagOperation Operation, ulong WireUid);

public static class FortuneFrames
{
    public const int BlockSize = 0x72;
    public const int AppliedSize = 1 + 2 * BlockSize;
    public const byte AppliedByAward = 1;
    public const byte AppliedByStarTears = 0;
    public const int UsesOffset = 0x00;
    public const int AppliedAtOffset = 0x04;
    public const int ExpiryOffset = 0x0C;
    public const int TitleOffset = 0x1C;
    public const int StatsOffset = 0x24;
    public const int RefineOffset = 0x68;
    public const int DropOffset = 0x6C;
    public const int ExpOffset = 0x6E;

    public const int TitleAnnounceSize = 12;
    public const int CardRecordSize = 0x1C;
    public const int ResultRecordSize = 12;
    public const int ResultsSize = FortuneRules.ResultCount * ResultRecordSize;
    public const int RollRequestSize = 16;
    public const int AwardMaxLength = 100;

    public static byte[] TitleAnnounce(ushort entityId, uint titleId)
    {
        var w = new PacketWriter(TitleAnnounceSize);
        w.WriteUInt64(entityId);
        w.WriteUInt32(titleId);
        return w.ToArray();
    }

    public static byte[] Cards(byte usesToday, byte todayExchange, IReadOnlyList<FortuneCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var w = new PacketWriter(3 + cards.Count * CardRecordSize);
        w.WriteByte(usesToday);
        w.WriteByte(todayExchange);
        WriteCards(w, cards);
        return w.ToArray();
    }

    public static byte[] CardsChanged(IReadOnlyList<FortuneCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var w = new PacketWriter(1 + cards.Count * CardRecordSize);
        WriteCards(w, cards);
        return w.ToArray();
    }

    public static bool TryReadChange(ReadOnlySpan<byte> payload, out FortuneChangeRequest request)
    {
        request = new FortuneChangeRequest(Array.Empty<ulong>(), Array.Empty<uint>());
        var r = new PacketReader(payload);
        if (r.Remaining < 2 * sizeof(uint)) return false;
        r.ReadUInt32();
        var jokerCount = r.ReadUInt32();
        if (jokerCount > FortuneRules.CardCount || r.Remaining < jokerCount * sizeof(ulong) + sizeof(uint)) return false;
        var uids = new ulong[jokerCount];
        for (var i = 0; i < uids.Length; i++) uids[i] = r.ReadUInt64();
        var indexCount = r.ReadUInt32();
        if (indexCount > FortuneRules.CardCount || r.Remaining < indexCount * sizeof(uint)) return false;
        var indices = new uint[indexCount];
        for (var i = 0; i < indices.Length; i++) indices[i] = r.ReadUInt32();
        request = new FortuneChangeRequest(uids, indices);
        return true;
    }

    public static bool TryReadRoll(ReadOnlySpan<byte> payload, out FortuneRollRequest request)
    {
        request = default;
        if (payload.Length < RollRequestSize) return false;
        var r = new PacketReader(payload);
        var power = r.ReadUInt32();
        var picks = new int[FortuneRules.ResultCount];
        for (var i = 0; i < picks.Length; i++)
        {
            var pick = r.ReadUInt32();
            picks[i] = pick > FortuneRules.JokerSlot ? FortuneRules.JokerSlot : (int)pick;
        }
        request = new FortuneRollRequest((int)Math.Min(power, FortuneRules.MaxPower), picks);
        return true;
    }

    public static byte[] Paid(ulong characterId, uint money, IReadOnlyList<FortunePaidJoker> jokers)
    {
        ArgumentNullException.ThrowIfNull(jokers);
        var w = new PacketWriter(13 + jokers.Count * 32);
        w.WriteUInt64(characterId);
        w.WriteUInt32(money);
        w.WriteByte((byte)jokers.Count);
        foreach (var joker in jokers)
        {
            w.WriteBytes(QuestResultFrames.ItemOperation(joker.Operation));
            w.WriteUInt64(joker.WireUid);
        }
        return w.ToArray();
    }

    public static byte[] Rolled(FortuneRollFrame roll)
    {
        ArgumentNullException.ThrowIfNull(roll);
        var award = Encoding.ASCII.GetBytes(roll.Award);
        var length = Math.Min(award.Length, AwardMaxLength);
        var w = new PacketWriter(1 + 2 * ResultsSize + 8 + 4 + 4 + 2 + length);
        w.WriteByte(roll.ThreeOfAKind ? (byte)1 : (byte)0);
        WriteResults(w, roll.Before);
        WriteResults(w, roll.Final);
        w.WriteInt64(roll.DurationSeconds);
        w.WriteUInt32(roll.TitleId);
        w.WriteUInt32(roll.UsesToday);
        w.WriteUInt16((ushort)length);
        w.WriteBytes(award.AsSpan(0, length));
        return w.ToArray();
    }

    private static void WriteCards(PacketWriter w, IReadOnlyList<FortuneCard> cards)
    {
        w.WriteByte((byte)cards.Count);
        foreach (var card in cards)
        {
            w.WriteUInt16(card.JokerSet);
            w.WriteUInt16(card.IsBasic);
            w.WriteUInt32(card.CardId);
            w.WriteUInt64(card.ItemUid);
            w.WriteUInt32(BitConverter.SingleToUInt32Bits(card.Weight));
            w.WriteUInt32(0);
            w.WriteUInt32(0);
        }
    }

    private static void WriteResults(PacketWriter w, IReadOnlyList<FortuneResult> results)
    {
        for (var i = 0; i < FortuneRules.ResultCount; i++)
        {
            var result = i < results.Count ? results[i] : default;
            w.WriteUInt32((uint)result.CardIndex);
            w.WriteUInt32(result.CardId);
            w.WriteUInt32(result.Joker ? 1u : 0u);
        }
    }

    public static byte[] Block(FortuneBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var bytes = new byte[BlockSize];
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[UsesOffset..], block.UsesToday);
        BinaryPrimitives.WriteInt64LittleEndian(span[AppliedAtOffset..], block.AppliedAt.ToUnixTimeSeconds());
        BinaryPrimitives.WriteInt64LittleEndian(span[ExpiryOffset..], block.Expiry.ToUnixTimeSeconds());
        BinaryPrimitives.WriteUInt32LittleEndian(span[TitleOffset..], block.TitleId);
        for (var slot = 0; slot < FortuneRules.StatSlots && slot < block.Bonus.Stats.Count; slot++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[(StatsOffset + slot * sizeof(short))..], Clamp(block.Bonus.Stats[slot]));
        }
        BinaryPrimitives.WriteInt16LittleEndian(span[RefineOffset..], Clamp(block.Bonus.Refine));
        BinaryPrimitives.WriteInt16LittleEndian(span[DropOffset..], Clamp(block.Bonus.Drop));
        BinaryPrimitives.WriteInt16LittleEndian(span[ExpOffset..], Clamp(block.Bonus.Exp));
        return bytes;
    }

    public static byte[] Applied(byte source, FortuneBlock block)
    {
        var frame = new byte[AppliedSize];
        frame[0] = source;
        Block(block).CopyTo(frame, 1);
        return frame;
    }

    private static short Clamp(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);
}
