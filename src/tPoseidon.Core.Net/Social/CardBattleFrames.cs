using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.CardBattle;

namespace tPoseidon.Core.Net.Social;

public readonly record struct CardBattleStatusOrder(ulong CharacterId, ushort ClientId, CardBattleStatus Status)
{
    public bool Winner => Status.Winner;

    public uint WonCardItemId => Status.WonCardItemId;
}

public static class CardBattleFrames
{
    public const ushort MakeBattle = 0x1F41;
    public const ushort MakeRefused = 0x1F43;
    public const ushort Ask = 0x1F44;
    public const ushort Accepted = 0x1F45;
    public const ushort AskRefused = 0x1F46;
    public const ushort Asked = 0x1F47;
    public const ushort AnswerYes = 0x1F49;
    public const ushort OpponentStart = 0x1F4A;
    public const ushort AnswerRefused = 0x1F4B;
    public const ushort AnswerNo = 0x1F4C;
    public const ushort Leave = 0x1F4D;
    public const ushort MasterStart = 0x1F4E;
    public const ushort OpponentLeft = 0x1F50;
    public const ushort Deck = 0x1F51;
    public const ushort DeckRefused = 0x1F53;
    public const ushort BattleCards = 0x1F54;
    public const ushort Drop = 0x1F55;
    public const ushort Dropped = 0x1F56;
    public const ushort DropRefused = 0x1F57;
    public const ushort OpponentDropped = 0x1F58;
    public const ushort RoundResult = 0x1F5D;
    public const ushort DrawResult = 0x1F5E;
    public const ushort FinalResult = 0x1F60;
    public const ushort ReserveExit = 0x1F61;
    public const ushort Chance = 0x1F6C;
    public const ushort ChanceDropped = 0x1F6D;
    public const ushort ChanceRefused = 0x1F6E;
    public const ushort OpponentChanceDropped = 0x1F6F;
    public const ushort ExitReserved = 0x1F62;
    public const ushort OpponentExitReserved = 0x1F64;
    public const ushort Chat = 0x1F65;
    public const ushort ChatEcho = 0x1F66;
    public const ushort OpponentChat = 0x1F68;
    public const ushort SelectRule = 0x1F70;
    public const ushort RuleSelected = 0x1F71;
    public const ushort RuleRefused = 0x1F72;
    public const ushort OpponentRule = 0x1F73;
    public const ushort ResultOk = 0x1F75;
    public const ushort ResultWaiting = 0x1F76;
    public const ushort ResultClosed = 0x1F77;
    public const ushort BattleError = 0x1F7B;

    public const ushort OrderStatusUpdate = 0x9A;
    public const ushort OrderStatusReply = 0x99;
    public const int StatusSize = 0x36;
    public const int StatusReplySize = 0x35;
    public const byte StatusOk = 0;
    public const byte StatusNoPlayer = 1;
    public const byte StatusNoOpponent = 2;
    public const byte StatusBagFull = 3;
    public const byte ErrorNoPlayer = 0x1B;
    public const byte ErrorNoOpponent = 0x1C;
    public const byte ErrorNotInBattle = 0x1D;
    public const int RewardBlockSize = 0x25;

    public const byte AlreadyInBattle = 3;
    public const byte NoBattle = 2;
    public const byte NotStandby = 5;
    public const byte TargetNotFound = 7;
    public const byte TargetBusy = 8;
    public const byte Refused = 9;
    public const byte NoAsk = 0x0B;
    public const byte OpponentMissing = 0x19;

    public const int PlayerRecordSize = 0x48;
    public const int StatBlockSize = 0x24;
    public const int SideBlockSize = 0xEA;
    public const int SideDeckOffset = 10;
    public const int NameSize = 20;
    public const int StatCount = 9;
    public const byte SidesInBattle = 2;

    public static byte[] Code(byte code) => new[] { code };

    public static byte[] Named(string name, Encoding text)
    {
        var w = new PacketWriter(24);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] Left(byte reason, string name, Encoding text)
    {
        var w = new PacketWriter(24);
        w.WriteByte(reason);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] Stats(CardBattleStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var w = new PacketWriter(StatBlockSize);
        WriteStats(w, stats);
        return w.ToArray();
    }

