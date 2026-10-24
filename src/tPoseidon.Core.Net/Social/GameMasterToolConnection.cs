using System.Collections.Concurrent;
using System.Text;
using tPoseidon.Core.Net.S2;

namespace tPoseidon.Core.Net.Social;

public sealed record ToolReply(ushort Opcode, byte[] Payload);

public static class GameMasterToolConnection
{
    private static readonly TimeSpan ReplyWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SettleWait = TimeSpan.FromSeconds(1);

    private static readonly ushort[] Replies =
    {
        GameMasterFrames.LoginOk,
        GameMasterFrames.TeleportToChrOk,
        GameMasterFrames.TeleportToChrFail,
        GameMasterFrames.TeleportToZoneFail,
        GameMasterFrames.KickOk,
        GameMasterFrames.KickFail,
        GameMasterFrames.AccountBlockOk,
        GameMasterFrames.AccountBlockFail,
        GameMasterFrames.SpeedOk,
        GameMasterFrames.ImmortalOk,
        GameMasterFrames.InvisibleOk,
        GameMasterFrames.CharCountReply,
        GameMasterFrames.ZoneCountsReply,
        GameMasterFrames.RemoveMyCampUnknownZone,
        GameMasterFrames.InstantChatRemoved,
        GameMasterFrames.InstantChatRemoveFail,
        GameMasterFrames.SummonNpcOk,
        GameMasterFrames.SummonNpcFail,
        GameMasterFrames.GameFlagOk,
        GameMasterFrames.GameFlagFail,
        GameMasterFrames.CharInfoReply,
        GameMasterFrames.CharInfoMissing,
        GameMasterFrames.SummonMonsterOk,
        GameMasterFrames.SummonMonsterFail,
        GameMasterFrames.SetIssueItemOk,
        GameMasterFrames.SetIssueItemFail,
        GameMasterFrames.OxGiveItemOk,
        GameMasterFrames.OxGiveItemFail,
        GameMasterFrames.GuildInfoReply,
        GameMasterFrames.GuildInfoMissing,
        GameMasterFrames.GuildMemoFail,
        GameMasterFrames.GuildBlockOk,
        GameMasterFrames.GuildBlockFail,
        GameMasterFrames.GuildDissolveOk,
        GameMasterFrames.GuildDissolveFail,
        GameMasterFrames.BlockOk,
        GameMasterFrames.BlockFail,
        GameMasterFrames.BlockWithCauseOk,
        GameMasterFrames.BlockWithCauseFail,
        GameMasterFrames.UnblockOk,
        GameMasterFrames.UnblockFail,
        GameMasterFrames.BlockListReply,
        GameMasterFrames.BlockListAllReply,
        GameMasterFrames.SetMusicOk,
        GameMasterFrames.SetMusicFail,
        GameMasterFrames.RemoveObjectOk,
        GameMasterFrames.RemoveObjectFail,
        GameMasterFrames.NpcActivated,
        GameMasterFrames.NpcActivateFail,
        GameMasterFrames.NpcDeactivated,
        GameMasterFrames.NpcDeactivateFail,
        GameMasterFrames.NpcListReply,
        GameMasterFrames.NpcRefreshed,
        GameMasterFrames.NpcRefreshFail,
        GameMasterEventFrames.ExpListReply,
        GameMasterEventFrames.ExpListRefused,
        GameMasterEventFrames.ExpReserved,
        GameMasterEventFrames.ExpReserveRefused,
        GameMasterEventFrames.ExpDeleted,
        GameMasterEventFrames.ExpDeleteRefused,
        GameMasterEventFrames.ExpQueryReply,
        GameMasterFrames.GuildGameFlagReply,
        GameMasterFrames.GuildRoomFlagReply,
        GameMasterEventFrames.AbilityListReply,
        GameMasterEventFrames.AbilityListRefused,
        GameMasterEventFrames.AbilityReserved,
        GameMasterEventFrames.AbilityReserveRefused,
        GameMasterEventFrames.AbilityDeleted,
        GameMasterEventFrames.AbilityDeleteRefused,
    };

