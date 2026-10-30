using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer;

public sealed class MegaphoneDelivery : IMegaphoneDelivery
{
    private readonly MasterServerHost _host;

    public MegaphoneDelivery(MasterServerHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public IReadOnlyCollection<ushort> Targets() =>
        _host.TryGetSibling(ServerIdentity.System1.ServerIndex, out _) ? [ServerIdentity.System1.ServerIndex] : [];

    public Task SendAsync(ushort target, QueuedMegaphone shout, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(shout);
        if (!_host.TryGetSibling(target, out var session))
        {
            throw new ObjectDisposedException(nameof(MasterServerSession), $"system server {target} is not connected");
        }
        return session.SendAsync(new InterServerFrame
        {
            Opcode = InterServerOpcode.SysCmdMegaphoneMessage,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = target,
                CharUid = shout.CharacterId,
                RequestId = 0,
            },
            Body = MegaphoneMessages.EncodePost(new MegaphonePost(shout.Code, shout.Kind, shout.Message), ClientText.Encoding),
        }, ct);
    }

    public Task CheckValidityAsync(ushort target, CancellationToken ct)
    {
        if (!_host.TryGetSibling(target, out var session))
        {
            throw new ObjectDisposedException(nameof(MasterServerSession), $"system server {target} is not connected");
        }
        return session.SendAsync(new InterServerFrame
        {
            Opcode = InterServerOpcode.SysCmdCheckValidity,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = target,
                CharUid = 0,
                RequestId = 0,
            },
            Body = [],
        }, ct);
    }
}
