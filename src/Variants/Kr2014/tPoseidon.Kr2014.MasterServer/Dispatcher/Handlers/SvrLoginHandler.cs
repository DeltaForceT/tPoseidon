using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class SvrLoginHandler : IInterServerHandler
{
    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ServerLoginRequest request;
        try
        {
            request = ServerLoginRequest.DecodeBody(frame.Body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"{session.RemoteEndpoint} bad McmdSvrLogin body: {ex.Message}");
            return;
        }

        if (ctx.ExpectedSiblingPasswords.Count > 0 &&
            ctx.ExpectedSiblingPasswords.TryGetValue(request.ServerName, out var expectedPassword))
        {
            if (!string.Equals(expectedPassword, request.Password, StringComparison.Ordinal))
            {
                ctx.Log($"sibling REJECTED: {request.ServerName} from {session.RemoteEndpoint} sent wrong password (expected {expectedPassword.Length} chars, got {(request.Password ?? string.Empty).Length} chars); replying McmdSvrLoginAckCancel");
                var cancel = new InterServerFrame
                {
                    Opcode = InterServerOpcode.McmdSvrLoginAckCancel,
                    Header = new InterServerHeader
                    {
                        SourceServerIndex = 0,
                        DestServerIndex = request.ServerIndex,
                        CharUid = 0,
                        RequestId = frame.Header.RequestId,
                    },
                    Body = Array.Empty<byte>(),
                };
                await session.SendAsync(cancel, ct).ConfigureAwait(false);
                return;
            }
        }
        else if (ctx.ExpectedSiblingPasswords.Count > 0)
        {
            ctx.Log($"sibling REJECTED: {request.ServerName} from {session.RemoteEndpoint} not in MasterServer.ini [world 0] sibling registry; replying McmdSvrLoginAckCancel");
            var cancel = new InterServerFrame
            {
                Opcode = InterServerOpcode.McmdSvrLoginAckCancel,
                Header = new InterServerHeader
                {
                    SourceServerIndex = 0,
                    DestServerIndex = request.ServerIndex,
                    CharUid = 0,
                    RequestId = frame.Header.RequestId,
                },
                Body = Array.Empty<byte>(),
            };
            await session.SendAsync(cancel, ct).ConfigureAwait(false);
            return;
        }

        session.RegisteredServerIndex = request.ServerIndex;
        ctx.Log($"sibling registered: {request.ServerName} (role={request.Role}, idx={request.ServerIndex}) from {session.RemoteEndpoint}");

        var zoneIds = (ctx.ZoneTable is not null && request.Role == ServerRole.Game)
            ? ctx.ZoneTable.GetZonesForServer(request.ServerIndex)
            : Array.Empty<ushort>();

        var unixSeconds = (uint)ctx.BootTime.ToUnixTimeSeconds();
        var ack = new ServerLoginAckOk
        {
            ServerTime = unixSeconds,
            ServerTimeMs = (uint)(ctx.BootTime.ToUnixTimeMilliseconds() % 1000),
            ZoneIds = zoneIds,
            SyncHourEnabled = false,
            SyncHour = 0,
            SyncMinute = 0,
            WorldCoordConstX = 0,
            WorldCoordConstY = 0,
        };

        var reply = new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdSvrLoginAckOk,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = request.ServerIndex,
                CharUid = 0,
                RequestId = frame.Header.RequestId,
            },
            Body = ack.EncodeBody(),
        };
        await session.SendAsync(reply, ct).ConfigureAwait(false);
        ctx.Log($"sent McmdSvrLoginAckOk to {request.ServerName} (mesh now {ctx.Host.Siblings.Count} sibling(s)); zoneSlice={zoneIds.Length} zone(s)");
    }
}