    private static readonly HashSet<ushort> Refusals = new()
    {
        GameMasterFrames.TeleportToChrFail,
        GameMasterFrames.TeleportToZoneFail,
        GameMasterFrames.KickFail,
        GameMasterFrames.AccountBlockFail,
        GameMasterFrames.RemoveMyCampUnknownZone,
        GameMasterFrames.InstantChatRemoveFail,
        GameMasterFrames.SummonNpcFail,
        GameMasterFrames.GameFlagFail,
        GameMasterFrames.CharInfoMissing,
        GameMasterFrames.SummonMonsterFail,
        GameMasterFrames.SetIssueItemFail,
        GameMasterFrames.OxGiveItemFail,
        GameMasterFrames.GuildInfoMissing,
        GameMasterFrames.GuildMemoFail,
        GameMasterFrames.GuildBlockFail,
        GameMasterFrames.GuildDissolveFail,
        GameMasterFrames.BlockFail,
        GameMasterFrames.BlockWithCauseFail,
        GameMasterFrames.UnblockFail,
        GameMasterFrames.SetMusicFail,
        GameMasterFrames.RemoveObjectFail,
        GameMasterFrames.NpcActivateFail,
        GameMasterFrames.NpcDeactivateFail,
        GameMasterFrames.NpcRefreshFail,
        GameMasterEventFrames.ExpListRefused,
        GameMasterEventFrames.ExpReserveRefused,
        GameMasterEventFrames.ExpDeleteRefused,
        GameMasterEventFrames.AbilityListRefused,
        GameMasterEventFrames.AbilityReserveRefused,
        GameMasterEventFrames.AbilityDeleteRefused,
    };

    public static bool IsRefusal(ToolReply reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        return Refusals.Contains(reply.Opcode);
    }

    public static async Task<IReadOnlyList<ToolReply>> RunAsync(
        string host,
        int port,
        string keyTablePath,
        string gmCharacter,
        string password,
        ushort opcode,
        byte[] body,
        bool expectsReply,
        Encoding text,
        CancellationToken ct) =>
        await ExchangeAsync(host, port, keyTablePath, gmCharacter, password, new GameMasterToolRequest(opcode, body), expectsReply, text, ct).ConfigureAwait(false);

    public static async Task<bool> LoginAsync(string host, int port, string keyTablePath, string gmCharacter, string password, Encoding text, CancellationToken ct)
    {
        var replies = await ExchangeAsync(host, port, keyTablePath, gmCharacter, password, null, false, text, ct).ConfigureAwait(false);
        return replies.Any(reply => reply.Opcode == GameMasterFrames.LoginOk);
    }

    private static async Task<IReadOnlyList<ToolReply>> ExchangeAsync(
        string host,
        int port,
        string keyTablePath,
        string gmCharacter,
        string password,
        GameMasterToolRequest? request,
        bool expectsReply,
        Encoding text,
        CancellationToken ct)
    {
        var received = new ConcurrentQueue<ToolReply>();
        var loggedIn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var answered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var session = S2Client.RunAsync(host, port, keyTablePath, "[tposeidon]", async (ctx, innerCt) =>
        {
            foreach (var reply in Replies)
            {
                ctx.Dispatcher.Register(reply, (got, payload, _) =>
                {
                    received.Enqueue(new ToolReply((ushort)got, payload.ToArray()));
                    if ((ushort)got == GameMasterFrames.LoginOk) loggedIn.TrySetResult(true);
                    else answered.TrySetResult(true);
                    return Task.CompletedTask;
                });
            }
            await ctx.SendFrameAsync(GameMasterFrames.ToolConnect, Array.Empty<byte>(), (byte)Random.Shared.Next(1, 256), innerCt).ConfigureAwait(false);
            await ctx.SendFrameAsync(GameMasterFrames.Login, LoginBody(gmCharacter, password, text), (byte)Random.Shared.Next(1, 256), innerCt).ConfigureAwait(false);
            _ = request is null
                ? AwaitLoginAsync(loggedIn.Task, stop)
                : SendCommandAsync(ctx, loggedIn.Task, expectsReply ? answered.Task : Task.Delay(SettleWait, stop.Token), request.Opcode, Prefixed(password, request.Body, text), stop);
        }, stop.Token);

        try
        {
            await session.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
        }
        return received.ToArray();
    }

