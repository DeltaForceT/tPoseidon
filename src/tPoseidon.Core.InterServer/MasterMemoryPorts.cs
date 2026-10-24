using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public readonly record struct MemoryPortOpenResult(IReadOnlyList<MemoryPort>? Slots, uint Code);

public sealed class MasterMemoryPorts
{
    private readonly MasterChannel _channel;

    public MasterMemoryPorts(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _channel = new MasterChannel(client, selfIndex, "memory port", log);
    }

    public async Task<MemoryPortOpenResult?> OpenAsync(ulong charUid, byte portType, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.McmdMemoryPortOpen, InterServerOpcode.McmdMemoryPortOpenAckOk,
            InterServerOpcode.McmdMemoryPortOpenAckCancel, MemoryPortMessages.EncodeType(portType), ct).ConfigureAwait(false);
        if (reply is not { } answer) return null;
        return answer.Ok
            ? new MemoryPortOpenResult(MemoryPortMessages.DecodeSlots(charUid, answer.Body), 0)
            : new MemoryPortOpenResult(null, MemoryPortMessages.DecodeCode(answer.Body));
    }

    public async Task<MemoryPort?> UseAsync(ulong charUid, MemoryPortSlotRequest request, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.McmdMemoryPortUse, InterServerOpcode.McmdMemoryPortUseAckOk,
            InterServerOpcode.McmdMemoryPortUseAckCancel, MemoryPortMessages.EncodeSlotRequest(request), ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? MemoryPortMessages.DecodeSlot(charUid, answer.Body) : null;
    }

    public async Task<bool> AddAsync(ulong charUid, MemoryPort slot, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.McmdMemoryPortAdd, InterServerOpcode.McmdMemoryPortAddAckOk,
            InterServerOpcode.McmdMemoryPortAddAckCancel, MemoryPortMessages.EncodeSlot(slot), ct).ConfigureAwait(false);
        return reply is { Ok: true };
    }

    public async Task<bool> DeleteAsync(ulong charUid, MemoryPortSlotRequest request, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.McmdMemoryPortDelete, InterServerOpcode.McmdMemoryPortDeleteAckOk,
            InterServerOpcode.McmdMemoryPortDeleteAckCancel, MemoryPortMessages.EncodeSlotRequest(request), ct).ConfigureAwait(false);
        return reply is { Ok: true };
    }
}
