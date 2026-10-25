using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed record WeddingArticleRequest(byte CardType, string CardMessage, string Title, byte MaleHair, byte FemaleHair);

public sealed record WeddingHallListing(ulong Wdid, ulong MasterId, string Title, bool Locked, uint MapId, bool Started);

public static class CoupleSocialFrames
{
    public const ushort SessionOpen = (ushort)Opcode.HANDSHAKE_FINAL;
    public const ushort LoverPosition = 0x2E81;
    public const ushort LoverPositionReply = 0x2E82;
    public const ushort LoverOffline = 0x2E83;
    public const ushort MoveToLover = 0x2E84;
    public const uint MoveToLoverBarredZoneType = 7;
    public const ushort OrderLoverPosition = 0xFA;
    public const ushort OrderMoveToLover = 0xFB;
    public const int LoverSpotSize = sizeof(ulong) + sizeof(uint) + 2 * sizeof(short);
    public const ushort HallListRequest = 0x2E96;
    public const ushort HallList = 0x2E97;
    public const ushort ArticleListRequest = 0x2EA0;
    public const ushort ArticleList = 0x2EA1;
    public const ushort WriteArticle = 0x2EA2;
    public const ushort ArticleCreated = 0x2EA3;
    public const ushort ModifyArticle = 0x2EA4;
    public const ushort ArticleUpdated = 0x2EA5;
    public const ushort DeleteArticle = 0x2EA6;
    public const ushort ArticleRemoved = 0x2EA7;
    public const ushort BreakUp = 0x2EA8;
    public const ushort BrokenUp = 0x2EA9;
    public const ushort RequestDivorce = 0x2EAA;
    public const ushort DivorceRequested = 0x2EAB;
    public const ushort DivorceAsked = 0x2EAF;
    public const ushort DivorceAnswer = 0x2EB0;
    public const ushort DivorceDone = 0x2EB2;
    public const ushort CoupleInfo = 0x2EB4;

    public const int ArticleMessageBytesLimit = 0x9B;
    public const int ArticleTitleBytesLimit = 0x41;

    public const ushort LinkMakeCouple = 0x2E7F;
    public const ushort LinkCoupleInfoRequest = 0x2E8A;
    public const ushort LinkMarriageOption = 0x2EAC;

    public static byte[] Info(CoupleRecord couple, ulong recipientId, bool loginPush, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(couple);
        ArgumentNullException.ThrowIfNull(text);
        var partner = couple.Partner(recipientId);
        var w = new PacketWriter(96);
        w.WriteUInt64(couple.CoupleCode);
        w.WriteUInt64(partner?.CharacterId ?? 0);
        w.WriteUInt32(couple.State);
        w.WriteNulTerminated(partner?.Name ?? string.Empty, text);
        w.WriteUInt32(couple.AdviceIndex1);
        w.WriteUInt32(couple.AdviceIndex2);
        w.WriteUInt32(couple.RelationPoint);
        w.WriteUInt32(couple.LovePoint);
        w.WriteUInt32(couple.FriendPoint);
        w.WriteUInt32((uint)(partner?.Job ?? 0));
        w.WriteUInt64(0);
        w.WriteUInt64(0);
        w.WriteUInt16((ushort)couple.RingItemIndex);
        w.WriteByte((byte)couple.TitleIndex);
        w.WriteByte(loginPush ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] LoverZone(ushort zone)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(zone);
        w.WriteUInt16(zone);
        return w.ToArray();
    }

