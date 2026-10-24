using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;
using tPoseidon.Kr2014.GameServer;

internal sealed class HonorTitleController
{
    public const byte ListTrailingByte = 0;

    private readonly MasterHonorTitles _master;
    private readonly Func<HonorTitleBook> _book;
    private readonly Func<InGameSessionState, string> _partnerName;
    private readonly Func<InGameSessionState, Opcode, byte[], Task> _sendToArea;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public HonorTitleController(
        MasterHonorTitles master,
        Func<HonorTitleBook> book,
        Func<InGameSessionState, string> partnerName,
        Func<InGameSessionState, Opcode, byte[], Task> sendToArea,
        Encoding text,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _book = book ?? throw new ArgumentNullException(nameof(book));
        _partnerName = partnerName ?? throw new ArgumentNullException(nameof(partnerName));
        _sendToArea = sendToArea ?? throw new ArgumentNullException(nameof(sendToArea));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public Task<HonorTitleLogin?> LoadAsync(ulong characterId, CancellationToken ct) => _master.LoadAsync(characterId, ct);

    public void Attach(InGameSessionState player, HonorTitleLogin? login)
    {
        ArgumentNullException.ThrowIfNull(player);
        var book = _book();
        player.HonorTitles = new HonorTitleList(login?.Titles ?? Array.Empty<ushort>(), book.Exists);
        player.ProfileTitle = login is null ? ProfileTitle.None : new ProfileTitle(login.ProfileTitleType, login.ProfileTitleId);
    }

    public LoginTitleFrames LoginFrames(HonorTitleLogin? login, ushort entityId, string partnerName)
    {
        var owned = new HonorTitleList(login?.Titles ?? Array.Empty<ushort>(), _book().Exists);
        return new LoginTitleFrames(
            HonorTitleFrames.List(owned.Snapshot(), ListTrailingByte),
            HonorTitleFrames.ProfileTitle(entityId, partnerName, login?.ProfileTitleType ?? 0, login?.ProfileTitleId ?? 0, _text));
    }

    public async Task SetProfileTitleAsync(InGameSessionState player, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character || !HonorTitleFrames.TryReadProfileTitle(payload.Span, out var titleType, out var titleId)) return;
        await ApplyProfileTitleAsync(player, character.CharacterId, new ProfileTitle(titleType, titleId), ct).ConfigureAwait(false);
    }

    public async Task ApplyProfileTitleAsync(InGameSessionState player, ulong characterId, ProfileTitle title, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.ProfileTitle = title;
        await _master.SaveProfileTitleAsync(characterId, title.TitleType, title.TitleId, ct).ConfigureAwait(false);
        await _sendToArea(player, Opcode.S2C_PROFILE_TITLE,
            HonorTitleFrames.ProfileTitle(player.PlayerEntityId, _partnerName(player), title.TitleType, title.TitleId, _text)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [title] 0x{player.PlayerEntityId:X4} shows title type {title.TitleType} id {title.TitleId}");
    }

    public Task SaveProfileTitleAsync(ulong characterId, ProfileTitle title, CancellationToken ct) =>
        _master.SaveProfileTitleAsync(characterId, title.TitleType, title.TitleId, ct);

    public async Task QuestClearedAsync(InGameSessionState player, QuestClear clear, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character || player.HonorTitles is not { } owned) return;
        var grants = _book().Grants(clear, owned);
        if (grants.Count == 0) return;
        var request = new HonorTitleAdd(player.MapId, player.PlayerEntityId,
            grants.Select(grant => new HonorTitleRecord(grant.TitleId, grant.QuestId, grant.QuestType, grant.Elapsed, grant.Hunt)).ToArray());
        var added = await _master.AddAsync(character.CharacterId, request, ct).ConfigureAwait(false);
        if (added is null)
        {
            Console.WriteLine($"{_logTag} [title] 0x{player.PlayerEntityId:X4} quest {clear.QuestId} (type {clear.QuestType}): titles {string.Join(",", grants.Select(grant => grant.TitleId))} not stored");
            return;
        }
        var book = _book();
        foreach (var titleId in added.TitleIds)
        {
            owned.TryAdd(titleId, book.Exists);
            await SendAsync(player, Opcode.S2C_HONOR_TITLE_ACQUIRED, HonorTitleFrames.Acquired(titleId)).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [title] 0x{player.PlayerEntityId:X4} earns honor title {titleId} from quest {clear.QuestId} (type {clear.QuestType})");
        }
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
