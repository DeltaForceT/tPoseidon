using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public readonly record struct MegaphoneQueued(bool Accepted, uint Value);

public sealed class MasterMegaphone
{
    private readonly MasterChannel _channel;
    private readonly Encoding _text;

    public MasterMegaphone(Func<MasterServerClient?> client, ushort selfIndex, Encoding text, Action<string> log)
    {
        _channel = new MasterChannel(client, selfIndex, "megaphone", log);
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public async Task<MegaphoneQueued?> EnqueueAsync(ulong charUid, MegaphoneEnqueueRequest request, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.CcmdMegaphone, InterServerOpcode.CcmdMegaphoneAckOk,
            InterServerOpcode.CcmdMegaphoneAckCancel, MegaphoneMessages.EncodeRequest(request, _text), ct).ConfigureAwait(false);
        return reply is not { } answer
            ? null
            : new MegaphoneQueued(answer.Ok, answer.Ok ? MegaphoneMessages.DecodeValue(answer.Body) : MegaphoneMessages.DecodeValue(answer.Body));
    }
}
