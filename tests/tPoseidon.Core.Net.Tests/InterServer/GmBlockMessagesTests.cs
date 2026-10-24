using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Core.Net.Tests.InterServer;

public sealed class GmBlockMessagesTests
{
    private static readonly string[] BlockThenUnblock = { "block", "unblock" };

    private sealed class RecordingStore : IGmBlockStore
    {
        public List<(string Call, ulong Character, byte Type, ushort Minutes, ulong Gm)> Calls { get; } = new();

        public int NextResult { get; set; } = GmBlockRules.Ok;

        public int BlockCharacter(ulong characterId, byte blockType, ushort minutes, ulong gameMasterId)
        {
            Calls.Add(("block", characterId, blockType, minutes, gameMasterId));
            return NextResult;
        }

        public int UnblockCharacter(ulong characterId, byte blockType, ulong gameMasterId)
        {
            Calls.Add(("unblock", characterId, blockType, 0, gameMasterId));
            return NextResult;
        }

        public int SetUserAuthority(string characterName, byte authority) => 1;

        public GmRunningBlock? LatestRunningBlock(ulong characterId) => null;

        public IReadOnlyList<GmActiveBlock> ActiveBlocks() => Active;

        public List<GmActiveBlock> Active { get; } = new();
    }

    private static InterServerFrame Request(InterServerOpcode opcode, byte[] body) => new()
    {
        Opcode = opcode,
        Header = new InterServerHeader { SourceServerIndex = 7, DestServerIndex = 0, CharUid = 0x4002, RequestId = 33 },
        Body = body,
    };

    [Fact]
    public void ABlockIsCharacterTypeMinutesAndGm()
    {
        var body = GmBlockMessages.EncodeBlock(new GmBlockRequest(0x0102030405060708, 2, 0x0A0B, 0x1112131415161718));

        Assert.Equal(new byte[]
        {
            0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01,
            0x02,
            0x0B, 0x0A,
            0x18, 0x17, 0x16, 0x15, 0x14, 0x13, 0x12, 0x11,
        }, body);
        Assert.True(GmBlockMessages.TryDecodeBlock(body, out var decoded));
        Assert.Equal(new GmBlockRequest(0x0102030405060708, 2, 0x0A0B, 0x1112131415161718), decoded);
    }

    [Fact]
    public void AnUnblockIsCharacterTypeAndGm()
    {
        var body = GmBlockMessages.EncodeUnblock(new GmUnblockRequest(0x4002, 3, 0x9001));

        Assert.Equal(GmBlockMessages.UnblockBodyLength, body.Length);
        Assert.True(GmBlockMessages.TryDecodeUnblock(body, out var decoded));
        Assert.Equal(new GmUnblockRequest(0x4002, 3, 0x9001), decoded);
        Assert.False(GmBlockMessages.TryDecodeUnblock(body.AsSpan(1), out _));
    }

    [Fact]
    public void TheMasterAnswersABlockWithOkAndTheSameRequestId()
    {
        var store = new RecordingStore();

        var reply = GmBlockMessages.Answer(store, Request(InterServerOpcode.CcmdBlock, GmBlockMessages.EncodeBlock(new GmBlockRequest(0x4002, 5, 30, 0x9001))))!;

        Assert.Equal(InterServerOpcode.CcmdBlockAckOk, reply.Opcode);
        Assert.Equal((ushort)7, reply.Header.DestServerIndex);
        Assert.Equal(33u, reply.Header.RequestId);
        Assert.Equal(GmBlockMessages.AckBodyLength, reply.Body.Length);
        Assert.True(GmBlockMessages.TryDecodeAck(reply.Body, out var echoed));
        Assert.Equal(new GmUnblockRequest(0x4002, 5, 0x9001), echoed);
        Assert.Equal(("block", 0x4002ul, (byte)5, (ushort)30, 0x9001ul), Assert.Single(store.Calls));
    }

    [Fact]
    public void AFailedUnblockIsCancelledWithTheRequestEchoed()
    {
        var store = new RecordingStore { NextResult = GmBlockRules.NoBlockRow };

        var reply = GmBlockMessages.Answer(store, Request(InterServerOpcode.CcmdUnblock, GmBlockMessages.EncodeUnblock(new GmUnblockRequest(0x4002, 2, 0x9001))))!;

        Assert.Equal(InterServerOpcode.CcmdUnblockAckCancel, reply.Opcode);
        Assert.True(GmBlockMessages.TryDecodeAck(reply.Body, out var echoed));
        Assert.Equal(new GmUnblockRequest(0x4002, 2, 0x9001), echoed);
    }

    [Fact]
    public void ABlockOfCharacterZeroIsCancelledWithoutTouchingTheDatabase()
    {
        var store = new RecordingStore();

        var reply = GmBlockMessages.Answer(store, Request(InterServerOpcode.CcmdBlock, GmBlockMessages.EncodeBlock(new GmBlockRequest(0, 2, 30, 0x9001))))!;

        Assert.Equal(InterServerOpcode.CcmdBlockAckCancel, reply.Opcode);
        Assert.Empty(store.Calls);
    }

    [Fact]
    public void OtherFramesGetNoAnswer()
    {
        Assert.Null(GmBlockMessages.Answer(new RecordingStore(), Request(InterServerOpcode.CcmdBlock, new byte[3])));
        Assert.Null(GmBlockMessages.Answer(new RecordingStore(), Request(InterServerOpcode.McmdFortuneUpdate, Array.Empty<byte>())));
        Assert.Null(GmBlockMessages.Cancelled(Request(InterServerOpcode.CcmdUnblock, Array.Empty<byte>())));
        Assert.Equal(InterServerOpcode.CcmdUnblockAckCancel, GmBlockMessages.Cancelled(Request(InterServerOpcode.CcmdUnblock, GmBlockMessages.EncodeUnblock(new GmUnblockRequest(0x4002, 2, 0x9001))))!.Opcode);
    }

    [Fact]
    public void WithoutAMasterConnectionTheRoutedStoreWritesDirectly()
    {
        var direct = new RecordingStore();
        var routed = new MasterRoutedGmBlockStore(() => null, direct, 7, _ => { });

        Assert.Equal(GmBlockRules.Ok, routed.BlockCharacter(0x4002, 2, 30, 0x9001));
        Assert.Equal(GmBlockRules.Ok, routed.UnblockCharacter(0x4002, 2, 0x9001));
        Assert.Equal(BlockThenUnblock, direct.Calls.Select(call => call.Call));
    }
}
