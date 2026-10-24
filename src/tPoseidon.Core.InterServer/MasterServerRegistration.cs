namespace tPoseidon.Core.InterServer;

public static class MasterServerRegistration
{
    public static async Task<MasterServerClient> ConnectAndRegisterAsync(
        string host,
        int port,
        ServerIdentity self,
        Action<string> log,
        CancellationToken ct,
        Func<InterServerFrame, CancellationToken, Task>? onBrokeredFrame = null,
        string password = "",
        Action<ServerLoginAckOk>? onAckOk = null)
    {
        ArgumentNullException.ThrowIfNull(log);

        var client = new MasterServerClient(host, port, self, log);

        var ackReceived = new TaskCompletionSource();
        client.OnFrame = async (frame, frameCt) =>
        {
            switch (frame.Opcode)
            {
                case InterServerOpcode.McmdSvrLoginAckOk:
                    try
                    {
                        if (frame.Body.Length > 0)
                        {
                            var ack = ServerLoginAckOk.DecodeBody(frame.Body);
                            log($"[{self.Name}] mesh joined: MasterServer accepted registration (idx={self.ServerIndex}); zoneSlice={ack.ZoneIds.Length} zone(s)");
                            onAckOk?.Invoke(ack);
                        }
                        else
                        {
                            log($"[{self.Name}] mesh joined: MasterServer accepted registration (idx={self.ServerIndex}); empty ack body (legacy Master)");
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        log($"[{self.Name}] mesh joined: MasterServer accepted registration (idx={self.ServerIndex}); ack body decode failed: {ex.Message}");
                    }
                    ackReceived.TrySetResult();
                    break;
                case InterServerOpcode.McmdSvrLoginAckCancel:
                    log($"[{self.Name}] MasterServer REJECTED registration (McmdSvrLoginAckCancel)");
                    break;
                default:
                    if (onBrokeredFrame is not null)
                    {
                        await onBrokeredFrame(frame, frameCt).ConfigureAwait(false);
                    }
                    break;
            }
        };

        await client.ConnectAsync(ct).ConfigureAwait(false);

        var request = new ServerLoginRequest
        {
            ServerName = self.Name,
            Role = self.Role,
            ServerIndex = self.ServerIndex,
            Password = password,
        };
        var frame = new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdSvrLogin,
            Header = new InterServerHeader
            {
                SourceServerIndex = self.ServerIndex,
                DestServerIndex = 0,
                CharUid = 0,
                RequestId = 0,
            },
            Body = request.EncodeBody(),
        };
        await client.SendAsync(frame, ct).ConfigureAwait(false);
        log($"[{self.Name}] sent McmdSvrLogin to MasterServer {host}:{port}");

        return client;
    }
}
