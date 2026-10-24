using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Dialogs;

public readonly record struct NpcScriptRequest(ushort NpcEntityId, uint FunctionId, IReadOnlyList<ushort> Args)
{
    public const ushort InnerOpcode = 0x007F;
    public const int CallHeaderSize = 7;
    public const int ArgCountOffset = CallHeaderSize - 1;
    public const int MaxArgs = byte.MaxValue;

    public byte[] Encode()
    {
        ArgumentNullException.ThrowIfNull(Args);
        if (Args.Count > MaxArgs)
        {
            throw new InvalidOperationException($"A script call carries at most {MaxArgs} arguments, got {Args.Count}.");
        }
        var body = new byte[CallHeaderSize + Args.Count * sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(body, NpcEntityId);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(sizeof(ushort)), FunctionId);
        body[ArgCountOffset] = (byte)Args.Count;
        for (var i = 0; i < Args.Count; i++) BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(CallHeaderSize + i * sizeof(ushort)), Args[i]);
        return TunnelledRequest.Wrap(InnerOpcode, body);
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out NpcScriptRequest request)
    {
        request = default;
        if (!TunnelledRequest.TrySplit(payload, out var innerOpcode, out var bodyOffset) || innerOpcode != InnerOpcode) return false;
        var rest = payload[bodyOffset..];
        if (rest.Length < CallHeaderSize) return false;
        var npcEntityId = BinaryPrimitives.ReadUInt16LittleEndian(rest);
        var functionId = BinaryPrimitives.ReadUInt32LittleEndian(rest[2..]);
        var argc = rest[6];
        rest = rest[CallHeaderSize..];
        if (rest.Length < argc * sizeof(ushort)) return false;
        var args = new ushort[argc];
        for (var i = 0; i < argc; i++) args[i] = BinaryPrimitives.ReadUInt16LittleEndian(rest[(i * sizeof(ushort))..]);
        request = new NpcScriptRequest(npcEntityId, functionId, args);
        return true;
    }
}
