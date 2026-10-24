using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

internal sealed record SpecialItemSession(
    InGameSessionState Player,
    Func<Character?> Reload,
    Func<CancellationToken, Task> ReleaseEquipment,
    Func<IReadOnlyList<short>, int, string, bool> StorePoints,
    Func<int, CancellationToken, Task> SkillForgotten,
    Action<string> FlushStats);

internal sealed class SpecialItemController
{
    public const ushort MegaphoneStyle = 0;
    public const uint MegaphoneNothingQueued = 0;

    private readonly IAccountStore _accounts;
    private readonly ISkillStore _skills;
    private readonly ItemManager _itemManager;
    private readonly IItemParamLookup _catalog;
    private readonly SystemServerLink _systemLink;
    private readonly Func<ushort, int> _mapOption;
    private readonly Func<SpecialPortTables> _ports;
    private readonly Func<int, SkillCardEntry?> _cardOfSkill;
    private readonly Func<int, int> _masterTmPoint;
    private readonly Encoding _text;
    private readonly string _logTag;

    public SpecialItemController(
        IAccountStore accounts,
        ISkillStore skills,
        ItemManager itemManager,
        IItemParamLookup catalog,
        SystemServerLink systemLink,
        Func<ushort, int> mapOption,
        Func<SpecialPortTables> ports,
        Func<int, SkillCardEntry?> cardOfSkill,
        Func<int, int> masterTmPoint,
        Encoding text,
        string logTag)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _systemLink = systemLink ?? throw new ArgumentNullException(nameof(systemLink));
        _mapOption = mapOption ?? throw new ArgumentNullException(nameof(mapOption));
        _ports = ports ?? throw new ArgumentNullException(nameof(ports));
        _cardOfSkill = cardOfSkill ?? throw new ArgumentNullException(nameof(cardOfSkill));
        _masterTmPoint = masterTmPoint ?? throw new ArgumentNullException(nameof(masterTmPoint));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public SpecialItemHost HostFor(SpecialItemSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var player = session.Player;
        Character Held() => player.Character ?? throw new InvalidOperationException("The session has no character.");
        return new SpecialItemHost
        {
            InNormalForm = () => IsNormalForm(player),
            Idle = () => IsIdle(player),
            MapId = () => player.MapId,
            ReturnMap = () => RestartArrival.NormalizeReturnMap(Held().Home),
            MapOption = _mapOption,
            TextEncoding = () => _text,
            Ports = _ports,
            StatPoints = () => StatusValueChart.PointsOf(Held()),
            GrowthWeights = () => new short[] { Held().PtsP, Held().PtsM, Held().PtsS, Held().PtsC },
            BonusPoints = () => Held().AttrPoints,
            GrowthGroup = () => StatGrowth.TypeIndexOf(Held().FType),
            WarehouseSlots = () => Held().WarehouseSlots,
            OwnedSkills = () => player.Skills,
            CardOfSkill = _cardOfSkill,
            MasterTmPoint = _masterTmPoint,
            CharacterInfo = () => CharacterInfoOf(Held()),
            Warp = (zone, position, ct) => player.RequestZoneChange is { } request
                ? request(new ZoneChangeRequest(zone, PortRules.ChangeZoneType, null, PortRules.WarpDelay, position), ct)
                : Task.CompletedTask,
            DyeHair = (color, _) => Task.FromResult(DyeHair(session, color)),
            ChangeGender = (newType, ct) => ChangeGenderAsync(session, newType, ct),
            ApplyStatPoints = (points, bonus, _) => Task.FromResult(session.StorePoints(points, bonus, "stat-reset")),
            ApplyGrowthGraph = (weights, points, bonus, _) => Task.FromResult(ApplyGrowthGraph(session, weights, points, bonus)),
            ForgetSkill = (skillId, refund, ct) => ForgetSkillAsync(session, skillId, refund, ct),
            ExpandStorage = (slots, _) => Task.FromResult(ExpandStorage(session, slots)),
            Rename = (name, itemIndex, ct) => RenameAsync(session, name, itemIndex, ct),
            Shout = (text, ct) => ShoutAsync(session, text, ct),
        };
    }

    private static bool IsNormalForm(InGameSessionState player) =>
        !player.Disguise.Disguised
        && !player.Effects.Snapshot().Any(held => held.IsApplied && held.Kind.Morph != MorphRule.None);

    private static bool IsIdle(InGameSessionState player) =>
        !player.IsDead
        && player.Quests is not { IsEngaged: true }
        && player.Trade is null
        && player.Stall is null
        && player.Upgrade == UpgradeWindow.None
        && !player.UpgradeBusy
        && player.Recycle is null
        && player.Dig is null
        && player.Cast is not { IsUnderWay: true };

    private static byte[] CharacterInfoOf(Character character) =>
        new GameCharDataPacket
        {
            CharacterId = character.CharacterId,
            UserId = character.OwnerAccountId,
            Slot = character.Slot,
            Name = character.Name,
            Species = (ushort)character.Species,
            FTypeIndex = (byte)StatGrowth.TypeIndexOf(character.FType),
            StatA = character.PtsP,
            StatB = character.PtsM,
            StatC = character.PtsS,
            StatD = character.PtsC,
            Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        }.EncodePayload();

