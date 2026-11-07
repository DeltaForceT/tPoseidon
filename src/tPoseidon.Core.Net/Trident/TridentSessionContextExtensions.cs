using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.Trident;

namespace tPoseidon.Core.Net.Trident;

public static class TridentSessionContextExtensions
{
    public static Task SendFrameAsync(
        this TridentSessionContext ctx,
        Opcode op,
        byte[] payload,
        byte randKey = 0x00,
        CancellationToken ct = default)
        => SendFrameAsync(ctx, (ushort)op, payload, randKey, ct);

        public static Task SendFrameAsync(
        this TridentSessionContext ctx,
        ushort op,
        byte[] payload,
        byte randKey = 0x00,
        CancellationToken ct = default)
    {
        return SendFrameAsync(ctx, op, payload, randKey, blockId: 0, ct);
    }

    public static Task SendFrameAsync(
        this TridentSessionContext ctx,
        Opcode op,
        byte[] payload,
        byte randKey,
        ushort blockId,
        CancellationToken ct = default)
        => SendFrameAsync(ctx, (ushort)op, payload, randKey, blockId, ct);

    public static Task SendFrameAsync(
        this TridentSessionContext ctx,
        ushort op,
        byte[] payload,
        byte randKey,
        ushort blockId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(payload);
        return ctx.Session.SendAsync(() => ctx.Cipher.Encrypt(
            opcode: op,
            sequence: blockId,
            randKey: randKey,
            flags: PackingFlags.PayloadEncrypted,
            plaintextPayload: payload,
            session: ctx.SessionInfo), ct);
    }

    public static Task SendUnencryptedFrameAsync(
        this TridentSessionContext ctx,
        ushort op,
        byte[] payload,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(payload);
        return ctx.Session.SendAsync(() => ctx.Cipher.Encrypt(
            opcode: op,
            sequence: 0,
            randKey: 0,
            flags: PackingFlags.None,
            plaintextPayload: payload,
            session: ctx.SessionInfo), ct);
    }
}
