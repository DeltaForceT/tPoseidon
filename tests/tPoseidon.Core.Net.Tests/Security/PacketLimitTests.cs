using System.Buffers.Binary;
using System.Net;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Crypto.S2;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.Core.Net.Tests.Security;

public sealed class PacketLimitTests
{
    private static readonly IPAddress Player = IPAddress.Parse("198.51.100.7");

    private static (SessionGuard Session, ConnectionGuard Guard, ManualTime Time, List<int> Aborts) Admit(Action<GuardSettings> change, IPAddress? address = null)
    {
        var settings = new GuardSettings();
        change(settings);
        var time = new ManualTime();
        var guard = ConnectionGuardAdmissionTests.NewGuard(settings, time);
        var session = guard.Admit(address ?? Player).Session!;
        var aborts = new List<int>();
        session.Bind(() => aborts.Add(1));
        return (session, guard, time, aborts);
    }

    [Fact]
    public void SessionBucket_CutsAFloodAcrossOpcodes()
    {
        var (session, guard, _, aborts) = Admit(s =>
        {
            s.PacketBurst = 10;
            s.OpcodeBurst = 100;
        });

        for (var i = 0; i < 10; i++)
        {
            Assert.True(session.AllowPacket((ushort)(0x0100 + i)));
        }

        Assert.False(session.AllowPacket(0x0200));
        Assert.Single(aborts);
        Assert.Equal(1, guard.Counters[GuardCounter.PacketFloods]);
    }

    [Fact]
    public void SessionBucket_RefillsForASteadyClient()
    {
        var (session, _, time, aborts) = Admit(s =>
        {
            s.PacketBurst = 10;
            s.PacketsPerSecond = 20;
            s.OpcodeBurst = 1000;
            s.OpcodePacketsPerSecond = 1000;
        });

        for (var i = 0; i < 2000; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(60));
            Assert.True(session.AllowPacket(0x0300));
        }

        Assert.Empty(aborts);
    }

    [Fact]
    public void OpcodeBucket_CutsOneOpcodeFloodingWhileAllowancesGetMore()
    {
        var (session, _, _, aborts) = Admit(s =>
        {
            s.OpcodeBurst = 5;
            s.OpcodeAllowances = [new OpcodeAllowance { Opcode = "0x0018", Name = "movement", Burst = 50, PerSecond = 10 }];
        });

        for (var i = 0; i < 50; i++)
        {
            Assert.True(session.AllowPacket(0x0018));
        }

        for (var i = 0; i < 5; i++)
        {
            Assert.True(session.AllowPacket(0x0075));
        }

        Assert.Empty(aborts);
        Assert.False(session.AllowPacket(0x0075));
        Assert.Single(aborts);
    }

    [Fact]
    public void DefaultAllowances_CoverMovementDialogAndChat()
    {
        var opcodes = OpcodeAllowance.Defaults().Select(a => a.TryParseOpcode(out var op) ? op : (ushort)0).ToHashSet();

        Assert.Contains((ushort)0x0018, opcodes);
        Assert.Contains((ushort)0x039F, opcodes);
        Assert.Contains((ushort)0x0036, opcodes);
        Assert.Contains((ushort)0x1777, opcodes);
        Assert.All(OpcodeAllowance.Defaults(), allowance => Assert.True(allowance.Burst > new GuardSettings().OpcodeBurst));
    }

    [Fact]
    public void ManyDistinctOpcodes_ShareOneBucketPastTheTrackingLimit()
    {
        var (session, _, _, aborts) = Admit(s =>
        {
            s.PacketBurst = 100000;
            s.OpcodeBurst = 3;
        });

        for (var op = 0; op < ConnectionGuard.MaxTrackedOpcodes; op++)
        {
            Assert.True(session.AllowPacket((ushort)(0x4000 + op)));
        }

        Assert.True(session.AllowPacket(0x7000));
        Assert.True(session.AllowPacket(0x7001));
        Assert.True(session.AllowPacket(0x7002));
        Assert.False(session.AllowPacket(0x7003));
        Assert.Single(aborts);
    }

    [Fact]
    public void TrustedSessions_HaveNoPacketLimits()
    {
        var (session, _, _, aborts) = Admit(s => s.PacketBurst = 1, IPAddress.Loopback);

        for (var i = 0; i < 1000; i++)
        {
            Assert.True(session.AllowPacket(0x0018));
        }

        Assert.Empty(aborts);
        Assert.Equal(ushort.MaxValue, session.MaxFrameBytes);
    }

    [Fact]
    public void Framer_ReportsOversizedFromTheHeaderBeforeTheBodyArrives()
    {
        var framer = new S2StreamFramer(KeyTable.Identity(), new IdentityDerivation(), 0);
        var header = new byte[S2PacketCipher.MinimumFrameSize];
        BinaryPrimitives.WriteUInt16LittleEndian(header, 30000);
        framer.Feed(header);

        Assert.Equal(S2FrameStatus.Oversized, framer.TryDrain(out _, 16384));
        Assert.Equal(S2FrameStatus.NeedMore, framer.TryDrain(out _));
        Assert.Equal(30000, framer.PeekLength());
    }

    [Fact]
    public void Framer_ReportsALengthBelowTheMinimumAsMalformed()
    {
        var framer = new S2StreamFramer(KeyTable.Identity(), new IdentityDerivation(), 0);
        var header = new byte[S2PacketCipher.MinimumFrameSize];
        BinaryPrimitives.WriteUInt16LittleEndian(header, 4);
        framer.Feed(header);

        Assert.Equal(S2FrameStatus.Malformed, framer.TryDrain(out _));
        Assert.False(framer.TryDrainOne(out _));
    }

    [Fact]
    public void Framer_StillDrainsWholeFramesTheSameWay()
    {
        var cipher = new S2PacketCipher(KeyTable.Identity(), new IdentityDerivation());
        var frame = cipher.Encrypt(0x0018, 0, 0x11, PackingFlags.PayloadEncrypted, [1, 2, 3], new SessionInfo { IsStrict = false });
        var framer = new S2StreamFramer(KeyTable.Identity(), new IdentityDerivation(), 0);

        framer.Feed(frame.AsSpan(0, 5));
        Assert.Equal(S2FrameStatus.NeedMore, framer.TryDrain(out _, 16384));
        framer.Feed(frame.AsSpan(5));
        Assert.Equal(S2FrameStatus.Frame, framer.TryDrain(out var drained, 16384));
        Assert.Equal(frame, drained);
        Assert.Equal(0, framer.Buffered);
    }
}
