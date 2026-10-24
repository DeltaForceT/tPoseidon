using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed record PartyMessage(ulong To, ushort Opcode, byte[] Payload);

public sealed record PartyOrder(ulong CharacterId, ushort OrderType, ushort AgencyId, byte[]? Data)
{
    public bool IsCancel => Data is null;
}

public sealed class PartyOutbox : IReadOnlyList<PartyMessage>
{
    private readonly List<PartyMessage> _messages = new();
    private readonly List<PartyOrder> _orders = new();

    public IReadOnlyList<PartyOrder> Orders => _orders;

    public int Count => _messages.Count;

    public PartyMessage this[int index] => _messages[index];

    public void Add(PartyMessage message) => _messages.Add(message);

    public void Order(PartyOrder order) => _orders.Add(order);

    public IEnumerator<PartyMessage> GetEnumerator() => _messages.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record PartyRules(int LimitLevel, TimeSpan OfflineMemberKeepTime)
{
    public const int MostMembers = 6;
    public const int DefaultLimitLevel = 1;
    public const int DefaultDepotInterval = 6_000_000;
    public const int UnsetDepotInterval = -1;
    public const int UnsetDepotIntervalFallback = 60_000;

    public static PartyRules FromIni(int limitLevel, int depotInterval) => new(
        limitLevel,
        TimeSpan.FromMilliseconds(depotInterval == UnsetDepotInterval ? UnsetDepotIntervalFallback : depotInterval));
}

public static class PartyCodes
{
    public const byte NotInParty = 2;
    public const byte TargetNotFound = 3;
    public const byte AlreadyInParty = 4;
    public const byte TargetHasPendingInvite = 5;
    public const byte NoPendingInvite = 6;
    public const byte NotMaster = 8;
    public const byte PartyFull = 9;
    public const byte NotAMember = 0x0B;
    public const byte LevelTooLow = 0x0C;
    public const byte PartyQuestRunning = 0x1B;

    public const uint AskNoPending = 0;
    public const uint AskPartyFull = 9;
    public const uint AskAskerGone = 0x0B;
    public const uint AskPending = 0x12;
    public const uint AskAskerHasParty = 0x13;
    public const uint AskPartyMissing = 0x14;
    public const uint PartyBecameFull = 0x17;

    public const uint InfoNotFound = 0;
    public const uint InfoFull = 9;
}

public enum PartyType : byte
{
    None = 0,
    Normal = 1,
    Royal = 2,
    Type = 3,
    SameGender = 4,
    Full = 5,
    Separate = 6,
    Level = 7,
    MultipleLv = 8,
    Beginner = 9,
    Fixed = 10,
}

public static class PartyDivision
{
    public const byte Equal = 0;
    public const byte EqualRounded = 1;
    public const byte Custom = 2;
    public const byte ByLevel = 3;
}

public sealed class PartyService
{
    private const int CompositionMinimum = 4;
    private const int BeginnerLevel = 31;
    private const int FixedLevelGap = 100;

    private readonly object _gate = new();
    private readonly Dictionary<ulong, Member> _online = new();
    private readonly Dictionary<ushort, Party> _parties = new();
    private readonly Dictionary<ulong, DepotEntry> _depot = new();
    private readonly Dictionary<ulong, ushort> _zones = new();
    private readonly Encoding _text;
    private readonly PartyRules _rules;
    private readonly TimeProvider _time;
    private ushort _lastPartyId;
    private ushort _lastMemberId;

    public PartyService(Encoding text, PartyRules rules, TimeProvider time)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public int PartyCount { get { lock (_gate) return _parties.Count; } }

