using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

internal sealed record SentFrame(Opcode Opcode, byte[] Payload, ushort Block);

internal sealed record WarpCall(ushort Zone, (ushort X, ushort Y)? Position);

internal sealed class SpecialItemHarness
{
    public const ulong CharacterId = 77;
    public const ushort EntityId = 0x0123;
    public const int ItemId = 19000;
    public const int MicrophoneItemId = 19655;

    private readonly Dictionary<int, ItemParamEntry> _definitions = new();

    public SpecialItemHarness(Species species = Species.Lion, int level = 50, int bonusPoints = 0)
    {
        Character = new Character
        {
            CharacterId = CharacterId,
            OwnerAccountId = 5,
            Slot = 0,
            Name = "Tester",
            Species = species,
            FType = FType.Power,
            PtsP = 4,
            PtsM = 2,
            PtsS = 1,
            PtsC = 3,
            Level = (ushort)level,
            AttrPoints = (ushort)bonusPoints,
            TmPoint = 10,
            TmLevel = 5,
            WarehouseSlots = 80,
            Home = 33,
            MapId = 148,
        };
        Stats = new StatManager(Character);
    }

    public InMemoryItemStore Items { get; } = new();

    public List<SentFrame> Sent { get; } = [];

    public List<SentFrame> Area { get; } = [];

    public Character Character { get; set; }

    public StatManager Stats { get; }

    public SpecialHostState State { get; } = new();

    public Dictionary<int, PresentItemEntry> Presents { get; } = new();

    public Dictionary<int, BonusPresentEntry> Bonuses { get; } = new();

    public Dictionary<int, KeyboxEntry> Keyboxes { get; } = new();

    public ItemUseService Service { get; private set; } = null!;

    public SpecialItemHost Host { get; private set; } = null!;

    public void Define(ItemParamEntry definition) => _definitions[definition.Id] = definition;

    public ItemParamEntry DefineSpecial(int effect, int itemId = ItemId, int options = ItemParamEntry.UsableOption | SpecialItemEffects.CustomDataOption)
    {
        var definition = new ItemParamEntry { Id = itemId, Type = ItemParamEntry.UseItemType, Effect = effect, Options = options, Class = 1 };
        Define(definition);
        return definition;
    }

    public Item Give(int itemId = ItemId, ushort stack = 1, byte slotKind = SlotKind.Inventory, byte slot = 0) =>
        Items.GrantToCharacter(CharacterId, itemId, 1, slotKind, slot, stack);

    public static ulong WireOf(Item item) => BagItemIds.OnWire(item.ItemUid);

    public void Build()
    {
        var catalog = new InMemoryItemParamLookup(_definitions.Values);
        Host = new SpecialItemHost
        {
            InNormalForm = () => State.NormalForm,
            Idle = () => State.Idle,
            MapId = () => State.MapId,
            ReturnMap = () => State.ReturnMap,
            MapOption = map => State.MapOptions.TryGetValue(map, out var option) ? option : 0,
            TextEncoding = () => Encoding.ASCII,
            Ports = () => State.Ports,
            StatPoints = () => State.Points,
            GrowthWeights = () => State.Weights,
            BonusPoints = () => State.Bonus,
            GrowthGroup = () => State.GrowthGroup,
            WarehouseSlots = () => State.WarehouseSlots,
            OwnedSkills = () => State.Skills,
            CardOfSkill = skill => State.Cards.TryGetValue(skill, out var card) ? card : null,
            MasterTmPoint = skill => State.MasterTm.TryGetValue(skill, out var points) ? points : 0,
            CharacterInfo = () => State.CharacterInfo,
            Warp = (zone, position, _) =>
            {
                State.Warps.Add(new WarpCall(zone, position));
                return Task.CompletedTask;
            },
            DyeHair = (color, _) =>
            {
                State.DyedColors.Add(color);
                return Task.FromResult(State.StoreSucceeds);
            },
            ChangeGender = (type, _) =>
            {
                State.GenderTypes.Add(type);
                return Task.FromResult(State.StoreSucceeds);
            },
            ApplyStatPoints = (points, bonus, _) =>
            {
                State.AppliedPoints.Add((points.ToArray(), bonus));
                return Task.FromResult(State.StoreSucceeds);
            },
            ApplyGrowthGraph = (weights, points, bonus, _) =>
            {
                State.AppliedGrowth.Add((weights.ToArray(), points.ToArray(), bonus));
                return Task.FromResult(State.StoreSucceeds);
            },
            ForgetSkill = (skill, refund, _) =>
            {
                State.ForgottenSkills.Add((skill, refund));
                return Task.FromResult(State.StoreSucceeds);
            },
            ExpandStorage = (slots, _) =>
            {
                State.StorageGrowth.Add(slots);
                return Task.FromResult(State.StoreSucceeds);
            },
            Rename = (name, itemIndex, _) =>
            {
                State.Renames.Add((name, itemIndex));
                return Task.FromResult(State.RenameCode);
            },
            Shout = (text, _) =>
            {
                State.Shouts.Add(text);
                return Task.FromResult(State.ShoutQueue);
            },
        };
        var player = new ItemUsePlayer
        {
            Character = () => Character,
            Stats = () => Stats,
            EntityId = () => EntityId,
            MaxHp = () => 1000,
            MaxMp = () => 1000,
            IsDead = () => State.Dead,
            SendToMap = (opcode, payload, block) =>
            {
                Area.Add(new SentFrame(opcode, payload, block));
                return Task.CompletedTask;
            },
            MaxWeight = () => 1000,
            Special = () => Host,
            BonusPresent = box => Bonuses.TryGetValue(box, out var bonus) ? bonus : null,
            Keybox = box => Keyboxes.TryGetValue(box, out var keybox) ? keybox : null,
            ExpPotion = (potion, life) =>
            {
                State.Potions.Add((potion, life));
                return State.PotionKnown;
            },
            Imprint = imprint =>
            {
                State.Imprints.Add(imprint);
                return State.ImprintKnown;
            },
        };
        Service = new ItemUseService(
            () => "test",
            (opcode, payload, _, block, _) =>
            {
                Sent.Add(new SentFrame(opcode, payload, block));
                return Task.CompletedTask;
            },
            Items,
            catalog,
            player,
            new CommandBlockIds(),
            new Random(1),
            "[test]",
            Presents,
            new ItemManager(Items, catalog));
    }

