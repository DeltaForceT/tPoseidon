using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Ranking;

namespace tPoseidon.Core.Net.Social;

public sealed class RankService
{
    public const ushort Request = 0x1D50;
    public const ushort Reply = 0x1D51;

    private readonly IRankingStore _store;
    private readonly Encoding _text;

    public RankService(IRankingStore store, Encoding text)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public byte[]? Answer(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 1) return null;
        var r = new PacketReader(payload);
        var rankType = r.ReadByte();
        switch (rankType)
        {
            case RankingRules.MonsterQuestRank when payload.Length >= 4:
                var monsterId = r.ReadUInt16();
                var monsterKind = r.ReadByte();
                var monsterRows = RankingRules.MonsterRankingType(monsterKind) is { } monsterType
                    ? _store.Top(monsterType, monsterId)
                    : Array.Empty<RankingRow>();
                var w = new PacketWriter(8 + monsterRows.Count * 32);
                w.WriteByte(RankingRules.MonsterQuestRank);
                w.WriteUInt16(monsterId);
                w.WriteByte(monsterKind);
                w.WriteByte((byte)monsterRows.Count);
                foreach (var row in monsterRows)
                {
                    w.WriteUInt16((ushort)row.RankingOrder);
                    w.WriteNulTerminated(row.CharName, _text);
                    w.WriteUInt32((uint)RankingRules.MonsterValue(RankingRules.MonsterRankingType(monsterKind)!.Value, row.Value1, row.Value2));
                }
                return w.ToArray();
            case RankingRules.CardBattleRank when payload.Length >= 2:
                var cardKind = r.ReadByte();
                var cardRows = RankingRules.CardBattleRankingType(cardKind) is { } cardType
                    ? _store.Top(cardType, RankingRules.CardBattleMonster)
                    : Array.Empty<RankingRow>();
                var c = new PacketWriter(4 + cardRows.Count * 32);
                c.WriteByte(RankingRules.CardBattleRank);
                c.WriteByte(cardKind);
                c.WriteByte((byte)cardRows.Count);
                foreach (var row in cardRows)
                {
                    c.WriteUInt16((ushort)row.RankingOrder);
                    c.WriteNulTerminated(row.CharName, _text);
                    c.WriteByte((byte)row.CharType);
                    c.WriteUInt16((ushort)row.Value1);
                }
                return c.ToArray();
            default:
                return null;
        }
    }
}
