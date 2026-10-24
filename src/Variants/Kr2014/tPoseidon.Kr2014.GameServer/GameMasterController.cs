using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.Social;
using tPoseidon.Kr2014.Wire;

internal sealed record GameMasterWorldActions(
    Func<InGameSessionState, uint, byte, Task<bool>> SummonMonsters,
    Func<ushort, ushort, Task<int>> RemoveObjects,
    Func<ushort, ushort, bool, Task<int?>> SetNpcActive,
    Func<ushort, Task> RefreshNpcs,
    Func<ushort, ushort, uint, ushort, bool> SetIssueItem,
    Func<InGameSessionState, uint, byte, Task<uint>> OxGiveItem,
    Func<InGameSessionState, Task> CloseShop,
    Func<InGameSessionState, Task> CloseCamp,
    Func<InGameSessionState, uint, IReadOnlyList<string>, Task> SummonNpc);

internal sealed class GameMasterController : IDisposable
{
    public const byte TeleportToZoneChangeType = 2;

    private readonly SystemServerLink _link;
    private readonly Func<ulong, InGameSessionState?> _playerById;
    private readonly Func<InGameSessionState, Task> _respawnToViewers;
    private readonly GameMasterChat _chat;
    private readonly Func<InGameSessionState, Task> _disconnect;
    private readonly Func<InGameSessionState, Task> _announceSpeed;
    private readonly Func<InGameSessionState, Task> _applyVisibility;
    private readonly Func<InGameSessionState, uint, ushort, Task> _presentItem;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly System.Text.Encoding _text;
    private readonly string _logTag;

    public GameMasterWorldActions? World { get; set; }

    public GameMasterRates Rates { get; } = new();

    public Func<IReadOnlyList<InGameSessionState>>? Players { get; init; }

    public Func<(float Exp, float Tm)>? FixedBonusRates { get; init; }

