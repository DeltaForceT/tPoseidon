using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class MasterRoutedQuestStore : IQuestStore
{
    private readonly MasterRoutedQuestCall _call;
    private readonly IQuestStore _fallback;
    private readonly Action<string> _log;

    public MasterRoutedQuestStore(
        MasterServerClient client,
        IQuestStore fallback,
        ushort selfIndex,
        Action<string> log,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(log);
        _call = new MasterRoutedQuestCall(client, selfIndex, log, timeout);
        _fallback = fallback;
        _log = log;
    }

    public void AddActive(Quest quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        try
        {
            var body = QuestStoreWire.EncodeQuest(quest);
            if (!_call.FireAndForget(quest.CharacterId, QuestStoreSubOp.AddActive, body))
            {
                _log($"quest AddActive char 0x{quest.CharacterId:X16}: master not connected; using local fallback");
                _fallback.AddActive(quest);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest AddActive char 0x{quest.CharacterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            _fallback.AddActive(quest);
        }
    }

    public IReadOnlyList<Quest> GetActiveForCharacter(ulong characterId)
    {
        try
        {
            var reply = _call.Invoke(characterId, QuestStoreSubOp.GetActiveForCharacter, Array.Empty<byte>(), out var ok);
            if (!ok)
            {
                _log($"quest GetActiveForCharacter char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.GetActiveForCharacter(characterId);
            }
            return QuestStoreWire.DecodeQuestList(reply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest GetActiveForCharacter char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.GetActiveForCharacter(characterId);
        }
    }

    public Quest? FindActive(ulong characterId, byte questType, int questId, int monsterId)
    {
        try
        {
            var w = new PacketWriter(16);
            w.WriteByte(questType);
            w.WriteInt32(questId);
            w.WriteInt32(monsterId);
            var reply = _call.Invoke(characterId, QuestStoreSubOp.FindActive, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"quest FindActive char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.FindActive(characterId, questType, questId, monsterId);
            }
            var reader = new PacketReader(reply);
            var present = reader.ReadByte() != 0;
            if (!present) return null;
            return QuestStoreWire.DecodeQuest(reply.AsSpan(1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest FindActive char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.FindActive(characterId, questType, questId, monsterId);
        }
    }

    public int CountActiveOfType(ulong characterId, byte questType)
    {
        try
        {
            var w = new PacketWriter(2);
            w.WriteByte(questType);
            var reply = _call.Invoke(characterId, QuestStoreSubOp.CountActiveOfType, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"quest CountActiveOfType char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.CountActiveOfType(characterId, questType);
            }
            var reader = new PacketReader(reply);
            return reader.ReadInt32();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest CountActiveOfType char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.CountActiveOfType(characterId, questType);
        }
    }

    public bool UpdateMonsterProgress(ulong characterId, int questId, int monsterId, short killCount, int recordTime)
    {
        try
        {
            var w = new PacketWriter(16);
            w.WriteInt32(questId);
            w.WriteInt32(monsterId);
            w.WriteInt16(killCount);
            w.WriteInt32(recordTime);
            if (!_call.FireAndForget(characterId, QuestStoreSubOp.UpdateMonsterProgress, w.ToArray()))
            {
                _log($"quest UpdateMonsterProgress char 0x{characterId:X16}: master not connected; using local fallback");
                return _fallback.UpdateMonsterProgress(characterId, questId, monsterId, killCount, recordTime);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest UpdateMonsterProgress char 0x{characterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.UpdateMonsterProgress(characterId, questId, monsterId, killCount, recordTime);
        }
    }

    public bool RemoveActive(ulong characterId, byte questType, int questId, int monsterId)
    {
        try
        {
            var w = new PacketWriter(16);
            w.WriteByte(questType);
            w.WriteInt32(questId);
            w.WriteInt32(monsterId);
            if (!_call.FireAndForget(characterId, QuestStoreSubOp.RemoveActive, w.ToArray()))
            {
                _log($"quest RemoveActive char 0x{characterId:X16}: master not connected; using local fallback");
                return _fallback.RemoveActive(characterId, questType, questId, monsterId);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest RemoveActive char 0x{characterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.RemoveActive(characterId, questType, questId, monsterId);
        }
    }
}

public sealed class MasterRoutedQuestHistoryStore : IQuestHistoryStore
{
    private readonly MasterRoutedQuestCall _call;
    private readonly IQuestHistoryStore _fallback;
    private readonly Action<string> _log;

    public MasterRoutedQuestHistoryStore(
        MasterServerClient client,
        IQuestHistoryStore fallback,
        ushort selfIndex,
        Action<string> log,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(log);
        _call = new MasterRoutedQuestCall(client, selfIndex, log, timeout);
        _fallback = fallback;
        _log = log;
    }

    public QuestHistory? Find(ulong characterId, byte questType, int questId, int monsterId)
    {
        try
        {
            var w = new PacketWriter(16);
            w.WriteByte(questType);
            w.WriteInt32(questId);
            w.WriteInt32(monsterId);
            var reply = _call.Invoke(characterId, QuestStoreSubOp.HistoryFind, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"quest-history Find char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.Find(characterId, questType, questId, monsterId);
            }
            var reader = new PacketReader(reply);
            var present = reader.ReadByte() != 0;
            if (!present) return null;
            return QuestStoreWire.DecodeQuestHistory(reply.AsSpan(1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest-history Find char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.Find(characterId, questType, questId, monsterId);
        }
    }

    public void Insert(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            if (!_call.FireAndForget(row.CharacterId, QuestStoreSubOp.HistoryInsert, QuestStoreWire.EncodeQuestHistory(row)))
            {
                _log($"quest-history Insert char 0x{row.CharacterId:X16}: master not connected; using local fallback");
                _fallback.Insert(row);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest-history Insert char 0x{row.CharacterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            _fallback.Insert(row);
        }
    }

    public bool Update(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            if (!_call.FireAndForget(row.CharacterId, QuestStoreSubOp.HistoryUpdate, QuestStoreWire.EncodeQuestHistory(row)))
            {
                _log($"quest-history Update char 0x{row.CharacterId:X16}: master not connected; using local fallback");
                return _fallback.Update(row);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest-history Update char 0x{row.CharacterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.Update(row);
        }
    }

    public IReadOnlyList<QuestHistory> GetForCharacter(ulong characterId)
    {
        try
        {
            var reply = _call.Invoke(characterId, QuestStoreSubOp.HistoryGetForCharacter, Array.Empty<byte>(), out var ok);
            if (!ok)
            {
                _log($"quest-history GetForCharacter char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.GetForCharacter(characterId);
            }
            return QuestStoreWire.DecodeQuestHistoryList(reply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest-history GetForCharacter char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.GetForCharacter(characterId);
        }
    }

    public void AppendLog(QuestHistory row)
    {
        ArgumentNullException.ThrowIfNull(row);
        try
        {
            if (!_call.FireAndForget(row.CharacterId, QuestStoreSubOp.HistoryAppendLog, QuestStoreWire.EncodeQuestHistory(row)))
            {
                _log($"quest-history AppendLog char 0x{row.CharacterId:X16}: master not connected; using local fallback");
                _fallback.AppendLog(row);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest-history AppendLog char 0x{row.CharacterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            _fallback.AppendLog(row);
        }
    }

    public bool CompletedToday(ulong characterId, byte questType, int questId, int monsterId, DateTime referenceUtc)
    {
        try
        {
            var w = new PacketWriter(24);
            w.WriteByte(questType);
            w.WriteInt32(questId);
            w.WriteInt32(monsterId);
            w.WriteInt64(referenceUtc.ToUniversalTime().Ticks);
            var reply = _call.Invoke(characterId, QuestStoreSubOp.HistoryCompletedToday, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"quest-history CompletedToday char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.CompletedToday(characterId, questType, questId, monsterId, referenceUtc);
            }
            var reader = new PacketReader(reply);
            return reader.ReadByte() != 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest-history CompletedToday char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.CompletedToday(characterId, questType, questId, monsterId, referenceUtc);
        }
    }

    public bool HasCompletedAtLeast(ulong characterId, byte questType, int questId, int monsterId, byte threshold)
    {
        try
        {
            var w = new PacketWriter(16);
            w.WriteByte(questType);
            w.WriteInt32(questId);
            w.WriteInt32(monsterId);
            w.WriteByte(threshold);
            var reply = _call.Invoke(characterId, QuestStoreSubOp.HistoryHasCompletedAtLeast, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"quest-history HasCompletedAtLeast char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.HasCompletedAtLeast(characterId, questType, questId, monsterId, threshold);
            }
            var reader = new PacketReader(reply);
            return reader.ReadByte() != 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"quest-history HasCompletedAtLeast char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.HasCompletedAtLeast(characterId, questType, questId, monsterId, threshold);
        }
    }
}

internal sealed class MasterRoutedQuestCall
{
    private static readonly HashSet<InterServerOpcode> AckOpcodes = new()
    {
        InterServerOpcode.TpQuestStoreCallAckOk,
        InterServerOpcode.TpQuestStoreCallAckCancel,
    };

    private readonly MasterServerClient _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;
    private readonly TimeSpan _timeout;

    public MasterRoutedQuestCall(MasterServerClient client, ushort selfIndex, Action<string> log, TimeSpan? timeout)
    {
        _client = client;
        _selfIndex = selfIndex;
        _log = log;
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public byte[] Invoke(ulong charUid, QuestStoreSubOp subOp, byte[] body, out bool ok)
    {
        if (!_client.IsConnected)
        {
            ok = false;
            return Array.Empty<byte>();
        }

        var request = BuildRequest(charUid, subOp, body);
        var reply = _client
            .SendAndAwaitReplyAsync(request, AckOpcodes, _timeout, CancellationToken.None)
            .GetAwaiter().GetResult();
        ok = reply.Opcode == InterServerOpcode.TpQuestStoreCallAckOk;
        return reply.Body;
    }

    public bool FireAndForget(ulong charUid, QuestStoreSubOp subOp, byte[] body)
    {
        if (!_client.IsConnected) return false;
        var request = BuildRequest(charUid, subOp, body);
        _client.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        return true;
    }

    private InterServerFrame BuildRequest(ulong charUid, QuestStoreSubOp subOp, byte[] body)
    {
        var fullBody = new byte[1 + body.Length];
        fullBody[0] = (byte)subOp;
        Buffer.BlockCopy(body, 0, fullBody, 1, body.Length);

        return new InterServerFrame
        {
            Opcode = InterServerOpcode.TpQuestStoreCall,
            Header = new InterServerHeader
            {
                SourceServerIndex = _selfIndex,
                DestServerIndex = 0,
                CharUid = charUid,
                RequestId = _client.AllocateRequestId(),
            },
            Body = fullBody,
        };
    }
}
