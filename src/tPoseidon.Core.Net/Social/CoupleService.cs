using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Wedding;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.Social;

public sealed record CoupleMessage(ulong To, ushort Opcode, byte[] Payload);

public sealed class CoupleService
{
    public static IReadOnlySet<byte> BreakUpStates { get; } = new HashSet<byte>
    {
        CoupleStates.Joined, CoupleStates.CloseEnough, CoupleStates.OptionsChosen, CoupleStates.Divorced,
    };

    private readonly object _gate = new();
    private readonly HashSet<ulong> _open = new();
    private readonly HashSet<ulong> _articleWrites = new();
    private readonly ICoupleRulesStore _store;
    private readonly IReadOnlyList<int> _deleteQuests;
    private readonly IReadOnlyList<int> _deleteSkills;
    private readonly Encoding _text;
    private readonly TimeProvider _time;
    private readonly IReadOnlyDictionary<int, CoupleEventPoints> _eventPoints;

    public CoupleService(
        ICoupleRulesStore store, IReadOnlyList<int> deleteQuests, IReadOnlyList<int> deleteSkills, IReadOnlyDictionary<int, CoupleEventPoints> eventPoints,
        Encoding text, TimeProvider time)
    {
        _eventPoints = eventPoints ?? throw new ArgumentNullException(nameof(eventPoints));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _deleteQuests = deleteQuests ?? throw new ArgumentNullException(nameof(deleteQuests));
        _deleteSkills = deleteSkills ?? throw new ArgumentNullException(nameof(deleteSkills));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public bool IsOpen(ulong characterId)
    {
        lock (_gate) return _open.Contains(characterId);
    }

    public IReadOnlyList<CoupleMessage> OpenSession(ulong characterId)
    {
        lock (_gate)
        {
            if (!_open.Add(characterId)) return Array.Empty<CoupleMessage>();
        }
        if (_store.LoadForCharacter(characterId) is not { } couple) return Array.Empty<CoupleMessage>();
        var info = InfoFor(couple, characterId, loginPush: true);
        if (couple.Partner(characterId) is { } partner && IsOpen(partner.CharacterId))
        {
            Event(characterId, CoupleCloseness.BothOnline);
            if (couple.State == CoupleStates.Joined && CoupleCloseness.IsAnniversary(couple.BeginCoupleDate.ToLocalTime(), _time.GetLocalNow()))
            {
                Event(characterId, CoupleCloseness.Anniversary);
            }
        }
        return new[] { info };
    }

    public string? Event(ulong characterId, int eventBit)
    {
        if (_store.LoadForCharacter(characterId) is not { } couple) return null;
        var now = _time.GetLocalNow();
        var mask = couple.Members.Aggregate(0L, (all, member) => all | member.EventBitmask);
        var today = CoupleCloseness.MaskToday(mask, couple.RecentUpdate.ToLocalTime(), now);
        if (CoupleCloseness.Apply(couple.State, couple.RelationPoint, couple.LovePoint, couple.FriendPoint, today, eventBit, _eventPoints) is not { } result) return null;
        if (!_store.UpdateEventPoints(couple.CoupleCode, characterId, result.Relation, result.Love, result.Friend, result.EventMask, result.State, now)) return null;
        return $"couple {couple.CoupleCode} event 0x{eventBit:X}: relation {result.Relation}, love {result.Love}, friend {result.Friend}, state {result.State}";
    }

    public string? Memo(ulong senderId, ulong targetId)
    {
        if (_store.LoadForCharacter(senderId)?.Partner(senderId) is not { } partner) return null;
        return Event(senderId, partner.CharacterId == targetId ? CoupleCloseness.MemoToLover : CoupleCloseness.MemoToOthers);
    }

    public string? Whisper(ulong senderId, ulong targetId)
    {
        if (_store.LoadForCharacter(senderId)?.Partner(senderId) is not { } partner || partner.CharacterId != targetId) return null;
        return Event(senderId, CoupleCloseness.WhisperToLover);
    }

    public string? GiftAccepted(ulong characterId, string senderName)
    {
        ArgumentNullException.ThrowIfNull(senderName);
        if (_store.LoadForCharacter(characterId)?.Partner(characterId) is not { } partner || !string.Equals(partner.Name, senderName, StringComparison.Ordinal)) return null;
        return Event(characterId, CoupleCloseness.GiftFromLover);
    }

    public IReadOnlyList<CoupleMessage> HallList(ulong characterId, IEnumerable<WeddingHallListing> halls)
    {
        ArgumentNullException.ThrowIfNull(halls);
        var found = new List<(WeddingHallListing Hall, CoupleRecord Couple)>();
        foreach (var hall in halls)
        {
            if (_store.Load(hall.Wdid) is { } couple) found.Add((hall, couple));
        }
        return new[] { new CoupleMessage(characterId, CoupleSocialFrames.HallList, CoupleSocialFrames.Halls(found, _text)) };
    }

    public IReadOnlyList<CoupleMessage>? ArticleList(ulong characterId, Func<IReadOnlyList<WeddingArticle>> articles)
    {
        ArgumentNullException.ThrowIfNull(articles);
        if (!IsOpen(characterId)) return null;
        return new[] { new CoupleMessage(characterId, CoupleSocialFrames.ArticleList, CoupleSocialFrames.Articles(articles(), _text)) };
    }

    public (IReadOnlyList<CoupleMessage> Messages, string Outcome) WriteArticle(
        ulong characterId, ReadOnlySpan<byte> payload, IWeddingBoardStore board, bool modify)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (!IsOpen(characterId)) return (Array.Empty<CoupleMessage>(), "no couple session");
        if (_store.LoadForCharacter(characterId) is not { } couple)
        {
            return (new[] { Refusal(characterId, WeddingHallError.CoupleMissing) }, "no couple");
        }
        if (modify && !board.HasArticle(couple.CoupleCode))
        {
            return (new[] { Refusal(characterId, WeddingHallError.ArticleMissing) }, $"couple {couple.CoupleCode} has no article");
        }
        if (couple.State != CoupleStates.OptionsChosen)
        {
            return (new[] { Refusal(characterId, WeddingHallError.InvalidState) }, $"state {couple.State} cannot post an article");
        }
        lock (_gate)
        {
            if (!_articleWrites.Add(couple.CoupleCode))
            {
                return (new[] { Refusal(characterId, WeddingHallError.ArticlePending) }, $"couple {couple.CoupleCode} has a write pending");
            }
        }
        try
        {
            if (CoupleSocialFrames.ReadArticle(payload, _text, out var textRefused) is not { } request)
            {
                return textRefused
                    ? (new[] { Refusal(characterId, WeddingHallError.ArticleTextRefused) }, "article text missing or too long")
                    : (Array.Empty<CoupleMessage>(), $"{payload.Length} B is shorter than an article");
            }
            var male = couple.Members.FirstOrDefault(member => !member.Female);
            var female = couple.Members.FirstOrDefault(member => member.Female);
            var article = new WeddingArticle(
                couple.CoupleCode, request.CardType, couple.DressItemIndex, request.MaleHair, request.FemaleHair,
                (byte)(male?.Job ?? 0), (byte)(female?.Job ?? 0), request.CardMessage, request.Title, male?.Name ?? string.Empty, female?.Name ?? string.Empty);
            return board.SaveArticle(article, _time.GetUtcNow()) switch
            {
                WeddingArticleSave.Created => (new[] { new CoupleMessage(characterId, CoupleSocialFrames.ArticleCreated, CoupleSocialFrames.Article(article, _text)) },
                    $"article of couple {couple.CoupleCode} created"),
                WeddingArticleSave.Updated => (new[] { new CoupleMessage(characterId, CoupleSocialFrames.ArticleUpdated, CoupleSocialFrames.Article(article, _text)) },
                    $"article of couple {couple.CoupleCode} updated"),
                _ => (Array.Empty<CoupleMessage>(), $"store refused the article of couple {couple.CoupleCode}"),
            };
        }
        finally
        {
            lock (_gate) _articleWrites.Remove(couple.CoupleCode);
        }
    }

