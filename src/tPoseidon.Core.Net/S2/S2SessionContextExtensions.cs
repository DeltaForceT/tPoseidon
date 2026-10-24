using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Core.Net.S2;

public static class S2SessionContextExtensions
{
    public static Task SendFrameAsync(
        this S2SessionContext ctx,
        Opcode op,
        byte[] payload,
        byte randKey = 0x00,
        CancellationToken ct = default)
        => SendFrameAsync(ctx, (ushort)op, payload, randKey, ct);

        public static Task SendFrameAsync(
        this S2SessionContext ctx,
        ushort op,
        byte[] payload,
        byte randKey = 0x00,
        CancellationToken ct = default)
    {
        return SendFrameAsync(ctx, op, payload, randKey, blockId: 0, ct);
    }

    public static Task SendFrameAsync(
        this S2SessionContext ctx,
        Opcode op,
        byte[] payload,
        byte randKey,
        ushort blockId,
        CancellationToken ct = default)
        => SendFrameAsync(ctx, (ushort)op, payload, randKey, blockId, ct);

    public static Task SendFrameAsync(
        this S2SessionContext ctx,
        ushort op,
        byte[] payload,
        byte randKey,
        ushort blockId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(payload);
        var frame = ctx.Cipher.Encrypt(
            opcode: op,
            sequence: blockId,
            randKey: randKey,
            flags: PackingFlags.PayloadEncrypted,
            plaintextPayload: payload,
            session: ctx.SessionInfo);
        return ctx.Session.SendAsync(frame, ct);
    }

    public static Task SendUnencryptedFrameAsync(
        this S2SessionContext ctx,
        ushort op,
        byte[] payload,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(payload);
        var frame = ctx.Cipher.Encrypt(
            opcode: op,
            sequence: 0,
            randKey: 0,
            flags: PackingFlags.None,
            plaintextPayload: payload,
            session: ctx.SessionInfo);
        return ctx.Session.SendAsync(frame, ct);
    }
}
