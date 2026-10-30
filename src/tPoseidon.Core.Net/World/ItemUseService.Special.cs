using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed partial class ItemUseService
{
    private int _specialBusy;

    private async Task UseSpecialAsync(
        SpecialItemHost host, Item item, ItemParamEntry definition, StatManager stats, ulong wireUid, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var effect = definition.Effect;
        if (SpecialItemEffects.UsesCustomData(effect) && !SpecialItemEffects.NeedsCustomData(definition))
        {
            await UsePlainAsync(item, definition, wireUid, ct).ConfigureAwait(false);
            return;
        }
        if (SpecialItemEffects.RequiresNormalForm(effect) && !host.InNormalForm())
        {
            Console.WriteLine($"{_logTag} {_remote()}   item {item.ItemIndex} refused: the player is not in the normal form");
            await SendAsync(Opcode.S2C_SKILL_NOTICE, SpecialItemFrames.FormRefused(), ct).ConfigureAwait(false);
            return;
        }
        var bytes = data.ToArray();
        switch (effect)
        {
            case SpecialItemEffects.PortablePort:
                await UsePortablePortAsync(host, item, definition, wireUid, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.HairDye:
                await UseHairDyeAsync(host, item, definition, wireUid, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.EmergencyPort:
                await UseEmergencyPortAsync(host, item, definition, wireUid, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.PortableAdvancedPort:
                await UseAdvancedPortAsync(host, item, definition, wireUid, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.GenderChange:
                await UseGenderChangeAsync(host, item, definition, wireUid, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.SkillReset:
                await UseSkillResetAsync(host, item, definition, wireUid, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.StatReset:
                await UseStatResetAsync(host, item, definition, stats, wireUid, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.StatResetAll:
                await UseStatResetAllAsync(host, item, definition, stats, wireUid, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.GrowthGraphReset:
                await UseGrowthGraphResetAsync(host, item, definition, stats, wireUid, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.StorageExpansion:
                await UseStorageExpansionAsync(host, item, definition, wireUid, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.BroomPort:
                await UseBroomPortAsync(host, item, definition, wireUid, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.Rename:
                await UseRenameAsync(host, item, definition, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.Megaphone:
                await UseMegaphoneAsync(host, item, definition, bytes, ct).ConfigureAwait(false);
                break;
            case SpecialItemEffects.FriendFinder:
                await UseFriendFinderAsync(host, item, definition, wireUid, bytes, ct).ConfigureAwait(false);
                break;
            default:
                await UsePlainAsync(item, definition, wireUid, ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task UsePlainAsync(Item item, ItemParamEntry definition, ulong wireUid, CancellationToken ct)
    {
        await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
        var left = Consume(item, definition);
        Console.WriteLine($"{_logTag} {_remote()}   item {item.ItemIndex} used (effect {definition.Effect} has no server action), {left} left");
    }

    private async Task AnswerAsync(ItemParamEntry definition, ulong wireUid, CancellationToken ct)
    {
        var block = _blocks.Next();
        await SendAsync(Opcode.S2C_ITEM_USE_EFFECT, ItemUseFrames.Used(wireUid, ItemUseFrames.EffectOnly((ushort)definition.Effect)), block, ct).ConfigureAwait(false);
        await _player.SendToMap(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), block).ConfigureAwait(false);
    }

    private async Task RefuseWithAsync(Opcode refusal, byte[] payload, string reason, CancellationToken ct)
    {
        await SendAsync(refusal, payload, ct).ConfigureAwait(false);
        await RefuseAsync(reason, ct).ConfigureAwait(false);
    }

    private BagOperation ConsumeWithRecord(Item item, ItemParamEntry definition)
    {
        var left = Consume(item, definition);
        var kind = left > 0 ? QuestResultFrames.StackChanged : QuestResultFrames.Removed;
        return new BagOperation(kind, definition.ObjectClass, (uint)item.ItemIndex, BagItemIds.OnWire(item.ItemUid), (ushort)left);
    }

    private bool TryEnter() => Interlocked.CompareExchange(ref _specialBusy, 1, 0) == 0;

    private void Leave() => Interlocked.Exchange(ref _specialBusy, 0);

    private int NextBelow(int bound)
    {
        lock (_rng) return _rng.Next(bound);
    }

    private async Task WarpTailAsync(
        SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, ushort zone, (ushort X, ushort Y)? position, CancellationToken ct)
    {
        await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
        var left = Consume(item, definition);
        Console.WriteLine($"{_logTag} {_remote()}   item {item.ItemIndex} sends the player to zone {zone}, {left} left");
        await host.Warp(zone, position, ct).ConfigureAwait(false);
    }

    private async Task<bool> RefusedByMapAsync(SpecialItemHost host, Item item, CancellationToken ct)
    {
        if (!PortRules.MapRefusesWarp(host.MapOption(host.MapId()))) return false;
        await RefuseWithAsync(Opcode.S2C_WARP_ITEM_REFUSED, SpecialItemFrames.Code(PortRules.MapRefusalCode), $"item {item.ItemIndex}: the map forbids teleporting", ct).ConfigureAwait(false);
        return true;
    }

    private async Task UsePortablePortAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, byte[] data, CancellationToken ct)
    {
        if (await RefusedByMapAsync(host, item, ct).ConfigureAwait(false)) return;
        if (!SpecialItemFrames.TryReadWord(data, out var zone))
        {
            await RefuseAsync($"item {item.ItemIndex}: no zone in the request", ct).ConfigureAwait(false);
            return;
        }
        if (!PortRules.IsZone(zone) || zone > PortRules.LastPortableZone)
        {
            await RefuseAsync($"item {item.ItemIndex}: zone {zone} is not a portable port zone", ct).ConfigureAwait(false);
            return;
        }
        await WarpTailAsync(host, item, definition, wireUid, zone, null, ct).ConfigureAwait(false);
    }

    private async Task UseEmergencyPortAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, CancellationToken ct)
    {
        var home = host.ReturnMap();
        if (!PortRules.IsZone(home))
        {
            await RefuseAsync($"item {item.ItemIndex}: return map {home} is not a zone", ct).ConfigureAwait(false);
            return;
        }
        if (home == host.MapId())
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.PortAlreadyThere, Array.Empty<byte>(), $"item {item.ItemIndex}: the player is on the return map {home}", ct).ConfigureAwait(false);
            return;
        }
        if (await RefusedByMapAsync(host, item, ct).ConfigureAwait(false)) return;
        await WarpTailAsync(host, item, definition, wireUid, home, null, ct).ConfigureAwait(false);
    }

    private async Task UseAdvancedPortAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, byte[] data, CancellationToken ct)
    {
        if (await RefusedByMapAsync(host, item, ct).ConfigureAwait(false)) return;
        if (!SpecialItemFrames.TryReadAdvancedZoneIndex(data, out var index))
        {
            await RefuseAsync($"item {item.ItemIndex}: no zone index in the request", ct).ConfigureAwait(false);
            return;
        }
        if (index > int.MaxValue || !host.Ports().Advanced.TryGetValue((int)index, out var row))
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.PortNoZone, Array.Empty<byte>(), $"item {item.ItemIndex}: zone index {index} has no {SpecialPortTables.AdvancedTable} row", ct).ConfigureAwait(false);
            return;
        }
        if (row.ZoneId == host.MapId())
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.PortSameZone, Array.Empty<byte>(), $"item {item.ItemIndex}: the player is already in zone {row.ZoneId}", ct).ConfigureAwait(false);
            return;
        }
        await WarpTailAsync(host, item, definition, wireUid, (ushort)row.ZoneId, null, ct).ConfigureAwait(false);
    }

    private async Task UseBroomPortAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, CancellationToken ct)
    {
        if (await RefusedByMapAsync(host, item, ct).ConfigureAwait(false)) return;
        if (!host.Ports().Random.TryGetValue(item.ItemIndex, out var row))
        {
            await RefuseAsync($"item {item.ItemIndex}: no {SpecialPortTables.RandomTable} row", ct).ConfigureAwait(false);
            return;
        }
        if (PortRules.PickRandomZone(row, NextBelow) is not { } zone || !PortRules.IsZone(zone))
        {
            await RefuseAsync($"item {item.ItemIndex}: the roll gave no zone", ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_ITEM_WARP_NOTICE, ItemUseFrames.Notice(row.Notice, host.TextEncoding()), ct).ConfigureAwait(false);
        await WarpTailAsync(host, item, definition, wireUid, (ushort)zone, null, ct).ConfigureAwait(false);
    }

    private async Task UseFriendFinderAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, byte[] data, CancellationToken ct)
    {
        if (!host.Idle())
        {
            await RefuseAsync($"item {item.ItemIndex}: the player is busy", ct).ConfigureAwait(false);
            return;
        }
        if (await RefusedByMapAsync(host, item, ct).ConfigureAwait(false)) return;
        if (!SpecialItemFrames.TryReadFriendFinder(data, out var target))
        {
            await RefuseAsync($"item {item.ItemIndex}: no friend position in the request", ct).ConfigureAwait(false);
            return;
        }
        if (!PortRules.IsZone(target.Map))
        {
            await RefuseAsync($"item {item.ItemIndex}: map {target.Map} is not a zone", ct).ConfigureAwait(false);
            return;
        }
        if (PortRules.MapRefusesArrival(host.MapOption(target.Map)))
        {
            await RefuseWithAsync(Opcode.S2C_WARP_ITEM_REFUSED, SpecialItemFrames.Code(PortRules.MapRefusalCode), $"item {item.ItemIndex}: map {target.Map} forbids arrivals", ct).ConfigureAwait(false);
            return;
        }
        (ushort X, ushort Y)? position = target.X >= 1 && target.Y >= 1 ? ((ushort)target.X, (ushort)target.Y) : null;
        await WarpTailAsync(host, item, definition, wireUid, target.Map, position, ct).ConfigureAwait(false);
    }

    private async Task UseHairDyeAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, byte[] data, CancellationToken ct)
    {
        if (data.Length < HairDyeRules.DataBytes)
        {
            await RefuseAsync($"item {item.ItemIndex}: no hair colour in the request", ct).ConfigureAwait(false);
            return;
        }
        var color = data[0];
        if (!HairDyeRules.ValidColor(color))
        {
            await RefuseAsync($"item {item.ItemIndex}: hair colour {color} is out of range", ct).ConfigureAwait(false);
            return;
        }
        if (!TryEnter())
        {
            await RefuseAsync($"item {item.ItemIndex}: another item operation is pending", ct).ConfigureAwait(false);
            return;
        }
        try
        {
            if (!await host.DyeHair(color, ct).ConfigureAwait(false))
            {
                await RefuseAsync($"item {item.ItemIndex}: the hair colour {color} was not stored", ct).ConfigureAwait(false);
                return;
            }
            var left = Consume(item, definition);
            var playerId = _player.EntityId();
            await SendAsync((Opcode)SpecialItemFrames.HairChanged, SpecialItemFrames.Hair(color), ct).ConfigureAwait(false);
            await _player.SendToMap((Opcode)SpecialItemFrames.HairSeen, SpecialItemFrames.HairSeenBy(playerId, color), 0).ConfigureAwait(false);
            await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   hair dye {item.ItemIndex}: colour {color}, {left} left");
        }
        finally
        {
            Leave();
        }
    }

    private async Task UseGenderChangeAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, byte[] data, CancellationToken ct)
    {
        if (!SpecialItemFrames.TryReadWord(data, out var value) || value >= GenderChangeRules.DataLimit)
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.GenderRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: the request carries {(data.Length < GenderChangeRules.DataBytes ? "no value" : $"value {value}")}", ct).ConfigureAwait(false);
            return;
        }
        if (_player.Character() is not { } character)
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.GenderRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: no character", ct).ConfigureAwait(false);
            return;
        }
        var worn = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment).Count;
        var free = PickupRules.BagCapacity - _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory).Count;
        if (GenderChangeRules.HasChangedJob(character) || GenderChangeRules.BagSlotsNeeded(worn) > free || GenderChangeRules.OppositeType((int)character.Species) is not { } newType)
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.GenderRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: type {(int)character.Species}, job changed {GenderChangeRules.HasChangedJob(character)}, {worn} worn, {free} free slots", ct).ConfigureAwait(false);
            return;
        }
        if (!TryEnter())
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.GenderRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: another item operation is pending", ct).ConfigureAwait(false);
            return;
        }
        try
        {
            if (!await host.ChangeGender(newType, ct).ConfigureAwait(false))
            {
                await RefuseWithAsync((Opcode)SpecialItemFrames.GenderRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: the new type {newType} was not stored", ct).ConfigureAwait(false);
                return;
            }
            var left = Consume(item, definition);
            var playerId = _player.EntityId();
            await _player.SendToMap((Opcode)SpecialItemFrames.GenderSeen, SpecialItemFrames.GenderChangedFor(playerId, newType, newType), 0).ConfigureAwait(false);
            await _player.SendToMap((Opcode)SkillShopFrames.AppearanceChanged, SkillShopFrames.BareAppearance(playerId), 0).ConfigureAwait(false);
            await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   gender change {item.ItemIndex}: type {(int)character.Species} -> {newType}, {left} left");
        }
        finally
        {
            Leave();
        }
    }

    private async Task UseSkillResetAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, byte[] data, CancellationToken ct)
    {
        if (!SpecialItemFrames.TryReadWord(data, out var skillId))
        {
            await RefuseAsync($"item {item.ItemIndex}: no skill in the request", ct).ConfigureAwait(false);
            return;
        }
        var owned = host.OwnedSkills();
        if (SkillResetRules.IsSpecial(skillId)
            || SkillResetRules.IsPrerequisiteOfOwned(skillId, owned.Keys, host.CardOfSkill)
            || !owned.TryGetValue(skillId, out var level)
            || host.CardOfSkill(skillId) is not { } card)
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.SkillResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: skill {skillId} cannot be reset", ct).ConfigureAwait(false);
            return;
        }
        if (!TryEnter())
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.SkillResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: another item operation is pending", ct).ConfigureAwait(false);
            return;
        }
        try
        {
            var refund = SkillResetRules.Refund(card, level, host.MasterTmPoint(skillId));
            if (!await host.ForgetSkill(skillId, refund, ct).ConfigureAwait(false))
            {
                await RefuseWithAsync((Opcode)SpecialItemFrames.SkillResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: skill {skillId} was not removed", ct).ConfigureAwait(false);
                return;
            }
            var left = Consume(item, definition);
            await SendAsync((Opcode)SpecialItemFrames.SkillReset, SpecialItemFrames.SkillResetFor((ushort)skillId, (ushort)refund), ct).ConfigureAwait(false);
            await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   skill reset {item.ItemIndex}: skill {skillId} level {level} removed, {refund} TM points back, {left} left");
        }
        finally
        {
            Leave();
        }
    }

    private static CharacterStatBlock StatBlockOf(StatManager stats, IReadOnlyList<short> points, int bonus) =>
        new(points, (ushort)bonus, stats.TmPoint, stats.TmLevel, (ushort)stats.Level, CharacterStatBlock.LoginMarkWhenUnset);

    private async Task UseStatResetAsync(SpecialItemHost host, Item item, ItemParamEntry definition, StatManager stats, ulong wireUid, byte[] data, CancellationToken ct)
    {
        if (data.Length < StatResetRules.PartialDataBytes)
        {
            await RefuseAsync($"item {item.ItemIndex}: the request carries {data.Length} of {StatResetRules.PartialDataBytes} bytes", ct).ConfigureAwait(false);
            return;
        }
        var take = data[..StatResetRules.PartialDataBytes].ToArray();
        var total = take.Sum(amount => (int)amount);
        if (total < StatResetRules.PartialMinimum || total > StatResetRules.PartialMaximum)
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.StatResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: {total} points asked", ct).ConfigureAwait(false);
            return;
        }
        if (!TryEnter())
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.StatResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: another item operation is pending", ct).ConfigureAwait(false);
            return;
        }
        try
        {
            if (!StatResetRules.TryPartial(take, host.StatPoints(), host.GrowthWeights(), stats.Level, out var reset, out var refunded))
            {
                await SendAsync((Opcode)SpecialItemFrames.StatResetRefused, Array.Empty<byte>(), ct).ConfigureAwait(false);
                await RefuseWithAsync((Opcode)SpecialItemFrames.StatResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: a stat would fall below its level baseline", ct).ConfigureAwait(false);
                return;
            }
            var bonus = host.BonusPoints() + refunded;
            if (!await host.ApplyStatPoints(reset, bonus, ct).ConfigureAwait(false))
            {
                await RefuseAsync($"item {item.ItemIndex}: the stat points were not stored", ct).ConfigureAwait(false);
                return;
            }
            var left = Consume(item, definition);
            await SendAsync((Opcode)SpecialItemFrames.StatReset, SpecialItemFrames.StatBlock(StatBlockOf(stats, reset, bonus)), ct).ConfigureAwait(false);
            await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   point back {item.ItemIndex}: {refunded} points returned, {bonus} bonus points, {left} left");
        }
        finally
        {
            Leave();
        }
    }

    private async Task UseStatResetAllAsync(SpecialItemHost host, Item item, ItemParamEntry definition, StatManager stats, ulong wireUid, CancellationToken ct)
    {
        if (!TryEnter())
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.StatResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: another item operation is pending", ct).ConfigureAwait(false);
            return;
        }
        try
        {
            if (!StatResetRules.TryAll(host.StatPoints(), host.GrowthWeights(), stats.Level, out var reset, out var refunded))
            {
                await SendAsync((Opcode)SpecialItemFrames.StatResetAllRefused, Array.Empty<byte>(), ct).ConfigureAwait(false);
                await RefuseWithAsync((Opcode)SpecialItemFrames.StatResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: no stat point to take back", ct).ConfigureAwait(false);
                return;
            }
            var bonus = host.BonusPoints() + refunded;
            if (!await host.ApplyStatPoints(reset, bonus, ct).ConfigureAwait(false))
            {
                await RefuseAsync($"item {item.ItemIndex}: the stat points were not stored", ct).ConfigureAwait(false);
                return;
            }
            var left = Consume(item, definition);
            await SendAsync((Opcode)SpecialItemFrames.StatResetAll, SpecialItemFrames.StatResetAllFor(StatBlockOf(stats, reset, bonus), (uint)refunded), ct).ConfigureAwait(false);
            await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   point back all {item.ItemIndex}: {refunded} points returned, {bonus} bonus points, {left} left");
        }
        finally
        {
            Leave();
        }
    }

    private async Task UseGrowthGraphResetAsync(SpecialItemHost host, Item item, ItemParamEntry definition, StatManager stats, ulong wireUid, byte[] data, CancellationToken ct)
    {
        var weights = data.Length >= StatResetRules.GrowthDataBytes ? data[..StatResetRules.GrowthDataBytes].ToArray() : [];
        if (!StatResetRules.ValidGrowthWeights(weights, host.GrowthGroup()))
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.GrowthResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: weights [{string.Join(",", weights)}] for group {host.GrowthGroup()}", ct).ConfigureAwait(false);
            return;
        }
        if (!TryEnter())
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.GrowthResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: another item operation is pending", ct).ConfigureAwait(false);
            return;
        }
        try
        {
            var points = StatResetRules.GrowthPoints(weights, stats.Level);
            var bonus = StatResetRules.GrowthBonus(stats.Level);
            if (!StatResetRules.GrowthConsistent(points, bonus, stats.Level))
            {
                await RefuseWithAsync((Opcode)SpecialItemFrames.GrowthResetRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: the points do not add up for level {stats.Level}", ct).ConfigureAwait(false);
                return;
            }
            if (!await host.ApplyGrowthGraph(weights, points, bonus, ct).ConfigureAwait(false))
            {
                await RefuseAsync($"item {item.ItemIndex}: the growth graph was not stored", ct).ConfigureAwait(false);
                return;
            }
            var left = Consume(item, definition);
            await SendAsync((Opcode)SpecialItemFrames.GrowthReset, SpecialItemFrames.GrowthResetFor(host.CharacterInfo(), StatBlockOf(stats, points, bonus)), ct).ConfigureAwait(false);
            await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   build reset {item.ItemIndex}: weights [{string.Join(",", weights)}], {bonus} bonus points, {left} left");
        }
        finally
        {
            Leave();
        }
    }

    private async Task UseStorageExpansionAsync(SpecialItemHost host, Item item, ItemParamEntry definition, ulong wireUid, CancellationToken ct)
    {
        if (!TryEnter())
        {
            await RefuseAsync($"item {item.ItemIndex}: another item operation is pending", ct).ConfigureAwait(false);
            return;
        }
        try
        {
            if (!StorageExpansionRules.Fits(host.WarehouseSlots()))
            {
                await RefuseWithAsync((Opcode)SpecialItemFrames.StorageRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: {host.WarehouseSlots()} storage slots cannot take {StorageExpansionRules.SlotsPerItem} more", ct).ConfigureAwait(false);
                return;
            }
            if (!await host.ExpandStorage(StorageExpansionRules.SlotsPerItem, ct).ConfigureAwait(false))
            {
                await RefuseWithAsync((Opcode)SpecialItemFrames.StorageRefused, Array.Empty<byte>(), $"item {item.ItemIndex}: the storage slots were not stored", ct).ConfigureAwait(false);
                return;
            }
            var left = Consume(item, definition);
            await AnswerAsync(definition, wireUid, ct).ConfigureAwait(false);
            await SendAsync((Opcode)SpecialItemFrames.StorageExpanded, SpecialItemFrames.StorageExpandedBy(StorageExpansionRules.SlotsPerItem), ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   storage permit {item.ItemIndex}: +{StorageExpansionRules.SlotsPerItem} slots, {left} left");
        }
        finally
        {
            Leave();
        }
    }

    private async Task UseRenameAsync(SpecialItemHost host, Item item, ItemParamEntry definition, byte[] data, CancellationToken ct)
    {
        if (!SpecialItemFrames.TryReadName(data, out var nameBytes))
        {
            await RefuseAsync($"item {item.ItemIndex}: the name field is {data.Length} bytes", ct).ConfigureAwait(false);
            return;
        }
        if (!CharacterNameRules.Accepts(nameBytes))
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.RenameRefused, SpecialItemFrames.Code(CharacterNameRules.InvalidNameCode), $"item {item.ItemIndex}: the name is not allowed", ct).ConfigureAwait(false);
            return;
        }
        if (!TryEnter()) return;
        try
        {
            var name = host.TextEncoding().GetString(nameBytes);
            var code = await host.Rename(name, item.ItemIndex, ct).ConfigureAwait(false);
            if (code != 0)
            {
                await SendAsync((Opcode)SpecialItemFrames.RenameRefused, SpecialItemFrames.Code(code), ct).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} {_remote()}   rename {item.ItemIndex} to '{name}' refused with code {code}");
                return;
            }
            var record = ConsumeWithRecord(item, definition);
            await SendAsync((Opcode)SpecialItemFrames.Renamed, SpecialItemFrames.RenamedWith(record), ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   rename {item.ItemIndex}: now '{name}'");
        }
        finally
        {
            Leave();
        }
    }

    private async Task UseMegaphoneAsync(SpecialItemHost host, Item item, ItemParamEntry definition, byte[] data, CancellationToken ct)
    {
        if (!SpecialItemFrames.TryReadMegaphone(data, out var request))
        {
            await RefuseAsync($"item {item.ItemIndex}: the request carries no name and text", ct).ConfigureAwait(false);
            return;
        }
        if (_player.Character() is not { } character
            || Held(character, request.WireMicrophoneUid) is not { } microphone
            || microphone.Definition.Effect != SpecialItemEffects.Microphone)
        {
            await RefuseWithAsync((Opcode)SpecialItemFrames.MegaphoneRefused, SpecialItemFrames.Code(SpecialItemFrames.MegaphoneNoMicrophone), $"item {item.ItemIndex}: no microphone with the request", ct).ConfigureAwait(false);
            return;
        }
        if (request.Text.Length > SpecialItemFrames.MegaphoneTextBytes)
        {
            await RefuseAsync($"item {item.ItemIndex}: the text is {request.Text.Length} bytes", ct).ConfigureAwait(false);
            return;
        }
        if (!TryEnter())
        {
            await RefuseAsync($"item {item.ItemIndex}: another item operation is pending", ct).ConfigureAwait(false);
            return;
        }
        try
        {
            var text = host.TextEncoding().GetString(request.Text);
            var shout = new MegaphoneShout(host.TextEncoding().GetString(request.Name), text, item.ItemIndex, definition.ObjectClass);
            if (await host.Shout(shout, ct).ConfigureAwait(false) is not { } queued)
            {
                await SendAsync((Opcode)SpecialItemFrames.MegaphoneRefused, SpecialItemFrames.Code(0), ct).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} {_remote()}   megaphone {item.ItemIndex} was not broadcast");
                return;
            }
            var record = ConsumeWithRecord(item, definition);
            await SendAsync((Opcode)SpecialItemFrames.MegaphoneSent, SpecialItemFrames.MegaphoneSentWith(record, queued), ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_remote()}   megaphone {item.ItemIndex}: '{text}' queued behind {queued}");
        }
        finally
        {
            Leave();
        }
    }
}