    public static string Describe(ToolReply reply) => reply.Opcode switch
    {
        GameMasterFrames.LoginOk => "GM login accepted",
        GameMasterFrames.TeleportToChrOk => "teleport: already there",
        GameMasterFrames.TeleportToChrFail => "teleport refused: a character is not online",
        GameMasterFrames.TeleportToZoneFail => "teleport refused: character not online or zone not 1..999",
        GameMasterFrames.KickOk => "kicked (with a reason: the reason was shown, the kick follows when the client confirms it)",
        GameMasterFrames.KickFail => "kick failed: not online",
        GameMasterFrames.AccountBlockOk => "account sanction changed",
        GameMasterFrames.AccountBlockFail => "account sanction refused: no such character",
        GameMasterFrames.SpeedOk => "speed set",
        GameMasterFrames.ImmortalOk => "immortal mode set",
        GameMasterFrames.InvisibleOk => "invisible mode set",
        GameMasterFrames.CharCountReply => DescribeCharCount(reply.Payload),
        GameMasterFrames.ZoneCountsReply => DescribeZoneCounts(reply.Payload),
        GameMasterFrames.InstantChatRemoved => "instant chat room closed (a room member got the not-master refusal instead)",
        GameMasterFrames.InstantChatRemoveFail => "remove instant chat room refused: not online or not in an instant chat room",
        GameMasterFrames.RemoveMyCampUnknownZone => "remove My Camp: the character's zone is not known (the order is still sent)",
        GameMasterFrames.SummonNpcOk => "NPC summoned (an NPC type the server does not know is skipped by the game server)",
        GameMasterFrames.SummonNpcFail => "summon NPC refused: the GM character is not on a game server",
        GameMasterFrames.GameFlagOk => "GM flag changed in the game",
        GameMasterFrames.GameFlagFail => "GM flag refused: the GM character is not in the game",
        GameMasterFrames.CharInfoReply => DescribeCharInfo(reply.Payload),
        GameMasterFrames.CharInfoMissing => "character info: not found",
        GameMasterFrames.SummonMonsterOk => "monsters summoned",
        GameMasterFrames.SummonMonsterFail => $"summon refused (reason {(reply.Payload.Length > 0 ? reply.Payload[0] : 0)}: 1 monster type, 2 map, 4 spawn failed or map not on this game server)",
        GameMasterFrames.SetIssueItemOk => "issue item set",
        GameMasterFrames.OxGiveItemOk => "OX items handed out",
        GameMasterFrames.GuildInfoReply => DescribeGuildInfo(reply.Payload),
        GameMasterFrames.GuildInfoMissing => "guild info: unknown guild",
        GameMasterFrames.GuildMemoFail => "guild memo refused: unknown guild",
        GameMasterFrames.GuildBlockOk => $"guild blocked: {ReadName(reply.Payload, 0)}",
        GameMasterFrames.GuildBlockFail => $"guild block refused ({(reply.Payload.Length > 0 && reply.Payload[0] == GameMasterFrames.GuildBlockUnknownGuild ? "unknown guild" : "no member online")}): {ReadName(reply.Payload, 1)}",
        GameMasterFrames.GuildDissolveOk => $"guild dissolved: {ReadName(reply.Payload, 0)}",
        GameMasterFrames.GuildDissolveFail => $"guild dissolve refused: {ReadName(reply.Payload, 0)}",
        GameMasterFrames.BlockOk => $"blocked (type {ReadShort(reply.Payload)})",
        GameMasterFrames.BlockFail => $"block refused (type {ReadShort(reply.Payload)}): a GM, not a block type, or not saved",
        GameMasterFrames.BlockWithCauseOk => "the player read the cause and was disconnected",
        GameMasterFrames.BlockWithCauseFail => reply.Payload.Length >= sizeof(ushort) ? "block with cause refused: not online" : "block with cause: the reader had no character",
        GameMasterFrames.UnblockOk => $"unblocked (type {ReadShort(reply.Payload)})",
        GameMasterFrames.UnblockFail => $"unblock refused (type {ReadShort(reply.Payload)}): not blocked or not a block type",
        GameMasterFrames.BlockListReply => DescribeBlockList(reply.Payload, withType: false),
        GameMasterFrames.BlockListAllReply => DescribeBlockList(reply.Payload, withType: true),
        GameMasterFrames.OxGiveItemFail => $"OX give item refused (reason {(reply.Payload.Length > 0 ? reply.Payload[0] : 0)}: 1 not an OX map, 2 GM not found, 4 the GM does not hold the item)",
        GameMasterFrames.SetIssueItemFail => "set issue item refused: no such NPC on the map",
        GameMasterFrames.SetMusicOk => "music set",
        GameMasterFrames.SetMusicFail => $"set music refused (reason {(reply.Payload.Length > 0 ? reply.Payload[0] : 0)})",
        GameMasterFrames.RemoveObjectOk => "monsters removed",
        GameMasterFrames.RemoveObjectFail => "remove refused: no such monster (on the map, or none of the monsters this GM spawned is left)",
        GameMasterFrames.NpcActivated => $"NPC shown (type {ReadUInt(reply.Payload)})",
        GameMasterFrames.NpcActivateFail => reply.Payload.Length > 0 && reply.Payload[0] != 0 ? "show NPC refused: no hidden NPC at that position" : "show NPC refused: the NPC is not on the map",
        GameMasterFrames.NpcDeactivated => $"NPC hidden (type {ReadUInt(reply.Payload)})",
        GameMasterFrames.NpcDeactivateFail => "hide NPC refused: no such NPC on the map",
        GameMasterFrames.NpcListReply => DescribeNpcList(reply.Payload),
        GameMasterFrames.NpcRefreshed => $"zone {ReadUInt(reply.Payload)}: hidden NPCs shown again",
        GameMasterFrames.NpcRefreshFail => $"zone {ReadUInt(reply.Payload)}: refresh refused",
        GameMasterEventFrames.ExpListReply => DescribeExpList(reply.Payload, withTime: true, withCurrent: true),
        GameMasterEventFrames.ExpReserved => DescribeExpList(reply.Payload, withTime: false, withCurrent: false),
        GameMasterEventFrames.ExpDeleted => DescribeExpList(reply.Payload, withTime: false, withCurrent: true),
        GameMasterEventFrames.ExpListRefused or GameMasterEventFrames.ExpReserveRefused or GameMasterEventFrames.ExpDeleteRefused => "EXP control refused: this SystemServer does not run the EXP events",
        GameMasterFrames.GuildGameFlagReply => reply.Payload.Length > 0 && reply.Payload[0] != 0 ? $"guild game rooms open (flag {reply.Payload[0]})" : "guild game rooms closed (flag 0)",
        GameMasterFrames.GuildRoomFlagReply => $"room flag {(reply.Payload.Length > 0 ? reply.Payload[0] : 0)}",
        GameMasterEventFrames.AbilityListReply => DescribeAbilityList(reply.Payload, withTime: true),
        GameMasterEventFrames.AbilityReserved or GameMasterEventFrames.AbilityDeleted => DescribeAbilityList(reply.Payload, withTime: false),
        GameMasterEventFrames.AbilityListRefused or GameMasterEventFrames.AbilityReserveRefused or GameMasterEventFrames.AbilityDeleteRefused => "ability control refused: this SystemServer does not run the ability events",
        GameMasterEventFrames.ExpQueryReply => GameMasterEventFrames.TryReadExpPair(reply.Payload, out var expNow, out var tmNow) ? $"set with exp-now: EXP +{expNow}%, TM +{tmNow}%" : $"0x{reply.Opcode:X4} ({reply.Payload.Length} B)",
        _ => $"0x{reply.Opcode:X4} ({reply.Payload.Length} B)",
    };