    public IReadOnlyList<ulong> PartyMembers(ulong characterId)
    {
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var member) || member.Party is not { } party) return Array.Empty<ulong>();
            return party.Online.Concat(party.Offline).Select(each => each.CharacterId).ToArray();
        }
    }

    public PartyOutbox Login(ulong characterId, string name, ushort kind, ushort level)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (_online.ContainsKey(characterId)) return outbox;
            var member = new Member(characterId, name, kind, level) { Zone = _zones.GetValueOrDefault(characterId) };
            if (_depot.Remove(characterId, out var entry)
                && _parties.TryGetValue(entry.PartyId, out var party)
                && party.Offline.Find(m => m.CharacterId == characterId) is { } kept)
            {
                party.Offline.Remove(kept);
                member.MemberId = kept.MemberId;
                member.Party = party;
                Broadcast(outbox, party, PartyFrames.MemberReturned, PartyFrames.MemberReturnedFor(member.MemberId, characterId, party.LevelFlag), except: null);
                party.Online.Add(member);
                RefreshMirrors(outbox, party);
                RecomputeShare(outbox, party);
            }
            else
            {
                member.MemberId = NextMemberId();
            }
            _online[characterId] = member;
        }
        return outbox;
    }

    public PartyOutbox Logout(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.Remove(characterId, out var member)) return outbox;
            ClearInviteState(member);
            if (member.Party is not { } party) return outbox;
            var wasMaster = party.MasterMemberId == member.MemberId;
            if (!wasMaster || party.Online.Count > 1)
            {
                _depot[characterId] = new DepotEntry(party.Id, _time.GetUtcNow() + _rules.OfflineMemberKeepTime);
            }
            party.Online.Remove(member);
            party.Offline.Add(member);
            if (party.PendingAsker == characterId) party.PendingAsker = 0;
            if (wasMaster && party.Online.Count > 0)
            {
                party.MasterMemberId = party.Online[0].MemberId;
                Broadcast(outbox, party, PartyFrames.MasterChanged, PartyFrames.MasterChangedFor(party.MasterMemberId, party.LevelFlag), except: null);
            }
            Broadcast(outbox, party, PartyFrames.MemberOffline, PartyFrames.MemberPair(member.MemberId, party.MasterMemberId), except: null);
            outbox.Order(new PartyOrder(member.CharacterId, SystemLinkFrames.OrderNotifyPartyPlayer, 0, null));
            RefreshMirrors(outbox, party);
            RecomputeShare(outbox, party);
            ForgetIfAbandoned(party);
        }
        return outbox;
    }

    public PartyOutbox ExpireOfflineMembers()
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            foreach (var (characterId, entry) in _depot.Where(e => e.Value.ExpiresAt <= now).ToArray())
            {
                _depot.Remove(characterId);
                if (!_parties.TryGetValue(entry.PartyId, out var party)) continue;
                party.Offline.RemoveAll(m => m.CharacterId == characterId);
                Broadcast(outbox, party, PartyFrames.OfflineMemberExpired, PartyFrames.Character(characterId), except: null);
                ForgetIfAbandoned(party);
            }
        }
        return outbox;
    }

    public PartyOutbox Make(ulong characterId, string partyName, string candidateName)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var maker)) return outbox;
            if (maker.Party is not null)
            {
                outbox.Add(new(maker.CharacterId, PartyFrames.MakeFailed, PartyFrames.Code(PartyCodes.AlreadyInParty)));
                return outbox;
            }
            ClearInviteState(maker);
            if (maker.Level < _rules.LimitLevel)
            {
                outbox.Add(new(maker.CharacterId, PartyFrames.MakeFailed, PartyFrames.Code(PartyCodes.LevelTooLow)));
                return outbox;
            }
            if (FindByName(candidateName) is not { } candidate)
            {
                outbox.Add(new(maker.CharacterId, PartyFrames.MakeFailed, PartyFrames.Code(PartyCodes.TargetNotFound)));
                return outbox;
            }
            if (candidate.Level < _rules.LimitLevel)
            {
                outbox.Add(InviteFailedTo(maker, candidate.Name, PartyCodes.LevelTooLow));
                return outbox;
            }
            if (HasPendingInvite(candidate))
            {
                outbox.Add(InviteFailedTo(maker, candidate.Name, PartyCodes.TargetHasPendingInvite));
                return outbox;
            }
            if (candidate.Party is { } candidateParty)
            {
                if (MasterOf(candidateParty) is not { } candidateMaster)
                {
                    outbox.Add(InviteFailedTo(maker, candidate.Name, PartyCodes.TargetHasPendingInvite));
                    return outbox;
                }
                outbox.Add(new(maker.CharacterId, PartyFrames.CandidateHasParty, PartyFrames.CandidateHasPartyFor(
                    candidateParty.Id, candidate.Name, candidateMaster.Name, AverageLevel(candidateParty),
                    (byte)TypeOf(candidateParty), (byte)candidateParty.Size, candidateParty.DivisionType,
                    candidateParty.ItemShare, _text)));
                return outbox;
            }
            var party = new Party(NextPartyId(), partyName, maker.MemberId);
            party.Online.Add(maker);
            maker.Party = party;
            _parties[party.Id] = party;
            outbox.Add(new(maker.CharacterId, PartyFrames.Made, PartyFrames.PartyMade(party.Id, maker.MemberId)));
            RefreshMirrors(outbox, party);
            SetInvite(party, maker, candidate);
            outbox.Add(new(candidate.CharacterId, PartyFrames.Invitation, PartyFrames.InvitationFor(
                party.Name, maker.Name, (byte)party.Online.Count, party.LevelFlag ? (byte)0 : PartyDivision.ByLevel,
                party.ItemShare, AverageLevel(party), _text)));
        }
        return outbox;
    }

    public PartyOutbox Invite(ulong characterId, string targetName)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var inviter)) return outbox;
            if (inviter.Party is not { } party)
            {
                outbox.Add(InviteFailedTo(inviter, targetName, PartyCodes.NotInParty));
                return outbox;
            }
            if (party.MasterMemberId != inviter.MemberId)
            {
                outbox.Add(InviteFailedTo(inviter, targetName, PartyCodes.NotMaster));
                return outbox;
            }
            if (party.IsFull)
            {
                outbox.Add(InviteFailedTo(inviter, targetName, PartyCodes.PartyFull));
                return outbox;
            }
            if (party.Quest is not null)
            {
                outbox.Add(InviteFailedTo(inviter, targetName, PartyCodes.PartyQuestRunning));
                return outbox;
            }
            if (FindByName(targetName) is not { } target)
            {
                outbox.Add(InviteFailedTo(inviter, targetName, PartyCodes.TargetNotFound));
                return outbox;
            }
            if (target.Party is not null)
            {
                outbox.Add(InviteFailedTo(inviter, target.Name, PartyCodes.AlreadyInParty));
                return outbox;
            }
            if (target.Level < _rules.LimitLevel)
            {
                outbox.Add(InviteFailedTo(inviter, target.Name, PartyCodes.LevelTooLow));
                return outbox;
            }
            if (HasPendingInvite(target))
            {
                outbox.Add(InviteFailedTo(inviter, target.Name, PartyCodes.TargetHasPendingInvite));
                return outbox;
            }
            SetInvite(party, inviter, target);
            outbox.Add(new(target.CharacterId, PartyFrames.Invitation, PartyFrames.InvitationFor(
                party.Name, inviter.Name, (byte)party.Size, party.DivisionType, party.ItemShare, AverageLevel(party), _text)));
        }
        return outbox;
    }

    public PartyOutbox AcceptInvite(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var accepter)) return outbox;
            if (!HasPendingInvite(accepter) || accepter.InvitedBy is not { } invite)
            {
                outbox.Add(new(accepter.CharacterId, PartyFrames.AcceptFailed, PartyFrames.Code(PartyCodes.NoPendingInvite)));
                return outbox;
            }
            var party = _parties[invite.PartyId];
            var inviter = party.Online.Find(m => m.MemberId == invite.MemberId);
            if (party.IsFull)
            {
                ClearInviteState(accepter);
                outbox.Add(new(accepter.CharacterId, PartyFrames.AcceptFailed, PartyFrames.Code(PartyCodes.PartyFull)));
                if (inviter is not null) outbox.Add(InviteFailedTo(inviter, accepter.Name, PartyCodes.PartyFull));
                return outbox;
            }
            ClearInviteState(accepter);
            if (party.Size < 2 && inviter is not null)
            {
                outbox.Add(new(inviter.CharacterId, PartyFrames.Formed, PartyFrames.PartyFormed(party.Id, party.Name, invite.MemberId, _text)));
            }
            Join(outbox, party, accepter);
            if (inviter is not null)
            {
                outbox.Add(new(inviter.CharacterId, PartyFrames.JoinedNotice, PartyFrames.Text(accepter.Name, _text)));
            }
            NotifyIfFull(outbox, party);
        }
        return outbox;
    }

    public PartyOutbox RefuseInvite(ulong characterId, byte reason)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var refuser) || refuser.InvitedBy is not { } invite) return outbox;
            ClearInviteState(refuser);
            if (!_parties.TryGetValue(invite.PartyId, out var party)) return outbox;
            if (party.Online.Find(m => m.MemberId == invite.MemberId) is not { } inviter) return outbox;
            outbox.Add(InviteFailedTo(inviter, refuser.Name, reason));
            if (party.Size < 2) LeaveParty(outbox, inviter);
        }
        return outbox;
    }

    public PartyOutbox CancelInvite(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var inviter) || inviter.Party is not { } party) return outbox;
            if (party.PendingInviteTarget == 0) return outbox;
            var targetId = party.PendingInviteTarget;
            party.PendingInviteTarget = 0;
            if (_online.TryGetValue(targetId, out var target))
            {
                target.InvitedBy = null;
                outbox.Add(new(target.CharacterId, PartyFrames.InviteCancelled, PartyFrames.Empty()));
            }
        }
        return outbox;
    }

    public PartyOutbox Leave(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (_online.TryGetValue(characterId, out var member) && member.Party is not null) LeaveParty(outbox, member);
        }
        return outbox;
    }

    public PartyOutbox Kick(ulong characterId, ushort memberId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master)) return outbox;
            if (master.Party is not { } party)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.KickRefused, PartyFrames.Code(PartyCodes.NotInParty)));
                return outbox;
            }
            if (party.MasterMemberId != master.MemberId)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.KickRefused, PartyFrames.Code(PartyCodes.NotMaster)));
                return outbox;
            }
            if (memberId == master.MemberId || party.Online.Find(m => m.MemberId == memberId) is not { } target)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.KickRefused, PartyFrames.Code(PartyCodes.NotAMember)));
                return outbox;
            }
            Broadcast(outbox, party, PartyFrames.MemberKicked, PartyFrames.Member(memberId), except: null);
            party.Online.Remove(target);
            target.Party = null;
            outbox.Order(new PartyOrder(target.CharacterId, SystemLinkFrames.OrderNotifyPartyPlayer, 0, null));
            if (party.Size < 2) Break(outbox, party);
            else
            {
                RefreshMirrors(outbox, party);
                RecomputeShare(outbox, party);
                outbox.Add(new(master.CharacterId, PartyFrames.KickDone, PartyFrames.Empty()));
            }
        }
        return outbox;
    }

    public PartyOutbox KickOffline(ulong characterId, ulong offlineCharacterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master) || master.Party is not { } party) return outbox;
            if (party.MasterMemberId != master.MemberId) return outbox;
            if (party.Offline.RemoveAll(m => m.CharacterId == offlineCharacterId) > 0)
            {
                _depot.Remove(offlineCharacterId);
                Broadcast(outbox, party, PartyFrames.OfflineMemberRemoved, PartyFrames.Character(offlineCharacterId), except: null);
                if (party.Size < 2) Break(outbox, party);
            }
            else
            {
                outbox.Add(new(master.CharacterId, PartyFrames.OfflineMemberNotFound, PartyFrames.Character(offlineCharacterId)));
            }
        }
        return outbox;
    }

    public PartyOutbox ChangeMaster(ulong characterId, ushort memberId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master)) return outbox;
            if (master.Party is not { } party || party.Online.Find(m => m.MemberId == memberId) is null)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.MasterChangeRefused, PartyFrames.Code(PartyCodes.NotInParty)));
                return outbox;
            }
            if (party.MasterMemberId != master.MemberId)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.MasterChangeRefused, PartyFrames.Code(PartyCodes.NotMaster)));
                return outbox;
            }
            party.MasterMemberId = memberId;
            outbox.Add(new(master.CharacterId, PartyFrames.MasterChangeDone, PartyFrames.Empty()));
            Broadcast(outbox, party, PartyFrames.MasterChanged, PartyFrames.MasterChangedFor(memberId, party.LevelFlag), except: null);
            RefreshMirrors(outbox, party);
        }
        return outbox;
    }

    public PartyOutbox Rename(ulong characterId, string newName)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master)) return outbox;
            if (master.Party is not { } party)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.RenameRefused, PartyFrames.Code(PartyCodes.NotInParty)));
                return outbox;
            }
            if (party.MasterMemberId != master.MemberId)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.RenameRefused, PartyFrames.Code(PartyCodes.NotMaster)));
                return outbox;
            }
            party.Name = newName;
            outbox.Add(new(master.CharacterId, PartyFrames.RenameDone, PartyFrames.Empty()));
            Broadcast(outbox, party, PartyFrames.Renamed, PartyFrames.Text(newName, _text), except: null);
        }
        return outbox;
    }

    public PartyOutbox ChangeDivision(ulong characterId, byte divisionType, IReadOnlyList<PartyDivisionShare> customShares)
    {
        ArgumentNullException.ThrowIfNull(customShares);
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master)) return outbox;
            if (master.Party is not { } party)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.DivisionRefused, PartyFrames.Code(PartyCodes.NotInParty)));
                return outbox;
            }
            if (party.MasterMemberId != master.MemberId)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.DivisionRefused, PartyFrames.Code(PartyCodes.NotMaster)));
                return outbox;
            }
            party.DivisionType = divisionType;
            party.Shares = divisionType switch
            {
                PartyDivision.Custom => customShares.Take(PartyRules.MostMembers).ToList(),
                PartyDivision.ByLevel => LevelShares(party),
                _ => new List<PartyDivisionShare>(),
            };
            outbox.Add(new(master.CharacterId, PartyFrames.DivisionChanged, PartyFrames.Empty()));
            Broadcast(outbox, party, PartyFrames.Division, PartyFrames.DivisionFor(party.DivisionType, party.Shares), except: null);
            RecomputeShare(outbox, party);
        }
        return outbox;
    }

    public PartyOutbox Chat(ulong characterId, string text)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var sender) || sender.Party is not { } party) return outbox;
            if (!party.Online.Contains(sender)) return outbox;
            Broadcast(outbox, party, PartyFrames.ChatLine, PartyFrames.ChatLineFor(sender.MemberId, text, _text), except: null);
        }
        return outbox;
    }

    public PartyOutbox SetItemShare(ulong characterId, byte value)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var member) || member.Party is not { } party) return outbox;
            if (party.MasterMemberId != member.MemberId)
            {
                outbox.Add(new(member.CharacterId, PartyFrames.ItemShareDenied, PartyFrames.Empty()));
                return outbox;
            }
            if (party.ItemShare == value) return outbox;
            party.ItemShare = value;
            Broadcast(outbox, party, PartyFrames.ItemShareChanged, PartyFrames.Code(value), except: null);
            RecomputeShare(outbox, party);
        }
        return outbox;
    }

    public PartyOutbox SetSecondOption(ulong characterId, byte value)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var member) || member.Party is not { } party) return outbox;
            party.SecondOption = value;
            Broadcast(outbox, party, PartyFrames.SecondOptionChanged, PartyFrames.Code(value), except: null);
        }
        return outbox;
    }

    public PartyOutbox SetMemberFlag(ulong characterId, ulong subjectCharacterId, bool raised)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (subjectCharacterId == 0) return outbox;
            if (!_online.TryGetValue(characterId, out var member) || member.Party is not { } party) return outbox;
            var subject = party.Online.Find(m => m.CharacterId == subjectCharacterId);
            Broadcast(outbox, party, raised ? PartyFrames.MemberFlagRaised : PartyFrames.MemberFlagCleared,
                PartyFrames.MemberFlagFor(raised, subjectCharacterId), except: subject);
        }
        return outbox;
    }

    public PartyOutbox AskToJoin(ulong characterId, ushort partyId, string askerName, ushort x, uint y)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.ContainsKey(characterId)) return outbox;
            if (!_parties.TryGetValue(partyId, out var party))
            {
                outbox.Add(new(characterId, PartyFrames.AskResult, PartyFrames.Code32(PartyCodes.AskPartyMissing)));
                return outbox;
            }
            RelayJoinAsk(outbox, characterId, party, askerName, x, y);
        }
        return outbox;
    }

    public PartyOutbox AskToJoinThroughMember(ulong characterId, string memberName, string askerName, ushort x, uint y)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.ContainsKey(characterId)) return outbox;
            if (FindByName(memberName) is not { } member)
            {
                outbox.Add(new(characterId, PartyFrames.AskResult, PartyFrames.Code32(PartyCodes.AskNoPending)));
                return outbox;
            }
            if (member.Party is not { } party)
            {
                outbox.Add(new(characterId, PartyFrames.AskResult, PartyFrames.Code32(PartyCodes.AskPartyMissing)));
                return outbox;
            }
            RelayJoinAsk(outbox, characterId, party, askerName, x, y);
        }
        return outbox;
    }

    public PartyOutbox AcceptJoin(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master) || master.Party is not { } party) return outbox;
            if (party.MasterMemberId != master.MemberId) return outbox;
            var askerId = party.PendingAsker;
            if (askerId == 0)
            {
                outbox.Add(MasterResultTo(master, PartyCodes.AskNoPending));
                return outbox;
            }
            if (party.IsFull)
            {
                outbox.Add(MasterResultTo(master, PartyCodes.AskPartyFull));
                return outbox;
            }
            if (party.Quest is not null)
            {
                outbox.Add(MasterResultTo(master, PartyCodes.PartyQuestRunning));
                return outbox;
            }
            party.PendingAsker = 0;
            if (!_online.TryGetValue(askerId, out var asker))
            {
                outbox.Add(MasterResultTo(master, PartyCodes.AskAskerGone));
                return outbox;
            }
            if (asker.Party is not null)
            {
                outbox.Add(MasterResultTo(master, PartyCodes.AskAskerHasParty));
                return outbox;
            }
            if (asker.Level < _rules.LimitLevel)
            {
                outbox.Add(MasterResultTo(master, PartyCodes.AskNoPending));
                return outbox;
            }
            outbox.Add(new(master.CharacterId, PartyFrames.JoinedNotice, PartyFrames.Text(asker.Name, _text)));
            ClearInviteState(asker);
            Join(outbox, party, asker);
            NotifyIfFull(outbox, party);
        }
        return outbox;
    }

    public PartyOutbox RefuseJoin(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master)) return outbox;
            if (master.Party is not { } party || party.MasterMemberId != master.MemberId)
            {
                outbox.Add(new(master.CharacterId, PartyFrames.JoinRefuseRefused, PartyFrames.Empty()));
                return outbox;
            }
            var askerId = party.PendingAsker;
            party.PendingAsker = 0;
            if (askerId != 0) outbox.Add(new(askerId, PartyFrames.JoinDenied, PartyFrames.Empty()));
        }
        return outbox;
    }

    public PartyOutbox Dissolve(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master) || master.Party is not { } party) return outbox;
            if (party.MasterMemberId != master.MemberId) return outbox;
            Break(outbox, party);
        }
        return outbox;
    }

    public PartyOutbox Info(ulong characterId, string name)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.ContainsKey(characterId)) return outbox;
            if (FindByName(name) is not { Party: { } party })
            {
                outbox.Add(new(characterId, PartyFrames.InfoFailed, PartyFrames.Code32(PartyCodes.InfoNotFound)));
                return outbox;
            }
            if (party.IsFull)
            {
                outbox.Add(new(characterId, PartyFrames.InfoFailed, PartyFrames.Code32(PartyCodes.InfoFull)));
                return outbox;
            }
            outbox.Add(new(characterId, PartyFrames.Info, PartyFrames.InfoFor(
                party.Id, party.Name, AverageLevel(party), (byte)TypeOf(party), (byte)party.Size,
                party.DivisionType, party.ItemShare, _text)));
        }
        return outbox;
    }

    public static PartyType TypeOf(IReadOnlyList<(ushort Kind, ushort Level)> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count == 0) return PartyType.Normal;
        var levels = members.Select(m => (int)m.Level).ToArray();
        if (levels.Max() - levels.Min() >= FixedLevelGap) return PartyType.Fixed;
        if (levels.Any(l => l < BeginnerLevel)) return PartyType.Beginner;
        if (members.Count >= CompositionMinimum)
        {
            if (levels.Distinct().Count() == 1) return PartyType.Level;
            var sorted = levels.OrderBy(l => l).ToArray();
            if (Enumerable.Range(1, sorted.Length - 1).All(i => sorted[i] == (i + 1) * sorted[0])) return PartyType.MultipleLv;
            var genders = members.Select(m => GenderOf(m.Kind)).ToArray();
            if (genders.All(g => g is not null) && genders.Distinct().Count() == 1) return PartyType.SameGender;
            var classes = members.Select(m => ClassOf(m.Kind)).ToArray();
            if (classes.All(c => c is not null))
            {
                if (Enumerable.Range(0, 4).All(c => classes.Contains((byte)c))) return PartyType.Royal;
                if (classes.Distinct().Count() == 1) return PartyType.Type;
            }
        }
        return PartyType.Normal;
    }

    public const int TypesPerJobTier = 9;
    public const int JobTiers = 3;
    public const int PairedSpecies = 8;

    public static int? SpeciesOf(ushort kind) =>
        kind is >= 1 and <= TypesPerJobTier * JobTiers ? (kind - 1) % TypesPerJobTier + 1 : null;

    public static byte? ClassOf(ushort kind) =>
        SpeciesOf(kind) is int species && species <= PairedSpecies ? (byte)((species - 1) / 2) : null;

    public static byte? GenderOf(ushort kind) =>
        SpeciesOf(kind) is int species && species <= PairedSpecies ? (byte)((species - 1) % 2) : null;

    public static IReadOnlyList<PartyDivisionShare> LevelShares(IReadOnlyList<(ushort MemberId, ushort Level)> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var sum = members.Sum(m => (int)m.Level);
        if (sum == 0) return Array.Empty<PartyDivisionShare>();
        var percents = members.Select(m => (int)Math.Round(m.Level * 100.0 / sum, MidpointRounding.AwayFromZero)).ToArray();
        var remainder = 100 - percents.Sum();
        for (var i = 0; remainder != 0 && percents.Length > 0; i = (i + 1) % percents.Length)
        {
            var step = Math.Sign(remainder);
            if (percents[i] + step < 0) continue;
            percents[i] += step;
            remainder -= step;
        }
        return members.Select((m, i) => new PartyDivisionShare(m.MemberId, (byte)percents[i])).ToArray();
    }

    public PartyOutbox ZoneEntered(ulong characterId, ushort zone)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            _zones[characterId] = zone;
            if (!_online.TryGetValue(characterId, out var member)) return outbox;
            member.Zone = zone;
            if (member.Party is not { } party) return outbox;
            Broadcast(outbox, party, PartyFrames.MemberZone, PartyFrames.ZoneFor(member.MemberId, zone), except: null);
            outbox.Order(new PartyOrder(member.CharacterId, SystemLinkFrames.OrderNotifyPartyPlayer, party.Id, MirrorFor(party, member)));
            if (party.Quest is { } quest && quest.Members.Find(m => m.CharacterId == member.CharacterId) is { } runner)
            {
                outbox.Order(new PartyOrder(member.CharacterId, SystemLinkFrames.OrderRecoverQuest, party.Id, SystemLinkFrames.EncodeQuestRecovery(new PartyQuestRecovery(
                    member.CharacterId, quest.PartyUid, quest.QuestId, quest.MonsterId, quest.TeamKills, quest.TargetCount,
                    quest.StartMs, quest.LimitMs, runner.CompletionCount, quest.Members.Select(m => (m.CharacterId, m.Kills)).ToArray()))));
                outbox.Order(TallyOrder(party, quest, member.CharacterId));
            }
            RecomputeShare(outbox, party);
        }
        return outbox;
    }

    public PartyOutbox ZoneLeft(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            _zones.Remove(characterId);
            if (!_online.TryGetValue(characterId, out var member)) return outbox;
            member.Zone = 0;
            if (member.Party is not { } party) return outbox;
            Broadcast(outbox, party, PartyFrames.MemberZoneCleared, PartyFrames.ZoneFor(member.MemberId, 0), except: null);
            RecomputeShare(outbox, party);
        }
        return outbox;
    }

    public bool BeginQuest(PartyQuestBegin begin)
    {
        ArgumentNullException.ThrowIfNull(begin);
        lock (_gate)
        {
            if (!_parties.TryGetValue(begin.PartyId, out var party)) return false;
            if (!party.Online.Exists(m => m.CharacterId == begin.CharacterId)) return false;
            party.Quest = new QuestRun(
                begin.PartyUid, begin.QuestId, begin.MonsterId, begin.TargetCount, begin.StartMs, begin.LimitMs,
                begin.Members.Take(SystemLinkFrames.MirrorSlots).Select(m => new QuestKills(m.CharacterId, m.CompletionCount)).ToList());
            return true;
        }
    }

    public PartyOutbox QuestKill(ulong killerId, ushort monsterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(killerId, out var killer) || killer.Party is not { Quest: { } quest } party) return outbox;
            if (quest.MonsterId != monsterId || quest.TeamKills >= quest.TargetCount) return outbox;
            if (quest.Members.Find(m => m.CharacterId == killerId) is not { } tally) return outbox;
            tally.Kills++;
            quest.TeamKills++;
            foreach (var member in party.Online) outbox.Order(TallyOrder(party, quest, member.CharacterId));
            if (quest.TeamKills >= quest.TargetCount) party.Quest = null;
        }
        return outbox;
    }

    public void CancelQuest(ulong characterId, uint questId)
    {
        lock (_gate)
        {
            if (_online.TryGetValue(characterId, out var member) && member.Party is { Quest: { } quest } party && quest.QuestId == questId)
            {
                party.Quest = null;
            }
        }
    }

    public PartyOutbox TearDownQuest(ulong characterId)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var master) || master.Party is not { Quest: { } quest } party) return outbox;
            if (party.MasterMemberId != master.MemberId) return outbox;
            foreach (var member in party.Online)
            {
                outbox.Order(new PartyOrder(member.CharacterId, SystemLinkFrames.OrderCancelQuest, party.Id,
                    SystemLinkFrames.EncodeQuestCancel(new PartyQuestCancel(member.CharacterId, quest.QuestId, 0, SystemLinkFrames.QuestCancelByMaster))));
            }
            party.Quest = null;
        }
        return outbox;
    }

    private static PartyOrder TallyOrder(Party party, QuestRun quest, ulong recipient) =>
        new(recipient, SystemLinkFrames.OrderUpdateQuest, party.Id, SystemLinkFrames.EncodeQuestTally(new PartyQuestTally(
            recipient, quest.QuestId, quest.TeamKills, quest.Members.Select(m => (m.CharacterId, m.Kills)).ToArray())));

    public PartyOutbox KindChanged(ulong characterId, byte kind, byte job)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var member)) return outbox;
            member.Kind = kind;
            if (member.Party is not { } party) return outbox;
            Broadcast(outbox, party, PartyFrames.MemberStateChanged, PartyFrames.MemberStateFor(member.MemberId, kind, job), except: null);
            RecomputeShare(outbox, party);
        }
        return outbox;
    }

    public PartyOutbox SetDead(ulong characterId, bool dead)
    {
        var outbox = new PartyOutbox();
        lock (_gate)
        {
            if (!_online.TryGetValue(characterId, out var member) || member.Dead == dead) return outbox;
            member.Dead = dead;
            if (member.Party is { } party) RecomputeShare(outbox, party);
        }
        return outbox;
    }

    private static void RecomputeTypeBonus(PartyOutbox outbox, Party party)
    {
        foreach (var owner in party.BonusOwners) outbox.Order(new PartyOrder(owner, SystemLinkFrames.OrderTypeBonus, 0, null));
        party.BonusOwners.Clear();
        if (MasterOf(party) is not { Zone: not 0 } master) return;
        uint? bonus = TypeOf(party) switch
        {
            PartyType.SameGender => PartyBonusBook.SameGenderBonus,
            PartyType.Type when ClassOf(master.Kind) is byte masterClass => PartyBonusBook.FirstClassBonus + masterClass,
            _ => null,
        };
        if (bonus is not uint value) return;
        var data = new byte[sizeof(uint)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        foreach (var member in party.Online.Where(m => m.Zone == master.Zone))
        {
            outbox.Order(new PartyOrder(member.CharacterId, SystemLinkFrames.OrderTypeBonus, party.Id, data));
            party.BonusOwners.Add(member.CharacterId);
        }
    }

    private static void RecomputeShare(PartyOutbox outbox, Party party)
    {
        RecomputeTypeBonus(outbox, party);
        foreach (var owner in party.ShareOwners) outbox.Order(new PartyOrder(owner, SystemLinkFrames.OrderShareExp, 0, null));
        party.ShareOwners.Clear();
        var type = TypeOf(party);
        var shareFlag = party.DivisionType == PartyDivision.Equal ? (byte)0 : (byte)1;
        foreach (var group in party.Online.Where(m => m.Zone != 0 && !m.Dead).GroupBy(m => m.Zone))
        {
            var members = group.Take(PartyBonusTables.MostGroupSize).ToArray();
            var qualifies = shareFlag == 1 && members.Length >= 2;
            var expBonus = qualifies ? PartyBonusTables.ExpMultiplier(type, members.Length) - 1f : 0f;
            var tmBonus = qualifies ? PartyBonusTables.TmMultiplier(type, members.Length) - 1f : 0f;
            var percentOf = party.Shares.ToDictionary(s => s.MemberId, s => (int)s.Percent);
            var sum = members.Sum(m => percentOf.GetValueOrDefault(m.MemberId));
            var entries = members.Select(m =>
            {
                var percent = PartyBonusTables.Percent(party.DivisionType, members.Length, percentOf.GetValueOrDefault(m.MemberId), sum);
                return new PartyShareEntry(expBonus, tmBonus, m.CharacterId, percent, percent);
            }).ToArray();
            var owner = members[0].CharacterId;
            outbox.Order(new PartyOrder(owner, SystemLinkFrames.OrderShareExp, party.Id,
                PartyBonusTables.EncodeShare(new PartyShare(shareFlag, party.ItemShare, entries))));
            party.ShareOwners.Add(owner);
            foreach (var member in members)
            {
                outbox.Add(new PartyMessage(member.CharacterId, PartyFrames.PartyBonus, PartyBonusTables.BonusFor(type, expBonus)));
            }
        }
    }

    private static void RefreshMirrors(PartyOutbox outbox, Party party)
    {
        foreach (var member in party.Online)
        {
            outbox.Order(new PartyOrder(member.CharacterId, SystemLinkFrames.OrderNotifyPartyPlayer, party.Id, MirrorFor(party, member)));
        }
    }

    private static byte[] MirrorFor(Party party, Member recipient)
    {
        var master = party.Online.Concat(party.Offline).FirstOrDefault(m => m.MemberId == party.MasterMemberId);
        var others = party.Online.Where(m => !ReferenceEquals(m, recipient))
            .Select(m => new PartyMirrorEntry(m.CharacterId, m.MemberId))
            .Take(SystemLinkFrames.MirrorSlots)
            .ToArray();
        return SystemLinkFrames.EncodeMirror(new PartyMirror(others, recipient.CharacterId, master?.CharacterId ?? 0, party.Id));
    }

    private void RelayJoinAsk(PartyOutbox outbox, ulong requesterId, Party party, string askerName, ushort x, uint y)
    {
        if (party.IsFull)
        {
            outbox.Add(new(requesterId, PartyFrames.AskResult, PartyFrames.Code32(PartyCodes.AskPartyFull)));
            return;
        }
        if (FindByName(askerName) is not { } asker)
        {
            outbox.Add(new(requesterId, PartyFrames.AskResult, PartyFrames.Code32(PartyCodes.AskNoPending)));
            return;
        }
        if (party.PendingAsker != 0)
        {
            outbox.Add(new(requesterId, PartyFrames.AskResult, PartyFrames.Code32(PartyCodes.AskPending)));
            return;
        }
        if (MasterOf(party) is not { } master)
        {
            outbox.Add(new(requesterId, PartyFrames.AskResult, PartyFrames.Code32(PartyCodes.AskPartyMissing)));
            return;
        }
        party.PendingAsker = asker.CharacterId;
        outbox.Add(new(master.CharacterId, PartyFrames.JoinAskRelayed, PartyFrames.JoinAskRelayFor(askerName, x, y, _text)));
    }

    private void Join(PartyOutbox outbox, Party party, Member joiner)
    {
        Broadcast(outbox, party, PartyFrames.MemberJoined, PartyFrames.MemberJoinedFor(InfoOf(joiner), party.LevelFlag, _text), except: null);
        party.Online.Add(joiner);
        joiner.Party = party;
        outbox.Add(new(joiner.CharacterId, PartyFrames.JoinState, PartyFrames.JoinStateFor(
            party.Id, party.Name, party.MasterMemberId, joiner.MemberId, party.DivisionType, party.LevelFlag,
            party.ItemShare, party.SecondOption,
            party.Online.Select(InfoOf).ToArray(), party.Offline.Select(InfoOf).ToArray(), _text)));
        RefreshMirrors(outbox, party);
        Recompute(outbox, party);
        RecomputeShare(outbox, party);
    }

    private static void Recompute(PartyOutbox outbox, Party party)
    {
        var type = TypeOf(party);
        var division = party.DivisionType;
        if (division == PartyDivision.Custom) division = PartyDivision.ByLevel;
        if (type == PartyType.Beginner) division = PartyDivision.ByLevel;
        if (type == PartyType.Fixed) division = PartyDivision.Equal;
        party.DivisionType = division;
        party.Shares = division == PartyDivision.ByLevel ? LevelShares(party) : new List<PartyDivisionShare>();
        Broadcast(outbox, party, PartyFrames.Division, PartyFrames.DivisionFor(party.DivisionType, party.Shares), except: null);
    }

    private void LeaveParty(PartyOutbox outbox, Member member)
    {
        var party = member.Party!;
        party.Online.Remove(member);
        member.Party = null;
        if (party.MasterMemberId == member.MemberId && party.Online.Count > 0)
        {
            party.MasterMemberId = party.Online[0].MemberId;
        }
        Broadcast(outbox, party, PartyFrames.MemberLeft, PartyFrames.MemberPair(member.MemberId, party.MasterMemberId), except: null);
        outbox.Order(new PartyOrder(member.CharacterId, SystemLinkFrames.OrderNotifyPartyPlayer, 0, null));
        if (party.Size < 2) Break(outbox, party);
        else
        {
            RefreshMirrors(outbox, party);
            RecomputeShare(outbox, party);
        }
        outbox.Add(new(member.CharacterId, PartyFrames.LeaveDone, PartyFrames.Empty()));
    }

    private void Break(PartyOutbox outbox, Party party)
    {
        Broadcast(outbox, party, PartyFrames.Broken, PartyFrames.Empty(), except: null);
        foreach (var owner in party.ShareOwners) outbox.Order(new PartyOrder(owner, SystemLinkFrames.OrderShareExp, 0, null));
        party.ShareOwners.Clear();
        foreach (var owner in party.BonusOwners) outbox.Order(new PartyOrder(owner, SystemLinkFrames.OrderTypeBonus, 0, null));
        party.BonusOwners.Clear();
        foreach (var member in party.Online)
        {
            member.Party = null;
            outbox.Order(new PartyOrder(member.CharacterId, SystemLinkFrames.OrderNotifyPartyPlayer, 0, null));
        }
        foreach (var member in party.Offline) _depot.Remove(member.CharacterId);
        if (party.PendingInviteTarget != 0 && _online.TryGetValue(party.PendingInviteTarget, out var target)) target.InvitedBy = null;
        party.Online.Clear();
        party.Offline.Clear();
        _parties.Remove(party.Id);
    }

    private void ForgetIfAbandoned(Party party)
    {
        if (party.Online.Count > 0) return;
        if (party.Offline.Any(m => _depot.TryGetValue(m.CharacterId, out var e) && e.PartyId == party.Id)) return;
        foreach (var member in party.Offline) _depot.Remove(member.CharacterId);
        _parties.Remove(party.Id);
    }

    private static void NotifyIfFull(PartyOutbox outbox, Party party)
    {
        if (party.IsFull && MasterOf(party) is { } master) outbox.Add(MasterResultTo(master, PartyCodes.PartyBecameFull));
    }

    private static void SetInvite(Party party, Member inviter, Member target)
    {
        target.InvitedBy = new PendingInvite(party.Id, inviter.MemberId);
        party.PendingInviteTarget = target.CharacterId;
    }

    private bool HasPendingInvite(Member member)
    {
        if (member.InvitedBy is not { } invite) return false;
        if (_parties.TryGetValue(invite.PartyId, out var party)
            && party.PendingInviteTarget == member.CharacterId
            && party.Online.Exists(m => m.MemberId == invite.MemberId)) return true;
        member.InvitedBy = null;
        return false;
    }

    private void ClearInviteState(Member member)
    {
        if (member.InvitedBy is { } invite && _parties.TryGetValue(invite.PartyId, out var party)
            && party.PendingInviteTarget == member.CharacterId)
        {
            party.PendingInviteTarget = 0;
        }
        member.InvitedBy = null;
    }

    private PartyMessage InviteFailedTo(Member to, string name, byte code) =>
        new(to.CharacterId, PartyFrames.InviteFailed, PartyFrames.NameAndCode(name, code, _text));

    private static PartyMessage MasterResultTo(Member master, uint code) =>
        new(master.CharacterId, PartyFrames.MasterResult, PartyFrames.Code32(code));

    private static void Broadcast(PartyOutbox outbox, Party party, ushort opcode, byte[] payload, Member? except)
    {
        foreach (var member in party.Online)
        {
            if (!ReferenceEquals(member, except)) outbox.Add(new(member.CharacterId, opcode, payload));
        }
    }

    private Member? FindByName(string name) =>
        _online.Values.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal));

    private static Member? MasterOf(Party party) => party.Online.Find(m => m.MemberId == party.MasterMemberId);

    private static ushort AverageLevel(Party party)
    {
        var members = party.Online.Count > 0 ? party.Online : party.Offline;
        return members.Count == 0 ? (ushort)0 : (ushort)(members.Sum(m => (int)m.Level) / members.Count);
    }

    private static PartyType TypeOf(Party party) => TypeOf(party.Online.Select(m => (m.Kind, m.Level)).ToArray());

    private static List<PartyDivisionShare> LevelShares(Party party) =>
        LevelShares(party.Online.Select(m => (m.MemberId, m.Level)).ToArray()).ToList();

    private static PartyMemberInfo InfoOf(Member member) =>
        new(member.Name, member.Kind, member.Zone, member.MemberId, member.Level, member.CharacterId);

    private ushort NextPartyId()
    {
        do { _lastPartyId = (ushort)(_lastPartyId == ushort.MaxValue ? 1 : _lastPartyId + 1); }
        while (_parties.ContainsKey(_lastPartyId));
        return _lastPartyId;
    }

    private ushort NextMemberId()
    {
        do { _lastMemberId = (ushort)(_lastMemberId == ushort.MaxValue ? 1 : _lastMemberId + 1); }
        while (_online.Values.Any(m => m.MemberId == _lastMemberId) || _parties.Values.Any(p => p.Offline.Exists(m => m.MemberId == _lastMemberId)));
        return _lastMemberId;
    }

    private sealed record PendingInvite(ushort PartyId, ushort MemberId);

    private sealed class QuestKills
    {
        public QuestKills(ulong characterId, byte completionCount)
        {
            CharacterId = characterId;
            CompletionCount = completionCount;
        }

        public ulong CharacterId { get; }
        public byte CompletionCount { get; }
        public ushort Kills { get; set; }
    }

    private sealed class QuestRun
    {
        public QuestRun(ulong partyUid, uint questId, ushort monsterId, ushort targetCount, uint startMs, uint limitMs, List<QuestKills> members)
        {
            PartyUid = partyUid;
            QuestId = questId;
            MonsterId = monsterId;
            TargetCount = targetCount;
            StartMs = startMs;
            LimitMs = limitMs;
            Members = members;
        }

        public ulong PartyUid { get; }
        public uint QuestId { get; }
        public ushort MonsterId { get; }
        public ushort TargetCount { get; }
        public uint StartMs { get; }
        public uint LimitMs { get; }
        public List<QuestKills> Members { get; }
        public ushort TeamKills { get; set; }
    }

    private sealed record DepotEntry(ushort PartyId, DateTimeOffset ExpiresAt);

    private sealed class Member
    {
        public Member(ulong characterId, string name, ushort kind, ushort level)
        {
            CharacterId = characterId;
            Name = name;
            Kind = kind;
            Level = level;
        }

        public ulong CharacterId { get; }
        public string Name { get; }
        public ushort Kind { get; set; }
        public ushort Level { get; set; }
        public ushort Zone { get; set; }
        public ushort MemberId { get; set; }
        public bool Dead { get; set; }
        public Party? Party { get; set; }
        public PendingInvite? InvitedBy { get; set; }
    }

    private sealed class Party
    {
        public Party(ushort id, string name, ushort masterMemberId)
        {
            Id = id;
            Name = name;
            MasterMemberId = masterMemberId;
        }

        public ushort Id { get; }
        public string Name { get; set; }
        public ushort MasterMemberId { get; set; }
        public List<Member> Online { get; } = new();
        public List<Member> Offline { get; } = new();
        public byte DivisionType { get; set; }
        public List<PartyDivisionShare> Shares { get; set; } = new();
        public byte ItemShare { get; set; }
        public byte SecondOption { get; set; }
        public bool LevelFlag { get; set; }
        public ulong PendingAsker { get; set; }
        public ulong PendingInviteTarget { get; set; }
        public QuestRun? Quest { get; set; }
        public List<ulong> ShareOwners { get; } = new();
        public List<ulong> BonusOwners { get; } = new();
        public int Size => Online.Count + Offline.Count;
        public bool IsFull => Size >= PartyRules.MostMembers;
    }
}
