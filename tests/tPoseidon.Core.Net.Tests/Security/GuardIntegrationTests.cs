using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class GuardIntegrationTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task NormalScriptedSession_IsByteIdenticalWithTheGuardOnAndOff()
    {
        var guarded = await RunScriptAsync(GuardHarness.NewGuard(GuardHarness.Settings()));
        var unguarded = await RunScriptAsync(GuardHarness.NewGuard(GuardHarness.Settings(s => s.Enabled = false)));

        Assert.NotEmpty(guarded);
        Assert.Equal(unguarded, guarded);
    }

    [Fact]
    public async Task ConnectionFlood_IsRefusedPastTheBurstWhileTheFirstSessionsKeepWorking()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s =>
        {
            s.NewConnectionsPerAddressBurst = 5;
            s.NewConnectionsPerAddressPerSecond = 0.01;
            s.StrikesBeforeBan = 1000;
        }));
        await using var harness = await GuardHarness.StartAsync(guard);

        var clients = new List<ScriptedClient>();
        try
        {
            for (var i = 0; i < 40; i++)
            {
                clients.Add(await harness.ConnectAsync());
            }

            for (var i = 5; i < clients.Count; i++)
            {
                Assert.True(await clients[i].WaitClosedAsync(Wait), $"connection {i} should have been refused");
            }

            for (var i = 0; i < 5; i++)
            {
                await clients[i].SendAsync(GuardHarness.EchoOpcode, [0x01, (byte)i]);
                var reply = await clients[i].ReceiveAsync(Wait);
                Assert.NotNull(reply);
                Assert.Equal(new byte[] { 0x01, (byte)i }, reply.Value.Payload);
            }

            Assert.Equal(35, guard.Counters[GuardCounter.RefusedAddressRate]);
            Assert.Equal(5, guard.Counters[GuardCounter.Accepted]);
        }
        finally
        {
            clients.ForEach(client => client.Dispose());
        }
    }

    [Fact]
    public async Task ConnectionFlood_EndsInABanThatKeepsPlayersAlreadyIn()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s =>
        {
            s.NewConnectionsPerAddressBurst = 3;
            s.NewConnectionsPerAddressPerSecond = 0.01;
        }));
        await using var harness = await GuardHarness.StartAsync(guard, sessionsLogIn: true);
        using var player = await harness.ConnectAsync();
        await player.SendAsync(GuardHarness.LoginOpcode, [0x01]);
        Assert.NotNull(await player.ReceiveAsync(Wait));
        using var waiting = await harness.ConnectAsync();
        await waiting.SendAsync(GuardHarness.EchoOpcode, [0x02]);
        Assert.NotNull(await waiting.ReceiveAsync(Wait));

        var flood = new List<ScriptedClient>();
        try
        {
            for (var i = 0; i < 30; i++)
            {
                flood.Add(await harness.ConnectAsync());
            }

            await WaitUntilAsync(() => guard.Counters[GuardCounter.Bans] == 1);
            Assert.True(await waiting.WaitClosedAsync(Wait));
            await player.SendAsync(GuardHarness.EchoOpcode, [0x03]);
            Assert.NotNull(await player.ReceiveAsync(Wait));
            Assert.True(guard.Counters[GuardCounter.RefusedBanned] > 0);
            Assert.True(guard.IsBanned(System.Net.IPAddress.Loopback));
        }
        finally
        {
            flood.ForEach(client => client.Dispose());
        }
    }

    [Fact]
    public async Task ManualBanAndUnban_TakeEffectOnTheRunningServer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tposeidon-guard-" + Guid.NewGuid().ToString("N"));
        try
        {
            var guard = GuardHarness.NewGuard(GuardHarness.Settings(), stateDirectory: directory);
            await using var harness = await GuardHarness.StartAsync(guard, sessionsLogIn: true);
            using var player = await harness.ConnectAsync();
            await player.SendAsync(GuardHarness.LoginOpcode, [0x01]);
            Assert.NotNull(await player.ReceiveAsync(Wait));
            Assert.True(tPoseidon.Core.Common.Security.AddressRange.TryParse("127.0.0.1", out var range));
            var ledger = tPoseidon.Core.Common.Security.SecurityFiles.LedgerPath(directory);

            tPoseidon.Core.Common.Security.SecurityLedger.Update(ledger, l => l.Ban(range, DateTimeOffset.UtcNow, null, "test"));

            Assert.True(await player.WaitClosedAsync(Wait));
            using (var refused = await harness.ConnectAsync())
            {
                Assert.True(await refused.WaitClosedAsync(Wait));
            }

            tPoseidon.Core.Common.Security.SecurityLedger.Update(ledger, l => l.Unban(range, DateTimeOffset.UtcNow));
            await WaitUntilAsync(() => !guard.IsBanned(System.Net.IPAddress.Loopback));
            using var back = await harness.ConnectAsync();
            await back.SendAsync(GuardHarness.LoginOpcode, [0x02]);
            Assert.NotNull(await back.ReceiveAsync(Wait));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PerAddressLimit_RefusesExtraConnectionsUntilOneCloses()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s => s.MaxConnectionsPerAddress = 3));
        await using var harness = await GuardHarness.StartAsync(guard);

        var open = new List<ScriptedClient>();
        for (var i = 0; i < 3; i++)
        {
            var client = await harness.ConnectAsync();
            await client.SendAsync(GuardHarness.EchoOpcode, [0x02]);
            Assert.NotNull(await client.ReceiveAsync(Wait));
            open.Add(client);
        }

        using (var extra = await harness.ConnectAsync())
        {
            Assert.True(await extra.WaitClosedAsync(Wait));
        }

        open[0].Dispose();
        await WaitUntilAsync(() => guard.Connections == 2);
        using var again = await harness.ConnectAsync();
        await again.SendAsync(GuardHarness.EchoOpcode, [0x03]);
        Assert.NotNull(await again.ReceiveAsync(Wait));
        open.Skip(1).ToList().ForEach(client => client.Dispose());
    }

    [Fact]
    public async Task Slowloris_PartialFrameIsCutWhileANormalSessionStays()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s =>
        {
            s.PartialFrameTimeoutSeconds = 1;
            s.HandshakeTimeoutSeconds = 30;
        }));
        await using var harness = await GuardHarness.StartAsync(guard);
        using var normal = await harness.ConnectAsync();
        using var slow = await harness.ConnectAsync();
        await slow.SendAsync(GuardHarness.EchoOpcode, [0x05]);
        Assert.NotNull(await slow.ReceiveAsync(Wait));

        var frame = slow.Frame(GuardHarness.EchoOpcode, [0x06, 0x07, 0x08]);
        var started = DateTime.UtcNow;
        var dripped = 0;
        var closed = false;
        while (!closed && dripped < frame.Length - 1)
        {
            try
            {
                await slow.SendRawAsync([frame[dripped++]]);
            }
            catch (IOException)
            {
                closed = true;
                break;
            }

            closed = await slow.WaitClosedAsync(TimeSpan.FromMilliseconds(300));
        }

        Assert.True(closed, "the dripping connection should have been closed");
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromSeconds(0.9), TimeSpan.FromSeconds(4));
        Assert.Equal(1, guard.Counters[GuardCounter.PartialFrames]);

        await normal.SendAsync(GuardHarness.EchoOpcode, [0x09]);
        Assert.NotNull(await normal.ReceiveAsync(Wait));
    }

    [Fact]
    public async Task SilentConnection_IsClosedAfterTheHandshakeTimeout()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s => s.HandshakeTimeoutSeconds = 1));
        await using var harness = await GuardHarness.StartAsync(guard);
        using var silent = await harness.ConnectAsync();

        Assert.True(await silent.WaitClosedAsync(Wait));
        Assert.Equal(1, guard.Counters[GuardCounter.HandshakeTimeouts]);
        await WaitUntilAsync(() => guard.Pending == 0 && guard.Connections == 0);
    }

    [Fact]
    public async Task SessionThatNeverLogsIn_IsClosedAndOneThatDoesStays()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s => s.LoginTimeoutSeconds = 1));
        await using var harness = await GuardHarness.StartAsync(guard, sessionsLogIn: true);
        using var lurker = await harness.ConnectAsync();
        using var player = await harness.ConnectAsync();
        await lurker.SendAsync(GuardHarness.EchoOpcode, [0x0A]);
        Assert.NotNull(await lurker.ReceiveAsync(Wait));
        await player.SendAsync(GuardHarness.LoginOpcode, [0x0B]);
        Assert.NotNull(await player.ReceiveAsync(Wait));

        Assert.True(await lurker.WaitClosedAsync(Wait));
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        await player.SendAsync(GuardHarness.EchoOpcode, [0x0C]);
        Assert.NotNull(await player.ReceiveAsync(Wait));
        Assert.Equal(1, guard.Counters[GuardCounter.LoginTimeouts]);
    }

    [Fact]
    public async Task IdleLoggedInSession_IsClosedWhereClientsKeepAlive()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s => s.IdleTimeoutSeconds = 1));
        await using var harness = await GuardHarness.StartAsync(guard, sessionsLogIn: true, clientsKeepAlive: true);
        using var idle = await harness.ConnectAsync();
        using var beating = await harness.ConnectAsync();
        foreach (var client in new[] { idle, beating })
        {
            await client.SendAsync(GuardHarness.LoginOpcode, [0x0D]);
            Assert.NotNull(await client.ReceiveAsync(Wait));
        }

        var until = DateTime.UtcNow + TimeSpan.FromSeconds(2.5);
        while (DateTime.UtcNow < until)
        {
            await beating.SendAsync(GuardHarness.EchoOpcode, [0x0E]);
            Assert.NotNull(await beating.ReceiveAsync(Wait));
            await Task.Delay(250);
        }

        Assert.True(await idle.WaitClosedAsync(Wait));
        Assert.Equal(1, guard.Counters[GuardCounter.IdleTimeouts]);
    }

    [Fact]
    public async Task OversizedFrame_IsCutFromItsHeader()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s => s.MaxFrameBytes = 16384));
        await using var harness = await GuardHarness.StartAsync(guard);
        using var client = await harness.ConnectAsync();
        var huge = client.Frame(GuardHarness.EchoOpcode, new byte[30000]);

        await client.SendRawAsync(huge[..64]);

        Assert.True(await client.WaitClosedAsync(Wait));
        Assert.Equal(1, guard.Counters[GuardCounter.OversizedFrames]);
    }

    [Fact]
    public async Task FrameThatDoesNotDecrypt_ClosesTheSession()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings());
        await using var harness = await GuardHarness.StartAsync(guard);
        using var client = await harness.ConnectAsync();
        var frame = client.Frame(GuardHarness.EchoOpcode, [0x10, 0x20, 0x30, 0x40]);
        frame[10] ^= 0x5A;

        await client.SendRawAsync(frame);

        Assert.True(await client.WaitClosedAsync(Wait));
        Assert.Equal(1, guard.Counters[GuardCounter.MalformedFrames]);
    }

    [Fact]
    public async Task PacketFlood_IsCutWhileASteadySessionKeepsGoing()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s =>
        {
            s.PacketBurst = 50;
            s.PacketsPerSecond = 20;
        }));
        await using var harness = await GuardHarness.StartAsync(guard);
        using var flooder = await harness.ConnectAsync();
        using var steady = await harness.ConnectAsync();

        var flood = new List<byte>();
        for (var i = 0; i < 400; i++)
        {
            flood.AddRange(flooder.Frame(GuardHarness.EchoOpcode, [(byte)i]));
        }

        try
        {
            await flooder.SendRawAsync(flood.ToArray());
        }
        catch (IOException)
        {
        }

        Assert.True(await flooder.WaitClosedAsync(Wait));
        Assert.Equal(1, guard.Counters[GuardCounter.PacketFloods]);

        for (var i = 0; i < 30; i++)
        {
            await steady.SendAsync(GuardHarness.EchoOpcode, [(byte)i]);
            Assert.NotNull(await steady.ReceiveAsync(Wait));
        }
    }

    [Fact]
    public async Task OneOpcodeFlood_IsCutButMovementHasItsHigherAllowance()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s => s.OpcodeBurst = 10));
        await using var harness = await GuardHarness.StartAsync(guard);
        using var spammer = await harness.ConnectAsync();
        using var walker = await harness.ConnectAsync();

        for (var i = 0; i < 100; i++)
        {
            await walker.SendAsync(GuardHarness.EchoOpcode, [(byte)i]);
            Assert.NotNull(await walker.ReceiveAsync(Wait));
        }

        var spam = new List<byte>();
        for (var i = 0; i < 30; i++)
        {
            spam.AddRange(spammer.Frame(GuardHarness.PlainOpcode, [(byte)i]));
        }

        await spammer.SendRawAsync(spam.ToArray());
        Assert.True(await spammer.WaitClosedAsync(Wait));
        Assert.Equal(1, guard.Counters[GuardCounter.PacketFloods]);
    }

    [Fact]
    public async Task BruteForceLogin_IsDelayedThenLockedOutWithTheUsualFailureReply()
    {
        var guard = GuardHarness.NewGuard(GuardHarness.Settings(s =>
        {
            s.LoginFailuresBeforeDelay = 1;
            s.LoginDelayMaxSeconds = 1;
            s.AccountLockoutFailures = 4;
        }));
        await using var harness = await GuardHarness.StartAsync(guard, sessionsLogIn: true);

        var closeTimes = new List<TimeSpan>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            closeTimes.Add(await TryLoginAsync(harness, "victim", "guess" + attempt, expectReply: false));
        }

        Assert.All(closeTimes.Skip(1), time => Assert.True(time >= TimeSpan.FromSeconds(0.9), $"a later failure took only {time}"));
        Assert.All(closeTimes.Skip(1), time => Assert.True(time - closeTimes[0] >= TimeSpan.FromSeconds(0.5), $"the first failure took {closeTimes[0]} and a delayed one only {time}"));

        await TryLoginAsync(harness, "victim", GuardHarness.RightPassword, expectReply: false);
        Assert.Equal(1, guard.Counters[GuardCounter.Lockouts]);
        Assert.Equal(1, guard.Counters[GuardCounter.LockedOutAttempts]);

        await TryLoginAsync(harness, "bystander", GuardHarness.RightPassword, expectReply: true);
    }

    private static async Task<TimeSpan> TryLoginAsync(GuardHarness harness, string account, string password, bool expectReply)
    {
        using var client = await harness.ConnectAsync();
        var started = DateTime.UtcNow;
        await client.SendAsync(GuardHarness.PasswordOpcode, System.Text.Encoding.ASCII.GetBytes(account + "\0" + password));
        if (expectReply)
        {
            var reply = await client.ReceiveAsync(Wait);
            Assert.NotNull(reply);
            Assert.Equal(GuardHarness.LoginReplyOpcode, reply.Value.Opcode);
            return DateTime.UtcNow - started;
        }

        Assert.True(await client.WaitClosedAsync(Wait));
        Assert.Empty(client.Received);
        return DateTime.UtcNow - started;
    }

    internal static async Task<byte[]> RunScriptAsync(ConnectionGuard guard)
    {
        await using var harness = await GuardHarness.StartAsync(guard);
        using var client = await harness.ConnectAsync();
        for (var i = 0; i < 12; i++)
        {
            await client.SendAsync(GuardHarness.EchoOpcode, [(byte)i, 0x10, 0x20, (byte)(i * 7)], (byte)(0x30 + i));
            Assert.NotNull(await client.ReceiveAsync(Wait));
        }

        return client.Received.ToArray();
    }

    internal static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "condition was not met in time");
            await Task.Delay(20);
        }
    }
}