    private static string DescribeExpList(byte[] payload, bool withTime, bool withCurrent)
    {
        if (!GameMasterEventFrames.TryReadExpList(payload, withTime, withCurrent, out var list)) return $"EXP events ({payload.Length} B)";
        var lines = new List<string>();
        if (list.Time is { } time) lines.Add("server time " + time.ToString(TimeFormat, System.Globalization.CultureInfo.InvariantCulture));
        if (list.Current is { } current) lines.Add(current == default ? "applied now: none" : $"applied now: {DescribeExpEvent(current)}");
        lines.Add(list.Running.Count == 0 ? "running: none" : "running:");
        lines.AddRange(list.Running.Select(entry => "  " + DescribeExpEvent(entry)));
        lines.Add(list.Waiting.Count == 0 ? "waiting: none" : "waiting:");
        lines.AddRange(list.Waiting.Select(entry => "  " + DescribeExpEvent(entry)));
        return string.Join(Environment.NewLine, lines);
    }

    private static string DescribeAbilityList(byte[] payload, bool withTime)
    {
        if (!GameMasterEventFrames.TryReadAbilityList(payload, withTime, out var list)) return $"ability events ({payload.Length} B)";
        var lines = new List<string>();
        if (list.Time is { } time) lines.Add("server time " + time.ToString(TimeFormat, System.Globalization.CultureInfo.InvariantCulture));
        lines.Add(list.Running.Count == 0 ? "running: none" : "running:");
        lines.AddRange(list.Running.Select(entry => "  " + DescribeAbilityEvent(entry)));
        lines.Add(list.Waiting.Count == 0 ? "waiting: none" : "waiting:");
        lines.AddRange(list.Waiting.Select(entry => "  " + DescribeAbilityEvent(entry)));
        return string.Join(Environment.NewLine, lines);
    }

