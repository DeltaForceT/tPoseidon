namespace tPoseidon.Core.Data;

public sealed class InMemoryGuildStore : IGuildStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, Guild> _guilds = new();
    private readonly Dictionary<(ulong G, ulong C), GuildMember> _members = new();
    private readonly Dictionary<(ulong G, ulong C), GuildMemberStandby> _standby = new();
    private readonly Dictionary<(ulong G, byte Grade), GuildGrade> _grades = new();
    private readonly Dictionary<ulong, GuildRecruit> _recruit = new();
    private readonly List<GuildNotice> _notices = new();
    private readonly List<GuildLogEntry> _log = new();
    private readonly List<GuildPenalty> _penalties = new();
    private readonly List<GuildPointHistoryEntry> _pointHistory = new();
    private readonly Dictionary<ulong, GuildLadder> _ladder = new();
    private readonly List<GuildLadderLogEntry> _ladderLog = new();
    private readonly List<GuildLadderLogEntry> _ladderIncreaseUpdate = new();
    private readonly List<GuildMatchEntry> _matches = new();
    private readonly List<GuildRankingEntry> _ranking = new();
    private readonly List<GuildStateEntry> _stateLog = new();
    private ulong _nextGuildCode = 1;
    private long _nextNoticeCode = 1;
    private long _nextLogSeq = 1;
    private long _nextHistorySeq = 1;
    private long _nextLadderLogSeq = 1;
    private long _nextLadderIncSeq = 1;
    private long _nextMatchCode = 1;
    private long _nextRankingSeq = 1;
    private long _nextStateSeq = 1;

    public Guild CreateGuild(string guildName, uint masterAccountId, ulong masterCharacterId, string guildIntro = "")
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(guildIntro);
        lock (_gate)
        {
            if (_guilds.Values.Any(g => string.Equals(g.GuildName, guildName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Guild '{guildName}' already exists.");
            }
            var guild = new Guild
            {
                GuildCode = _nextGuildCode++,
                GuildName = guildName,
                MasterAccountId = masterAccountId,
                MasterCharacterId = masterCharacterId,
                GuildIntro = guildIntro,
                GuildCreatedAt = DateTimeOffset.UtcNow,
            };
            _guilds[guild.GuildCode] = guild;
            _members[(guild.GuildCode, masterCharacterId)] = new GuildMember
            {
                GuildCode = guild.GuildCode,
                CharacterId = masterCharacterId,
                MemberNick = "",
                MemberCreatedAt = DateTimeOffset.UtcNow,
                GradeCode = 0,
                MemberRight = 0xFF,
            };
            return guild;
        }
    }

    public Guild? FindGuild(ulong guildCode)
    {
        lock (_gate) { return _guilds.TryGetValue(guildCode, out var g) ? g : null; }
    }

    public Guild? FindGuildByName(string guildName)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        lock (_gate)
        {
            return _guilds.Values.FirstOrDefault(g => string.Equals(g.GuildName, guildName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<Guild> ListGuilds(int offset = 0, int limit = 50)
    {
        lock (_gate)
        {
            return _guilds.Values.OrderByDescending(g => g.GuildPoint).Skip(offset).Take(limit).ToArray();
        }
    }

    public Guild UpdateGuildHeader(Guild guild)
    {
        ArgumentNullException.ThrowIfNull(guild);
        lock (_gate)
        {
            _guilds[guild.GuildCode] = guild;
            return guild;
        }
    }

    public bool DisbandGuild(ulong guildCode)
    {
        lock (_gate)
        {
            if (!_guilds.Remove(guildCode)) return false;
            var mKeys = _members.Keys.Where(k => k.G == guildCode).ToArray();
            foreach (var k in mKeys) _members.Remove(k);
            var sKeys = _standby.Keys.Where(k => k.G == guildCode).ToArray();
            foreach (var k in sKeys) _standby.Remove(k);
            var gKeys = _grades.Keys.Where(k => k.G == guildCode).ToArray();
            foreach (var k in gKeys) _grades.Remove(k);
            _recruit.Remove(guildCode);
            _ladder.Remove(guildCode);
            return true;
        }
    }

    public GuildMember AddMember(ulong guildCode, ulong characterId, string memberNick, byte gradeCode = 0)
    {
        ArgumentNullException.ThrowIfNull(memberNick);
        lock (_gate)
        {
            var member = new GuildMember
            {
                GuildCode = guildCode,
                CharacterId = characterId,
                MemberNick = memberNick,
                MemberCreatedAt = DateTimeOffset.UtcNow,
                GradeCode = gradeCode,
            };
            _members[(guildCode, characterId)] = member;
            if (_guilds.TryGetValue(guildCode, out var g))
            {
                _guilds[guildCode] = g with { GuildMembers = (short)(g.GuildMembers + 1) };
            }
            return member;
        }
    }

    public bool RemoveMember(ulong guildCode, ulong characterId)
    {
        lock (_gate)
        {
            if (!_members.Remove((guildCode, characterId))) return false;
            if (_guilds.TryGetValue(guildCode, out var g) && g.GuildMembers > 0)
            {
                _guilds[guildCode] = g with { GuildMembers = (short)(g.GuildMembers - 1) };
            }
            return true;
        }
    }

    public GuildMember? FindMember(ulong guildCode, ulong characterId)
    {
        lock (_gate) { return _members.TryGetValue((guildCode, characterId), out var m) ? m : null; }
    }

    public IReadOnlyList<GuildMember> GetMembers(ulong guildCode)
    {
        lock (_gate)
        {
            return _members.Values.Where(m => m.GuildCode == guildCode)
                .OrderBy(m => m.GradeCode)
                .ThenByDescending(m => m.MemberPoint)
                .ToArray();
        }
    }

    public GuildMember? FindMembershipForCharacter(ulong characterId)
    {
        lock (_gate)
        {
            return _members.Values.FirstOrDefault(m => m.CharacterId == characterId);
        }
    }

    public bool UpdateMemberGrade(ulong guildCode, ulong characterId, byte gradeCode)
    {
        lock (_gate)
        {
            if (!_members.TryGetValue((guildCode, characterId), out var m)) return false;
            _members[(guildCode, characterId)] = m with { GradeCode = gradeCode };
            return true;
        }
    }

    public int AddMemberPoints(ulong guildCode, ulong characterId, int delta)
    {
        lock (_gate)
        {
            if (!_members.TryGetValue((guildCode, characterId), out var m)) return 0;
            var updated = m with { MemberPoint = m.MemberPoint + delta };
            _members[(guildCode, characterId)] = updated;
            return updated.MemberPoint;
        }
    }

    public GuildMemberStandby AddStandby(ulong guildCode, ulong characterId, byte standbyState = 0)
    {
        lock (_gate)
        {
            var s = new GuildMemberStandby
            {
                GuildCode = guildCode,
                CharacterId = characterId,
                StandbyState = standbyState,
                StandbyAt = DateTimeOffset.UtcNow,
            };
            _standby[(guildCode, characterId)] = s;
            return s;
        }
    }

    public IReadOnlyList<GuildMemberStandby> GetStandbyForGuild(ulong guildCode)
    {
        lock (_gate)
        {
            return _standby.Values.Where(s => s.GuildCode == guildCode)
                .OrderBy(s => s.StandbyAt)
                .ToArray();
        }
    }

    public bool RemoveStandby(ulong guildCode, ulong characterId)
    {
        lock (_gate) { return _standby.Remove((guildCode, characterId)); }
    }

    public GuildGrade UpsertGrade(GuildGrade grade)
    {
        ArgumentNullException.ThrowIfNull(grade);
        lock (_gate)
        {
            _grades[(grade.GuildCode, grade.GradeCode)] = grade;
            return grade;
        }
    }

    public IReadOnlyList<GuildGrade> GetGrades(ulong guildCode)
    {
        lock (_gate)
        {
            return _grades.Values.Where(g => g.GuildCode == guildCode)
                .OrderBy(g => g.GradeCode)
                .ToArray();
        }
    }

    public bool DeleteGrade(ulong guildCode, byte gradeCode)
    {
        lock (_gate) { return _grades.Remove((guildCode, gradeCode)); }
    }

    public GuildRecruit UpsertRecruit(GuildRecruit recruit)
    {
        ArgumentNullException.ThrowIfNull(recruit);
        lock (_gate)
        {
            var record = recruit with { RecruitUpdatedAt = DateTimeOffset.UtcNow };
            _recruit[recruit.GuildCode] = record;
            return record;
        }
    }

    public GuildRecruit? GetRecruit(ulong guildCode)
    {
        lock (_gate) { return _recruit.TryGetValue(guildCode, out var r) ? r : null; }
    }

    public bool DeleteRecruit(ulong guildCode)
    {
        lock (_gate) { return _recruit.Remove(guildCode); }
    }

    public GuildNotice AddNotice(ulong guildCode, ulong authorCharId, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        lock (_gate)
        {
            var notice = new GuildNotice
            {
                NoticeCode = _nextNoticeCode++,
                GuildCode = guildCode,
                CharacterId = authorCharId,
                NoticeContent = content,
                NoticeCreatedAt = DateTimeOffset.UtcNow,
            };
            _notices.Add(notice);
            return notice;
        }
    }

    public IReadOnlyList<GuildNotice> GetNotices(ulong guildCode, int limit = 20)
    {
        lock (_gate)
        {
            return _notices.Where(n => n.GuildCode == guildCode)
                .OrderByDescending(n => n.NoticeCreatedAt)
                .Take(limit)
                .ToArray();
        }
    }

    public bool DeleteNotice(long noticeCode)
    {
        lock (_gate)
        {
            var idx = _notices.FindIndex(n => n.NoticeCode == noticeCode);
            if (idx < 0) return false;
            _notices.RemoveAt(idx);
            return true;
        }
    }

    public GuildLogEntry AppendLog(ulong guildCode, byte logType, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            var entry = new GuildLogEntry
            {
                LogSeq = _nextLogSeq++,
                LogAt = DateTimeOffset.UtcNow,
                LogType = logType,
                GuildCode = guildCode,
                LogMsg = message,
            };
            _log.Add(entry);
            return entry;
        }
    }

    public IReadOnlyList<GuildLogEntry> GetLog(ulong guildCode, int limit = 50)
    {
        lock (_gate)
        {
            return _log.Where(e => e.GuildCode == guildCode)
                .OrderByDescending(e => e.LogSeq)
                .Take(limit)
                .ToArray();
        }
    }

    public GuildPenalty AddPenalty(ulong characterId, string guildName, byte penaltyType, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var pen = new GuildPenalty
            {
                CharacterId = characterId,
                GuildName = guildName,
                PenaltyType = penaltyType,
                SecedeAt = now,
                ReleaseAt = now + duration,
            };
            _penalties.Add(pen);
            return pen;
        }
    }

    public IReadOnlyList<GuildPenalty> GetPenaltiesForCharacter(ulong characterId)
    {
        lock (_gate)
        {
            return _penalties.Where(p => p.CharacterId == characterId)
                .OrderByDescending(p => p.SecedeAt)
                .ToArray();
        }
    }

    public GuildPointHistoryEntry AppendPointHistory(ulong guildCode, ulong? characterId, int delta, byte historyType)
    {
        lock (_gate)
        {
            var entry = new GuildPointHistoryEntry
            {
                Seq = _nextHistorySeq++,
                CharacterId = characterId,
                AddGuildPoint = delta,
                CreatedAt = DateTimeOffset.UtcNow,
                HistoryType = historyType,
                GuildCode = guildCode,
            };
            _pointHistory.Add(entry);
            if (_guilds.TryGetValue(guildCode, out var g))
            {
                _guilds[guildCode] = g with { GuildPoint = g.GuildPoint + delta };
            }
            return entry;
        }
    }

    public IReadOnlyList<GuildPointHistoryEntry> GetPointHistory(ulong guildCode, int limit = 50)
    {
        lock (_gate)
        {
            return _pointHistory.Where(e => e.GuildCode == guildCode)
                .OrderByDescending(e => e.Seq)
                .Take(limit)
                .ToArray();
        }
    }

    public GuildLadder UpsertLadder(GuildLadder ladder)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        lock (_gate)
        {
            _ladder[ladder.GuildCode] = ladder;
            return ladder;
        }
    }

    public GuildLadder? GetLadder(ulong guildCode)
    {
        lock (_gate) { return _ladder.TryGetValue(guildCode, out var l) ? l : null; }
    }

    public GuildLadderLogEntry AppendLadderLog(ulong guildCode, string guildName, int newLadder, int increase)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        lock (_gate)
        {
            var entry = new GuildLadderLogEntry
            {
                Seq = _nextLadderLogSeq++,
                GuildCode = guildCode,
                GuildName = guildName,
                GuildLadder = newLadder,
                LadderIncrease = increase,
                LogAt = DateTimeOffset.UtcNow,
            };
            _ladderLog.Add(entry);
            return entry;
        }
    }

    public IReadOnlyList<GuildLadderLogEntry> GetLadderLog(ulong guildCode, int limit = 50)
    {
        lock (_gate)
        {
            return _ladderLog.Where(e => e.GuildCode == guildCode)
                .OrderByDescending(e => e.Seq)
                .Take(limit)
                .ToArray();
        }
    }

    public GuildLadderLogEntry AppendLadderIncreaseUpdate(ulong guildCode, string guildName, int newLadder, int increase)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        lock (_gate)
        {
            var entry = new GuildLadderLogEntry
            {
                Seq = _nextLadderIncSeq++,
                GuildCode = guildCode,
                GuildName = guildName,
                GuildLadder = newLadder,
                LadderIncrease = increase,
                LogAt = DateTimeOffset.UtcNow,
            };
            _ladderIncreaseUpdate.Add(entry);
            return entry;
        }
    }

    public GuildMatchEntry RecordMatch(GuildMatchEntry match)
    {
        ArgumentNullException.ThrowIfNull(match);
        lock (_gate)
        {
            var entry = match with
            {
                MatchCode = _nextMatchCode++,
                MatchedAt = DateTimeOffset.UtcNow,
            };
            _matches.Add(entry);
            return entry;
        }
    }

    public IReadOnlyList<GuildMatchEntry> GetMatches(ulong guildCode, int limit = 50)
    {
        lock (_gate)
        {
            return _matches.Where(m => m.GuildCode == guildCode)
                .OrderByDescending(m => m.MatchCode)
                .Take(limit)
                .ToArray();
        }
    }

    public GuildRankingEntry SnapshotRanking(GuildRankingEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            var stored = entry with
            {
                Seq = _nextRankingSeq++,
                SnapshotAt = DateTimeOffset.UtcNow,
            };
            _ranking.Add(stored);
            return stored;
        }
    }

    public IReadOnlyList<GuildRankingEntry> GetRanking(string weekDay, int limit = 100)
    {
        ArgumentNullException.ThrowIfNull(weekDay);
        lock (_gate)
        {
            return _ranking.Where(r => r.WeekDay == weekDay)
                .OrderBy(r => r.Ranking)
                .Take(limit)
                .ToArray();
        }
    }

    public GuildStateEntry AppendState(ulong guildCode, byte stateType, string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        lock (_gate)
        {
            var entry = new GuildStateEntry
            {
                Seq = _nextStateSeq++,
                GuildCode = guildCode,
                StateType = stateType,
                StateNote = note,
                RegAt = DateTimeOffset.UtcNow,
            };
            _stateLog.Add(entry);
            return entry;
        }
    }

    public IReadOnlyList<GuildStateEntry> GetStateLog(ulong guildCode, int limit = 50)
    {
        lock (_gate)
        {
            return _stateLog.Where(e => e.GuildCode == guildCode)
                .OrderByDescending(e => e.Seq)
                .Take(limit)
                .ToArray();
        }
    }
}