    public static byte[] Player(BattleSide side, CardBattleStats stats, Encoding text)
    {
        var w = new PacketWriter(PlayerRecordSize);
        WritePlayer(w, side, stats, text);
        return w.ToArray();
    }

    public static byte[] MasterStarted(CardBattleStats own, BattleSide opponent, CardBattleStats opponentStats, Encoding text)
    {
        var w = new PacketWriter(StatBlockSize + PlayerRecordSize);
        WriteStats(w, own);
        WritePlayer(w, opponent, opponentStats, text);
        return w.ToArray();
    }

    public static byte[] OpponentStarted(CardBattleMatch match, CardBattleStats masterStats, CardBattleStats opponentStats, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(match);
        var opponent = match.Opponent ?? throw new ArgumentException("The battle has no opponent.", nameof(match));
        var w = new PacketWriter(7 + PlayerRecordSize * 2);
        w.WriteUInt16(match.BattleId);
        w.WriteUInt16(match.Master.ClientId);
        w.WriteUInt16(opponent.ClientId);
        w.WriteByte(SidesInBattle);
        WritePlayer(w, match.Master, masterStats, text);
        WritePlayer(w, opponent, opponentStats, text);
        return w.ToArray();
    }

    public static byte[] Cards(byte round, BattleSide own, BattleSide other)
    {
        var w = new PacketWriter(1 + SideBlockSize * 2);
        w.WriteByte(round);
        WriteSide(w, own);
        WriteSide(w, other);
        return w.ToArray();
    }

