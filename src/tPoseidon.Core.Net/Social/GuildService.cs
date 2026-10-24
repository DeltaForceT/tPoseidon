using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed record GuildMessage(ulong To, ushort Opcode, byte[] Payload);

public sealed record GuildNameOrder(ulong CharacterId, string GuildName, string Alias);

public sealed class GuildOutbox : IReadOnlyList<GuildMessage>
{
    private readonly List<GuildMessage> _messages = new();
    private readonly List<GuildNameOrder> _orders = new();

    public IReadOnlyList<GuildNameOrder> Orders => _orders;

    public int Count => _messages.Count;

    public GuildMessage this[int index] => _messages[index];

    public void Add(GuildMessage message) => _messages.Add(message);

    public void Order(GuildNameOrder order) => _orders.Add(order);

    public IEnumerator<GuildMessage> GetEnumerator() => _messages.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class GuildService
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, Member> _online = new();
    private readonly IGuildRulesStore _rules;
    private readonly Encoding _text;
    private readonly TimeProvider _time;

    public GuildService(IGuildRulesStore rules, Encoding text, TimeProvider time)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public GuildOutbox Login(ulong characterId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            var member = new Member(characterId, name);
            _online[characterId] = member;
            Refresh(member);
            if (member.State == GuildRules.Joined) outbox.Order(new GuildNameOrder(characterId, member.GuildName, member.Nick));
        }
        return outbox;
    }

    public void MemberRenamed(ulong characterId, string newName)
    {
        ArgumentNullException.ThrowIfNull(newName);
        lock (_gate)
        {
            if (_online.TryGetValue(characterId, out var member)) member.Name = newName;
        }
    }

    public GuildOutbox Logout(ulong characterId)
    {
        lock (_gate) _online.Remove(characterId);
        return new GuildOutbox();
    }

    public GuildOutbox OpenSession(ulong characterId)
    {
        lock (_gate)
        {
            if (_online.TryGetValue(characterId, out var member)) member.SessionOpen = true;
        }
        return new GuildOutbox();
    }

    public GuildOutbox ZoneEntered(ulong characterId)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (_online.TryGetValue(characterId, out var member) && member.State == GuildRules.Joined)
            {
                outbox.Order(new GuildNameOrder(characterId, member.GuildName, member.Nick));
            }
        }
        return outbox;
    }

    public GuildGameEntrant? GameEntrant(ulong characterId, ushort level)
    {
        lock (_gate)
        {
            return _online.TryGetValue(characterId, out var member) && member.State == GuildRules.Joined
                ? new GuildGameEntrant(member.CharacterId, member.Name, level, member.GuildCode, member.GuildName, member.Rights)
                : null;
        }
    }

    public GuildOutbox OwnInfo(ulong characterId, ulong requestedCharacterId)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            if (requestedCharacterId != characterId)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.OwnInfoResult, GuildFrames.OwnInfoFailed(GuildFrames.OwnInfoNotMyCharacter)));
                return outbox;
            }
            SendOwnState(outbox, member, Refresh(member));
        }
        return outbox;
    }

    public GuildOutbox Create(ulong characterId, string guildName, string intro)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(intro);
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            if (member.State == GuildRules.Joined)
            {
                outbox.Add(CreateRefusal(characterId, GuildFrames.CreateAlreadyMember, null));
                return outbox;
            }
            if (member.State == GuildRules.Applying)
            {
                outbox.Add(CreateRefusal(characterId, GuildFrames.CreateCandidate, null));
                return outbox;
            }
            var created = _rules.Create(characterId, guildName, intro);
            if (!created.Succeeded)
            {
                outbox.Add(CreateRefusal(characterId, CreateCode(created.Error), created.Penalty));
                return outbox;
            }
            if (Refresh(member) is not { } detail)
            {
                outbox.Add(CreateRefusal(characterId, GuildFrames.CreateError, null));
                return outbox;
            }
            outbox.Add(new GuildMessage(characterId, GuildFrames.Created,
                GuildFrames.CreatedFor(BlockOf(member, detail), GradeIndexOf(member.GradeCode), member.Rights, _text)));
        }
        return outbox;
    }

    public GuildOutbox Apply(ulong characterId, uint guildCode)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            var applied = _rules.Apply(characterId, guildCode);
            if (!applied.Succeeded)
            {
                var (type, date) = PenaltyOf(applied.Penalty);
                outbox.Add(new GuildMessage(characterId, GuildFrames.ApplyResult,
                    GuildFrames.ApplyResultFor(ApplyCode(applied.Error), false, type, date, _text)));
                return outbox;
            }
            member.State = GuildRules.Applying;
            var answerers = OnlineMembers(guildCode).Where(m => (m.Rights & GuildFrames.RightVolunteers) != 0).ToArray();
            outbox.Add(new GuildMessage(characterId, GuildFrames.ApplyResult,
                GuildFrames.ApplyResultFor(GuildFrames.Ok, answerers.Length > 0, 0, string.Empty, _text)));
            foreach (var answerer in answerers)
            {
                outbox.Add(new GuildMessage(answerer.CharacterId, GuildFrames.NewVolunteer, GuildFrames.NewVolunteerNotice()));
            }
        }
        return outbox;
    }

    public GuildOutbox CancelApplication(ulong characterId)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            byte code;
            if (member.State == GuildRules.Joined)
            {
                code = GuildFrames.CancelAlreadyMember;
            }
            else if (member.State != GuildRules.Applying)
            {
                code = GuildFrames.CancelNotApplying;
            }
            else
            {
                var error = _rules.CancelApplication(characterId);
                code = error switch
                {
                    GuildRules.Ok => GuildFrames.Ok,
                    101 => GuildFrames.CancelNotApplying,
                    102 => GuildFrames.CancelAlreadyMember,
                    _ => GuildFrames.CancelError,
                };
                if (code is GuildFrames.Ok or GuildFrames.CancelNotApplying) member.ClearGuild();
            }
            outbox.Add(new GuildMessage(characterId, GuildFrames.CancelApplyResult, GuildFrames.CancelApplyResultFor(code)));
        }
        return outbox;
    }

    public GuildOutbox Leave(ulong characterId)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            if (member.State != GuildRules.Joined || _rules.Detail(member.GuildCode) is null)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.LeaveResult, GuildFrames.LeaveRefused(GuildFrames.LeaveInvalidGuild)));
                return outbox;
            }
            var guildCode = member.GuildCode;
            var error = _rules.Leave(characterId, guildCode);
            if (error != GuildRules.Ok)
            {
                var reason = error switch
                {
                    101 => GuildFrames.LeaveInvalidGuild,
                    102 => GuildFrames.LeaveMaster,
                    _ => GuildFrames.LeaveNotInGuild,
                };
                outbox.Add(new GuildMessage(characterId, GuildFrames.LeaveResult, GuildFrames.LeaveRefused(reason)));
                return outbox;
            }
            var left = GuildFrames.LeaveResultFor(characterId, member.Name, _text);
            foreach (var recipient in OnlineMembers(guildCode)) outbox.Add(new GuildMessage(recipient.CharacterId, GuildFrames.LeaveResult, left));
            ClearMembership(outbox, member);
        }
        return outbox;
    }

    public GuildOutbox MemberList(ulong characterId, uint mode)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            if (member.State != GuildRules.Joined || _rules.Detail(member.GuildCode) is null)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.MemberListResult, GuildFrames.MemberListRefused(mode)));
                return outbox;
            }
            var rows = _rules.Members(member.GuildCode).Select(entry => new GuildMemberRow(
                entry.CharacterId,
                GradeIndexOf(entry.GradeCode),
                (byte)entry.Job,
                (ushort)entry.Level,
                entry.MemberPoint,
                Local(entry.JoinedAt),
                entry.CharName,
                _online.ContainsKey(entry.CharacterId) ? null : Local(entry.LastLogoutAt))).ToArray();
            outbox.Add(new GuildMessage(characterId, GuildFrames.MemberListResult, GuildFrames.MemberListFor(rows, mode, _text)));
        }
        return outbox;
    }

    public GuildOutbox VolunteerList(ulong characterId)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            if (member.State != GuildRules.Joined || (member.Rights & GuildFrames.RightVolunteers) == 0)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.VolunteerListResult, GuildFrames.VolunteerListRefused(GuildFrames.VolunteerNoPermission)));
                return outbox;
            }
            var rows = _rules.Applicants(member.GuildCode).Take(byte.MaxValue)
                .Select(a => new GuildVolunteerRow(a.CharacterId, (ushort)a.Level, a.CharName, (uint)a.Job, Local(a.AppliedAt)))
                .ToArray();
            outbox.Add(new GuildMessage(characterId, GuildFrames.VolunteerListResult, GuildFrames.VolunteerListFor(rows, _text)));
        }
        return outbox;
    }

    public GuildOutbox AnswerApply(ulong characterId, ulong applicantId, uint answer)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            var detail = member.State == GuildRules.Joined ? _rules.Detail(member.GuildCode) : null;
            uint? refusal = detail is null
                ? GuildFrames.AnswerInvalidGuild
                : (member.Rights & GuildFrames.RightVolunteers) == 0
                    ? GuildFrames.AnswerNoRight
                    : answer == GuildFrames.AnswerAccept && detail.Guild.GuildMembers > GuildFrames.MostMembersToAccept
                        ? GuildFrames.AnswerMemberFull
                        : null;
            if (refusal is { } early)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.AnswerApplyResult, GuildFrames.AnswerApplyResultFor(early, true, applicantId, answer)));
                return outbox;
            }
            var guild = detail!.Guild;
            var approve = answer == GuildFrames.AnswerAccept;
            var permitted = _rules.Permit(applicantId, member.GuildCode, approve ? GuildRules.Approve : GuildRules.Reject);
            if (!permitted.Succeeded)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.AnswerApplyResult,
                    GuildFrames.AnswerApplyResultFor(AnswerCode(permitted.Error), true, applicantId, answer)));
                return outbox;
            }
            outbox.Add(new GuildMessage(characterId, GuildFrames.AnswerApplyResult, GuildFrames.AnswerApplyResultFor(GuildFrames.Ok, false, applicantId, answer)));
            var existing = approve ? OnlineMembers(member.GuildCode).ToArray() : Array.Empty<Member>();
            if (_online.TryGetValue(applicantId, out var applicant))
            {
                outbox.Add(new GuildMessage(applicantId, GuildFrames.MasterAnswered,
                    GuildFrames.MasterAnsweredFor(guild.GuildName, approve ? GuildFrames.MasterAnswerOk : GuildFrames.MasterAnswerCancel, _text)));
                if (approve)
                {
                    SendOwnState(outbox, applicant, Refresh(applicant));
                    if (applicant.State == GuildRules.Joined) outbox.Order(new GuildNameOrder(applicantId, applicant.GuildName, applicant.Nick));
                }
                else
                {
                    applicant.ClearGuild();
                }
            }
            if (approve && permitted.Value is { } joined)
            {
                var today = GuildFrames.ShortDate(Local(_time.GetUtcNow()));
                var notice = GuildFrames.MemberJoinedFor(applicantId, joined.CharName, (ushort)joined.Level, (byte)joined.Job, today, today, _text);
                foreach (var recipient in existing) outbox.Add(new GuildMessage(recipient.CharacterId, GuildFrames.MemberJoined, notice));
            }
        }
        return outbox;
    }

    public GuildOutbox ForceOut(ulong characterId, IReadOnlyList<byte[]> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            if (member.State != GuildRules.Joined || _rules.Detail(member.GuildCode) is null)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.ForceOutResult,
                    GuildFrames.ForceOutResultFor(GuildFrames.ForceOutInvalidGuild, Array.Empty<(byte[], uint)>())));
                return outbox;
            }
            if ((member.Rights & GuildFrames.RightEditMembers) == 0)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.ForceOutResult,
                    GuildFrames.ForceOutResultFor(GuildFrames.ForceOutNoPermission, Array.Empty<(byte[], uint)>())));
                return outbox;
            }
            var guildCode = member.GuildCode;
            var recipients = OnlineMembers(guildCode).Where(m => m.CharacterId != characterId).ToArray();
            var results = new List<(byte[] Entry, uint Status)>(entries.Count);
            var removed = new List<ulong>();
            foreach (var entry in entries)
            {
                var target = GuildFrames.ForceOutTarget(entry);
                var kicked = _rules.Kick(target, guildCode).Succeeded;
                results.Add((entry, kicked ? GuildFrames.ForceOutRemoved : GuildFrames.ForceOutSkipped));
                if (kicked) removed.Add(target);
            }
            outbox.Add(new GuildMessage(characterId, GuildFrames.ForceOutResult, GuildFrames.ForceOutResultFor(GuildFrames.Ok, results)));
            if (removed.Count == 0) return outbox;
            var notice = GuildFrames.MembersRemovedFor(removed);
            foreach (var recipient in recipients) outbox.Add(new GuildMessage(recipient.CharacterId, GuildFrames.MembersRemoved, notice));
            foreach (var target in removed)
            {
                if (_online.TryGetValue(target, out var victim)) ClearMembership(outbox, victim);
            }
        }
        return outbox;
    }

    public GuildOutbox Dissolve(ulong characterId)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is not { } member) return outbox;
            var detail = member.State == GuildRules.Joined ? _rules.Detail(member.GuildCode) : null;
            if (detail is null)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.DissolveResult, GuildFrames.DissolveResultFor(true, GuildFrames.DissolveInvalidGuild)));
                return outbox;
            }
            if (detail.Guild.MasterCharacterId != characterId)
            {
                outbox.Add(new GuildMessage(characterId, GuildFrames.DissolveResult, GuildFrames.DissolveResultFor(true, GuildFrames.DissolveNotMaster)));
                return outbox;
            }
            var guildCode = member.GuildCode;
            var roster = _rules.Members(guildCode).Select(m => m.CharacterId).ToArray();
            var error = _rules.Dissolve(characterId, guildCode);
            if (error != GuildRules.Ok)
            {
                var reason = error switch
                {
                    101 => GuildFrames.DissolveInvalidGuild,
                    102 => GuildFrames.DissolveNotMaster,
                    _ => GuildFrames.DissolveError,
                };
                outbox.Add(new GuildMessage(characterId, GuildFrames.DissolveResult, GuildFrames.DissolveResultFor(true, reason)));
                return outbox;
            }
            outbox.Add(new GuildMessage(characterId, GuildFrames.DissolveResult, GuildFrames.DissolveResultFor(false, 0)));
            var members = OnlineMembers(guildCode).ToArray();
            var notice = GuildFrames.MembersRemovedFor(roster);
            foreach (var other in members.Where(m => m.CharacterId != characterId))
            {
                outbox.Add(new GuildMessage(other.CharacterId, GuildFrames.MembersRemoved, notice));
            }
            foreach (var gone in members) ClearMembership(outbox, gone);
        }
        return outbox;
    }

    public (int Error, GuildOutbox Outbox) ForceDissolve(uint guildCode, ulong masterCharacterId)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            var roster = _rules.Members(guildCode).Select(m => m.CharacterId).ToArray();
            var error = _rules.Dissolve(masterCharacterId, guildCode);
            if (error != GuildRules.Ok) return (error, outbox);
            var members = OnlineMembers(guildCode).ToArray();
            var notice = GuildFrames.MembersRemovedFor(roster);
            foreach (var member in members) outbox.Add(new GuildMessage(member.CharacterId, GuildFrames.MembersRemoved, notice));
            foreach (var gone in members) ClearMembership(outbox, gone);
        }
        return (GuildRules.Ok, outbox);
    }

    public GuildOutbox DetailByCode(ulong characterId, uint guildCode)
    {
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is null) return outbox;
            outbox.Add(new GuildMessage(characterId, GuildFrames.Detail, DetailPayload(guildCode)));
        }
        return outbox;
    }

    public GuildOutbox DetailByCharacter(ulong characterId, string characterName)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        var outbox = new GuildOutbox();
        lock (_gate)
        {
            if (Session(characterId) is null) return outbox;
            var target = _online.Values.FirstOrDefault(m => string.Equals(m.Name, characterName, StringComparison.OrdinalIgnoreCase));
            var payload = target is null
                ? GuildFrames.DetailRefused(GuildFrames.DetailNoCharacter)
                : target.State != GuildRules.Joined
                    ? GuildFrames.DetailRefused(GuildFrames.DetailNotFound)
                    : DetailPayload(target.GuildCode);
            outbox.Add(new GuildMessage(characterId, GuildFrames.Detail, payload));
        }
        return outbox;
    }

    private byte[] DetailPayload(uint guildCode)
    {
        if (_rules.Detail(guildCode) is not { } detail) return GuildFrames.DetailRefused(GuildFrames.DetailNotFound);
        var guild = detail.Guild;
        return GuildFrames.DetailFor(
            new GuildDetailView(guildCode, guild.GuildName, detail.MasterName, guild.GuildPoint, guild.GuildMembers, guild.GuildIntro), _text);
    }

    private void SendOwnState(GuildOutbox outbox, Member member, GuildDetail? detail)
    {
        var id = member.CharacterId;
        if (member.State == GuildRules.Joined && detail is not null)
        {
            var gradeIndex = GradeIndexOf(member.GradeCode);
            outbox.Add(new GuildMessage(id, GuildFrames.OwnInfoResult,
                GuildFrames.OwnInfoFor(member.GuildCode, id, member.Nick, gradeIndex, member.NewNotice, inGuild: true, _text)));
            var grades = Enumerable.Range(1, GuildFrames.GradeCount)
                .Select(code => detail.Grades.FirstOrDefault(g => g.GradeCode == code)?.GradeName ?? string.Empty)
                .ToArray();
            var state = new GuildStateView(BlockOf(member, detail), (ushort)detail.MaxMembers, gradeIndex, member.Rights, grades);
            outbox.Add(new GuildMessage(id, GuildFrames.State, GuildFrames.StateFor(state, _text)));
            return;
        }
        if (member.State == GuildRules.Applying)
        {
            outbox.Add(new GuildMessage(id, GuildFrames.OwnInfoResult, GuildFrames.OwnInfoFailed(GuildFrames.OwnInfoApplying)));
            return;
        }
        outbox.Add(new GuildMessage(id, GuildFrames.OwnInfoResult, GuildFrames.OwnInfoFor(0, id, string.Empty, 0, false, inGuild: false, _text)));
    }

    private static GuildBlock BlockOf(Member member, GuildDetail detail) =>
        new(member.CharacterId, member.GuildCode, detail.Guild.GuildName, detail.MasterName, detail.Guild.GuildNotice);

    private static void ClearMembership(GuildOutbox outbox, Member member)
    {
        member.ClearGuild();
        outbox.Order(new GuildNameOrder(member.CharacterId, string.Empty, string.Empty));
    }

    private GuildDetail? Refresh(Member member)
    {
        var info = _rules.CharacterGuild(member.CharacterId);
        member.ClearGuild();
        member.State = info.State;
        if (info.State != GuildRules.Joined) return null;
        if (_rules.Detail(info.GuildCode) is not { } detail)
        {
            member.State = GuildRules.NotInGuild;
            return null;
        }
        member.GuildCode = info.GuildCode;
        member.GuildName = detail.Guild.GuildName;
        member.Nick = info.MemberNick;
        member.GradeCode = info.GradeCode;
        member.NewNotice = info.NewNotice;
        member.Rights = detail.Guild.MasterCharacterId == member.CharacterId ? GuildFrames.MasterRights : (ushort)info.MemberRight;
        return detail;
    }

    private Member? Session(ulong characterId) =>
        _online.TryGetValue(characterId, out var member) && member.SessionOpen ? member : null;

    private IEnumerable<Member> OnlineMembers(uint guildCode) =>
        _online.Values.Where(m => m.State == GuildRules.Joined && m.GuildCode == guildCode);

    private DateTimeOffset Local(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, _time.LocalTimeZone);

    private (byte Type, string Date) PenaltyOf(GuildPenalty? penalty) =>
        penalty is null ? ((byte)0, string.Empty) : (penalty.PenaltyType, GuildFrames.ShortDate(Local(penalty.ReleaseAt)));

    private GuildMessage CreateRefusal(ulong characterId, byte code, GuildPenalty? penalty)
    {
        var (type, date) = PenaltyOf(penalty);
        return new GuildMessage(characterId, GuildFrames.Created, GuildFrames.CreateRefused(code, type, date, _text));
    }

    private static byte GradeIndexOf(byte gradeCode) =>
        gradeCode is >= GuildRules.MasterGrade and <= GuildRules.NewMemberGrade ? (byte)(gradeCode - GuildRules.MasterGrade) : (byte)(GuildRules.NewMemberGrade - GuildRules.MasterGrade);

    private static byte CreateCode(int error) => error switch
    {
        102 => GuildFrames.CreateExistingName,
        103 => GuildFrames.CreateOneMasterPerAccount,
        104 => GuildFrames.CreatePenalty,
        107 => GuildFrames.CreateCandidate,
        _ => GuildFrames.CreateError,
    };

    private static byte ApplyCode(int error) => error switch
    {
        101 => GuildFrames.ApplyOtherGuildMember,
        102 => GuildFrames.ApplyOnApplicant,
        103 => GuildFrames.ApplyPenalty,
        105 => GuildFrames.ApplyTargetFull,
        108 => GuildFrames.ApplyBlockedByGm,
        _ => GuildFrames.ApplyError,
    };

    private static uint AnswerCode(int error) => error switch
    {
        101 => GuildFrames.AnswerAlreadyMember,
        102 => GuildFrames.AnswerInvalidVolunteer,
        104 => GuildFrames.AnswerInvalidGuild,
        105 => GuildFrames.AnswerMemberFull,
        106 => GuildFrames.AnswerAlreadyOurMember,
        _ => GuildFrames.AnswerQueryFail,
    };

    private sealed class Member
    {
        public Member(ulong characterId, string name)
        {
            CharacterId = characterId;
            Name = name;
        }

        public ulong CharacterId { get; }

        public string Name { get; set; }

        public bool SessionOpen { get; set; }

        public byte State { get; set; }

        public uint GuildCode { get; set; }

        public string GuildName { get; set; } = string.Empty;

        public string Nick { get; set; } = string.Empty;

        public byte GradeCode { get; set; }

        public bool NewNotice { get; set; }

        public ushort Rights { get; set; }

        public void ClearGuild()
        {
            State = GuildRules.NotInGuild;
            GuildCode = 0;
            GuildName = string.Empty;
            Nick = string.Empty;
            GradeCode = 0;
            NewNotice = false;
            Rights = 0;
        }
    }
}
