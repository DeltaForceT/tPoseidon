using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed class PartyBoard
{
    private readonly Dictionary<ushort, PartyPost> _parties = new();
    private readonly Dictionary<ulong, SeekerPost> _seekers = new();
    private readonly Encoding _text;
    private long _sequence;

    public PartyBoard(Encoding text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public int PartyCount => _parties.Count;

    public int SeekerCount => _seekers.Count;

    public PartyPost? Party(ushort partyId) => _parties.GetValueOrDefault(partyId);

    public SeekerPost? Seeker(ulong characterId) => _seekers.GetValueOrDefault(characterId);

    public PartyPost AddParty(PartyPostFields fields, string comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        var post = new PartyPost(fields, comment, ++_sequence);
        _parties[post.PartyId] = post;
        return post;
    }

    public bool RemoveParty(ushort partyId) => _parties.Remove(partyId);

    public SeekerPost AddSeeker(ulong characterId, uint type, ushort level, string name, string comment)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(comment);
        var post = new SeekerPost(characterId, type, level, name, comment, ++_sequence);
        _seekers[characterId] = post;
        return post;
    }

    public bool RemoveSeeker(ulong characterId) => _seekers.Remove(characterId);

    public byte[]? ListParties(PartyBoardList request)
    {
        if (request.NearLevel)
        {
            var filtered = IsPartySort(request.Sort)
                ? Filtered(PartyIndex(request.Sort), post => Near(post.AverageLevel, request.Level))
                : new List<PartyPost>();
            return PartyPage(FilteredOrder(filtered, request.HighToLow), request.Page);
        }
        return IsPartySort(request.Sort)
            ? PartyPage(PartyIndex(request.Sort, request.HighToLow), request.Page)
            : null;
    }

    public byte[]? SearchParties(PartyBoardSearch request)
    {
        switch (request.Mode)
        {
            case PartyBoardFrames.SearchByName:
                var named = _parties.Values.FirstOrDefault(post => string.Equals(post.MasterName, request.Text, StringComparison.Ordinal));
                return PartyBoardFrames.PageOf(named is null ? Array.Empty<byte[]>() : new[] { PartyRecord(named) });
            case PartyBoardFrames.SearchLevelAtLeast:
            case PartyBoardFrames.SearchLevelAtMost:
                if (!IsPartySort(request.Sort))
                {
                    return PartyPage(Array.Empty<PartyPost>(), request.Page);
                }
                var level = PartyBoardFrames.ParseNumber(request.Text);
                var filtered = Filtered(PartyIndex(request.Sort), post => LevelRange(request.Mode, post.AverageLevel, level));
                return PartyPage(FilteredOrder(filtered, request.HighToLow), request.Page);
            default:
                return null;
        }
    }

    public byte[]? ListSeekers(PartyBoardList request) =>
        IsSeekerSort(request.Sort)
            ? SeekerPage(SeekerIndex(request.Sort, request.HighToLow), request.Page)
            : null;

    public byte[]? SearchSeekers(PartyBoardSearch request)
    {
        switch (request.Mode)
        {
            case PartyBoardFrames.SearchByName:
                var named = _seekers.Values.FirstOrDefault(post => string.Equals(post.Name, request.Text, StringComparison.Ordinal));
                return PartyBoardFrames.PageOf(named is null ? Array.Empty<byte[]>() : new[] { SeekerRecord(named) });
            case PartyBoardFrames.SearchLevelAtLeast:
            case PartyBoardFrames.SearchLevelAtMost:
                if (!IsSeekerSort(request.Sort))
                {
                    return SeekerPage(Array.Empty<SeekerPost>(), request.Page);
                }
                var level = PartyBoardFrames.ParseNumber(request.Text);
                var filtered = Filtered(SeekerIndex(request.Sort), post => LevelRange(request.Mode, post.Level, level));
                return SeekerPage(FilteredOrder(filtered, request.HighToLow), request.Page);
            default:
                return null;
        }
    }

    private static bool IsPartySort(byte sort) =>
        sort is PartyBoardFrames.SortDefault or PartyBoardFrames.PartySortByType or PartyBoardFrames.PartySortByLevel;

    private static bool IsSeekerSort(byte sort) =>
        sort is PartyBoardFrames.SortDefault or PartyBoardFrames.SeekerSortDefaultAlias or PartyBoardFrames.SeekerSortByLevel;

    private static bool Near(int postLevel, int level) => level >= postLevel - PartyBoardFrames.LevelWindow && level <= postLevel + PartyBoardFrames.LevelWindow;

    private static bool LevelRange(byte mode, int postLevel, int level) => mode == PartyBoardFrames.SearchLevelAtLeast
        ? postLevel >= level && postLevel <= level + PartyBoardFrames.LevelWindow
        : postLevel <= level && postLevel >= level - PartyBoardFrames.LevelWindow;

    private static List<T> Filtered<T>(IEnumerable<T> index, Func<T, bool> keep) =>
        index.Where(keep).Take(PartyBoardFrames.FilteredCapacity).ToList();

    private static List<T> FilteredOrder<T>(List<T> filtered, bool highToLow)
    {
        if (!highToLow) filtered.Reverse();
        return filtered;
    }

    private List<PartyPost> PartyIndex(byte sort, bool highToLow = true) => sort switch
    {
        PartyBoardFrames.PartySortByType => Ordered(_parties.Values, post => post.SortType, post => post.Sequence, highToLow),
        PartyBoardFrames.PartySortByLevel => Ordered(_parties.Values, post => post.SortLevel, post => post.Sequence, highToLow),
        _ => _parties.Values.OrderByDescending(post => post.Sequence).ToList(),
    };

    private List<SeekerPost> SeekerIndex(byte sort, bool highToLow = true) => sort == PartyBoardFrames.SeekerSortByLevel
        ? Ordered(_seekers.Values, post => post.SortLevel, post => post.Sequence, highToLow)
        : _seekers.Values.OrderByDescending(post => post.Sequence).ToList();

    private static List<T> Ordered<T>(IEnumerable<T> source, Func<T, long> key, Func<T, long> sequence, bool highToLow) => highToLow
        ? source.OrderByDescending(key).ThenBy(sequence).ToList()
        : source.OrderBy(key).ThenByDescending(sequence).ToList();

    private byte[] PartyPage(IReadOnlyList<PartyPost> ordered, ushort page) =>
        PartyBoardFrames.PageOf(Page(ordered, page).Select(PartyRecord).ToList());

    private byte[] SeekerPage(IReadOnlyList<SeekerPost> ordered, ushort page) =>
        PartyBoardFrames.PageOf(Page(ordered, page).Select(SeekerRecord).ToList());

    private static IEnumerable<T> Page<T>(IReadOnlyList<T> ordered, ushort page) =>
        ordered.Skip(Math.Min(page * PartyBoardFrames.PageSize, ordered.Count)).Take(PartyBoardFrames.PageSize);

    private byte[] PartyRecord(PartyPost post) => PartyBoardFrames.PartyRecord(
        post.PartyId, post.MasterType, post.PartyType, post.AverageLevel, post.MasterName, post.Comment, _text);

    private byte[] SeekerRecord(SeekerPost post) => PartyBoardFrames.SeekerRecord(
        post.CharacterId, post.Type, post.Level, post.Name, post.Comment, _text);

    public readonly record struct PartyPostFields(
        ushort PartyId, uint MasterType, string MasterName, uint PartyType, byte MemberCount, ushort AverageLevel, byte Division, byte ItemShare);

    public sealed class PartyPost
    {
        public PartyPost(PartyPostFields fields, string comment, long sequence)
        {
            PartyId = fields.PartyId;
            Comment = comment;
            SortType = fields.PartyType;
            SortLevel = fields.AverageLevel;
            Sequence = sequence;
            Refresh(fields);
        }

        public ushort PartyId { get; }

        public string Comment { get; }

        public uint MasterType { get; set; }

        public string MasterName { get; set; } = string.Empty;

        public uint PartyType { get; set; }

        public ushort AverageLevel { get; set; }

        public byte MemberCount { get; set; }

        public byte Division { get; set; }

        public byte ItemShare { get; set; }

        public ulong NoticeOwner { get; set; }

        public long SortType { get; }

        public long SortLevel { get; }

        public long Sequence { get; }

        public void Refresh(PartyPostFields fields)
        {
            MasterType = fields.MasterType;
            MasterName = fields.MasterName;
            PartyType = fields.PartyType;
            MemberCount = fields.MemberCount;
            AverageLevel = fields.AverageLevel;
            Division = fields.Division;
            ItemShare = fields.ItemShare;
        }
    }

    public sealed class SeekerPost
    {
        public SeekerPost(ulong characterId, uint type, ushort level, string name, string comment, long sequence)
        {
            CharacterId = characterId;
            Type = type;
            Level = level;
            Name = name;
            Comment = comment;
            SortLevel = level;
            Sequence = sequence;
        }

        public ulong CharacterId { get; }

        public uint Type { get; }

        public ushort Level { get; }

        public string Name { get; }

        public string Comment { get; }

        public long SortLevel { get; }

        public long Sequence { get; }
    }
}
