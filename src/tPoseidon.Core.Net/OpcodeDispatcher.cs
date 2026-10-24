using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net;

public sealed class OpcodeDispatcher
{
    private const int HandlerTableCapacity = 398;

    private readonly Dictionary<Opcode, PacketHandler> _handlers = new(HandlerTableCapacity);
    private PacketHandler? _unknownHandler;
    private PacketGate? _gate;

        public delegate Task<bool> PacketGate(Opcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

        public delegate Task PacketHandler(Opcode opcode, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

        public void Register(Opcode opcode, PacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (!_handlers.TryAdd(opcode, handler))
        {
            throw new InvalidOperationException(
                $"Handler already registered for opcode 0x{(ushort)opcode:X4} ({opcode}).");
        }
    }

        public void Register(ushort opcode, PacketHandler handler)
        => Register((Opcode)opcode, handler);

        public bool Unregister(Opcode opcode) => _handlers.Remove(opcode);

        public void SetGate(PacketGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        _gate = gate;
    }

        public void SetUnknownHandler(PacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _unknownHandler = handler;
    }

        public async Task DispatchAsync(
        Opcode opcode,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        if (_gate is not null && await _gate(opcode, payload, cancellationToken).ConfigureAwait(false)) return;

        if (_handlers.TryGetValue(opcode, out var handler))
        {
            await handler(opcode, payload, cancellationToken).ConfigureAwait(false);
        }
        else if (_unknownHandler is not null)
        {
            await _unknownHandler(opcode, payload, cancellationToken).ConfigureAwait(false);
        }
    }

        public int HandlerCount => _handlers.Count;

        public IReadOnlyCollection<Opcode> RegisteredOpcodes => _handlers.Keys;
}
