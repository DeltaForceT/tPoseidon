using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class QuestStoreHandler : IInterServerHandler
{
    private readonly IQuestStore _quests;
    private readonly IQuestHistoryStore _history;

    public QuestStoreHandler(IQuestStore quests, IQuestHistoryStore history)
    {
        ArgumentNullException.ThrowIfNull(quests);
        ArgumentNullException.ThrowIfNull(history);
        _quests = quests;
        _history = history;
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        var charUid = frame.Header.CharUid;
        if (charUid == 0)
        {
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            ctx.Log($"quest-store: invalid chruid=0 from idx={frame.Header.SourceServerIndex}; replied CANCEL");
            return;
        }

        if (frame.Body.Length == 0)
        {
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            ctx.Log($"quest-store char 0x{charUid:X16}: empty body; replied CANCEL");
            return;
        }

        var subOp = (QuestStoreSubOp)frame.Body[0];
        byte[] body = new byte[frame.Body.Length - 1];
        Buffer.BlockCopy(frame.Body, 1, body, 0, body.Length);

        byte[] responseBody;
        try
        {
            responseBody = ExecuteSubOp(subOp, charUid, body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"quest-store char 0x{charUid:X16} sub-op 0x{(byte)subOp:X2}: store threw {ex.GetType().Name}: {ex.Message}; replying CANCEL");
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            return;
        }

        await ReplyOkAsync(session, frame, responseBody, ct).ConfigureAwait(false);
    }

    private byte[] ExecuteSubOp(QuestStoreSubOp subOp, ulong charUid, byte[] body)
    {
        switch (subOp)
        {
            case QuestStoreSubOp.AddActive:
                _quests.AddActive(QuestStoreWire.DecodeQuest(body));
                return Array.Empty<byte>();

            case QuestStoreSubOp.GetActiveForCharacter:
                return QuestStoreWire.EncodeQuestList(_quests.GetActiveForCharacter(charUid));

            case QuestStoreSubOp.FindActive:
            {
                var reader = new PacketReader(body);
                var questType = reader.ReadByte();
                var questId = reader.ReadInt32();
                var monsterId = reader.ReadInt32();
                var found = _quests.FindActive(charUid, questType, questId, monsterId);
                var payload = new PacketWriter(64);
                payload.WriteByte((byte)(found is null ? 0 : 1));
                if (found is not null)
                {
                    payload.WriteBytes(QuestStoreWire.EncodeQuest(found));
                }
                return payload.ToArray();
            }

            case QuestStoreSubOp.CountActiveOfType:
            {
                var reader = new PacketReader(body);
                var questType = reader.ReadByte();
                var count = _quests.CountActiveOfType(charUid, questType);
                var payload = new PacketWriter(4);
                payload.WriteInt32(count);
                return payload.ToArray();
            }

            case QuestStoreSubOp.UpdateMonsterProgress:
            {
                var reader = new PacketReader(body);
                var questId = reader.ReadInt32();
                var monsterId = reader.ReadInt32();
                var killCount = reader.ReadInt16();
                var recordTime = reader.ReadInt32();
                var ok = _quests.UpdateMonsterProgress(charUid, questId, monsterId, killCount, recordTime);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case QuestStoreSubOp.RemoveActive:
            {
                var reader = new PacketReader(body);
                var questType = reader.ReadByte();
                var questId = reader.ReadInt32();
                var monsterId = reader.ReadInt32();
                var ok = _quests.RemoveActive(charUid, questType, questId, monsterId);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case QuestStoreSubOp.HistoryFind:
            {
                var reader = new PacketReader(body);
                var questType = reader.ReadByte();
                var questId = reader.ReadInt32();
                var monsterId = reader.ReadInt32();
                var found = _history.Find(charUid, questType, questId, monsterId);
                var payload = new PacketWriter(64);
                payload.WriteByte((byte)(found is null ? 0 : 1));
                if (found is not null)
                {
                    payload.WriteBytes(QuestStoreWire.EncodeQuestHistory(found));
                }
                return payload.ToArray();
            }

            case QuestStoreSubOp.HistoryInsert:
                _history.Insert(QuestStoreWire.DecodeQuestHistory(body));
                return Array.Empty<byte>();

            case QuestStoreSubOp.HistoryUpdate:
            {
                var ok = _history.Update(QuestStoreWire.DecodeQuestHistory(body));
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case QuestStoreSubOp.HistoryGetForCharacter:
                return QuestStoreWire.EncodeQuestHistoryList(_history.GetForCharacter(charUid));

            case QuestStoreSubOp.HistoryAppendLog:
                _history.AppendLog(QuestStoreWire.DecodeQuestHistory(body));
                return Array.Empty<byte>();

            case QuestStoreSubOp.HistoryCompletedToday:
            {
                var reader = new PacketReader(body);
                var questType = reader.ReadByte();
                var questId = reader.ReadInt32();
                var monsterId = reader.ReadInt32();
                var referenceUtcTicks = reader.ReadInt64();
                var ok = _history.CompletedToday(charUid, questType, questId, monsterId, new DateTime(referenceUtcTicks, DateTimeKind.Utc));
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case QuestStoreSubOp.HistoryHasCompletedAtLeast:
            {
                var reader = new PacketReader(body);
                var questType = reader.ReadByte();
                var questId = reader.ReadInt32();
                var monsterId = reader.ReadInt32();
                var threshold = reader.ReadByte();
                var ok = _history.HasCompletedAtLeast(charUid, questType, questId, monsterId, threshold);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            default:
                throw new InvalidOperationException($"unknown sub-op 0x{(byte)subOp:X2}");
        }
    }

    private static Task ReplyOkAsync(MasterServerSession session, InterServerFrame request, byte[] body, CancellationToken ct) =>
        ReplyAsync(session, request, InterServerOpcode.TpQuestStoreCallAckOk, body, ct);

    private static Task ReplyCancelAsync(MasterServerSession session, InterServerFrame request, CancellationToken ct) =>
        ReplyAsync(session, request, InterServerOpcode.TpQuestStoreCallAckCancel, Array.Empty<byte>(), ct);

    private static Task ReplyAsync(MasterServerSession session, InterServerFrame request, InterServerOpcode opcode, byte[] body, CancellationToken ct)
    {
        var reply = new InterServerFrame
        {
            Opcode = opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = request.Header.SourceServerIndex,
                CharUid = request.Header.CharUid,
                RequestId = request.Header.RequestId,
            },
            Body = body,
        };
        return session.SendAsync(reply, ct);
    }
}