    public Task UseWithDataAsync(Item item, byte[] data)
    {
        var payload = new byte[sizeof(ulong) + sizeof(ushort) + data.Length];
        BitConverter.GetBytes(WireOf(item)).CopyTo(payload, 0);
        BitConverter.GetBytes((ushort)data.Length).CopyTo(payload, sizeof(ulong));
        data.CopyTo(payload, sizeof(ulong) + sizeof(ushort));
        return Service.UseWithDataAsync(payload, CancellationToken.None);
    }

    public Task UseAsync(Item item) => Service.UseAsync(BitConverter.GetBytes(WireOf(item)), CancellationToken.None);

    public bool InBag(Item item) => Items.FindByUid(item.ItemUid) is not null;

    public int StackOf(Item item) => Items.FindByUid(item.ItemUid)?.Stack ?? 0;

    public IReadOnlyList<ushort> SentWords => Sent.Select(frame => (ushort)frame.Opcode).ToArray();

    public IReadOnlyList<ushort> AreaWords => Area.Select(frame => (ushort)frame.Opcode).ToArray();
}

internal sealed class SpecialHostState
{
    public bool NormalForm { get; set; } = true;

    public bool Idle { get; set; } = true;

    public bool Dead { get; set; }

    public bool StoreSucceeds { get; set; } = true;

    public ushort MapId { get; set; } = 148;

    public ushort ReturnMap { get; set; } = 33;

    public Dictionary<ushort, int> MapOptions { get; } = new();

    public SpecialPortTables Ports { get; set; } = SpecialPortTables.Empty;

    public IReadOnlyList<short> Points { get; set; } = new short[12];

    public IReadOnlyList<short> Weights { get; set; } = new short[] { 4, 2, 1, 3 };

    public int Bonus { get; set; }

    public int GrowthGroup { get; set; }

    public int WarehouseSlots { get; set; } = 80;

    public IReadOnlyDictionary<int, int> Skills { get; set; } = new Dictionary<int, int>();

    public Dictionary<int, SkillCardEntry> Cards { get; } = new();

    public Dictionary<int, int> MasterTm { get; } = new();

    public byte[] CharacterInfo { get; set; } = new byte[69];

    public uint RenameCode { get; set; }

    public uint? ShoutQueue { get; set; } = 0;

    public List<WarpCall> Warps { get; } = [];

    public List<byte> DyedColors { get; } = [];

    public List<byte> GenderTypes { get; } = [];

    public List<(short[] Points, int Bonus)> AppliedPoints { get; } = [];

    public List<(byte[] Weights, short[] Points, int Bonus)> AppliedGrowth { get; } = [];

    public List<(int Skill, int Refund)> ForgottenSkills { get; } = [];

    public List<int> StorageGrowth { get; } = [];

    public List<(string Name, int ItemIndex)> Renames { get; } = [];

    public List<string> Shouts { get; } = [];

    public bool PotionKnown { get; set; } = true;

    public bool ImprintKnown { get; set; } = true;

    public List<(int Item, int Life)> Potions { get; } = [];

    public List<int> Imprints { get; } = [];
}
