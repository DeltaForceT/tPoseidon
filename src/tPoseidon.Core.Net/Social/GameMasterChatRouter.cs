using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed record GameMasterChatOutcome(GameMasterOutbox? Outbox, string? Reply, IReadOnlyList<string> Audit);

public sealed class GameMasterChatRouter
{
    private readonly GameMasterService _service;
    private readonly IGmAuthorityStore _authority;
    private readonly Encoding _text;
    private readonly Func<string, ulong?> _online;
    private readonly Func<ulong, string?> _onlineName;
    private readonly Func<string> _blockTime;
    private readonly Func<IReadOnlyList<string>> _helpNames;

    public Func<ulong, string, GameMasterOutbox?>? InstantChatRemover { get; set; }

    public GameMasterChatRouter(
        GameMasterService service,
        IGmAuthorityStore authority,
        Encoding text,
        Func<string, ulong?> online,
        Func<ulong, string?> onlineName,
        Func<string> blockTime,
        Func<IReadOnlyList<string>> helpNames)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _online = online ?? throw new ArgumentNullException(nameof(online));
        _onlineName = onlineName ?? throw new ArgumentNullException(nameof(onlineName));
        _blockTime = blockTime ?? throw new ArgumentNullException(nameof(blockTime));
        _helpNames = helpNames ?? throw new ArgumentNullException(nameof(helpNames));
    }

    public GameMasterChatOutcome? Route(ulong characterId, string characterName, string clientIp, string line)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        ArgumentNullException.ThrowIfNull(clientIp);
        ArgumentNullException.ThrowIfNull(line);
        if (!GameMasterChat.IsCommandLine(line)) return null;
        var who = $"0x{characterId:X} '{characterName}' from {clientIp}";
        var audit = new List<string>();
        if (GameMasterChat.Parse(line) is not { } command)
        {
            audit.Add($"[gm] audit: {who} sent an unknown @ command ({GameMasterChat.WordCount(line)} word(s))");
            return new GameMasterChatOutcome(null, GameMasterChat.UnknownCommandLine, audit);
        }
        var args = command.Arguments;
        var keyword = GameMasterChat.CommandMark + command.Name.ToLowerInvariant();
        var registered = _authority.GmCharacterId(characterName) == characterId;

        if (command.Name == GameMasterChat.Login)
        {
            audit.Add($"[gm] audit: {who} tries {keyword}");
            var attempt = registered && args.Count == 1
                ? _service.Login(characterId, characterName, GameMasterChat.Password(args[0], _text), clientIp, kickOnRefusal: false)
                : null;
            if (attempt is not null) audit.AddRange(attempt.Notes.Select(note => $"[gm] audit: {note}"));
            if (attempt is null || attempt.Refused)
            {
                audit.Add($"[gm] audit: login of {who} refused");
                return new GameMasterChatOutcome(null, GameMasterChat.NotAllowedLine, audit);
            }
            return new GameMasterChatOutcome(attempt, null, audit);
        }

        if (!_service.IsLoggedIn(characterId) || !registered)
        {
            audit.Add($"[gm] audit: {who} used {keyword} and was refused ({(registered ? "no GM login" : "not a registered GM character")})");
            var dropped = _service.IsLoggedIn(characterId) ? _service.Logout(characterId) : null;
            return new GameMasterChatOutcome(dropped, GameMasterChat.NotAllowedLine, audit);
        }

        GameMasterOutbox? outbox = command.Name switch
        {
            GameMasterChat.Logout => _service.Logout(characterId),
            GameMasterChat.TeleportChrToZone when args.Count is 1 or 2 && ushort.TryParse(args[^1], out var zone) =>
                _service.TeleportToZone(characterId, args.Count == 2 ? args[0] : string.Empty, zone, _online),
            GameMasterChat.TeleportSelfToChr when args.Count is 1 or 2 =>
                _service.TeleportToChr(characterId, args.Count == 2 ? args[0] : string.Empty, args[^1], _online),
            GameMasterChat.Kick when args.Count == 1 => GameMasterService.Kick(characterId, args[0], _online),
            GameMasterChat.Kick when args.Count == 2 => _service.KickWithCause(characterId, false, args[0], args[1], _online),
            GameMasterChat.Memo when args.Count == 2 => _service.Memo(args[0], args[1]),
            GameMasterChat.RemovePlayerShop when args.Count == 1 => GameMasterService.RemovePersonalShop(args[0], _online),
            GameMasterChat.RemoveMyCamp when args.Count == 1 => GameMasterService.RemoveMyCamp(args[0], _online),
            GameMasterChat.RemoveMyChat when args.Count == 1 => InstantChatRemover?.Invoke(characterId, args[0]),
            GameMasterChat.Speed when args.Count == 1 && byte.TryParse(args[0], out var speed) =>
                _service.SuperRun(characterId, false, speed),
            GameMasterChat.Immortal when args.Count == 1 && GameMasterChat.TryParseSwitch(args[0], out var immortal) =>
                _service.Immortal(characterId, false, immortal),
            GameMasterChat.Invisible when args.Count == 1 && GameMasterChat.TryParseSwitch(args[0], out var invisible) =>
                _service.Invisible(characterId, false, invisible),
            GameMasterChat.SuperNotice when args.Count == 1 => _service.SuperNotice(0, args[0]),
            GameMasterChat.SuperNotice when args.Count == 2 && ushort.TryParse(args[0], out var scrolls) => _service.SuperNotice(scrolls, args[1]),
            GameMasterChat.Block when args.Count is 2 or 3 && int.TryParse(args[1], out var days) =>
                _service.NormalBlockFromChat(characterId, args[0], days, args.Count == 3 ? args[2] : null, _online, _blockTime()),
            _ when GameMasterChatBlocks.TypeOf(command.Name) is { } blockType && args.Count == 2 && int.TryParse(args[1], out var minutes) =>
                _service.TimedBlockFromChat(characterId, blockType, args[0], minutes, _online, _blockTime()),
            _ when GameMasterChatBlocks.ListOf(command.Name) is { } list && args.Count == 0 =>
                _service.BlockListFromChat(list.Type, list.Header, _onlineName),
            GameMasterChat.SummonMonster when args.Count is 1 or 2 && uint.TryParse(args[0], out var monsterType) =>
                args.Count == 1 ? GameMasterService.SummonMonster(characterId, monsterType, 0, 1)
                    : byte.TryParse(args[1], out var monsterCount) ? GameMasterService.SummonMonster(characterId, monsterType, 0, monsterCount) : null,
            GameMasterChat.CommandHelp when args.Count == 0 => GameMasterService.CommandHelp(_helpNames()),
            _ => null,
        };
        if (outbox is null)
        {
            audit.Add($"[gm] audit: {keyword} from {who} not understood ({args.Count} argument(s))");
            return new GameMasterChatOutcome(null, GameMasterChat.BadArgumentsLine, audit);
        }
        audit.Add($"[gm] audit: {who} {keyword} {string.Join(' ', args)}".TrimEnd());
        audit.AddRange(outbox.Notes.Select(note => $"[gm] audit: {keyword} {note}"));
        return new GameMasterChatOutcome(outbox, null, audit);
    }
}
