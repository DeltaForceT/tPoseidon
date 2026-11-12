using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guardian;
using tPoseidon.Core.Game.Notices;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.SoulSeed;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class SoulSeedController
{
    private readonly MasterSoulSeeds _master;
    private readonly MasterGuardian _guardians;
    private readonly IItemStore _items;
    private readonly Func<SoulSeedTables> _tables;
    private readonly Func<GuardianTables> _guardianTables;
    private readonly Func<InGameSessionState, CancellationToken, Task> _showGuardians;
    private readonly Func<InGameSessionState, Opcode, byte[], Task> _broadcast;
    private readonly Func<ServerNoticeTexts> _notices;
    private readonly MasterSystemNotice _announce;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public SoulSeedController(
        MasterSoulSeeds master,
        MasterGuardian guardians,
        IItemStore items,
        Func<SoulSeedTables> tables,
        Func<GuardianTables> guardianTables,
        Func<InGameSessionState, CancellationToken, Task> showGuardians,
        Func<InGameSessionState, Opcode, byte[], Task> broadcast,
        Func<ServerNoticeTexts> notices,
        MasterSystemNotice announce,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _guardians = guardians ?? throw new ArgumentNullException(nameof(guardians));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _guardianTables = guardianTables ?? throw new ArgumentNullException(nameof(guardianTables));
        _showGuardians = showGuardians ?? throw new ArgumentNullException(nameof(showGuardians));
        _broadcast = broadcast ?? throw new ArgumentNullException(nameof(broadcast));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _announce = announce ?? throw new ArgumentNullException(nameof(announce));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<bool> IsPlantedAsync(ulong characterId, CancellationToken ct)
    {
        var (loaded, state) = await _master.LoadAsync(characterId, ct).ConfigureAwait(false);
        return loaded && state is not null;
    }

    public async Task LoadAsync(InGameSessionState player, ulong characterId, bool loggingIn, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        var (loaded, state) = await _master.LoadAsync(characterId, ct).ConfigureAwait(false);
        if (!loaded) return;
        if (state is not null)
        {
            player.Seed.Restore(new SoulSeedRecord(true, state.SeedItemIndex, state.StartAt, state.LastUpdateAt, state.Points));
            Console.WriteLine($"{_logTag} [soul seed] char 0x{characterId:X16} seed {state.SeedItemIndex} planted, points {string.Join(",", state.Points)}");
            if (loggingIn) await SendAsync(player, Opcode.S2C_SOUL_SEED_PLANTED, SoulSeedFrames.Planted(player.Seed.Record(), false)).ConfigureAwait(false);
        }
        await SendAsync(player, Opcode.S2C_SOUL_SEED_STATE, SoulSeedFrames.StateOf(player.Seed)).ConfigureAwait(false);
    }

    public async Task<bool> PlantAsync(InGameSessionState player, Item item, ulong wireUid, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(item);
        if (player.Character is not { } character || player.Seed.Planted || player.SeedBusy) return false;
        player.SeedBusy = true;
        try
        {
            var used = await _master.UseAsync(character.CharacterId,
                new SoulSeedUseRequest(item.ItemIndex, item.ItemUid, SoulSeedRules.UseFlag(item.Stack)), ct).ConfigureAwait(false);
            if (used is not { Ok: true })
            {
                Console.WriteLine($"{_logTag} [soul seed] 0x{player.PlayerEntityId:X4} seed {item.ItemIndex} refused by the Master (code {used?.Code.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"})");
                return true;
            }
            player.Seed.Plant(item.ItemIndex, DateTimeOffset.UtcNow);
            await SendAsync(player, Opcode.S2C_SOUL_SEED_PLANTED, SoulSeedFrames.Planted(player.Seed.Record(), true)).ConfigureAwait(false);
            await SendAsync(player, Opcode.S2C_ITEM_USED, ItemUseFrames.Consumed(wireUid)).ConfigureAwait(false);
            if (_items.FindByUid(item.ItemUid) is { } held)
            {
                if (held.Stack > 1) _items.AdjustStack(held.ItemUid, -1);
                else _items.Delete(held.ItemUid);
            }
            Console.WriteLine($"{_logTag} [soul seed] 0x{player.PlayerEntityId:X4} plants seed {item.ItemIndex}");
            return true;
        }
        finally
        {
            player.SeedBusy = false;
        }
    }

    public IReadOnlyList<(Opcode Opcode, byte[] Payload)> Event(InGameSessionState player, int eventType, int count)
    {
        ArgumentNullException.ThrowIfNull(player);
        var tables = _tables();
        tables.EventsByType.TryGetValue(eventType, out var row);
        var outcome = player.Seed.OnEvent(eventType, count, row);
        if (!outcome.Processed || row is null) return Array.Empty<(Opcode Opcode, byte[] Payload)>();
        var frames = new List<(Opcode Opcode, byte[] Payload)>();
        if (!outcome.Granted)
        {
            frames.Add((Opcode.S2C_SOUL_SEED_PROGRESS, SoulSeedFrames.Progress(false, outcome.Counter, row)));
        }
        else
        {
            if (outcome.Complete) frames.Add((Opcode.S2C_SOUL_SEED_COMPLETE, Array.Empty<byte>()));
            if (outcome.Changed)
            {
                frames.Add((Opcode.S2C_SOUL_SEED_PROGRESS, SoulSeedFrames.Progress(true, outcome.Counter, row)));
                if (outcome.BuffChanged) frames.Add((Opcode.S2C_SOUL_SEED_BUFF, SoulSeedFrames.Buff(player.Seed.Buff())));
            }
        }
        frames.Add((Opcode.S2C_SOUL_SEED_STATE, SoulSeedFrames.StateOf(player.Seed)));
        if (player.Character is { } character)
        {
            foreach (var backup in outcome.Backups)
            {
                _ = _master.SavePointsAsync(character.CharacterId, backup.SavedPoints, CancellationToken.None);
                Console.WriteLine($"{_logTag} [soul seed] 0x{player.PlayerEntityId:X4} points saved: {string.Join(",", backup.SavedPoints)}");
            }
        }
        return frames;
    }

    public async Task TickAsync(IEnumerable<InGameSessionState> players, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(players);
        foreach (var player in players)
        {
            if (!player.Seed.TickBuff((int)elapsed.TotalMilliseconds)) continue;
            await SendAsync(player, Opcode.S2C_SOUL_SEED_BUFF_ENDED, Array.Empty<byte>()).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [soul seed] 0x{player.PlayerEntityId:X4} buff ended");
        }
    }

    public async Task EventAsync(InGameSessionState player, int eventType, int count)
    {
        foreach (var (opcode, payload) in Event(player, eventType, count)) await SendAsync(player, opcode, payload).ConfigureAwait(false);
    }

    public Task QuestClearedAsync(InGameSessionState player, QuestClear clear) =>
        SoulSeedRules.QuestEvent(clear.QuestType) is { } eventType ? EventAsync(player, eventType, 1) : Task.CompletedTask;

    public async Task<int> CheckAwakenAsync(InGameSessionState player, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return NpcScript.Refused;
        player.Guardians ??= await _guardians.LoadAsync(character.CharacterId, ct).ConfigureAwait(false);
        return player.Guardians is { Awakened: true } ? 1 : 2;
    }

    public async Task<int> AwakenAsync(InGameSessionState player, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character || !player.Seed.Planted || player.SeedBusy) return NpcScript.Refused;
        player.Guardians ??= await _guardians.LoadAsync(character.CharacterId, ct).ConfigureAwait(false);
        if (player.Guardians is { SlotMaxCount: > 0 } owned && owned.SlotMaxCount <= owned.Guardians.Count)
        {
            if (player.Quests is { } talk)
            {
                await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
                talk.Close();
            }
            await SendAsync(player, Opcode.S2C_AWAKEN_REFUSED, [(byte)SoulSeedRules.GuardianSlotsFullCode]).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} refused: {owned.Guardians.Count} of {owned.SlotMaxCount} guardian slots used");
            return NpcScript.Refused;
        }
        var tables = _tables();
        var seed = player.Seed;
        var sums = new int[SoulSeedRules.NormalTypes];
        var combined = new int[SoulSeedRules.NormalTypes];
        for (var k = 0; k < SoulSeedRules.NormalTypes; k++)
        {
            sums[k] = seed.Total(k) + seed.Total(k + SoulSeedRules.NormalTypes);
            combined[k] = Math.Max(1, seed.Level(k) + seed.Level(k + SoulSeedRules.NormalTypes));
        }
        int kind;
        lock (_rngGate) kind = SoulSeedRules.GuardianKind(sums, _rng.Next);
        if (!tables.GuardianTypesBySeed.TryGetValue(seed.SeedItemId, out var types) || kind >= types.Count)
        {
            Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} NotFound SeedParam {seed.SeedItemId}");
            return NpcScript.Refused;
        }
        var guardianType = types[kind];
        var baseLevels = Enumerable.Range(0, SoulSeedRules.NormalTypes).Sum(seed.Level);
        var specialLevels = Enumerable.Range(SoulSeedRules.NormalTypes, SoulSeedRules.NormalTypes).Sum(seed.Level);
        var rare = SoulSeedRules.RareType(combined, baseLevels, specialLevels);
        var name = tables.GuardianNames.TryGetValue(guardianType, out var commonName) ? commonName : string.Empty;
        var awakening = new GuardianAwakening(
            character.CharacterId, name, (short)guardianType, (byte)kind, rare, 0,
            (byte)combined[0], (byte)combined[1], (byte)combined[2], (byte)combined[3],
            SoulSeedRules.SummonGuardianSkill,
            GuardianRuneRules.Defaults(_guardianTables().Params.GetValueOrDefault(guardianType)));
        player.SeedBusy = true;
        _ = CompleteAwakenAsync(player, awakening);
        Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} asks for guardian {guardianType} (kind {kind}, rare {rare}, levels {string.Join(",", combined)})");
        return NpcScript.Pending;
    }

    public Task<int> ResetAwakeningAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return Task.FromResult(NpcScript.Refused);
        if (player.SeedBusy)
        {
            Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} reset refused: a guardian request is pending");
            return Task.FromResult(NpcScript.Refused);
        }
        player.SeedBusy = true;
        _ = CompleteResetAsync(player, character.CharacterId);
        Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} asks for the awakening reset (skill {SoulSeedRules.SummonGuardianSkill})");
        return Task.FromResult(NpcScript.Pending);
    }

    private async Task CompleteResetAsync(InGameSessionState player, ulong characterId)
    {
        try
        {
            if (!await _master.ResetAwakeningAsync(characterId, SoulSeedRules.SummonGuardianSkill, CancellationToken.None).ConfigureAwait(false))
            {
                Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} reset refused by the Master");
                return;
            }
            player.Seed.Clear();
            player.Skills = player.Skills.Where(skill => skill.Key != SoulSeedRules.SummonGuardianSkill).ToDictionary(skill => skill.Key, skill => skill.Value);
            player.Guardians = await _guardians.LoadAsync(characterId, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} awakening reset: seed and awakening flags cleared, guardians and skill {SoulSeedRules.SummonGuardianSkill} removed");
        }
        finally
        {
            player.SeedBusy = false;
        }
    }

    private async Task CompleteAwakenAsync(InGameSessionState player, GuardianAwakening awakening)
    {
        try
        {
            var awakened = await _master.AwakenAsync(awakening, CancellationToken.None).ConfigureAwait(false);
            if (awakened is null)
            {
                Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} guardian {awakening.GuardianType} refused by the Master");
                return;
            }
            player.Seed.Clear();
            player.Guardians = await _guardians.LoadAsync(awakening.CharacterId, CancellationToken.None).ConfigureAwait(false);
            if (player.Quests is { } talk)
            {
                await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
                talk.Close();
            }
            var first = awakened.SlotNum == 0;
            if (first)
            {
                player.Skills = new Dictionary<int, int>(player.Skills) { [SoulSeedRules.SummonGuardianSkill] = 1 };
                await SendAsync(player, Opcode.S2C_SKILL_LEARNED, SkillFrames.Record((ushort)SoulSeedRules.SummonGuardianSkill, 1, 0)).ConfigureAwait(false);
                await _broadcast(player, Opcode.S2C_SKILL_LEARN_SEEN, SkillFrames.LearnSeen(player.PlayerEntityId)).ConfigureAwait(false);
            }
            await _showGuardians(player, CancellationToken.None).ConfigureAwait(false);
            await SendAsync(player, first ? Opcode.S2C_AWAKENED : Opcode.S2C_GUARDIAN_AWAKENED,
                GuardianFrames.Awakened(player.PlayerEntityId, awakening.RareType)).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [awaken] 0x{player.PlayerEntityId:X4} guardian {awakened.GuardianUid} ({awakening.Name}) in slot {awakened.SlotNum}");
            if (player.Character is { } awakener && _notices().Awakened((byte)awakened.SlotNum, awakener.Name) is { } notice)
            {
                await _announce.SendAsync(notice, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            player.SeedBusy = false;
        }
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