    public GameMasterController(
        SystemServerLink link,
        Func<ulong, InGameSessionState?> playerById,
        Func<InGameSessionState, Task> respawnToViewers,
        GameMasterChat chat,
        Func<InGameSessionState, Task> disconnect,
        Func<InGameSessionState, Task> announceSpeed,
        Func<InGameSessionState, Task> applyVisibility,
        Func<InGameSessionState, uint, ushort, Task> presentItem,
        Func<ushort, Opcode, byte[], Task> sendToMap,
        System.Text.Encoding text,
        string logTag)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _playerById = playerById ?? throw new ArgumentNullException(nameof(playerById));
        _respawnToViewers = respawnToViewers ?? throw new ArgumentNullException(nameof(respawnToViewers));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _disconnect = disconnect ?? throw new ArgumentNullException(nameof(disconnect));
        _announceSpeed = announceSpeed ?? throw new ArgumentNullException(nameof(announceSpeed));
        _applyVisibility = applyVisibility ?? throw new ArgumentNullException(nameof(applyVisibility));
        _presentItem = presentItem ?? throw new ArgumentNullException(nameof(presentItem));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
        _link.ZonedOrderReceived += ZonedOrderAsync;
        _link.MessageReceived += MessageAsync;
    }

    public static byte SpawnFlagsOf(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.GameMasterAttribute ? PlayerSpawnLoginPacket.GmEagleFlag : (byte)0;
    }

    public bool IsCommand(string line) => _chat.IsCommand(line);

    public void Dispose()
    {
        _link.OrderReceived -= OrderAsync;
        _link.ZonedOrderReceived -= ZonedOrderAsync;
        _link.MessageReceived -= MessageAsync;
    }

    public async Task EnteredZoneAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (Rates.HasExp) await SendRateSlotsAsync(player, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task ApplyAbilityAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!GameMasterEventFrames.TryReadAbilityChange(payload.Span, out var stat, out var percent))
        {
            Console.WriteLine($"{_logTag} [gm] ability control dropped: {payload.Length} B");
            return;
        }
        if (!Rates.SetAbility(stat, percent)) return;
        Console.WriteLine($"{_logTag} [gm] ability control: stat {stat} +{percent}%");
        foreach (var player in Players?.Invoke() ?? Array.Empty<InGameSessionState>())
        {
            if (player.Character is null) continue;
            player.RecomputeStats?.Invoke();
            await SendAbilitiesAsync(player, ct).ConfigureAwait(false);
        }
    }

    private async Task SendAbilitiesAsync(InGameSessionState player, CancellationToken ct)
    {
        try
        {
            await player.SendFrameAsync((Opcode)GameMasterEventFrames.AbilityPercents, Rates.AbilityPacket(), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            Console.WriteLine($"{_logTag} [gm] ability percentages for 0x{player.PlayerEntityId:X4} not delivered: {ex.Message}");
        }
    }

    private async Task ApplyExpAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!GameMasterEventFrames.TryReadExpPair(payload.Span, out var expPercent, out var tmPercent))
        {
            Console.WriteLine($"{_logTag} [gm] EXP control dropped: {payload.Length} B");
            return;
        }
        if (!Rates.SetExp(expPercent, tmPercent)) return;
        Console.WriteLine($"{_logTag} [gm] EXP control: EXP +{expPercent}%, TM +{tmPercent}%");
        foreach (var player in Players?.Invoke() ?? Array.Empty<InGameSessionState>())
        {
            if (player.Character is null) continue;
            await SendRateSlotsAsync(player, ct).ConfigureAwait(false);
        }
    }

    private async Task SendRateSlotsAsync(InGameSessionState player, CancellationToken ct)
    {
        var fixedRates = FixedBonusRates?.Invoke() ?? (0f, 0f);
        try
        {
            await player.SendFrameAsync(Opcode.GAME_BATCH_CONST_377, GameBatchFloatArrayPacket.ForRateSlotsKr2014(fixedRates.Tm, Rates.TmSlotRate).EncodePayload(), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
            await player.SendFrameAsync(Opcode.GAME_BATCH_CONST_376, GameBatchFloatArrayPacket.ForRateSlotsKr2014(fixedRates.Exp, Rates.ExpSlotRate).EncodePayload(), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            Console.WriteLine($"{_logTag} [gm] rate slots for 0x{player.PlayerEntityId:X4} not delivered: {ex.Message}");
        }
    }

    private Task MessageAsync(ushort opcode, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        switch (opcode)
        {
            case GameMasterEventFrames.ExpApply:
                return ApplyExpAsync(payload, ct);
            case GameMasterEventFrames.AbilityApply:
                return ApplyAbilityAsync(payload, ct);
            case GameMasterFrames.KickCharacter:
                if (!GameMasterFrames.TryReadKick(payload.Span, out var kickedId) || _playerById(kickedId) is not { } kicked)
                {
                    Console.WriteLine($"{_logTag} [gm] kick dropped: {payload.Length} B or character not on this server");
                    return Task.CompletedTask;
                }
                Console.WriteLine($"{_logTag} [gm] 0x{kickedId:X} kicked");
                return _disconnect(kicked);
            case GameMasterFrames.LinkChatReply:
                if (!GameMasterFrames.TryReadChatLine(payload.Span, _text, out var gmId, out var line)
                    || _playerById(gmId) is not { Character: { } gmCharacter } gm)
                {
                    return Task.CompletedTask;
                }
                return gm.SendFrameAsync((Opcode)ChatFrames.Said, ChatFrames.Line(gm.PlayerEntityId, gmCharacter.Name, line, _text), (byte)Random.Shared.Next(1, 256), ct);
            default:
                return Task.CompletedTask;
        }
    }

    private Task AcknowledgeAsync(OfficerOrder order, ushort report, uint result, CancellationToken ct) =>
        _link.ReportAsync(order with { OrderType = report, Data = GameMasterFrames.AckResult(result) }, ct);

    private async Task ZonedOrderAsync(ushort zone, OfficerOrder order, CancellationToken ct)
    {
        if (World is null) return;
        switch (order.OrderType)
        {
            case GameMasterFrames.OrderNpcOnOff when GameMasterFrames.TryReadNpcOnOff(order.Data, out var on, out var npcId):
                var chrType = await World.SetNpcActive(zone, npcId, on).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [gm] NPC {npcId} on zone {zone} {(on ? "activated" : "deactivated")}: {(chrType is null ? "not found" : "done")}");
                var ack = new GameMasterFrames.NpcOnOffAck(on, chrType is null ? 1u : GameMasterFrames.AckOk, npcId, zone, (uint)(chrType ?? 0));
                await _link.ReportAsync(order with { OrderType = GameMasterFrames.ReportNpcOnOff, Data = GameMasterFrames.NpcOnOffAckBody(ack) }, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderNpcRefresh:
                await World.RefreshNpcs(zone).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [gm] NPC activation refreshed on zone {zone}");
                await AcknowledgeAsync(order, GameMasterFrames.ReportNpcRefresh, GameMasterFrames.AckOk, ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task OrderAsync(OfficerOrder order, CancellationToken ct)
    {
        switch (order.OrderType)
        {
            case GameMasterFrames.OrderSetGmAttribute:
                await SetGmAttributeAsync(order).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderSuperRun when GameMasterFrames.TryReadSuperRun(order.Data, out var speed):
                if (_playerById(order.CharacterId) is not { } runner) return;
                runner.GameMasterSpeed = Math.Clamp(speed, (uint)GameMasterFrames.NormalSpeed, (uint)GameMasterFrames.MaxSpeed);
                Console.WriteLine($"{_logTag} [gm] 0x{order.CharacterId:X} speed x{runner.GameMasterSpeed}");
                await _announceSpeed(runner).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderPresentItem when GameMasterFrames.TryReadPresent(order.Data, out var receiverId, out var itemId, out var itemCount):
                if (_playerById(receiverId) is not { } receiver)
                {
                    Console.WriteLine($"{_logTag} [gm] present item {itemId} x{itemCount} for 0x{receiverId:X}: not on this server");
                    return;
                }
                await _presentItem(receiver, itemId, itemCount).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderOxQuestion:
                if (_playerById(order.CharacterId) is not { } asker) return;
                Console.WriteLine($"{_logTag} [gm] OX question on map {asker.MapId}: \"{GameMasterFrames.ReadQuestion(order.Data, _text)}\"");
                await _sendToMap(asker.MapId, (Opcode)GameMasterFrames.OxTimerSound, Array.Empty<byte>()).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderOxGiveItem when GameMasterFrames.TryReadOxGift(order.Data, out var giftItem, out var giftCount):
                if (World is null) return;
                if (_playerById(order.CharacterId) is not { } giver)
                {
                    await AcknowledgeAsync(order, GameMasterFrames.ReportOxGiveItem, GameMasterFrames.AckNoGameMaster, ct).ConfigureAwait(false);
                    return;
                }
                var giftResult = await World.OxGiveItem(giver, giftItem, giftCount).ConfigureAwait(false);
                await AcknowledgeAsync(order, GameMasterFrames.ReportOxGiveItem, giftResult, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderOxAnswer when order.Data.Length >= 1:
                if (_playerById(order.CharacterId) is not { } revealer) return;
                Console.WriteLine($"{_logTag} [gm] OX answer {order.Data[0]} on map {revealer.MapId}");
                await _sendToMap(revealer.MapId, (Opcode)GameMasterFrames.OxAnswerSound, Array.Empty<byte>()).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderCharInfo when GameMasterFrames.TryReadCharacterId(order.Data, out var subjectId):
                var info = _playerById(subjectId) is { Character: { } subject } subjectState
                    ? new GameMasterFrames.CharacterInfo(subject.Name, subject.CharacterId, subjectState.MapId, subject.Level, subject.Money, false, false)
                    : new GameMasterFrames.CharacterInfo(string.Empty, subjectId, 0, 0, 0, true, false);
                await _link.ReportAsync(order with { OrderType = GameMasterFrames.ReportCharInfo, Data = GameMasterFrames.CharInfoReport(info, _text) }, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderSetMusic:
                if (_playerById(order.CharacterId) is not { } composer || World is null) return;
                var music = GameMasterFrames.ReadMusicName(order.Data, _text);
                Console.WriteLine($"{_logTag} [gm] music '{music}' on map {composer.MapId}");
                await _sendToMap(composer.MapId, (Opcode)GameMasterFrames.MusicChange, GameMasterFrames.MusicPacket(music, _text)).ConfigureAwait(false);
                await AcknowledgeAsync(order, GameMasterFrames.ReportSetMusic, GameMasterFrames.AckOk, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderSummonMonster when GameMasterFrames.TryReadSummon(order.Data, out var summonType, out var summonZone, out var summonCount):
                if (_playerById(order.CharacterId) is not { } summoner || World is null) return;
                if (summonZone != 0 || !GameMasterFrames.IsSummonableMonster(summonType))
                {
                    Console.WriteLine($"{_logTag} [gm] summon {summonType} in zone {summonZone} refused");
                    await AcknowledgeAsync(order, GameMasterFrames.ReportSummonMonster, GameMasterFrames.AckBadMonsterType, ct).ConfigureAwait(false);
                    return;
                }
                var summoned = await World.SummonMonsters(summoner, summonType, summonCount).ConfigureAwait(false);
                await AcknowledgeAsync(order, GameMasterFrames.ReportSummonMonster, summoned ? GameMasterFrames.AckOk : GameMasterFrames.AckFailed, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderSummonNpc when GameMasterFrames.TryReadSummonNpc(order.Data, _text, out var npcType, out var npcLines):
                if (World is null) return;
                if (_playerById(order.CharacterId) is not { } npcSummoner)
                {
                    await AcknowledgeAsync(order, GameMasterFrames.ReportSummonNpc, GameMasterFrames.AckSummonNpcNoGameMaster, ct).ConfigureAwait(false);
                    return;
                }
                await World.SummonNpc(npcSummoner, npcType, npcLines).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [gm] summon NPC {npcType} with {npcLines.Count} line(s) on map {npcSummoner.MapId}");
                await AcknowledgeAsync(order, GameMasterFrames.ReportSummonNpc, GameMasterFrames.AckOk, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderSetIssueItem when GameMasterFrames.TryReadIssueItem(order.Data, out var issuer, out var issuedItem, out var issuedCount):
                if (_playerById(order.CharacterId) is not { } issuingGm || World is null) return;
                var issued = World.SetIssueItem(issuingGm.MapId, issuer, issuedItem, issuedCount);
                Console.WriteLine($"{_logTag} [gm] issue item {issuedItem} x{issuedCount} on NPC {issuer} (map {issuingGm.MapId}): {(issued ? "set" : "no such NPC")}");
                await AcknowledgeAsync(order, GameMasterFrames.ReportSetIssueItem, issued ? GameMasterFrames.AckOk : GameMasterFrames.AckNoSuchNpc, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderRemoveObject when GameMasterFrames.TryReadObjectId(order.Data, out var removedId):
                if (_playerById(order.CharacterId) is not { } remover || World is null) return;
                var removedCount = await World.RemoveObjects(remover.MapId, removedId).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [gm] removed {removedCount} object(s) on map {remover.MapId} (id {removedId})");
                await AcknowledgeAsync(order, GameMasterFrames.ReportRemoveObject, removedId == 0 || removedCount > 0 ? GameMasterFrames.AckOk : GameMasterFrames.AckRemoveObjectFailed, ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderRemovePersonalShop when GameMasterFrames.TryReadCharacterId(order.Data, out var shopKeeperId):
                if (_playerById(shopKeeperId) is not { } keeper || World is null) return;
                Console.WriteLine($"{_logTag} [gm] closes the personal shop of 0x{shopKeeperId:X}");
                await World.CloseShop(keeper).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderRemoveMyCamp when GameMasterFrames.TryReadCharacterId(order.Data, out var campOwnerId):
                if (_playerById(campOwnerId) is not { } campOwner || World is null) return;
                Console.WriteLine($"{_logTag} [gm] closes the My Camp of 0x{campOwnerId:X}");
                await World.CloseCamp(campOwner).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderImmortal when GameMasterFrames.TryReadToggle(order.Data, out var immortal):
                if (_playerById(order.CharacterId) is not { } protectedPlayer) return;
                protectedPlayer.GameMasterImmortal = immortal;
                Console.WriteLine($"{_logTag} [gm] 0x{order.CharacterId:X} immortal {(immortal ? "on" : "off")}");
                break;
            case GameMasterFrames.OrderInvisible when GameMasterFrames.TryReadToggle(order.Data, out var invisible):
                if (_playerById(order.CharacterId) is not { } hidden) return;
                if (hidden.GameMasterInvisible == invisible) return;
                hidden.GameMasterInvisible = invisible;
                Console.WriteLine($"{_logTag} [gm] 0x{order.CharacterId:X} invisible {(invisible ? "on" : "off")}");
                await _applyVisibility(hidden).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderTeleportToZone when GameMasterFrames.TryReadZone(order.Data, out var zone):
                if (_playerById(order.CharacterId) is not { RequestZoneChange: { } toZone } moved)
                {
                    Console.WriteLine($"{_logTag} [gm] teleport 0x{order.CharacterId:X} to zone {zone}: not on this server");
                    return;
                }
                if (moved.MapId == zone) return;
                Console.WriteLine($"{_logTag} [gm] teleport 0x{order.CharacterId:X} to zone {zone}");
                await toZone(new ZoneChangeRequest(zone, TeleportToZoneChangeType, null, TimeSpan.Zero), ct).ConfigureAwait(false);
                break;
            case GameMasterFrames.OrderTeleportToChr when GameMasterFrames.TryReadChrToChr(order.Data, out var whoId, out var toId, out var relayZone, out var relayX, out var relayY):
                if (relayZone != 0)
                {
                    if (_playerById(whoId) is not { RequestZoneChange: { } relayed })
                    {
                        Console.WriteLine($"{_logTag} [gm] relayed teleport of 0x{whoId:X}: character is not on this server");
                        return;
                    }
                    Console.WriteLine($"{_logTag} [gm] relayed teleport 0x{whoId:X} to 0x{toId:X}: zone {relayZone} ({relayX},{relayY})");
                    await relayed(new ZoneChangeRequest(relayZone, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, (relayX, relayY)), ct).ConfigureAwait(false);
                    return;
                }
                if (_playerById(toId) is not { } anchor)
                {
                    Console.WriteLine($"{_logTag} [gm] teleport 0x{whoId:X} to 0x{toId:X}: target is not on this server");
                    return;
                }
                if (_playerById(whoId) is not { RequestZoneChange: { } toChr })
                {
                    Console.WriteLine($"{_logTag} [gm] teleport 0x{whoId:X} to 0x{toId:X}: handing over to the server of 0x{whoId:X}");
                    await _link.SendAsync(GameMasterFrames.LinkTeleportRelay, GameMasterFrames.ChrToChr(whoId, toId, anchor.MapId, anchor.PlayerX, anchor.PlayerY), ct).ConfigureAwait(false);
                    return;
                }
                Console.WriteLine($"{_logTag} [gm] teleport 0x{whoId:X} to 0x{toId:X}: zone {anchor.MapId} ({anchor.PlayerX},{anchor.PlayerY})");
                await toChr(new ZoneChangeRequest(anchor.MapId, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, (anchor.PlayerX, anchor.PlayerY)), ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task SetGmAttributeAsync(OfficerOrder order)
    {
        if (!GameMasterFrames.TryReadGmAttribute(order.Data, out var on))
        {
            Console.WriteLine($"{_logTag} [gm] set-GM order for 0x{order.CharacterId:X} dropped: {order.Data.Length} B");
            return;
        }
        if (_playerById(order.CharacterId) is not { } player)
        {
            Console.WriteLine($"{_logTag} [gm] set-GM order for 0x{order.CharacterId:X}: character is not on this server");
            return;
        }
        player.GameMasterAttribute = on;
        Console.WriteLine($"{_logTag} [gm] 0x{order.CharacterId:X} GM attribute {(on ? "set" : "cleared")}");
        if (player.Character is not null) await _respawnToViewers(player).ConfigureAwait(false);
    }
}
