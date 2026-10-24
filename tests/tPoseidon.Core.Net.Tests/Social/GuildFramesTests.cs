using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GuildFramesTests
{
    private static readonly Encoding Latin = Encoding.Latin1;
    private static readonly string[] Grades = { "Master", "SubMaster", "Secretary", "Secretary", "OldMember", "Member", "NewMember" };
    private static readonly GuildBlock Block = new(0x1001, 77, "Crew", "Ana", "hello");

    private static string Text(byte[] payload, int offset, int field) => GuildFrames.ReadText(payload, offset, field, Latin);

    [Fact]
    public void OwnInfoPutsTheGuildCodeUidAliasAndGradeWhereTheClientCopiesThem()
    {
        var payload = GuildFrames.OwnInfoFor(77, 0x1001, "Master", 0, newNotice: true, inGuild: true, Latin);

        Assert.Equal(GuildFrames.OwnInfoLength, payload.Length);
        Assert.Equal(new byte[] { 0, 0, 1, 1, 1, 0, 0 }, payload[..7]);
        Assert.Equal(77u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(7)));
        Assert.Equal(0x1001ul, BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(0x0B)));
        Assert.Equal("Master", Text(payload, 0x13, GuildFrames.AliasLength));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0x2B)));
        Assert.All(payload[0x2F..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void OwnInfoFailureCarriesTheReasonInTheSecondByte()
    {
        var payload = GuildFrames.OwnInfoFailed(GuildFrames.OwnInfoApplying);

        Assert.Equal(GuildFrames.OwnInfoLength, payload.Length);
        Assert.Equal(new byte[] { 1, 3 }, payload[..2]);
        Assert.All(payload[2..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void GuildStateIsSevenHundredSeventySixBytesWithTheBlockGradesAndNoNotices()
    {
        var payload = GuildFrames.StateFor(new GuildStateView(Block, 64, 0, GuildFrames.MasterRights, Grades), Latin);

        Assert.Equal(GuildFrames.StateLength, payload.Length);
        Assert.Equal(64, BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(0x11)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0x27)));
        Assert.Equal(0x1001ul, BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(0x2B)));
        Assert.Equal(77u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0x2B + 8)));
        Assert.Equal("Crew", Text(payload, 0x2B + 0x0C, GuildFrames.BlockGuildNameLength));
        Assert.Equal("Ana", Text(payload, 0x2B + 0x2E, GuildFrames.CharacterNameLength));
        Assert.Equal("hello", Text(payload, 0x2B + 0x42, GuildFrames.GreetingLength));
        Assert.Equal(new byte[] { 1, 1, 0, 0, 0, 0, 0, 0 }, payload[0xB3..0xBB]);
        Assert.All(payload[0xBB..0xEB], b => Assert.Equal(0, b));
        Assert.All(payload[0xEB..(0xEB + GuildFrames.GradeTableHeadLength)], b => Assert.Equal(0, b));
        for (var grade = 0; grade < GuildFrames.GradeCount; grade++)
        {
            Assert.Equal(Grades[grade], Text(payload, 0xEB + GuildFrames.GradeTableHeadLength + grade * GuildFrames.GradeNameLength, GuildFrames.GradeNameLength));
        }
        Assert.Equal(0, payload[0x157]);
    }

    [Fact]
    public void AMemberGradeRowCarriesOnlyTheRightsTheMemberHolds()
    {
        var payload = GuildFrames.StateFor(new GuildStateView(Block, 64, 6, GuildFrames.RightVolunteers, Grades), Latin);

        Assert.All(payload[0xB3..(0xB3 + 6 * 8)], b => Assert.Equal(0, b));
        Assert.Equal(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }, payload[(0xB3 + 6 * 8)..0xEB]);
    }

    [Fact]
    public void ATwentyFourByteGuildNameFillsTheBlockNameAndStaysTerminated()
    {
        var name = new string('N', 24);
        var payload = GuildFrames.StateFor(new GuildStateView(Block with { GuildName = name }, 64, 0, 0, Grades), Latin);

        Assert.Equal(name, Text(payload, 0x2B + 0x0C, GuildFrames.BlockGuildNameLength));
        Assert.Equal(0, payload[0x2B + 0x24]);
    }

    [Fact]
    public void CreatedReplyIs203BytesWithTheBlockAtElevenAndTheGradeRightsAt0x93()
    {
        var payload = GuildFrames.CreatedFor(Block, 0, GuildFrames.MasterRights, Latin);

        Assert.Equal(GuildFrames.CreatedLength, payload.Length);
        Assert.Equal(0, payload[0]);
        Assert.Equal(0x1001ul, BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(0x0B)));
        Assert.Equal(77u, BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(0x13)));
        Assert.Equal("Crew", Text(payload, 0x17, GuildFrames.BlockGuildNameLength));
        Assert.Equal(new byte[] { 1, 1 }, payload[0x93..0x95]);
    }

    [Fact]
    public void ACreatePenaltyCarriesTypeAndReleaseDateAndIsStillFullLength()
    {
        var payload = GuildFrames.CreateRefused(GuildFrames.CreatePenalty, 0, "26-11-18", Latin);

        Assert.Equal(GuildFrames.CreatedLength, payload.Length);
        Assert.Equal(new byte[] { 3, 0 }, payload[..2]);
        Assert.Equal("26-11-18", Text(payload, 2, GuildFrames.DateLength));
    }

    [Fact]
    public void ApplyResultPutsThePenaltyTypeAt0x0EAndTheDateAt0x0F()
    {
        var payload = GuildFrames.ApplyResultFor(GuildFrames.ApplyPenalty, false, 1, "26-10-25", Latin);

        Assert.Equal(GuildFrames.ApplyResultLength, payload.Length);
        Assert.Equal(3, payload[0]);
        Assert.Equal(1, payload[0x0E]);
        Assert.Equal("26-10-25", Text(payload, 0x0F, GuildFrames.DateLength));
    }

    [Fact]
    public void LeaveResultCarriesTheLeaverAndTheFailureFlagAtTheEnd()
    {
        var left = GuildFrames.LeaveResultFor(0x1002, "Bo", Latin);
        var refused = GuildFrames.LeaveRefused(GuildFrames.LeaveMaster);

        Assert.Equal(GuildFrames.LeaveResultLength, left.Length);
        Assert.Equal(0x1002ul, BinaryPrimitives.ReadUInt64LittleEndian(left.AsSpan(4)));
        Assert.Equal("Bo", Text(left, 0x0C, GuildFrames.CharacterNameLength));
        Assert.Equal(new byte[] { 0, 0 }, left[0x20..]);
        Assert.Equal(GuildFrames.LeaveResultLength, refused.Length);
        Assert.Equal(new byte[] { 1, 1 }, refused[0x20..]);
    }

    [Fact]
    public void MemberListEntriesAre0x68BytesWithTheNamedFields()
    {
        var rows = new[]
        {
            new GuildMemberRow(0x1001, 0, 3, 40, 12, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), "Ana", null),
            new GuildMemberRow(0x1002, 6, 5, 41, 0, new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero), "Bo",
                new DateTimeOffset(2026, 10, 17, 0, 0, 0, TimeSpan.Zero)),
        };

        var payload = GuildFrames.MemberListFor(rows, 2, Latin);

        Assert.Equal(GuildFrames.MemberListHeaderLength + 2 * GuildFrames.MemberEntryLength, payload.Length);
        Assert.Equal(new uint[] { 0, 0, 2, 2 }, Enumerable.Range(0, 4).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(i * 4))).ToArray());
        var second = payload.AsSpan(GuildFrames.MemberListHeaderLength + GuildFrames.MemberEntryLength).ToArray();
        Assert.Equal(0x1002ul, BinaryPrimitives.ReadUInt64LittleEndian(second));
        Assert.Equal(6, second[8]);
        Assert.Equal(5, second[9]);
        Assert.Equal(41, BinaryPrimitives.ReadUInt16LittleEndian(second.AsSpan(0x0A)));
        Assert.Equal("26-10-02", Text(second, 0x25, GuildFrames.DateLength));
        Assert.Equal("Bo", Text(second, 0x2E, GuildFrames.CharacterNameLength));
        Assert.Equal("26-10-17", Text(second, 0x42, GuildFrames.DateLength));
        var first = payload.AsSpan(GuildFrames.MemberListHeaderLength, GuildFrames.MemberEntryLength).ToArray();
        Assert.Equal(12, BinaryPrimitives.ReadInt32LittleEndian(first.AsSpan(0x0C)));
        Assert.All(first[0x42..0x4B], b => Assert.Equal(0, b));
    }

    [Fact]
    public void AMemberListRefusalEchoesTheMode()
    {
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0 }, GuildFrames.MemberListRefused(1));
    }

    [Fact]
    public void RemovedMembersAreFortyByteEntriesLedByTheUid()
    {
        var payload = GuildFrames.MembersRemovedFor(new ulong[] { 0x1002, 0x1003 });

        Assert.Equal(4 + 2 * GuildFrames.RemovedEntryLength, payload.Length);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(payload));
        Assert.Equal(0x1003ul, BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(4 + GuildFrames.RemovedEntryLength)));
    }

    [Fact]
    public void ANewMemberPushIsUidNameLevelJobAndTwoDates()
    {
        var payload = GuildFrames.MemberJoinedFor(0x1003, "Cy", 42, 7, "26-10-18", "26-10-18", Latin);

        var expected = new List<byte> { 0x03, 0x10, 0, 0, 0, 0, 0, 0 };
        expected.AddRange(Latin.GetBytes("Cy\0"));
        expected.AddRange(new byte[] { 42, 0, 7 });
        expected.AddRange(Latin.GetBytes("26-10-18\026-10-18\0"));
        Assert.Equal(expected.ToArray(), payload);
    }

    [Fact]
    public void TheApplicantListRepeatsTheCountAndWritesVariableEntries()
    {
        var payload = GuildFrames.VolunteerListFor(
            new[] { new GuildVolunteerRow(0x1003, 12, "Cy", 2, new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero)) }, Latin);

        var expected = new List<byte> { 0, 0, 1, 1, 0x03, 0x10, 0, 0, 0, 0, 0, 0, 12, 0 };
        expected.AddRange(Latin.GetBytes("Cy\0"));
        expected.AddRange(new byte[] { 2, 0, 0, 0 });
        expected.AddRange(Latin.GetBytes("26-10-18\0"));
        Assert.Equal(expected.ToArray(), payload);
        Assert.Equal(new byte[] { 1, 2, 0, 0 }, GuildFrames.VolunteerListRefused(GuildFrames.VolunteerNoPermission));
    }

    [Fact]
    public void AForceOutRequestIsReadAndTheReplyStampsEachEntryStatus()
    {
        var request = new byte[GuildFrames.ForceOutHeaderLength + GuildFrames.ForceOutEntryLength];
        request[GuildFrames.RequestHeaderLength] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(request.AsSpan(GuildFrames.ForceOutHeaderLength + 8), 0x1002);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(GuildFrames.ForceOutHeaderLength + 0x10), 0xAAAA);

        var entries = GuildFrames.ReadForceOutRequest(request)!;
        var reply = GuildFrames.ForceOutResultFor(GuildFrames.Ok, new[] { (entries[0], GuildFrames.ForceOutSkipped) });

        Assert.Single(entries);
        Assert.Equal(0x1002ul, GuildFrames.ForceOutTarget(entries[0]));
        Assert.Equal(GuildFrames.ForceOutResultHeaderLength + GuildFrames.ForceOutEntryLength, reply.Length);
        Assert.Equal(new byte[] { 1, 0 }, reply[0x0C..0x0E]);
        Assert.Equal(0x1002ul, BinaryPrimitives.ReadUInt64LittleEndian(reply.AsSpan(0x0E + 8)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(reply.AsSpan(0x0E + 0x10)));
        Assert.Null(GuildFrames.ReadForceOutRequest(request.AsSpan(0, request.Length - 1)));
    }

    [Fact]
    public void TheAnswerRequestKeepsTheApplicantAndTheAnswerAfterTheHeader()
    {
        var request = new byte[GuildFrames.AnswerApplyRequestLength];
        BinaryPrimitives.WriteUInt64LittleEndian(request.AsSpan(0x0C), 0x1003);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(0x14), 1);

        Assert.Equal((0x1003ul, 1u), GuildFrames.ReadAnswerApplyRequest(request));
        Assert.Equal(
            new byte[] { 4, 0, 0, 0, 1, 0, 0, 0, 0x03, 0x10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            GuildFrames.AnswerApplyResultFor(GuildFrames.AnswerMemberFull, true, 0x1003, 0));
    }

    [Fact]
    public void TheCreateRequestNameAndIntroComeFromTheirFixedFields()
    {
        var request = new byte[GuildFrames.CreateRequestLength];
        Latin.GetBytes("Crew").CopyTo(request, 0);
        Latin.GetBytes("Ana").CopyTo(request, 0x19);
        Latin.GetBytes("we hunt").CopyTo(request, 0x2D);

        Assert.Equal(new GuildCreateRequest("Crew", "we hunt"), GuildFrames.ReadCreateRequest(request, Latin));
        Assert.Null(GuildFrames.ReadCreateRequest(request.AsSpan(0, request.Length - 1), Latin));
    }

    [Fact]
    public void GuildDetailPlacesTheNamedFields()
    {
        var payload = GuildFrames.DetailFor(new GuildDetailView(77, "Crew", "Ana", 1234, 9, "we hunt"), Latin);

        Assert.Equal(1 + GuildFrames.DetailLength, payload.Length);
        var body = payload[1..];
        Assert.Equal("Crew", Text(body, 0, GuildFrames.GuildNameLength));
        Assert.Equal("Ana", Text(body, 0x19, GuildFrames.DetailMasterNameLength));
        Assert.Equal(1234, BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(0x3C)));
        Assert.Equal(9, BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(0x58)));
        Assert.Equal("we hunt", Text(body, 0xC1, GuildFrames.DetailIntroLength));
        Assert.Equal(77u, BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(0x104)));
    }

    [Fact]
    public void TheNameTagIsEntityNameAliasAndSevenZeroBytes()
    {
        var expected = new List<byte> { 0x34, 0x12 };
        expected.AddRange(Latin.GetBytes("Crew\0Master\0"));
        expected.AddRange(new byte[7]);

        Assert.Equal(expected.ToArray(), GuildFrames.NameTagFor(0x1234, "Crew", "Master", Latin));
    }

    [Fact]
    public void TheNameOrderIsTwentyFivePlusTwentyOneBytesAndRoundTrips()
    {
        var data = GuildFrames.NameOrderFor("Crew", "Master", Latin);

        Assert.Equal(GuildFrames.NameOrderLength, data.Length);
        Assert.Equal(("Crew", "Master"), GuildFrames.ReadNameOrder(data, Latin));
        Assert.Equal((string.Empty, string.Empty), GuildFrames.ReadNameOrder(GuildFrames.NameOrderFor(string.Empty, string.Empty, Latin), Latin));
    }
}