    public (IReadOnlyList<CoupleMessage> Messages, string Outcome) DeleteArticle(ulong characterId, IWeddingBoardStore board)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (!IsOpen(characterId)) return (Array.Empty<CoupleMessage>(), "no couple session");
        if (_store.LoadForCharacter(characterId) is not { } couple)
        {
            return (new[] { Refusal(characterId, WeddingHallError.CoupleMissing) }, "no couple");
        }
        if (!board.HasArticle(couple.CoupleCode))
        {
            return (new[] { Refusal(characterId, WeddingHallError.ArticleMissing) }, $"couple {couple.CoupleCode} has no article");
        }
        if (!board.DeleteArticle(couple.CoupleCode)) return (Array.Empty<CoupleMessage>(), $"store kept the article of couple {couple.CoupleCode}");
        return (new[] { new CoupleMessage(characterId, CoupleSocialFrames.ArticleRemoved, CoupleSocialFrames.Removed(couple.CoupleCode)) },
            $"article of couple {couple.CoupleCode} deleted");
    }

    public IReadOnlyList<string> PartyChanged(IReadOnlyList<ulong> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var notes = new List<string>();
        foreach (var member in members)
        {
            if (_store.LoadForCharacter(member)?.Partner(member) is not { } partner) continue;
            var note = Event(member, CoupleCloseness.PartyEvent(members.Contains(partner.CharacterId), members.Count));
            if (note is not null) notes.Add(note);
        }
        return notes;
    }

    public void Close(ulong characterId)
    {
        lock (_gate) _open.Remove(characterId);
    }

    public IReadOnlyList<CoupleMessage> LoverPosition(ulong characterId, Func<ulong, ushort?> zoneOf)
    {
        ArgumentNullException.ThrowIfNull(zoneOf);
        if (!IsOpen(characterId)) return Array.Empty<CoupleMessage>();
        var lover = _store.LoadForCharacter(characterId)?.Partner(characterId);
        if (lover is null || !IsOpen(lover.CharacterId) || zoneOf(lover.CharacterId) is not { } zone)
        {
            return new[] { new CoupleMessage(characterId, CoupleSocialFrames.LoverOffline, Array.Empty<byte>()) };
        }
        Event(characterId, CoupleCloseness.LoverPosition);
        return new[] { new CoupleMessage(characterId, CoupleSocialFrames.LoverPositionReply, CoupleSocialFrames.LoverZone(zone)) };
    }

    public WeddingOutbox MoveToLover(ulong characterId, ushort zone, Func<ushort, int?> zoneType)
    {
        ArgumentNullException.ThrowIfNull(zoneType);
        var outbox = new WeddingOutbox();
        if (!IsOpen(characterId) || _store.LoadForCharacter(characterId) is not { State: CoupleStates.Married } couple)
        {
            outbox.Notes.Add("no couple session or not married");
            return outbox;
        }
        if (couple.Partner(characterId) is not { } lover || !IsOpen(lover.CharacterId))
        {
            outbox.Messages.Add(new CoupleMessage(characterId, CoupleSocialFrames.LoverOffline, Array.Empty<byte>()));
            outbox.Notes.Add("lover not online");
            return outbox;
        }
        if (zoneType(zone) == (int)CoupleSocialFrames.MoveToLoverBarredZoneType)
        {
            outbox.Messages.Add(new CoupleMessage(characterId, CoupleSocialFrames.LoverOffline, CoupleSocialFrames.MoveRefused(CoupleSocialFrames.MoveToLoverBarredZoneType)));
            outbox.Notes.Add($"zone {zone} is of type {CoupleSocialFrames.MoveToLoverBarredZoneType}");
            return outbox;
        }
        outbox.Orders.Add(new WeddingOrder(lover.CharacterId, null, CoupleSocialFrames.OrderLoverPosition, CoupleSocialFrames.LoverPositionOrder(lover.CharacterId, characterId)));
        outbox.Notes.Add($"asks the server of 0x{lover.CharacterId:X} for the lover position");
        return outbox;
    }

    public (IReadOnlyList<CoupleMessage> Messages, string Outcome) BreakUp(ulong characterId)
    {
        if (!IsOpen(characterId)) return (Array.Empty<CoupleMessage>(), "no couple session");
        if (_store.LoadForCharacter(characterId) is not { } couple) return (Array.Empty<CoupleMessage>(), "no couple");
        if (!BreakUpStates.Contains(couple.State)) return (Array.Empty<CoupleMessage>(), $"state {couple.State} cannot break up");
        var deleteState = couple.State == CoupleStates.Divorced ? CoupleStates.DivorceDeleted : CoupleStates.CoupleDeleted;
        if (!_store.Delete(couple.CoupleCode, deleteState, _deleteQuests, _deleteSkills)) return (Array.Empty<CoupleMessage>(), "store refused the delete");
        var messages = couple.Members
            .Where(member => IsOpen(member.CharacterId))
            .Select(member => new CoupleMessage(member.CharacterId, CoupleSocialFrames.BrokenUp, Array.Empty<byte>()))
            .ToList();
        return (messages, $"couple {couple.CoupleCode} deleted with state {deleteState}");
    }

    public (IReadOnlyList<CoupleMessage> Messages, string Outcome) RequestDivorce(ulong characterId)
    {
        if (!IsOpen(characterId)) return (Array.Empty<CoupleMessage>(), "no couple session");
        if (_store.LoadForCharacter(characterId) is not { } couple || couple.Partner(characterId) is not { } partner)
        {
            return (Array.Empty<CoupleMessage>(), "no couple");
        }
        if (couple.State != CoupleStates.Married) return (Array.Empty<CoupleMessage>(), $"state {couple.State} cannot request a divorce");
        if (!_store.RequestDivorce(couple.CoupleCode, characterId, partner.CharacterId, _time.GetUtcNow())) return (Array.Empty<CoupleMessage>(), "store refused the request");
        var messages = new List<CoupleMessage>();
        if (IsOpen(partner.CharacterId))
        {
            messages.Add(new CoupleMessage(partner.CharacterId, CoupleSocialFrames.DivorceRequested, CoupleSocialFrames.Character(characterId)));
        }
        messages.AddRange(Refreshed(couple.CoupleCode));
        return (messages, $"couple {couple.CoupleCode} divorce requested");
    }

    public (IReadOnlyList<CoupleMessage> Messages, string Outcome) AnswerDivorce(ulong characterId, bool accept)
    {
        if (!IsOpen(characterId)) return (Array.Empty<CoupleMessage>(), "no couple session");
        if (_store.LoadForCharacter(characterId) is not { } couple) return (Array.Empty<CoupleMessage>(), "no couple");
        if (couple.State != CoupleStates.DivorceRequested) return (Array.Empty<CoupleMessage>(), $"state {couple.State} has no divorce request");
        var stored = accept ? _store.ConfirmDivorce(couple.CoupleCode, _time.GetUtcNow()) : _store.CancelDivorce(couple.CoupleCode);
        if (!stored) return (Array.Empty<CoupleMessage>(), "store refused the answer");
        var messages = new List<CoupleMessage>();
        if (accept)
        {
            messages.AddRange(couple.Members
                .Where(member => IsOpen(member.CharacterId))
                .Select(member => new CoupleMessage(member.CharacterId, CoupleSocialFrames.DivorceDone, Array.Empty<byte>())));
        }
        messages.AddRange(Refreshed(couple.CoupleCode));
        return (messages, $"couple {couple.CoupleCode} divorce {(accept ? "confirmed" : "cancelled")}");
    }

    public (IReadOnlyList<CoupleMessage> Messages, string Outcome) MakeCouple(ulong maleId, ulong femaleId)
    {
        if (!IsOpen(maleId) || !IsOpen(femaleId)) return (Array.Empty<CoupleMessage>(), "a partner has no couple session");
        var registration = _store.Register(maleId, femaleId, _time.GetUtcNow());
        if (!registration.Succeeded) return (Array.Empty<CoupleMessage>(), $"store error {(int)registration.Error}");
        return (Refreshed(registration.CoupleCode), $"couple {registration.CoupleCode} made");
    }

    public IReadOnlyList<CoupleMessage> Refresh(ulong characterId) =>
        _store.LoadForCharacter(characterId) is { } couple ? Refreshed(couple.CoupleCode) : Array.Empty<CoupleMessage>();

    private List<CoupleMessage> Refreshed(ulong coupleCode)
    {
        var messages = new List<CoupleMessage>();
        if (_store.Load(coupleCode) is not { } couple) return messages;
        foreach (var member in couple.Members.Where(member => IsOpen(member.CharacterId)))
        {
            messages.Add(InfoFor(couple, member.CharacterId, loginPush: false));
        }
        return messages;
    }

    private static CoupleMessage Refusal(ulong characterId, uint code) =>
        new(characterId, WeddingHallFrames.Error, WeddingHallFrames.Code(code));

    private CoupleMessage InfoFor(CoupleRecord couple, ulong characterId, bool loginPush) =>
        new(characterId, CoupleSocialFrames.CoupleInfo, CoupleSocialFrames.Info(couple, characterId, loginPush, _text));
}