    public static byte[] Removed(ulong coupleCode)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(coupleCode);
        return w.ToArray();
    }

    public static byte[] DivorceAskedBody(string requesterName, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(requesterName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(requesterName.Length + 1);
        w.WriteNulTerminated(requesterName, text);
        return w.ToArray();
    }

    public static byte[] Character(ulong characterId)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static bool TryReadMoveToLover(ReadOnlySpan<byte> payload, out ushort zone)
    {
        zone = 0;
        if (payload.Length < sizeof(ushort) + sizeof(uint)) return false;
        zone = new PacketReader(payload).ReadUInt16();
        return true;
    }

    public static byte[] MoveRefused(uint reason)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(reason);
        return w.ToArray();
    }

    public static byte[] LoverPositionOrder(ulong loverId, ulong requesterId)
    {
        var w = new PacketWriter(2 * sizeof(ulong));
        w.WriteUInt64(loverId);
        w.WriteUInt64(requesterId);
        return w.ToArray();
    }

    public static bool TryReadLoverPositionOrder(ReadOnlySpan<byte> payload, out ulong loverId, out ulong requesterId)
    {
        loverId = 0;
        requesterId = 0;
        if (payload.Length < 2 * sizeof(ulong)) return false;
        var r = new PacketReader(payload);
        loverId = r.ReadUInt64();
        requesterId = r.ReadUInt64();
        return true;
    }

    public static byte[] LoverSpot(ulong requesterId, uint zone, short x, short y)
    {
        var w = new PacketWriter(LoverSpotSize);
        w.WriteUInt64(requesterId);
        w.WriteUInt32(zone);
        w.WriteInt16(x);
        w.WriteInt16(y);
        return w.ToArray();
    }

    public static bool TryReadLoverSpot(ReadOnlySpan<byte> payload, out ulong requesterId, out uint zone, out short x, out short y)
    {
        requesterId = 0;
        zone = 0;
        x = 0;
        y = 0;
        if (payload.Length < LoverSpotSize) return false;
        var r = new PacketReader(payload);
        requesterId = r.ReadUInt64();
        zone = r.ReadUInt32();
        x = r.ReadInt16();
        y = r.ReadInt16();
        return true;
    }

    public static bool TryReadAnswer(ReadOnlySpan<byte> payload, out bool accept)
    {
        accept = false;
        if (payload.Length < 1) return false;
        accept = payload[0] != 0;
        return true;
    }

    public static byte[] MakeCouple(ulong maleId, ulong femaleId)
    {
        var w = new PacketWriter(16);
        w.WriteUInt64(maleId);
        w.WriteUInt64(femaleId);
        return w.ToArray();
    }

    public static bool TryReadMakeCouple(ReadOnlySpan<byte> payload, out ulong maleId, out ulong femaleId)
    {
        maleId = femaleId = 0;
        if (payload.Length < 16) return false;
        var r = new PacketReader(payload);
        maleId = r.ReadUInt64();
        femaleId = r.ReadUInt64();
        return true;
    }

    public static bool TryReadCharacter(ReadOnlySpan<byte> payload, out ulong characterId)
    {
        characterId = 0;
        if (payload.Length < 8) return false;
        characterId = new PacketReader(payload).ReadUInt64();
        return true;
    }

    public static byte[] MarriageOption(ulong coupleCode, ushort ring, ushort dress, byte title)
    {
        var w = new PacketWriter(13);
        w.WriteUInt64(coupleCode);
        w.WriteUInt16(ring);
        w.WriteUInt16(dress);
        w.WriteByte(title);
        return w.ToArray();
    }

    public static bool TryReadMarriageOption(ReadOnlySpan<byte> payload, out ulong coupleCode, out ushort ring, out ushort dress, out byte title)
    {
        coupleCode = 0;
        ring = dress = 0;
        title = 0;
        if (payload.Length < 13) return false;
        var r = new PacketReader(payload);
        coupleCode = r.ReadUInt64();
        ring = r.ReadUInt16();
        dress = r.ReadUInt16();
        title = r.ReadByte();
        return true;
    }

    public static byte[] Halls(IReadOnlyList<(WeddingHallListing Hall, CoupleRecord Couple)> halls, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(halls);
        ArgumentNullException.ThrowIfNull(text);
        var listed = halls
            .Select(entry => (entry.Hall, Groom: entry.Couple.Members.FirstOrDefault(member => !member.Female), Bride: entry.Couple.Members.FirstOrDefault(member => member.Female)))
            .Where(entry => entry.Groom is not null && entry.Bride is not null)
            .Take(ushort.MaxValue)
            .ToArray();
        var w = new PacketWriter(2 + listed.Length * 96);
        w.WriteUInt16((ushort)listed.Length);
        foreach (var (hall, groom, bride) in listed)
        {
            w.WriteUInt64(hall.Wdid);
            w.WriteUInt64(hall.MasterId);
            w.WriteNulTerminated(hall.Title, text);
            w.WriteByte(hall.Locked ? (byte)1 : (byte)0);
            w.WriteUInt32(hall.MapId);
            w.WriteUInt32((uint)groom!.Job);
            w.WriteNulTerminated(groom.Name, text);
            w.WriteUInt32((uint)bride!.Job);
            w.WriteNulTerminated(bride.Name, text);
            w.WriteByte(hall.Started ? (byte)1 : (byte)0);
        }
        return w.ToArray();
    }

    public static byte[] Articles(IReadOnlyList<WeddingArticle> articles, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(articles);
        ArgumentNullException.ThrowIfNull(text);
        var listed = articles.Take(ushort.MaxValue).ToArray();
        var w = new PacketWriter(2 + listed.Length * 160);
        w.WriteUInt16((ushort)listed.Length);
        foreach (var article in listed) WriteArticleEntry(w, article, text);
        return w.ToArray();
    }

    public static byte[] Article(WeddingArticle article, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(160);
        WriteArticleEntry(w, article, text);
        return w.ToArray();
    }

    public static WeddingArticleRequest? ReadArticle(ReadOnlySpan<byte> payload, Encoding text, out bool textRefused)
    {
        ArgumentNullException.ThrowIfNull(text);
        textRefused = false;
        if (payload.Length < 1) return null;
        var cardType = payload[0];
        var rest = payload[1..];
        if (NextText(ref rest, ArticleMessageBytesLimit, text) is not { } message || NextText(ref rest, ArticleTitleBytesLimit, text) is not { } title)
        {
            textRefused = true;
            return null;
        }
        if (rest.Length < 2) return null;
        return new WeddingArticleRequest(cardType, message, title, rest[0], rest[1]);
    }

    private static string? NextText(ref ReadOnlySpan<byte> rest, int byteLimit, Encoding text)
    {
        var length = rest.IndexOf((byte)0);
        if (length < 0 || length >= byteLimit) return null;
        var value = text.GetString(rest[..length]);
        rest = rest[(length + 1)..];
        return value;
    }

    private static void WriteArticleEntry(PacketWriter w, WeddingArticle article, Encoding text)
    {
        w.WriteUInt64(article.CoupleCode);
        w.WriteByte(article.CardType);
        w.WriteUInt16((ushort)Math.Clamp(article.DressType, 0, ushort.MaxValue));
        w.WriteByte(article.MaleHair);
        w.WriteByte(article.FemaleHair);
        w.WriteByte(article.MaleJob);
        w.WriteByte(article.FemaleJob);
        w.WriteNulTerminated(article.CardMessage, text);
        w.WriteNulTerminated(article.Title, text);
        w.WriteNulTerminated(article.MaleName, text);
        w.WriteNulTerminated(article.FemaleName, text);
    }
}
