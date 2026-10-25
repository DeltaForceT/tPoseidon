using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GuildServiceTests
{
    private const ulong Ana = 0x1001;
    private const ulong Bo = 0x1002;
    private const ulong Cy = 0x1003;

    private static readonly Encoding Text = Encoding.Latin1;

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakeGuildRules : IGuildRulesStore
    {
        private readonly Dictionary<ulong, (string Name, int Level, int Job)> _characters = new()
        {
            [Ana] = ("Ana", 40, 1), [Bo] = ("Bo", 41, 2), [Cy] = ("Cy", 42, 3),
        };

        public Dictionary<uint, (string Name, ulong Master, int Max)> Guilds { get; } = new();

        public Dictionary<ulong, (uint Guild, byte Grade, string Nick, short Right)> Joined { get; } = new();

        public Dictionary<ulong, uint> Applying { get; } = new();

        public GuildPenalty? Penalty { get; set; }

        public short ExtraMembers { get; set; }

        public GuildResult<GuildFounded> Create(ulong characterId, string guildName, string guildIntro)
        {
            if (Penalty is { } penalty) return GuildResult.Fail<GuildFounded>(104, penalty);
            if (Guilds.Values.Any(g => g.Name == guildName)) return GuildResult.Fail<GuildFounded>(102);
            var code = (uint)(Guilds.Count + 77);
            Guilds[code] = (guildName, characterId, 64);
            Joined[characterId] = (code, GuildRules.MasterGrade, GuildRules.MasterNick, 0);
            return GuildResult.Ok(new GuildFounded(code, GuildRules.MasterNick, Array.Empty<GuildGrade>()));
        }

        public GuildResult<bool> Apply(ulong characterId, uint guildCode)
        {
            if (Joined.ContainsKey(characterId)) return GuildResult.Fail<bool>(101);
            if (Applying.ContainsKey(characterId)) return GuildResult.Fail<bool>(102);
            if (!Guilds.TryGetValue(guildCode, out var guild)) return GuildResult.Fail<bool>(104);
            if (Count(guildCode) >= guild.Max) return GuildResult.Fail<bool>(105);
            Applying[characterId] = guildCode;
            return GuildResult.Ok(true);
        }

        public int CancelApplication(ulong characterId) => Applying.Remove(characterId) ? GuildRules.Ok : 101;

        public GuildResult<GuildPermitted> Permit(ulong characterId, uint guildCode, byte permit)
        {
            if (!Applying.TryGetValue(characterId, out var applied) || applied != guildCode) return GuildResult.Fail<GuildPermitted>(102);
            Applying.Remove(characterId);
            if (permit == GuildRules.Approve) Joined[characterId] = (guildCode, GuildRules.NewMemberGrade, string.Empty, 0);
            var c = _characters[characterId];
            return GuildResult.Ok(new GuildPermitted(1, c.Name, c.Level, c.Job));
        }

        public int Leave(ulong characterId, uint guildCode)
        {
            if (!Guilds.TryGetValue(guildCode, out var guild)) return 101;
            if (guild.Master == characterId) return 102;
            return Joined.Remove(characterId) ? GuildRules.Ok : 103;
        }

        public GuildResult<uint> Kick(ulong characterId, uint guildCode)
        {
            if (Guilds[guildCode].Master == characterId) return GuildResult.Fail<uint>(101);
            return Joined.Remove(characterId) ? GuildResult.Ok(1u) : GuildResult.Fail<uint>(102);
        }

        public int Dissolve(ulong chiefCharacterId, uint guildCode)
        {
            if (!Guilds.TryGetValue(guildCode, out var guild)) return 101;
            if (guild.Master != chiefCharacterId) return 102;
            foreach (var id in Joined.Where(m => m.Value.Guild == guildCode).Select(m => m.Key).ToArray()) Joined.Remove(id);
            Guilds.Remove(guildCode);
            return GuildRules.Ok;
        }

        public CharGuildInfo CharacterGuild(ulong characterId)
        {
            if (Joined.TryGetValue(characterId, out var m)) return new CharGuildInfo(GuildRules.Joined, m.Guild, m.Nick, m.Grade, 0, false, 1, m.Right);
            if (Applying.TryGetValue(characterId, out var g)) return new CharGuildInfo(GuildRules.Applying, g, string.Empty, 0, 0, false, 0, 0);
            return new CharGuildInfo(GuildRules.NotInGuild, 0, string.Empty, 0, 0, false, 0, 0);
        }

        public GuildDetail? Detail(uint guildCode)
        {
            if (!Guilds.TryGetValue(guildCode, out var guild)) return null;
            var grades = GuildRules.DefaultGrades
                .Select((g, i) => new GuildGrade { GuildCode = guildCode, GradeCode = (byte)(i + 1), GradeName = g.Name, GradePoint = g.Point })
                .ToArray();
            var header = new Guild
            {
                GuildCode = guildCode,
                GuildName = guild.Name,
                MasterAccountId = 1,
                MasterCharacterId = guild.Master,
                GuildMembers = (short)(Count(guildCode) + ExtraMembers),
                GuildIntro = "we hunt",
            };
            return new GuildDetail(header, guild.Max, _characters[guild.Master].Name, new GuildLadder { GuildCode = guildCode }, grades, Array.Empty<GuildNotice>());
        }

        public IReadOnlyList<GuildRosterEntry> Members(uint guildCode) => Joined
            .Where(m => m.Value.Guild == guildCode)
            .Select(m => new GuildRosterEntry(m.Key, _characters[m.Key].Name, m.Value.Nick, m.Value.Grade, 0, m.Value.Right,
                _characters[m.Key].Level, _characters[m.Key].Job, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch))
            .ToArray();

        public IReadOnlyList<GuildApplicant> Applicants(uint guildCode) => Applying
            .Where(a => a.Value == guildCode)
            .Select(a => new GuildApplicant(a.Key, _characters[a.Key].Name, _characters[a.Key].Level, _characters[a.Key].Job, DateTimeOffset.UnixEpoch))
            .ToArray();

        private int Count(uint guildCode) => Joined.Values.Count(m => m.Guild == guildCode);
    }

    private static (GuildService Service, FakeGuildRules Rules) NewService()
    {
        var rules = new FakeGuildRules();
        var service = new GuildService(rules, Text, new FixedTime());
        return (service, rules);
    }

    private static void Enter(GuildService service, params (ulong Id, string Name)[] characters)
    {
        foreach (var (id, name) in characters)
        {
            service.Login(id, name);
            service.OpenSession(id);
        }
    }

    private static (GuildService Service, FakeGuildRules Rules) GuildOfAnaAndBo()
    {
        var (service, rules) = NewService();
        rules.Guilds[77] = ("Crew", Ana, 64);
        rules.Joined[Ana] = (77, GuildRules.MasterGrade, GuildRules.MasterNick, 0);
        rules.Joined[Bo] = (77, GuildRules.NewMemberGrade, string.Empty, 0);
        Enter(service, (Ana, "Ana"), (Bo, "Bo"), (Cy, "Cy"));
        return (service, rules);
    }

    private static byte[] AnswerEntry(ulong target)
    {
        var entry = new byte[GuildFrames.ForceOutEntryLength];
        BinaryPrimitives.WriteUInt64LittleEndian(entry.AsSpan(8), target);
        return entry;
    }

    [Fact]
    public void RequestsBeforeTheGuildSessionOpensAreIgnored()
    {
        var (service, _) = NewService();
        service.Login(Ana, "Ana");

        Assert.Empty(service.OwnInfo(Ana, Ana));
        Assert.Empty(service.Create(Ana, "Crew", string.Empty));
    }

    [Fact]
    public void AMemberAtLoginGetsOwnInfoThenTheGuildState()
    {
        var (service, _) = GuildOfAnaAndBo();

        var sent = service.OwnInfo(Bo, Bo);

        Assert.Collection(
            sent,
            m =>
            {
                Assert.Equal((Bo, GuildFrames.OwnInfoResult), (m.To, m.Opcode));
                Assert.Equal(GuildFrames.OwnInfoFor(77, Bo, string.Empty, 6, false, inGuild: true, Text), m.Payload);
            },
            m =>
            {
                Assert.Equal((Bo, GuildFrames.State), (m.To, m.Opcode));
                Assert.Equal(GuildFrames.StateLength, m.Payload.Length);
                Assert.Equal("Crew", GuildFrames.ReadText(m.Payload, 0x2B + 0x0C, GuildFrames.BlockGuildNameLength, Text));
            });
    }

    [Fact]
    public void OwnInfoForSomeoneElseIsRefusedAndAnApplicantGetsTheApplyingState()
    {
        var (service, rules) = NewService();
        rules.Guilds[77] = ("Crew", Ana, 64);
        rules.Applying[Cy] = 77;
        Enter(service, (Cy, "Cy"));

        Assert.Equal(GuildFrames.OwnInfoFailed(GuildFrames.OwnInfoNotMyCharacter), Assert.Single(service.OwnInfo(Cy, Ana)).Payload);
        Assert.Equal(GuildFrames.OwnInfoFailed(GuildFrames.OwnInfoApplying), Assert.Single(service.OwnInfo(Cy, Cy)).Payload);
    }

    [Fact]
    public void LoginAndZoneEntryOrderTheGuildNameForTheNameplate()
    {
        var (service, _) = GuildOfAnaAndBo();

        Assert.Equal(new GuildNameOrder(Ana, "Crew", GuildRules.MasterNick), Assert.Single(service.Login(Ana, "Ana").Orders));
        Assert.Equal(new GuildNameOrder(Bo, "Crew", string.Empty), Assert.Single(service.ZoneEntered(Bo).Orders));
        Assert.Empty(service.ZoneEntered(Cy).Orders);
    }

    [Fact]
    public void CreatingAGuildRepliesWithTheMasterBlock()
    {
        var (service, _) = NewService();
        Enter(service, (Ana, "Ana"));

        var reply = Assert.Single(service.Create(Ana, "Crew", "we hunt"));

        Assert.Equal((Ana, GuildFrames.Created), (reply.To, reply.Opcode));
        Assert.Equal(GuildFrames.CreatedFor(new GuildBlock(Ana, 77, "Crew", "Ana", string.Empty), 0, GuildFrames.MasterRights, Text), reply.Payload);
    }

    [Fact]
    public void AMemberOrApplicantCannotCreateAndAPenaltyCarriesItsDate()
    {
        var (service, rules) = GuildOfAnaAndBo();
        rules.Applying[Cy] = 77;
        service.Login(Cy, "Cy");
        service.OpenSession(Cy);

        Assert.Equal(GuildFrames.CreateAlreadyMember, Assert.Single(service.Create(Bo, "Other", string.Empty)).Payload[0]);
        Assert.Equal(GuildFrames.CreateCandidate, Assert.Single(service.Create(Cy, "Other", string.Empty)).Payload[0]);

        var (fresh, freshRules) = NewService();
        Enter(fresh, (Ana, "Ana"));
        freshRules.Penalty = new GuildPenalty
        {
            CharacterId = Ana, PenaltyType = GuildRules.DissolvePenalty, ReleaseAt = new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero),
        };
        Assert.Equal(GuildFrames.CreateRefused(GuildFrames.CreatePenalty, 0, "26-11-18", Text), Assert.Single(fresh.Create(Ana, "Crew", string.Empty)).Payload);
    }

    [Fact]
    public void ApplyingAnswersTheApplicantAndNotifiesWhoCanAnswer()
    {
        var (service, _) = GuildOfAnaAndBo();

        var sent = service.Apply(Cy, 77);

        Assert.Collection(
            sent,
            m =>
            {
                Assert.Equal((Cy, GuildFrames.ApplyResult), (m.To, m.Opcode));
                Assert.Equal(GuildFrames.ApplyResultFor(GuildFrames.Ok, true, 0, string.Empty, Text), m.Payload);
            },
            m => Assert.Equal((Ana, GuildFrames.NewVolunteer, GuildFrames.NewVolunteerLength), (m.To, m.Opcode, m.Payload.Length)));
    }

    [Fact]
    public void ApplyingToAFullGuildIsRefusedWithTargetFull()
    {
        var (service, rules) = GuildOfAnaAndBo();
        rules.Guilds[77] = ("Crew", Ana, 2);

        Assert.Equal(GuildFrames.ApplyTargetFull, Assert.Single(service.Apply(Cy, 77)).Payload[0]);
    }

    [Fact]
    public void CancellingAnApplicationFollowsTheGuildState()
    {
        var (service, _) = GuildOfAnaAndBo();
        service.Apply(Cy, 77);

        Assert.Equal(new byte[] { GuildFrames.Ok }, Assert.Single(service.CancelApplication(Cy)).Payload);
        Assert.Equal(new byte[] { GuildFrames.CancelNotApplying }, Assert.Single(service.CancelApplication(Cy)).Payload);
        Assert.Equal(new byte[] { GuildFrames.CancelAlreadyMember }, Assert.Single(service.CancelApplication(Bo)).Payload);
    }

    [Fact]
    public void TheApplicantListNeedsTheVolunteerRight()
    {
        var (service, _) = GuildOfAnaAndBo();
        service.Apply(Cy, 77);

        var master = Assert.Single(service.VolunteerList(Ana));
        var member = Assert.Single(service.VolunteerList(Bo));

        Assert.Equal(new byte[] { 0, 0, 1, 1 }, master.Payload[..4]);
        Assert.Equal(Cy, BinaryPrimitives.ReadUInt64LittleEndian(master.Payload.AsSpan(4)));
        Assert.Equal(GuildFrames.VolunteerListRefused(GuildFrames.VolunteerNoPermission), member.Payload);
    }

    [Fact]
    public void AcceptingAnApplicantTellsEveryoneAndGivesTheNewMemberTheGuildState()
    {
        var (service, _) = GuildOfAnaAndBo();
        service.Apply(Cy, 77);

        var sent = service.AnswerApply(Ana, Cy, GuildFrames.AnswerAccept);

        Assert.Equal(
            new[]
            {
                (Ana, GuildFrames.AnswerApplyResult), (Cy, GuildFrames.MasterAnswered), (Cy, GuildFrames.OwnInfoResult), (Cy, GuildFrames.State),
                (Ana, GuildFrames.MemberJoined), (Bo, GuildFrames.MemberJoined),
            },
            sent.Select(m => (m.To, m.Opcode)).ToArray());
        Assert.Equal(GuildFrames.AnswerApplyResultFor(GuildFrames.Ok, false, Cy, 0), sent[0].Payload);
        Assert.Equal(GuildFrames.MasterAnsweredFor("Crew", GuildFrames.MasterAnswerOk, Text), sent[1].Payload);
        Assert.Equal(GuildFrames.MemberJoinedFor(Cy, "Cy", 42, 3, "26-10-18", "26-10-18", Text), sent[4].Payload);
        Assert.Equal(new GuildNameOrder(Cy, "Crew", string.Empty), Assert.Single(sent.Orders));
    }

    [Fact]
    public void RefusingAnApplicantOnlyAnswersBothSides()
    {
        var (service, _) = GuildOfAnaAndBo();
        service.Apply(Cy, 77);

        var sent = service.AnswerApply(Ana, Cy, 1);

        Assert.Equal(new[] { (Ana, GuildFrames.AnswerApplyResult), (Cy, GuildFrames.MasterAnswered) }, sent.Select(m => (m.To, m.Opcode)).ToArray());
        Assert.Equal(GuildFrames.MasterAnsweredFor("Crew", GuildFrames.MasterAnswerCancel, Text), sent[1].Payload);
        Assert.Empty(sent.Orders);
    }

    [Fact]
    public void AnswersAreRefusedWithoutTheRightOrAboveTheMemberCap()
    {
        var (service, rules) = GuildOfAnaAndBo();
        service.Apply(Cy, 77);

        Assert.Equal(GuildFrames.AnswerApplyResultFor(GuildFrames.AnswerNoRight, true, Cy, 0), Assert.Single(service.AnswerApply(Bo, Cy, 0)).Payload);
        rules.ExtraMembers = GuildFrames.MostMembersToAccept;
        Assert.Equal(GuildFrames.AnswerApplyResultFor(GuildFrames.AnswerMemberFull, true, Cy, 0), Assert.Single(service.AnswerApply(Ana, Cy, 0)).Payload);
        Assert.Equal(GuildFrames.AnswerApplyResultFor(GuildFrames.AnswerInvalidVolunteer, true, Bo, 1), Assert.Single(service.AnswerApply(Ana, Bo, 1)).Payload);
    }

    [Fact]
    public void LeavingIsBroadcastToTheGuildAndClearsTheNameplate()
    {
        var (service, _) = GuildOfAnaAndBo();

        var sent = service.Leave(Bo);

        var left = GuildFrames.LeaveResultFor(Bo, "Bo", Text);
        Assert.Equal(new[] { Ana, Bo }, sent.Select(m => m.To).OrderBy(id => id).ToArray());
        Assert.All(sent, m =>
        {
            Assert.Equal(GuildFrames.LeaveResult, m.Opcode);
            Assert.Equal(left, m.Payload);
        });
        Assert.Equal(new GuildNameOrder(Bo, string.Empty, string.Empty), Assert.Single(sent.Orders));
        Assert.Equal(GuildFrames.LeaveRefused(GuildFrames.LeaveInvalidGuild), Assert.Single(service.Leave(Bo)).Payload);
    }

    [Fact]
    public void TheMasterCannotLeave()
    {
        var (service, _) = GuildOfAnaAndBo();

        Assert.Equal(GuildFrames.LeaveRefused(GuildFrames.LeaveMaster), Assert.Single(service.Leave(Ana)).Payload);
    }

    [Fact]
    public void TheMemberListMarksOnlineMembersWithoutALastLogin()
    {
        var (service, _) = GuildOfAnaAndBo();

        var reply = Assert.Single(service.MemberList(Ana, 0));

        Assert.Equal(GuildFrames.MemberListHeaderLength + 2 * GuildFrames.MemberEntryLength, reply.Payload.Length);
        Assert.Equal(GuildFrames.MemberListRefused(0), Assert.Single(service.MemberList(Cy, 0)).Payload);
    }

    [Fact]
    public void ForcingOutAMemberRepliesToTheMasterAndRemovesTheRowElsewhere()
    {
        var (service, _) = GuildOfAnaAndBo();

        var sent = service.ForceOut(Ana, new[] { AnswerEntry(Bo), AnswerEntry(Ana) });

        Assert.Equal(new[] { (Ana, GuildFrames.ForceOutResult), (Bo, GuildFrames.MembersRemoved) }, sent.Select(m => (m.To, m.Opcode)).ToArray());
        Assert.Equal(GuildFrames.ForceOutResultFor(GuildFrames.Ok, new[] { (AnswerEntry(Bo), GuildFrames.ForceOutRemoved), (AnswerEntry(Ana), GuildFrames.ForceOutSkipped) }),
            sent[0].Payload);
        Assert.Equal(GuildFrames.MembersRemovedFor(new[] { Bo }), sent[1].Payload);
        Assert.Equal(new GuildNameOrder(Bo, string.Empty, string.Empty), Assert.Single(sent.Orders));
    }

    [Fact]
    public void OnlyAMemberWithTheEditRightCanForceOut()
    {
        var (service, _) = GuildOfAnaAndBo();

        Assert.Equal(GuildFrames.ForceOutNoPermission, Assert.Single(service.ForceOut(Bo, new[] { AnswerEntry(Ana) })).Payload[0x0D]);
        Assert.Equal(GuildFrames.ForceOutInvalidGuild, Assert.Single(service.ForceOut(Cy, new[] { AnswerEntry(Ana) })).Payload[0x0D]);
    }

    [Fact]
    public void DissolvingClearsEveryOnlineMember()
    {
        var (service, _) = GuildOfAnaAndBo();

        Assert.Equal(GuildFrames.DissolveResultFor(true, GuildFrames.DissolveNotMaster), Assert.Single(service.Dissolve(Bo)).Payload);
        var sent = service.Dissolve(Ana);

        Assert.Equal(new[] { (Ana, GuildFrames.DissolveResult), (Bo, GuildFrames.MembersRemoved) }, sent.Select(m => (m.To, m.Opcode)).ToArray());
        Assert.Equal(new byte[] { 0, 0 }, sent[0].Payload);
        Assert.Equal(new[] { Ana, Bo }, sent.Orders.Select(o => o.CharacterId).OrderBy(id => id).ToArray());
        Assert.All(sent.Orders, o => Assert.Equal((string.Empty, string.Empty), (o.GuildName, o.Alias)));
    }

    [Fact]
    public void DetailIsFoundByCodeOrByAnOnlineMembersName()
    {
        var (service, _) = GuildOfAnaAndBo();

        var byCode = Assert.Single(service.DetailByCode(Cy, 77)).Payload;

        Assert.Equal(GuildFrames.DetailFor(new GuildDetailView(77, "Crew", "Ana", 0, 2, "we hunt"), Text), byCode);
        Assert.Equal(byCode, Assert.Single(service.DetailByCharacter(Cy, "bo")).Payload);
        Assert.Equal(GuildFrames.DetailRefused(GuildFrames.DetailNotFound), Assert.Single(service.DetailByCode(Cy, 5)).Payload);
        Assert.Equal(GuildFrames.DetailRefused(GuildFrames.DetailNoCharacter), Assert.Single(service.DetailByCharacter(Cy, "Nobody")).Payload);
        Assert.Equal(GuildFrames.DetailRefused(GuildFrames.DetailNotFound), Assert.Single(service.DetailByCharacter(Ana, "Cy")).Payload);
    }

    [Fact]
    public void AGuildSmsToAnotherGuildGoesToItsMaster()
    {
        var (service, _) = GuildOfAnaAndBo();
        service.OpenSession(Cy);

        var plan = service.GuildSms(Cy, 77);

        Assert.Equal(new GuildSmsPlan(GuildSmsOutcome.Deliver, Ana, "Cy"), plan);
    }

    [Fact]
    public void AGuildSmsToTheOwnGuildIsRefused()
    {
        var (service, _) = GuildOfAnaAndBo();

        Assert.Equal(GuildSmsOutcome.OwnGuild, service.GuildSms(Bo, 77).Outcome);
    }

    [Fact]
    public void AGuildSmsToAnUnknownGuildOrBeforeTheSessionOpensIsNotDelivered()
    {
        var (service, _) = GuildOfAnaAndBo();
        service.OpenSession(Cy);
        service.Login(0x1999, "Dee");

        Assert.Equal(GuildSmsOutcome.UnknownGuild, service.GuildSms(Cy, 5).Outcome);
        Assert.Equal(GuildSmsOutcome.Dropped, service.GuildSms(0x1999, 77).Outcome);
    }
}
