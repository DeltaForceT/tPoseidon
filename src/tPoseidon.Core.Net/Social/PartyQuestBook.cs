using System.Collections.Concurrent;
using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;

namespace tPoseidon.Core.Net.Social;

public sealed record PartyQuestPlayer(S2SessionContext Context, ulong CharacterId, string Name, ushort MapId, int Level, bool Hunting);

public sealed record PartyQuestStart(ulong PartyUid, IReadOnlyDictionary<ulong, int> CompletionCounts);

public interface IPartyQuestMaster
{
    Task<PartyQuestStart?> AcceptAsync(ulong characterId, uint questId, ushort monsterId, ushort targetCount, IReadOnlyList<ulong> members, CancellationToken ct);

    Task<bool> CompleteAsync(ulong characterId, uint questId, ulong partyUid, ushort killCount, int recordTime, byte memberCount, CancellationToken ct);

    Task<int?> RewardAsync(ulong characterId, ulong partyUid, uint questId, CancellationToken ct);

    Task<IReadOnlyList<PartyQuestRecord>> ListAsync(ulong characterId, CancellationToken ct);
}

public sealed class PartyQuestItem
{
    public required uint QuestId { get; init; }
    public required ulong PartyUid { get; init; }
    public required ushort MonsterId { get; init; }
    public required ushort TargetCount { get; init; }
    public required uint LimitMs { get; init; }
    public required uint StartMs { get; init; }
    public required int CompletionCount { get; init; }
    public required byte MemberCount { get; init; }
    public ushort OwnKills { get; set; }
    public ushort TeamKills { get; set; }
    public bool Success { get; set; }
    public int RecordTime { get; set; }
    public uint LastTimerSecond { get; set; }

    public uint RemainingAt(uint nowMs)
    {
        var elapsed = unchecked(nowMs - StartMs);
        return elapsed >= LimitMs ? 0 : LimitMs - elapsed;
    }
}

public static class PartyQuestResults
{
    public const int Ready = 1;
    public const int Finished = 2;
    public const int MembersBusy = 5;
    public const int NoParty = 6;
    public const int NotMaster = 7;
    public const int NotInZone = 8;
    public const int LevelOutOfRange = 9;

    public const int AcceptMembersBusy = 2;
    public const int AcceptNoParty = 3;
    public const int AcceptNotMaster = 4;
    public const int AcceptNotInZone = 5;
    public const int AcceptLevelOutOfRange = 6;

    public const int NoRoom = 2;
    public const int Unknown = -1;
}

public sealed class PartyQuestBook
{
    private readonly ConcurrentDictionary<ulong, PartyQuestItem> _items = new();
    private readonly PartyQuestCatalog _catalog;
    private readonly SystemServerLink _link;
    private readonly IPartyQuestMaster _master;
    private readonly Func<ulong, PartyQuestPlayer?> _players;
    private readonly Encoding _text;
    private readonly Action<string> _log;

