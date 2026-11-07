using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.Trident;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Trident;

public sealed class TridentSessionAdapter
{
    private readonly TcpClientSession _session;
    private readonly TridentPacketCipher _cipher;
    private readonly KeyTable _table;
    private readonly ITridentCipherDerivation _derivation;
    private readonly OpcodeDispatcher _dispatcher;
    private readonly SessionInfo _sessionInfo;

    public TridentSessionAdapter(
        TcpClientSession session,
        TridentPacketCipher cipher,
        KeyTable table,
        ITridentCipherDerivation derivation,
        OpcodeDispatcher dispatcher,
        SessionInfo sessionInfo)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(cipher);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(derivation);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(sessionInfo);

        _session = session;
        _cipher = cipher;
        _table = table;
        _derivation = derivation;
        _dispatcher = dispatcher;
        _sessionInfo = sessionInfo;
    }

        public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var framer = new TridentStreamFramer(_table, _derivation, _sessionInfo.RecvKey);

        await foreach (var chunk in _session.ReadRawAsync(cancellationToken))
        {
            framer.Feed(chunk.Span);
            var madeProgress = false;
            var guard = _session.Guard;
            while (true)
            {
                var status = framer.TryDrain(out var frame, guard.MaxFrameBytes);
                if (status == TridentFrameStatus.NeedMore || (status != TridentFrameStatus.Frame && !guard.IsGuarded))
                {
                    break;
                }

                if (status != TridentFrameStatus.Frame)
                {
                    RejectFrame(guard, framer, status);
                    return;
                }

                madeProgress = true;
                guard.Buffered(0, madeProgress: true);
                TridentDecryptedPacket decoded;
                try
                {
                    decoded = _cipher.Decrypt(frame, _sessionInfo);
                }
                catch (InvalidPacketException ex)
                {
                    var hex = Convert.ToHexString(frame, 0, Math.Min(frame.Length, 16));
                    Console.Error.WriteLine($"[s2] decrypt failed: {ex.Message} (first16={hex}, frameLen={frame.Length}, recvKey=0x{_sessionInfo.RecvKey:X2})");
                    guard.Reject(GuardIncident.MalformedFrame, $"a {frame.Length} B frame did not decrypt, closed");
                    return;
                }
                framer.OnSessionKeyAdvanced(_sessionInfo.RecvKey);
                if (!guard.AllowPacket(decoded.Opcode))
                {
                    return;
                }

                guard.FrameAccepted();

                var opcode = (Opcode)decoded.Opcode;
                try
                {
                    await _dispatcher.DispatchAsync(opcode, decoded.Payload, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    var payloadHex = Convert.ToHexString(
                        decoded.Payload, 0, Math.Min(decoded.Payload.Length, 32));
                    Console.Error.WriteLine(
                        $"[s2] handler for 0x{(ushort)opcode:X4} ({opcode}) threw: {ex.GetType().Name}: {ex.Message} " +
                        $"(payload {decoded.Payload.Length}B, first32={payloadHex})");
                }
            }

            guard.Buffered(framer.Buffered, madeProgress);
        }
    }

    private static void RejectFrame(SessionGuard guard, TridentStreamFramer framer, TridentFrameStatus status)
    {
        if (status == TridentFrameStatus.Oversized)
        {
            guard.Reject(GuardIncident.OversizedFrame, $"a frame declared {framer.PeekLength()} B, the limit is {guard.MaxFrameBytes} B, closed");
            return;
        }

        guard.Reject(GuardIncident.MalformedFrame, $"a frame header did not check out ({framer.Buffered} B buffered), closed");
    }
}