    public static byte[] Round(ushort roundWinnerClientId, byte round, BattleSide own, BattleSide other)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(other);
        var w = new PacketWriter(3 + SideBlockSize * 2 + ScoreBlock.Size * 2);
        w.WriteUInt16(roundWinnerClientId);
        w.WriteByte(round);
        WriteSide(w, own);
        WriteSide(w, other);
        WriteScore(w, own.Score);
        WriteScore(w, other.Score);
        return w.ToArray();
    }

    public static byte[] Final(CardBattleMatch match, BattleSide own, CardBattleStats ownStats, CardBattleStats otherStats)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(own);
        var other = match.Other(own) ?? throw new ArgumentException("The battle has no opponent.", nameof(match));
        var w = new PacketWriter(12 + SideBlockSize * 2 + ScoreBlock.Size * 2 + RewardBlockSize * 2 + StatBlockSize * 2);
        var winner = match.Winner;
        w.WriteUInt16(winner is null ? (ushort)0 : ReferenceEquals(winner, match.Master) ? (ushort)0 : (ushort)1);
        w.WriteUInt16(winner?.ClientId ?? 0);
        w.WriteUInt16(0);
        w.WriteUInt16(0);
        w.WriteUInt16(CardBattleRules.FirstPlayCount);
        w.WriteUInt16(0);
        WriteSide(w, own);
        WriteSide(w, other);
        WriteScore(w, own.Score);
        WriteScore(w, other.Score);
        WriteReward(w, own.Reward);
        WriteReward(w, other.Reward);
        WriteStats(w, ownStats);
        WriteStats(w, otherStats);
        return w.ToArray();
    }

    public static byte[] Status(CardBattleMatch match, BattleSide side)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(side);
        var other = match.Other(side);
        var won = ReferenceEquals(match.Winner, side);
        var payout = side.Payout;
        var w = new PacketWriter(StatusSize);
        w.WriteUInt64(side.CharacterId);
        w.WriteUInt16(side.ClientId);
        WriteScore(w, side.Score);
        w.WriteByte(0);
        w.WriteUInt32(payout.WonCardItemId);
        w.WriteByte(won ? (byte)1 : (byte)0);
        w.WriteUInt64(payout.OpponentCharacterId);
        w.WriteUInt32(payout.LostCardItemId);
        w.WriteUInt32(payout.PresentItemId);
        w.WriteByte(won && side.Set is not null ? (byte)1 : (byte)0);
        w.WriteByte(won && side.Score.Wins > CardBattleRules.PerfectWins ? (byte)1 : (byte)0);
        w.WriteByte(other?.Set is not null ? (byte)1 : (byte)0);
        w.WriteByte(other?.ChanceUsed == true ? (byte)1 : (byte)0);
        w.WriteUInt16(won && side.Set is { } set ? (ushort)set.Grade : (ushort)0);
        w.WriteUInt32(payout.SecondWonCardItemId);
        w.WriteUInt32(payout.SecondLostCardItemId);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static CardBattleStatusOrder? ReadStatus(ReadOnlySpan<byte> data)
    {
        if (data.Length < StatusSize) return null;
        var r = new PacketReader(data);
        var characterId = r.ReadUInt64();
        var clientId = r.ReadUInt16();
        var wins = r.ReadByte();
        var score = r.ReadInt16();
        r.Skip(4);
        var overDistance = r.ReadByte() != 0;
        var card = r.ReadUInt32();
        var winner = r.ReadByte() != 0;
        var opponentId = r.ReadUInt64();
        var lostCard = r.ReadUInt32();
        var present = r.ReadUInt32();
        var special = r.ReadByte() != 0;
        var perfect = r.ReadByte() != 0;
        var opponentHoldsSet = r.ReadByte() != 0;
        var opponentUsedChance = r.ReadByte() != 0;
        var specialBonus = r.ReadInt16();
        var secondWonCard = r.ReadUInt32();
        var secondLostCard = r.ReadUInt32();
        var noExp = r.ReadByte() != 0;
        return new CardBattleStatusOrder(characterId, clientId,
            new CardBattleStatus(wins, score, overDistance, card, winner, present, special, perfect, specialBonus, noExp)
            {
                LostCardItemId = lostCard,
                OpponentCharacterId = opponentId,
                OpponentHoldsSet = opponentHoldsSet,
                OpponentUsedChance = opponentUsedChance,
                SecondWonCardItemId = secondWonCard,
                SecondLostCardItemId = secondLostCard,
            });
    }

    public static byte[] StatusReply(ulong characterId, ushort clientId, byte code, bool winner, CardBattleRewardBlock reward, bool deckReady)
    {
        ArgumentNullException.ThrowIfNull(reward);
        var w = new PacketWriter(StatusReplySize);
        w.WriteUInt32(code);
        w.WriteUInt64(characterId);
        w.WriteUInt16(clientId);
        w.WriteUInt32((uint)reward.Level);
        w.WriteByte(winner ? (byte)1 : (byte)0);
        w.WriteByte(reward.Special ? (byte)1 : (byte)0);
        w.WriteUInt32((uint)reward.Exp);
        w.WriteUInt32Field(reward.ExpTotal);
        w.WriteUInt32(reward.WonCardItemId);
        w.WriteUInt32((uint)reward.Money);
        w.WriteUInt32(reward.PresentItemId);
        w.WriteUInt32(reward.LostCardItemId);
        w.WriteUInt32(reward.SecondWonCardItemId);
        w.WriteUInt32(reward.SecondLostCardItemId);
        w.WriteByte(deckReady ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static (ulong CharacterId, ushort ClientId, byte Code, CardBattleRewardBlock Reward)? ReadStatusReply(ReadOnlySpan<byte> data)
    {
        if (data.Length < StatusReplySize) return null;
        var r = new PacketReader(data);
        var code = (byte)r.ReadUInt32();
        var characterId = r.ReadUInt64();
        var clientId = r.ReadUInt16();
        var level = (int)r.ReadUInt32();
        r.Skip(1);
        var special = r.ReadByte() != 0;
        var exp = (int)r.ReadUInt32();
        var expTotal = r.ReadUInt32();
        var card = r.ReadUInt32();
        var money = (int)r.ReadUInt32();
        var present = r.ReadUInt32();
        var lostCard = r.ReadUInt32();
        var secondWonCard = r.ReadUInt32();
        var secondLostCard = r.ReadUInt32();
        return (characterId, clientId, code, new CardBattleRewardBlock(level, special, exp, expTotal, card, lostCard, money, present, secondWonCard, secondLostCard));
    }

    private static void WriteReward(PacketWriter w, CardBattleRewardBlock reward)
    {
        w.WriteUInt32((uint)reward.Level);
        w.WriteByte(reward.Special ? (byte)1 : (byte)0);
        w.WriteUInt32((uint)reward.Exp);
        w.WriteUInt32Field(reward.ExpTotal);
        w.WriteUInt32(reward.WonCardItemId);
        w.WriteUInt32(reward.LostCardItemId);
        w.WriteUInt32((uint)reward.Money);
        w.WriteUInt32(reward.PresentItemId);
        w.WriteUInt32(reward.SecondWonCardItemId);
        w.WriteUInt32(reward.SecondLostCardItemId);
    }

    public static byte[] Card(BattleCard card)
    {
        var w = new PacketWriter(BattleCard.Size);
        WriteCard(w, card);
        return w.ToArray();
    }

    public static bool TryReadDeck(ReadOnlySpan<byte> payload, out BattleCard[] deck)
    {
        deck = Array.Empty<BattleCard>();
        if (payload.Length < BattleCard.Size * CardBattleRules.DeckSize) return false;
        var r = new PacketReader(payload);
        deck = new BattleCard[CardBattleRules.DeckSize];
        for (var slot = 0; slot < deck.Length; slot++) deck[slot] = ReadCard(ref r);
        return true;
    }

    public static bool TryReadDrop(ReadOnlySpan<byte> payload, out byte slot, out BattleCard card)
    {
        slot = 0;
        card = default;
        if (payload.Length < 1 + BattleCard.Size) return false;
        var r = new PacketReader(payload);
        slot = r.ReadByte();
        card = ReadCard(ref r);
        return true;
    }

    public static bool TryReadText(ReadOnlySpan<byte> payload, Encoding text, out string value)
    {
        value = string.Empty;
        try
        {
            var r = new PacketReader(payload);
            value = r.ReadNulTerminated(text);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    private static BattleCard ReadCard(ref PacketReader r) =>
        new(r.ReadUInt64(), r.ReadUInt32(), r.ReadUInt32(), r.ReadByte(), r.ReadUInt32(), r.ReadUInt32(), r.ReadByte());

    private static void WriteCard(PacketWriter w, BattleCard card)
    {
        w.WriteUInt64(card.ItemUid);
        w.WriteUInt32(card.ItemId);
        w.WriteUInt32(card.CardType);
        w.WriteByte(card.Number);
        w.WriteUInt32(card.Result);
        w.WriteUInt32(card.TieBreak);
        w.WriteByte(card.Steal);
    }

    private static void WriteSide(PacketWriter w, BattleSide side)
    {
        ArgumentNullException.ThrowIfNull(side);
        w.WriteZero(SideDeckOffset);
        for (var slot = 0; slot < CardBattleRules.DeckSize; slot++)
        {
            if (slot < side.Deck.Length) WriteCard(w, side.Deck[slot].Encoded());
            else w.WriteZero(BattleCard.Size);
        }
        w.WriteZero(SideBlockSize - SideDeckOffset - BattleCard.Size * CardBattleRules.DeckSize);
    }

    private static void WriteScore(PacketWriter w, ScoreBlock score)
    {
        w.WriteByte(score.Wins);
        w.WriteInt16(score.Score);
        w.WriteInt16(score.LastDelta);
        w.WriteByte(score.Streak);
        w.WriteByte(score.ChanceAvailable);
    }

    private static void WriteStats(PacketWriter w, CardBattleStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var values = new[] { stats.WinPoint, stats.WinCount, stats.BattleCount, stats.Dis, stats.SpecialDis, stats.TotalBattleToday, stats.OtherBattleToday };
        foreach (var value in values) w.WriteUInt32((uint)Math.Max(0, value));
        w.WriteZero((StatCount - values.Length) * sizeof(uint));
    }

    private static void WritePlayer(PacketWriter w, BattleSide side, CardBattleStats stats, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(side);
        ArgumentNullException.ThrowIfNull(text);
        w.WriteUInt16(side.ClientId);
        w.WriteUInt32((uint)Math.Max(0, side.Level));
        w.WriteUInt16(0);
        var name = text.GetBytes(side.Name);
        var length = Math.Min(name.Length, NameSize - 1);
        w.WriteBytes(name.AsSpan(0, length));
        w.WriteZero(NameSize - length);
        w.WriteUInt32(0);
        w.WriteUInt32(0);
        WriteStats(w, stats);
    }
}