    public PartyQuestBook(
        PartyQuestCatalog catalog,
        SystemServerLink link,
        IPartyQuestMaster master,
        Func<ulong, PartyQuestPlayer?> players,
        Encoding text,
        Action<string> log)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _players = players ?? throw new ArgumentNullException(nameof(players));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _link.OrderReceived += ApplyOrderAsync;
    }

    public PartyQuestCatalog Catalog => _catalog;

    public PartyQuestItem? Held(ulong characterId) => _items.GetValueOrDefault(characterId);

    public bool IsRunning(ulong characterId) => Held(characterId) is { Success: false };

    public static uint NowMs() => unchecked((uint)(DateTime.UtcNow.ToFileTimeUtc() / TimeSpan.TicksPerMillisecond));

    public async Task<int> CheckAsync(PartyQuestPlayer me, int questId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(me);
        if (_catalog.GetById(questId) is not { } quest) return PartyQuestResults.Unknown;
        if (Held(me.CharacterId) is { Success: true } held && held.QuestId == questId) return PartyQuestResults.Finished;
        if (_link.MirrorOf(me.CharacterId) is not { } mirror) return PartyQuestResults.NoParty;
        if (mirror.MasterCharacterId != me.CharacterId) return PartyQuestResults.NotMaster;
        if (MembersHere(me, mirror) is not { } members) return PartyQuestResults.NotInZone;
        if (!LevelsFit(quest, members)) return PartyQuestResults.LevelOutOfRange;
        var busy = members.Where(m => m.Hunting || IsRunning(m.CharacterId)).Select(m => m.Name).ToArray();
        if (busy.Length > 0)
        {
            await me.Context.SendFrameAsync(PartyQuestFrames.MembersBusy, PartyQuestFrames.MembersBusyFor(busy, _text), NextKey(), ct).ConfigureAwait(false);
            return PartyQuestResults.MembersBusy;
        }
        return PartyQuestResults.Ready;
    }

    public int AcceptRefusal(PartyQuestPlayer me, int questId)
    {
        ArgumentNullException.ThrowIfNull(me);
        if (_catalog.GetById(questId) is not { } quest) return PartyQuestResults.Unknown;
        if (_link.MirrorOf(me.CharacterId) is not { } mirror) return PartyQuestResults.AcceptNoParty;
        if (mirror.MasterCharacterId != me.CharacterId) return PartyQuestResults.AcceptNotMaster;
        if (MembersHere(me, mirror) is not { } members) return PartyQuestResults.AcceptNotInZone;
        if (!LevelsFit(quest, members)) return PartyQuestResults.AcceptLevelOutOfRange;
        if (members.Any(m => m.Hunting || IsRunning(m.CharacterId))) return PartyQuestResults.AcceptMembersBusy;
        return 0;
    }

    public async Task<bool> AcceptAsync(PartyQuestPlayer me, int questId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(me);
        if (_catalog.GetById(questId) is not { Targets.Count: > 0 } quest || _link.MirrorOf(me.CharacterId) is not { } mirror) return false;
        var memberIds = new[] { me.CharacterId }.Concat(mirror.Others.Select(o => o.CharacterId)).ToArray();
        var memberCount = (byte)memberIds.Length;
        var monsterId = (ushort)quest.Targets[0].Monster;
        var targetCount = (ushort)(quest.Targets[0].Count * memberCount);
        var limitMs = (uint)(quest.TimeMinutes * 60_000);
        var start = await _master.AcceptAsync(me.CharacterId, (uint)questId, monsterId, targetCount, memberIds, ct).ConfigureAwait(false);
        if (start is null || MembersHere(me, mirror) is not { } members || members.Any(m => IsRunning(m.CharacterId)))
        {
            await AbortAsync(me, (uint)questId, ct).ConfigureAwait(false);
            return false;
        }
        var startMs = NowMs();
        foreach (var member in members)
        {
            var completions = start.CompletionCounts.GetValueOrDefault(member.CharacterId);
            _items[member.CharacterId] = new PartyQuestItem
            {
                QuestId = (uint)questId,
                PartyUid = start.PartyUid,
                MonsterId = monsterId,
                TargetCount = targetCount,
                LimitMs = limitMs,
                StartMs = startMs,
                CompletionCount = completions,
                MemberCount = memberCount,
            };
            var key = NextKey();
            await member.Context.SendFrameAsync(PartyQuestFrames.Given, PartyQuestFrames.GivenFor((uint)questId, monsterId, targetCount, limitMs, (byte)completions), key, ct).ConfigureAwait(false);
            await member.Context.SendFrameAsync(PartyQuestFrames.Timer, PartyQuestFrames.TimerFor(limitMs, limitMs), NextKey(), ct).ConfigureAwait(false);
            await member.Context.SendFrameAsync(PartyQuestFrames.Kills, PartyQuestFrames.KillsFor(0, targetCount, Array.Empty<(ulong, ushort)>()), NextKey(), ct).ConfigureAwait(false);
        }
        await _link.SendAsync(SystemLinkFrames.BeginPartyQuest, SystemLinkFrames.EncodeBeginPartyQuest(new PartyQuestBegin(
            mirror.PartyId, me.MapId, me.CharacterId, start.PartyUid, (uint)questId, monsterId, targetCount, startMs, limitMs,
            members.Select(m => new PartyQuestMember(m.CharacterId, (byte)start.CompletionCounts.GetValueOrDefault(m.CharacterId))).ToArray())), ct).ConfigureAwait(false);
        _log($"[party-quest] quest {questId} given to {members.Count} members: {targetCount} of monster {monsterId} in {quest.TimeMinutes} min (party uid {start.PartyUid})");
        return true;
    }

    public Task<int?> RewardAsync(ulong characterId, PartyQuestItem item, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _master.RewardAsync(characterId, item.PartyUid, item.QuestId, ct);
    }

    public async Task FinishAsync(ulong characterId, uint questId, CancellationToken ct)
    {
        if (_items.TryGetValue(characterId, out var item) && item.QuestId == questId) _items.TryRemove(characterId, out _);
        await _link.SendAsync(SystemLinkFrames.GuildPointForQuest, SystemLinkFrames.Character(characterId), ct).ConfigureAwait(false);
    }

    public Task KilledAsync(ulong characterId, int monsterType, CancellationToken ct)
    {
        if (Held(characterId) is not { Success: false } item || item.MonsterId != monsterType) return Task.CompletedTask;
        return _link.SendAsync(SystemLinkFrames.PartyQuestKill, SystemLinkFrames.KillReport(characterId, item.MonsterId), ct);
    }

    public async Task RestoreAsync(ulong characterId, CancellationToken ct)
    {
        if (_items.ContainsKey(characterId)) return;
        var records = await _master.ListAsync(characterId, ct).ConfigureAwait(false);
        if (records.Count == 0) return;
        var record = records[0];
        _items.TryAdd(characterId, new PartyQuestItem
        {
            QuestId = record.QuestId,
            PartyUid = record.PartyUid,
            MonsterId = record.MonsterId,
            TargetCount = record.TargetCount,
            LimitMs = 0,
            StartMs = 0,
            CompletionCount = record.ComCount,
            MemberCount = record.MemberCount,
            OwnKills = record.KillCount,
            TeamKills = record.TargetCount,
            Success = true,
            RecordTime = record.RecordTime,
        });
    }

    public async Task TickAsync(CancellationToken ct)
    {
        var now = NowMs();
        foreach (var (characterId, item) in _items)
        {
            if (item.Success) continue;
            var remaining = item.RemainingAt(now);
            var second = unchecked(now - item.StartMs) / 1000;
            if (remaining > 0 && second == item.LastTimerSecond) continue;
            item.LastTimerSecond = second;
            var player = _players(characterId);
            if (remaining > 0)
            {
                if (player is not null)
                {
                    await player.Context.SendFrameAsync(PartyQuestFrames.Timer, PartyQuestFrames.TimerFor(remaining, item.LimitMs), NextKey(), ct).ConfigureAwait(false);
                }
                continue;
            }
            _items.TryRemove(new KeyValuePair<ulong, PartyQuestItem>(characterId, item));
            if (player is null) continue;
            await player.Context.SendFrameAsync(PartyQuestFrames.Timer, PartyQuestFrames.TimerFor(0, item.LimitMs), NextKey(), ct).ConfigureAwait(false);
            await player.Context.SendFrameAsync(PartyQuestFrames.TimedOut, PartyQuestFrames.Quest(item.QuestId), NextKey(), ct).ConfigureAwait(false);
            if (_link.MirrorOf(characterId)?.MasterCharacterId == characterId)
            {
                await _link.SendAsync(SystemLinkFrames.CancelPartyQuest, SystemLinkFrames.CharacterAndQuest(characterId, item.QuestId), ct).ConfigureAwait(false);
            }
            _log($"[party-quest] quest {item.QuestId} timed out for 0x{characterId:X}");
        }
    }

    private async Task ApplyOrderAsync(OfficerOrder order, CancellationToken ct)
    {
        switch (order.OrderType)
        {
            case SystemLinkFrames.OrderUpdateQuest:
                await ApplyTallyAsync(SystemLinkFrames.DecodeQuestTally(order.Data), ct).ConfigureAwait(false);
                break;
            case SystemLinkFrames.OrderRecoverQuest:
                await RecoverAsync(SystemLinkFrames.DecodeQuestRecovery(order.Data), ct).ConfigureAwait(false);
                break;
            case SystemLinkFrames.OrderCancelQuest:
                var cancel = SystemLinkFrames.DecodeQuestCancel(order.Data);
                if (Held(cancel.CharacterId) is not { } item || item.QuestId != cancel.QuestId) return;
                _items.TryRemove(cancel.CharacterId, out _);
                if (_players(cancel.CharacterId) is { } player)
                {
                    await player.Context.SendFrameAsync(PartyQuestFrames.Cancelled, PartyQuestFrames.CancelledFor(cancel.QuestId, cancel.Reason), NextKey(), ct).ConfigureAwait(false);
                }
                break;
        }
    }

    private async Task RecoverAsync(PartyQuestRecovery recovery, CancellationToken ct)
    {
        if (_players(recovery.RecipientCharacterId) is not { } player) return;
        if (Held(recovery.RecipientCharacterId) is { } held && (!held.Success || held.QuestId == recovery.QuestId)) return;
        var item = new PartyQuestItem
        {
            QuestId = recovery.QuestId,
            PartyUid = recovery.PartyUid,
            MonsterId = recovery.MonsterId,
            TargetCount = recovery.TargetCount,
            LimitMs = recovery.LimitMs,
            StartMs = recovery.StartMs,
            CompletionCount = recovery.RecipientCompletionCount,
            MemberCount = (byte)recovery.Members.Count,
            OwnKills = recovery.Members.FirstOrDefault(m => m.CharacterId == recovery.RecipientCharacterId).Kills,
            TeamKills = recovery.TeamKills,
        };
        var remaining = item.RemainingAt(NowMs());
        if (remaining == 0) return;
        _items[recovery.RecipientCharacterId] = item;
        await player.Context.SendFrameAsync(PartyQuestFrames.Given, PartyQuestFrames.GivenFor(item.QuestId, item.MonsterId, item.TargetCount, item.LimitMs, (byte)item.CompletionCount), NextKey(), ct).ConfigureAwait(false);
        await player.Context.SendFrameAsync(PartyQuestFrames.Timer, PartyQuestFrames.TimerFor(remaining, item.LimitMs), NextKey(), ct).ConfigureAwait(false);
        await player.Context.SendFrameAsync(PartyQuestFrames.Kills, PartyQuestFrames.KillsFor(item.TeamKills, item.TargetCount, recovery.Members), NextKey(), ct).ConfigureAwait(false);
        _log($"[party-quest] quest {item.QuestId} recovered for 0x{recovery.RecipientCharacterId:X}: {item.TeamKills}/{item.TargetCount}, {remaining} ms left");
    }

    private async Task ApplyTallyAsync(PartyQuestTally tally, CancellationToken ct)
    {
        if (Held(tally.RecipientCharacterId) is not { } item || item.QuestId != tally.QuestId) return;
        item.OwnKills = tally.Members.FirstOrDefault(m => m.CharacterId == tally.RecipientCharacterId).Kills;
        item.TeamKills = tally.TeamKills;
        var player = _players(tally.RecipientCharacterId);
        if (player is not null)
        {
            await player.Context.SendFrameAsync(PartyQuestFrames.Kills, PartyQuestFrames.KillsFor(item.TeamKills, item.TargetCount, tally.Members), NextKey(), ct).ConfigureAwait(false);
        }
        if (item.Success || item.TeamKills < item.TargetCount) return;
        item.Success = true;
        item.RecordTime = (int)item.RemainingAt(NowMs());
        var stored = await _master.CompleteAsync(tally.RecipientCharacterId, item.QuestId, item.PartyUid, item.OwnKills, item.RecordTime, item.MemberCount, ct).ConfigureAwait(false);
        _log($"[party-quest] quest {item.QuestId} finished for 0x{tally.RecipientCharacterId:X}: {item.OwnKills} own kills, stored {stored}");
        if (player is not null)
        {
            var returnWarp = _catalog.GetById((int)item.QuestId) is { ReturnMapId: not 0 };
            await player.Context.SendFrameAsync(PartyQuestFrames.Succeeded, PartyQuestFrames.SucceededFor(item.QuestId, returnWarp), NextKey(), ct).ConfigureAwait(false);
        }
    }

    private async Task AbortAsync(PartyQuestPlayer me, uint questId, CancellationToken ct)
    {
        await me.Context.SendFrameAsync(Common.Opcode.S2C_NPC_DIALOG_REFUSED, Array.Empty<byte>(), NextKey(), ct).ConfigureAwait(false);
        await _link.SendAsync(SystemLinkFrames.CancelPartyQuest, SystemLinkFrames.CharacterAndQuest(me.CharacterId, questId), ct).ConfigureAwait(false);
        _log($"[party-quest] quest {questId} for 0x{me.CharacterId:X} aborted");
    }

    private List<PartyQuestPlayer>? MembersHere(PartyQuestPlayer me, PartyMirror mirror)
    {
        var members = new List<PartyQuestPlayer> { me };
        foreach (var other in mirror.Others)
        {
            if (_players(other.CharacterId) is not { } player || player.MapId != me.MapId) return null;
            members.Add(player);
        }
        return members;
    }

    private static bool LevelsFit(PartyQuestInfoEntry quest, IReadOnlyList<PartyQuestPlayer> members) =>
        members.Min(m => m.Level) >= quest.MinLevel && members.Max(m => m.Level) <= quest.MaxLevel;

    private static byte NextKey() => (byte)Random.Shared.Next(1, 256);
}