    private bool DyeHair(SpecialItemSession session, byte color)
    {
        var character = session.Player.Character;
        if (character is null) return false;
        var slot = HairDyeRules.SlotOf(character);
        if (_accounts.EditCharacter(character.CharacterId, CharacterEdit.Hair((byte)slot, color)) != CharacterEditCodes.Done) return false;
        session.Reload();
        Log(session.Player, $"hair slot {slot} dyed to {color}");
        return true;
    }

    private async Task<bool> ChangeGenderAsync(SpecialItemSession session, byte newType, CancellationToken ct)
    {
        var player = session.Player;
        if (player.Character is not { } character) return false;
        var species = (Species)newType;
        if (_accounts.EditCharacter(character.CharacterId, CharacterEdit.Gender(species)) != CharacterEditCodes.Done) return false;
        session.Reload();
        player.PlayerCharacterType = newType;
        await session.ReleaseEquipment(ct).ConfigureAwait(false);
        var operations = new List<BagOperation>(GenderChangeRules.GrantedItems);
        foreach (var itemIndex in new int[] { SpeciesDefaults.EarItemIndexForSpecies(species), SpeciesDefaults.TailItemIndexForSpecies(species) })
        {
            var definition = _catalog.GetById(itemIndex);
            var granted = _itemManager.GrantRewardItem(character.CharacterId, itemIndex, 1, itemClassOverride: definition?.ObjectClass);
            operations.Add(new BagOperation(
                QuestResultFrames.Created, granted.ItemClass, (uint)itemIndex, BagItemIds.OnWire(granted.ItemUid), granted.Stack, granted.ItemSlot));
        }
        await player.SendFrameAsync(Opcode.S2C_ITEM_ADDED, QuestResultFrames.ItemOperations(operations), NextKey(), ct).ConfigureAwait(false);
        await _systemLink.SendAsync(
            SystemLinkFrames.CharacterTypeChanged, SystemLinkFrames.CharacterTypeChangedFor(character.CharacterId, newType, newType), ct).ConfigureAwait(false);
        Log(player, $"changed to type {newType}");
        return true;
    }

    private bool ApplyGrowthGraph(SpecialItemSession session, IReadOnlyList<byte> weights, IReadOnlyList<short> points, int bonus)
    {
        var character = session.Player.Character;
        if (character is null) return false;
        var edit = CharacterEdit.GrowthWeights(weights[0], weights[1], weights[2], weights[3]);
        if (_accounts.EditCharacter(character.CharacterId, edit) != CharacterEditCodes.Done) return false;
        session.Reload();
        return session.StorePoints(points, bonus, "growth-reset");
    }

    private async Task<bool> ForgetSkillAsync(SpecialItemSession session, int skillId, int refund, CancellationToken ct)
    {
        var player = session.Player;
        if (player.Character is not { } character || player.Stats is not { } stats) return false;
        if (!_skills.Delete(character.CharacterId, skillId)) return false;
        player.Skills = player.Skills.Where(held => held.Key != skillId).ToDictionary(held => held.Key, held => held.Value);
        player.Cooldowns.Stop(skillId);
        stats.AddTmPoints(refund);
        session.FlushStats("skill-reset");
        await session.SkillForgotten(skillId, ct).ConfigureAwait(false);
        Log(player, $"skill {skillId} forgotten, {refund} TM points returned");
        return true;
    }

    private bool ExpandStorage(SpecialItemSession session, int slots)
    {
        var character = session.Player.Character;
        if (character is null) return false;
        var total = (ushort)(character.WarehouseSlots + slots);
        if (_accounts.EditCharacter(character.CharacterId, CharacterEdit.Storage(total)) != CharacterEditCodes.Done) return false;
        session.Reload();
        Log(session.Player, $"storage now {total} slots");
        return true;
    }

    private async Task<uint> RenameAsync(SpecialItemSession session, string name, int itemIndex, CancellationToken ct)
    {
        var player = session.Player;
        if (player.Character is not { } character) return CharacterEditCodes.Failed;
        var oldName = character.Name;
        var code = _accounts.EditCharacter(character.CharacterId, CharacterEdit.Rename(name, itemIndex));
        if (code != CharacterEditCodes.Done) return code;
        session.Reload();
        await _systemLink.SendAsync(
            SystemLinkFrames.CharacterRenamed, SystemLinkFrames.CharacterRenamedFor(character.CharacterId, oldName, name, _text), ct).ConfigureAwait(false);
        Log(player, $"renamed from '{oldName}' to '{name}'");
        return CharacterEditCodes.Done;
    }

    private async Task<uint?> ShoutAsync(SpecialItemSession session, string text, CancellationToken ct)
    {
        if (session.Player.Character is not { } character) return null;
        var message = $"{character.Name} : {text}";
        await _systemLink.SendAsync(
            SystemLinkFrames.Megaphone, SystemLinkFrames.MegaphoneFor(character.CharacterId, MegaphoneStyle, message, _text), ct).ConfigureAwait(false);
        Log(session.Player, $"shouts '{message}'");
        return MegaphoneNothingQueued;
    }

    private static byte NextKey() => (byte)Random.Shared.Next(1, 256);

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [special item] 0x{player.PlayerEntityId:X4} {message}");
}
