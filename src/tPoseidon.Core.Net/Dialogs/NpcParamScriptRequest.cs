using System.Buffers.Binary;
using System.Text;

namespace tPoseidon.Core.Net.Dialogs;

public enum NpcScriptParameterKind : byte
{
    Number = 0,
    Text = 1,
}

public readonly record struct NpcScriptParameter(byte Kind, int Number, string Text)
{
    public static NpcScriptParameter OfNumber(int number) => new((byte)NpcScriptParameterKind.Number, number, string.Empty);

    public static NpcScriptParameter OfText(string text) => new((byte)NpcScriptParameterKind.Text, 0, text);
}

public readonly record struct NpcParamScriptRequest(
    ushort NpcEntityId,
    byte FunctionId,
    IReadOnlyList<NpcScriptParameter> Parameters,
    IReadOnlyList<ushort> Args)
{
    public const int MaxParameters = 8;
    public const int MaxArgs = byte.MaxValue;

    private const int HeaderSize = sizeof(ushort) + sizeof(byte) + sizeof(byte);

    public int NumberAt(int index) =>
        index >= 0 && index < Parameters.Count && Parameters[index].Kind == (byte)NpcScriptParameterKind.Number ? Parameters[index].Number : 0;

    public byte[] Encode()
    {
        ArgumentNullException.ThrowIfNull(Parameters);
        ArgumentNullException.ThrowIfNull(Args);
        if (Parameters.Count > MaxParameters)
        {
            throw new InvalidOperationException($"A parameter script call carries at most {MaxParameters} parameters, got {Parameters.Count}.");
        }
        if (Args.Count > MaxArgs)
        {
            throw new InvalidOperationException($"A parameter script call carries at most {MaxArgs} arguments, got {Args.Count}.");
        }
        using var body = new MemoryStream();
        Span<byte> word = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteUInt16LittleEndian(word, NpcEntityId);
        body.Write(word[..sizeof(ushort)]);
        body.WriteByte(FunctionId);
        body.WriteByte((byte)Parameters.Count);
        foreach (var parameter in Parameters)
        {
            body.WriteByte(parameter.Kind);
            if (parameter.Kind == (byte)NpcScriptParameterKind.Number)
            {
                BinaryPrimitives.WriteInt32LittleEndian(word, parameter.Number);
                body.Write(word);
            }
            else if (parameter.Kind == (byte)NpcScriptParameterKind.Text)
            {
                body.Write(Encoding.UTF8.GetBytes(parameter.Text));
                body.WriteByte(0);
            }
        }
        body.WriteByte((byte)Args.Count);
        foreach (var arg in Args)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(word, arg);
            body.Write(word[..sizeof(ushort)]);
        }
        return body.ToArray();
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out NpcParamScriptRequest request)
    {
        request = default;
        if (payload.Length < HeaderSize) return false;
        var npcEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        var functionId = payload[sizeof(ushort)];
        var parameterCount = payload[sizeof(ushort) + sizeof(byte)];
        if (parameterCount > MaxParameters) return false;
        var rest = payload[HeaderSize..];
        var parameters = new NpcScriptParameter[parameterCount];
        for (var i = 0; i < parameterCount; i++)
        {
            if (rest.IsEmpty) return false;
            var kind = rest[0];
            rest = rest[sizeof(byte)..];
            switch (kind)
            {
                case (byte)NpcScriptParameterKind.Number:
                    if (rest.Length < sizeof(int)) return false;
                    parameters[i] = NpcScriptParameter.OfNumber(BinaryPrimitives.ReadInt32LittleEndian(rest));
                    rest = rest[sizeof(int)..];
                    break;
                case (byte)NpcScriptParameterKind.Text:
                    var end = rest.IndexOf((byte)0);
                    if (end < 0) return false;
                    parameters[i] = NpcScriptParameter.OfText(Encoding.UTF8.GetString(rest[..end]));
                    rest = rest[(end + 1)..];
                    break;
                default:
                    parameters[i] = new NpcScriptParameter(kind, 0, string.Empty);
                    break;
            }
        }
        if (rest.IsEmpty) return false;
        var argCount = rest[0];
        rest = rest[sizeof(byte)..];
        if (rest.Length < argCount * sizeof(ushort)) return false;
        var args = new ushort[argCount];
        for (var i = 0; i < argCount; i++) args[i] = BinaryPrimitives.ReadUInt16LittleEndian(rest[(i * sizeof(ushort))..]);
        request = new NpcParamScriptRequest(npcEntityId, functionId, parameters, args);
        return true;
    }
}