    private static string DescribeAbilityEvent(GameMasterAbilityEvent entry) =>
        $"{GameMasterToolCommands.StatName(entry.Stat)} +{entry.Percent}%, {LocalTime(entry.StartTime)} to {LocalTime(entry.EndTime)}";

    private static string DescribeExpEvent(GameMasterExpEvent entry) =>
        $"EXP +{entry.ExpPercent}%, TM +{entry.TmPercent}%, {LocalTime(entry.StartTime)} to {LocalTime(entry.EndTime)}";

    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss";

    private static string LocalTime(long unixSeconds) =>
        unixSeconds >= DateTimeOffset.MinValue.ToUnixTimeSeconds() && unixSeconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds()
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).LocalDateTime.ToString(TimeFormat, System.Globalization.CultureInfo.InvariantCulture)
            : $"{unixSeconds} (Unix seconds)";

    private static string ReadName(byte[] payload, int offset) =>
        payload.Length > offset ? new PacketReader(payload.AsSpan(offset)).ReadNulTerminated(GameMasterToolCommands.ClientText()) : string.Empty;

    private static string DescribeGuildInfo(byte[] payload)
    {
        var text = GameMasterToolCommands.ClientText();
        var r = new PacketReader(payload);
        var code = r.ReadUInt32();
        var name = r.ReadNulTerminated(text);
        var members = r.ReadByte();
        var master = r.ReadNulTerminated(text);
        var created = r.ReadNulTerminated(text);
        if (r.ReadByte() == 0) return $"{name} (id {code}): {members} member(s), master {master}, created {created}, no member online";
        var masterOnline = r.ReadByte() != 0;
        var count = r.ReadByte();
        var online = new string[count];
        for (var i = 0; i < count; i++) online[i] = r.ReadNulTerminated(text);
        return $"{name} (id {code}): {members} member(s), master {master}{(masterOnline ? " (online)" : string.Empty)}, created {created}, online: {string.Join(", ", online)}";
    }

    private static ushort ReadShort(byte[] payload) => payload.Length >= sizeof(ushort) ? new PacketReader(payload).ReadUInt16() : (ushort)0;

    private static string DescribeBlockList(byte[] payload, bool withType)
    {
        if (!GameMasterFrames.TryReadBlockList(payload, GameMasterToolCommands.ClientText(), withType, out var entries)) return $"block list ({payload.Length} B)";
        if (entries.Count == 0) return "no blocked characters";
        return string.Join(Environment.NewLine, entries.Select(entry =>
            $"{entry.Name} (0x{entry.CharacterId:X}){(withType ? $", type {entry.Type}" : string.Empty)}, {entry.Minutes} minute(s) from {entry.Time}"));
    }

    private static uint ReadUInt(byte[] payload) => GameMasterFrames.TryReadAckResult(payload, out var value) ? value : 0;

    private static string DescribeNpcList(byte[] payload)
    {
        if (!GameMasterFrames.TryReadNpcList(payload, out var npcs)) return $"0x{GameMasterFrames.NpcListReply:X4} ({payload.Length} B)";
        if (npcs.Count == 0) return "no hidden NPCs";
        return string.Join(Environment.NewLine, npcs.Select(npc => $"{npc.Index}: zone {npc.Zone}, NPC type {npc.ChrType}"));
    }

    private static string DescribeZoneCounts(byte[] payload)
    {
        if (!GameMasterFrames.TryReadZoneCounts(payload, out var zones)) return $"0x{GameMasterFrames.ZoneCountsReply:X4} ({payload.Length} B)";
        if (zones.Count == 0) return "no characters online";
        return string.Join(Environment.NewLine, zones.Select(zone => $"zone {zone.Zone}: {zone.Count} characters"));
    }

    private static string DescribeCharCount(byte[] payload)
    {
        if (payload.Length < sizeof(ushort) + sizeof(uint)) return $"0x{GameMasterFrames.CharCountReply:X4} ({payload.Length} B)";
        var r = new PacketReader(payload);
        var zone = r.ReadUInt16();
        return $"zone {zone}: {r.ReadUInt32()} characters";
    }

    private static string DescribeCharInfo(byte[] payload)
    {
        var text = GameMasterToolCommands.ClientText();
        var r = new PacketReader(payload);
        var account = r.ReadNulTerminated(text);
        var name = r.ReadNulTerminated(text);
        var characterId = r.ReadUInt64();
        var zone = r.ReadUInt16();
        var level = r.ReadUInt32();
        var money = r.ReadUInt32();
        return $"{name} (account '{account}', id 0x{characterId:X}): zone {zone}, level {level}, money {money}";
    }

    private static async Task SendCommandAsync(S2SessionContext ctx, Task loggedIn, Task answered, ushort opcode, byte[] body, CancellationTokenSource stop)
    {
        try
        {
            if (await Task.WhenAny(loggedIn, Task.Delay(ReplyWait, stop.Token)).ConfigureAwait(false) != loggedIn) return;
            await ctx.SendFrameAsync(opcode, body, (byte)Random.Shared.Next(1, 256), stop.Token).ConfigureAwait(false);
            await Task.WhenAny(answered, Task.Delay(ReplyWait, stop.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
        }
    }

    private static async Task AwaitLoginAsync(Task loggedIn, CancellationTokenSource stop)
    {
        try
        {
            await Task.WhenAny(loggedIn, Task.Delay(ReplyWait, stop.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
        }
    }

    private static byte[] LoginBody(string gmCharacter, string password, Encoding text)
    {
        var w = new PacketWriter(64);
        w.WriteBytes(GameMasterChat.Password(password, text));
        w.WriteNulTerminated(gmCharacter, text);
        return w.ToArray();
    }

    private static byte[] Prefixed(string password, byte[] body, Encoding text)
    {
        var prefix = GameMasterChat.Password(password, text);
        var all = new byte[prefix.Length + body.Length];
        prefix.CopyTo(all, 0);
        body.CopyTo(all, prefix.Length);
        return all;
    }
}
