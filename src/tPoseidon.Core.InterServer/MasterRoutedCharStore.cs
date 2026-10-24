using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class MasterRoutedCharStore : ICharStore
{
    private static readonly HashSet<InterServerOpcode> AckOpcodes = new()
    {
        InterServerOpcode.TpCharStoreCallAckOk,
        InterServerOpcode.TpCharStoreCallAckCancel,
    };

    private readonly MasterServerClient _client;
    private readonly ICharStore _fallback;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;
    private readonly TimeSpan _timeout;

    public MasterRoutedCharStore(
        MasterServerClient client,
        ICharStore fallback,
        ushort selfIndex,
        Action<string> log,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(log);
        _client = client;
        _fallback = fallback;
        _selfIndex = selfIndex;
        _log = log;
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public IReadOnlyList<Character> GetCharactersForAccount(uint accountId)
    {
        try
        {
            var w = new PacketWriter(4);
            w.WriteUInt32(accountId);
            var reply = Invoke(0, CharStoreSubOp.GetCharactersForAccount, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"char GetCharactersForAccount account {accountId}: master CANCEL; using local fallback");
                return _fallback.GetCharactersForAccount(accountId);
            }
            return CharStoreWire.DecodeCharacterList(reply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char GetCharactersForAccount account {accountId}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.GetCharactersForAccount(accountId);
        }
    }

    public Character? GetCharacterById(ulong characterId)
    {
        try
        {
            var reply = Invoke(characterId, CharStoreSubOp.GetCharacterById, Array.Empty<byte>(), out var ok);
            if (!ok)
            {
                _log($"char GetCharacterById char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.GetCharacterById(characterId);
            }
            var reader = new PacketReader(reply);
            var present = reader.ReadByte() != 0;
            if (!present) return null;
            return CharStoreWire.DecodeCharacter(reply.AsSpan(1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char GetCharacterById char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.GetCharacterById(characterId);
        }
    }

    public void AddCharacter(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        try
        {
            if (!FireAndForget(0, CharStoreSubOp.AddCharacter, CharStoreWire.EncodeCharacter(character)))
            {
                _log($"char AddCharacter char 0x{character.CharacterId:X16}: master not connected; using local fallback");
                _fallback.AddCharacter(character);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char AddCharacter char 0x{character.CharacterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            _fallback.AddCharacter(character);
        }
    }

    public bool DeleteCharacter(ulong characterId, uint ownerAccountId)
    {
        try
        {
            var w = new PacketWriter(4);
            w.WriteUInt32(ownerAccountId);
            if (!FireAndForget(characterId, CharStoreSubOp.DeleteCharacter, w.ToArray()))
            {
                _log($"char DeleteCharacter char 0x{characterId:X16}: master not connected; using local fallback");
                return _fallback.DeleteCharacter(characterId, ownerAccountId);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char DeleteCharacter char 0x{characterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.DeleteCharacter(characterId, ownerAccountId);
        }
    }

    public bool SetCharacterMap(ulong characterId, ushort mapId)
    {
        try
        {
            var w = new PacketWriter(2);
            w.WriteUInt16(mapId);
            if (!FireAndForget(characterId, CharStoreSubOp.SetCharacterMap, w.ToArray()))
            {
                _log($"char SetCharacterMap char 0x{characterId:X16}: master not connected; using local fallback");
                return _fallback.SetCharacterMap(characterId, mapId);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char SetCharacterMap char 0x{characterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.SetCharacterMap(characterId, mapId);
        }
    }

    public bool SetCharacterHome(ulong characterId, ushort homeMapId)
    {
        try
        {
            var w = new PacketWriter(2);
            w.WriteUInt16(homeMapId);
            if (!FireAndForget(characterId, CharStoreSubOp.SetCharacterHome, w.ToArray()))
            {
                _log($"char SetCharacterHome char 0x{characterId:X16}: master not connected; using local fallback");
                return _fallback.SetCharacterHome(characterId, homeMapId);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char SetCharacterHome char 0x{characterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.SetCharacterHome(characterId, homeMapId);
        }
    }

    public bool SetCharacterPosition(ulong characterId, ushort mapId, ushort positionX, ushort positionY)
    {
        try
        {
            var w = new PacketWriter(6);
            w.WriteUInt16(mapId);
            w.WriteUInt16(positionX);
            w.WriteUInt16(positionY);
            if (!FireAndForget(characterId, CharStoreSubOp.SetCharacterPosition, w.ToArray()))
            {
                _log($"char SetCharacterPosition char 0x{characterId:X16}: master not connected; using local fallback");
                return _fallback.SetCharacterPosition(characterId, mapId, positionX, positionY);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char SetCharacterPosition char 0x{characterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.SetCharacterPosition(characterId, mapId, positionX, positionY);
        }
    }

    public bool SaveCharacterStats(ulong characterId, CharacterStatsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        try
        {
            if (!FireAndForget(characterId, CharStoreSubOp.SaveCharacterStats, CharStoreWire.EncodeStatsSnapshot(snapshot)))
            {
                _log($"char SaveCharacterStats char 0x{characterId:X16}: master not connected; using local fallback");
                return _fallback.SaveCharacterStats(characterId, snapshot);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char SaveCharacterStats char 0x{characterId:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.SaveCharacterStats(characterId, snapshot);
        }
    }

    public uint EditCharacter(ulong characterId, CharacterEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        try
        {
            var reply = Invoke(characterId, CharStoreSubOp.EditCharacter, CharStoreWire.EncodeEdit(edit), out var ok);
            if (ok) return new PacketReader(reply).ReadUInt32();
            _log($"char EditCharacter char 0x{characterId:X16}: master CANCEL; using local fallback");
            return _fallback.EditCharacter(characterId, edit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"char EditCharacter char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.EditCharacter(characterId, edit);
        }
    }

    private byte[] Invoke(ulong charUid, CharStoreSubOp subOp, byte[] body, out bool ok)
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
        ok = reply.Opcode == InterServerOpcode.TpCharStoreCallAckOk;
        return reply.Body;
    }

    private bool FireAndForget(ulong charUid, CharStoreSubOp subOp, byte[] body)
    {
        if (!_client.IsConnected) return false;
        var request = BuildRequest(charUid, subOp, body);
        _client.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        return true;
    }

    private InterServerFrame BuildRequest(ulong charUid, CharStoreSubOp subOp, byte[] body)
    {
        var fullBody = new byte[1 + body.Length];
        fullBody[0] = (byte)subOp;
        Buffer.BlockCopy(body, 0, fullBody, 1, body.Length);

        return new InterServerFrame
        {
            Opcode = InterServerOpcode.TpCharStoreCall,
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
