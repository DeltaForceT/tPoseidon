using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed partial class NpcScriptExecutor
{
    private (int Kind, ushort? Message)? _eventTalk;

    private async Task<int> NotifyCardBattleRankAsync(CancellationToken ct)
    {
        await SendAsync(NpcFunctionFrames.CardBattleRankNotice, Array.Empty<byte>(), ct).ConfigureAwait(false);
        return NpcCheckRules.Allowed;
    }

    private async Task<int> GuildSetupCheckAsync(CancellationToken ct)
    {
        if (_catalogs.GuildSetup is not { } requirement || _player.Stats() is not { } stats) return NpcScript.Refused;
        var code = GuildSetupRules.Check(requirement, Level, stats.Money);
        if (code != GuildSetupRules.Ready) return code;
        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        await SendAsync(NpcFunctionFrames.GuildSetupWindow, NpcFunctionFrames.GuildSetup(stats.Money, requirement.MinGelder), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   guild setup window opened (level {Level}, gelder {stats.Money}, needed {requirement.MinGelder})");
        return NpcScript.Silent;
    }

    private async Task<int> GuildWindowAsync(int functionId, CancellationToken ct)
    {
        if (NpcFunctionFrames.GuildWindow(functionId) is not ushort opcode) return NpcScript.Refused;
        await SendAsync(opcode, Array.Empty<byte>(), ct).ConfigureAwait(false);
        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   guild window 0x{opcode:X4} opened (function {functionId})");
        return NpcScript.Silent;
    }

    private async Task<int> EventTalkStartAsync(int kind, IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (_npc is not { } npc) return NpcScript.Refused;
        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        await SendAsync(NpcFunctionFrames.EventStartOpcode(kind), NpcFunctionFrames.EventStart(npc.ChrType), ct).ConfigureAwait(false);
        _eventTalk = (kind, args.Count > 0 ? args[0] : null);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   event {(kind == NpcEventTalk.Scene ? "scene" : "talk")} of NPC {npc.ChrType} started (then message {(args.Count > 0 ? args[0].ToString(System.Globalization.CultureInfo.InvariantCulture) : "none")})");
        return NpcScript.Silent;
    }

    private async Task<int> LimitTeleportAsync(int userLimit, IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (args.Count < LimitTeleportRules.Args || TeleportRow is not { } row || _player.Stats() is not { } stats || _player.ZoneUserCount is not { } countUsers) return NpcScript.Refused;
        var index = args[LimitTeleportRules.DestinationArg];
        if (index >= row.Destinations.Count || !TeleportRules.IsZone(row.Destinations[index].Zone)) return NpcScript.Refused;
        var destination = row.Destinations[index];
        if (stats.Money < destination.Cost)
        {
            await SendMessageAsync(args[LimitTeleportRules.NoMoneyMessageArg], QuestInSlot(0) ?? 0, ct).ConfigureAwait(false);
            return NpcScript.Silent;
        }
        var zone = (ushort)destination.Zone;
        var users = await countUsers(zone, ct).ConfigureAwait(false);
        if (!LimitTeleportRules.HasRoom(users, userLimit))
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   limited teleport to zone {zone} refused: {(users is int count ? $"{count} users" : "no count")} (limit {userLimit})");
            await SendMessageAsync(args[LimitTeleportRules.FullMessageArg], QuestInSlot(0) ?? 0, ct).ConfigureAwait(false);
            return NpcScript.Silent;
        }
        await SendMessageAsync(args[LimitTeleportRules.OkMessageArg], QuestInSlot(0) ?? 0, ct).ConfigureAwait(false);
        Close();
        if (destination.Cost != 0)
        {
            stats.GrantMoney(-destination.Cost, StatChangeSource.Teleport);
            await SendAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money), ct).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} {_ctx.Remote}   limited teleport {row.Id} destination {index}: zone {zone} ({users} users, limit {userLimit}), cost {destination.Cost}");
        if (ZoneChangeRequested is { } move)
        {
            await move(new ZoneChangeRequest(zone, tPoseidon.Core.Game.Warp.TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), ct).ConfigureAwait(false);
        }
        return NpcScript.Silent;
    }

    private async Task<int> CheckEnterPartyZoneAsync(IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (args.Count != PartyZoneRules.Args || _talk is not { BossPartyZone: var zone } || _player.LatestBossKills is not { } latestKills) return NpcScript.Refused;
        if (!_catalogs.PartyZones.ReEnterMinutes.TryGetValue(zone, out var reEnterMinutes)) return NpcScript.Refused;
        if (await latestKills(ct).ConfigureAwait(false) is not { } kills) return NpcScript.Refused;
        var bosses = _catalogs.PartyZones.Bosses.TryGetValue(zone, out var zoneBosses) ? zoneBosses : new HashSet<int>();
        if (PartyZoneRules.MinutesLeft(reEnterMinutes, bosses, kills.Select(kill => (kill.MonsterId, kill.LatestKill)), DateTimeOffset.UtcNow) is not int left)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   party zone {zone} may be entered");
            return PartyZoneRules.Open;
        }
        await SendAsync(NpcFunctionFrames.ZoneCommand, NpcFunctionFrames.ReEnterRemain(left), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   party zone {zone} closed for {left} more minutes");
        return PartyZoneRules.Closed;
    }

    private async Task<int> MonsterSummonAsync(IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (args.Count < MonsterSummonRules.Args || _player.SummonMonsters is not { } summon) return NpcScript.Refused;
        await summon(args[0], ct).ConfigureAwait(false);
        await SendMessageAsync(args[1], QuestInSlot(0) ?? 0, ct).ConfigureAwait(false);
        return NpcScript.Silent;
    }

    private Task<int> NpcSkillStartAsync(IReadOnlyList<ushort> args, CancellationToken ct) =>
        args.Count < NpcSkillRules.Args || _player.StartNpcSkill is not { } start
            ? Task.FromResult(NpcScript.Refused)
            : start(args[0], ct);

    private async Task<int> WarpGateAsync(bool bySpecies, CancellationToken ct)
    {
        if (_player.Character() is not { } character) return NpcScript.Refused;
        var room = bySpecies
            ? WarpGateRules.GuardianRoomOfSpecies(character.JobTier, (int)character.Species)
            : WarpGateRules.GuardianRoomOfJob(character.JobTier);
        if (room is not int zone || ZoneChangeRequested is not { } move) return NpcCheckRules.Allowed;
        await SendAsync(NpcFunctionFrames.WarpGateClose, Array.Empty<byte>(), ct).ConfigureAwait(false);
        Close();
        Console.WriteLine($"{_logTag} {_ctx.Remote}   warp gate to the guardian room {zone} (job {character.JobTier}, species {character.Species})");
        await move(new ZoneChangeRequest((ushort)zone, tPoseidon.Core.Game.Warp.TeleportArrival.ChangeZoneType, null, TimeSpan.Zero), ct).ConfigureAwait(false);
        return NpcScript.Pending;
    }

    public async Task EventTalkEndedAsync(ushort opcode, CancellationToken ct)
    {
        if (NpcFunctionFrames.EventKindOfEnd(opcode) is not int kind || _eventTalk is not { } pending || pending.Kind != kind) return;
        _eventTalk = null;
        if (pending.Message is ushort message && _npc is not null)
        {
            await SendAsync(NpcFunctionFrames.EventEndedOpcode(kind), NpcFunctionFrames.EventEnded(_npcEntityId, message), ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_ctx.Remote}   event {(kind == NpcEventTalk.Scene ? "scene" : "talk")} ended; NPC 0x{_npcEntityId:X4} goes on with message {message}");
            return;
        }
        await SendAsync(NpcFunctionFrames.EventEndedOpcode(kind), NpcFunctionFrames.EventEndedWithoutTalk(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   event {(kind == NpcEventTalk.Scene ? "scene" : "talk")} ended");
        Close();
    }

    private Task SendAsync(ushort opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
