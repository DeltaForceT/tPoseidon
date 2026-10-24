using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

internal sealed class TradeSide
{
    public TradeSide(InGameSessionState player)
    {
        Player = player;
    }

    public InGameSessionState Player { get; }
    public bool Ready { get; set; }
    public uint Money { get; set; }
    public TradeOfferSlot[] Slots { get; } = new TradeOfferSlot[TradeFrames.SlotsPerSide];
    public bool Checked { get; set; }
    public bool Locked { get; set; }

    public IEnumerable<TradeOfferSlot> Offered => Slots.Where(slot => !slot.IsEmpty);
}

internal sealed class TradeSession
{
    public TradeSession(InGameSessionState inviter, InGameSessionState invitee)
    {
        Inviter = new TradeSide(inviter);
        Invitee = new TradeSide(invitee);
    }

    public object Gate { get; } = new();
    public TradeSide Inviter { get; }
    public TradeSide Invitee { get; }
    public bool Joined { get; set; }
    public TradeSide? FirstChecker { get; set; }
    public bool Committing { get; set; }

    public TradeSide SideOf(InGameSessionState player) => ReferenceEquals(Inviter.Player, player) ? Inviter : Invitee;

    public TradeSide OppositeOf(InGameSessionState player) => ReferenceEquals(Inviter.Player, player) ? Invitee : Inviter;
}

internal sealed class TradeController
{
    public static readonly IReadOnlySet<ushort> RefusingZones = new HashSet<ushort> { 0x2F, 0xDA };
    public const int NotExchangeableOption = 0x400;
    public const int PropertyOptionsClass = 2;

    private readonly MasterTrade _master;
    private readonly IItemStore _items;
    private readonly tPoseidon.Core.LibConfig.IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly Encoding _text;
    private readonly Func<ushort, ushort, InGameSessionState?> _playerOnMap;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public TradeController(
        MasterTrade master, IItemStore items, tPoseidon.Core.LibConfig.IItemParamLookup catalog, IItemPropertyStore properties,
        Encoding text, Func<ushort, ushort, InGameSessionState?> playerOnMap, int bagCapacity, string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _playerOnMap = playerOnMap ?? throw new ArgumentNullException(nameof(playerOnMap));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task InviteAsync(InGameSessionState self, ReadOnlyMemory<byte> body)
    {
        if (self.Character is not { } character || !TradeFrames.TryReadInvite(body.Span, out var targetId)) return;
        var target = _playerOnMap(self.MapId, targetId);
        if (target is null || ReferenceEquals(target, self) || target.Trade is not null || target.IsDead)
        {
            await SendAsync(self, Opcode.TRADE_UNAVAILABLE).ConfigureAwait(false);
            return;
        }
        if (self.Trade is not null)
        {
            Console.WriteLine($"{_logTag} [trade] 0x{self.PlayerEntityId:X4} invites while trading: old trade cancelled");
            await CloseAsync(self).ConfigureAwait(false);
            return;
        }
        if (RefusingZones.Contains(self.MapId))
        {
            await SendAsync(self, Opcode.TRADE_UNAVAILABLE).ConfigureAwait(false);
            return;
        }
        var session = new TradeSession(self, target);
        self.Trade = session;
        target.Trade = session;
        Console.WriteLine($"{_logTag} [trade] 0x{self.PlayerEntityId:X4} invites 0x{target.PlayerEntityId:X4}");
        await SendAsync(target, Opcode.TRADE_INVITE, TradeFrames.Invite(self.PlayerEntityId, character.Name, _text)).ConfigureAwait(false);
    }

    public async Task AcceptAsync(InGameSessionState self)
    {
        if (self.Trade is not { } session || !ReferenceEquals(session.Invitee.Player, self) || session.Joined)
        {
            await SendAsync(self, Opcode.TRADE_CLOSED).ConfigureAwait(false);
            return;
        }
        lock (session.Gate) session.Joined = true;
        Console.WriteLine($"{_logTag} [trade] 0x{self.PlayerEntityId:X4} joins 0x{session.Inviter.Player.PlayerEntityId:X4}");
        await SendAsync(session.Inviter.Player, Opcode.TRADE_JOINED).ConfigureAwait(false);
        await SendAsync(self, Opcode.TRADE_OPENED).ConfigureAwait(false);
    }

    public Task DeclineAsync(InGameSessionState self) => CloseAsync(self);

    public Task CancelAsync(InGameSessionState self) => CloseAsync(self);

    public async Task CloseAsync(InGameSessionState self)
    {
        if (self.Trade is not { } session)
        {
            await SendAsync(self, Opcode.TRADE_CLOSED).ConfigureAwait(false);
            return;
        }
        var opposite = session.OppositeOf(self).Player;
        Detach(session);
        Console.WriteLine($"{_logTag} [trade] 0x{self.PlayerEntityId:X4} closes the trade with 0x{opposite.PlayerEntityId:X4}");
        await SendAsync(self, Opcode.TRADE_CLOSED).ConfigureAwait(false);
        await SendAsync(opposite, Opcode.TRADE_CLOSED).ConfigureAwait(false);
    }

    public async Task LeaveAsync(InGameSessionState self)
    {
        if (self.Trade is not { } session) return;
        var opposite = session.OppositeOf(self).Player;
        Detach(session);
        await SendAsync(opposite, Opcode.TRADE_CLOSED).ConfigureAwait(false);
    }

    public async Task UpdateAsync(InGameSessionState self, ReadOnlyMemory<byte> body)
    {
        if (self.Trade is not { Joined: true } session || self.Character is not { } character || self.Stats is not { } stats) return;
        if (!TradeFrames.TryReadUpdate(body.Span, out var ready, out var money, out var slots))
        {
            await CloseAsync(self).ConfigureAwait(false);
            return;
        }
        var own = session.SideOf(self);
        var opposite = session.OppositeOf(self);
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        ushort code;
        lock (session.Gate)
        {
            code = own.Checked ? TradeFrames.CodeAlreadyChecked
                : !slots.Where(slot => !slot.IsEmpty).All(slot => Holds(bag, slot)) ? TradeFrames.CodeBagMismatch
                : slots.Where(slot => !slot.IsEmpty).Select(slot => slot.WireUid).Distinct().Count() != slots.Count(slot => !slot.IsEmpty) ? TradeFrames.CodeBagMismatch
                : !slots.Where(slot => !slot.IsEmpty).All(slot => Exchangeable(bag, slot)) ? TradeFrames.CodeNotExchangeable
                : money > stats.Money || money > TradeRules.MaxOfferedMoney ? TradeFrames.CodeMoneyInvalid
                : (long)(opposite.Player.Stats?.Money ?? 0) + money >= TradeRules.OppositeMoneyCap ? TradeFrames.CodeOppositeMoneyOver
                : TradeFrames.CodeOk;
            if (code == TradeFrames.CodeOk)
            {
                own.Ready = ready;
                own.Money = money;
                for (var index = 0; index < TradeFrames.SlotsPerSide; index++) own.Slots[index] = index < slots.Count ? slots[index] : default;
            }
        }
        if (code is not (TradeFrames.CodeOk or TradeFrames.CodeMoneyInvalid or TradeFrames.CodeOppositeMoneyOver))
        {
            Console.WriteLine($"{_logTag} [trade] 0x{self.PlayerEntityId:X4} update refused with code {code}");
            await CloseAsync(self).ConfigureAwait(false);
            return;
        }
        await SendUpdateAsync(session, own, code).ConfigureAwait(false);
    }

    public async Task CheckAsync(InGameSessionState self, ReadOnlyMemory<byte> body)
    {
        if (self.Trade is not { Joined: true } session || !TradeFrames.TryReadFlag(body.Span, out var isChecked)) return;
        var own = session.SideOf(self);
        var opposite = session.OppositeOf(self);
        lock (session.Gate)
        {
            own.Checked = isChecked;
            if (isChecked)
            {
                session.FirstChecker ??= own;
            }
            else
            {
                opposite.Checked = false;
                session.FirstChecker = null;
            }
        }
        foreach (var (recipient, partner) in new[] { (own, opposite), (opposite, own) })
        {
            await SendAsync(recipient.Player, Opcode.TRADE_PARTNER_CHECK,
                TradeFrames.CheckState(partner.Checked, ReferenceEquals(session.FirstChecker, partner))).ConfigureAwait(false);
            await SendAsync(recipient.Player, Opcode.TRADE_CHECK,
                TradeFrames.CheckState(recipient.Checked, ReferenceEquals(session.FirstChecker, recipient))).ConfigureAwait(false);
        }
    }

    public async Task ConfirmAsync(InGameSessionState self)
    {
        if (self.Trade is not { Joined: true } session) return;
        if (!ReferenceEquals(session.FirstChecker, session.SideOf(self)))
        {
            await CloseAsync(self).ConfigureAwait(false);
            return;
        }
        await SendAsync(self, Opcode.TRADE_CONFIRM).ConfigureAwait(false);
        await SendAsync(session.OppositeOf(self).Player, Opcode.TRADE_AWAIT_DECISION).ConfigureAwait(false);
    }

    public async Task LockAsync(InGameSessionState self)
    {
        if (self.Trade is not { Joined: true } session) return;
        lock (session.Gate) session.SideOf(self).Locked = true;
        await SendAsync(session.OppositeOf(self).Player, Opcode.TRADE_PARTNER_LOCKED).ConfigureAwait(false);
    }

    public Task ConfirmCancelAsync(InGameSessionState self) => UnlockAsync(self, Opcode.TRADE_EDIT_AGAIN);

    public Task FinalCancelAsync(InGameSessionState self) => UnlockAsync(self, Opcode.TRADE_FINAL_EDIT_AGAIN);

    public async Task ChatAsync(InGameSessionState self, ReadOnlyMemory<byte> body)
    {
        if (self.Trade is not { Joined: true } session) return;
        await SendAsync(session.OppositeOf(self).Player, Opcode.TRADE_CHAT, body.ToArray()).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(InGameSessionState self)
    {
        if (self.Trade is not { Joined: true } session || self.Character is not { } character) return;
        var own = session.SideOf(self);
        var opposite = session.OppositeOf(self);
        bool commit;
        lock (session.Gate)
        {
            if (own.Locked || session.Committing) return;
            own.Locked = true;
            commit = opposite.Locked && own.Checked && opposite.Checked && ReferenceEquals(session.FirstChecker, own);
            if (commit) session.Committing = true;
        }
        if (!commit || opposite.Player.Character is not { } partner)
        {
            Console.WriteLine($"{_logTag} [trade] 0x{self.PlayerEntityId:X4} executes before both sides locked and checked: cancelled");
            await CloseAsync(self).ConfigureAwait(false);
            return;
        }
        var result = await _master.TradeAsync(OfferOf(character.CharacterId, own), OfferOf(partner.CharacterId, opposite), _bagCapacity, default).ConfigureAwait(false);
        if (!result.Succeeded || result.First is not { } mine || result.Second is not { } theirs)
        {
            Console.WriteLine($"{_logTag} [trade] 0x{self.PlayerEntityId:X4} with 0x{opposite.Player.PlayerEntityId:X4}: store refused with code {result.Error}");
            await CloseAsync(self).ConfigureAwait(false);
            return;
        }
        Detach(session);
        Settle(self, mine);
        Settle(opposite.Player, theirs);
        Console.WriteLine($"{_logTag} [trade] 0x{self.PlayerEntityId:X4} and 0x{opposite.Player.PlayerEntityId:X4} traded: {mine.Changes.Count} / {theirs.Changes.Count} item changes, money {mine.Money} / {theirs.Money}");
        await SendAsync(self, Opcode.TRADE_COMPLETED, TradeFrames.Settled(mine.Money, Operations(mine))).ConfigureAwait(false);
        await SendAsync(opposite.Player, Opcode.TRADE_RECEIVED, TradeFrames.Settled(theirs.Money, Operations(theirs))).ConfigureAwait(false);
    }

    private async Task UnlockAsync(InGameSessionState self, Opcode reply)
    {
        if (self.Trade is not { Joined: true } session) return;
        lock (session.Gate)
        {
            session.Inviter.Locked = false;
            session.Invitee.Locked = false;
        }
        await SendAsync(self, reply).ConfigureAwait(false);
        await SendAsync(session.OppositeOf(self).Player, reply).ConfigureAwait(false);
    }

    private async Task SendUpdateAsync(TradeSession session, TradeSide changed, ushort code)
    {
        var opposite = ReferenceEquals(changed, session.Inviter) ? session.Invitee : session.Inviter;
        var changedView = View(changed);
        var oppositeView = View(opposite);
        var changedExcess = Excess(changed, opposite);
        var oppositeExcess = Excess(opposite, changed);
        await SendAsync(changed.Player, Opcode.TRADE_UPDATE, TradeFrames.Update(changedView, changedExcess, oppositeView, oppositeExcess, code)).ConfigureAwait(false);
        await SendAsync(opposite.Player, Opcode.TRADE_UPDATE, TradeFrames.Update(oppositeView, oppositeExcess, changedView, changedExcess, TradeFrames.CodeOk)).ConfigureAwait(false);
    }

    private TradeRecordView View(TradeSide side)
    {
        var bag = side.Player.Character is { } owner ? _items.GetForCharacterBySlotKind(owner.CharacterId, SlotKind.Inventory) : Array.Empty<Item>();
        var views = side.Slots.Select(slot =>
        {
            if (slot.IsEmpty) return (TradeItemView?)null;
            var stored = BagItemIds.Stored(slot.WireUid);
            if (bag.FirstOrDefault(item => item.ItemUid == stored) is not { } item) return null;
            return new TradeItemView(item.ItemClass, (uint)item.ItemIndex, BagItemIds.OnWire(item.ItemUid), (ushort)Math.Min(slot.Count, ushort.MaxValue),
                (byte)item.RefineType, item.RefineLevel, item.RefineState, item.ItemSlot);
        }).ToList();
        return new TradeRecordView(side.Ready, side.Money, views);
    }

    private ushort Excess(TradeSide side, TradeSide from)
    {
        if (side.Player.Character is not { } owner) return 0;
        var bag = _items.GetForCharacterBySlotKind(owner.CharacterId, SlotKind.Inventory);
        var leaving = side.Offered.Count(slot => bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(slot.WireUid)) is { } item
            && (item.ItemClass != TradeRules.CommonClass || slot.Count >= item.Stack));
        var arriving = from.Offered.Count();
        return (ushort)Math.Clamp(bag.Count - leaving + arriving - _bagCapacity, 0, ushort.MaxValue);
    }

    private bool Exchangeable(IReadOnlyList<Item> bag, TradeOfferSlot slot)
    {
        var stored = BagItemIds.Stored(slot.WireUid);
        return bag.FirstOrDefault(item => item.ItemUid == stored) is { } item && IsExchangeable(item);
    }

    public bool IsExchangeable(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_catalog.GetById(item.ItemIndex) is not { } definition) return false;
        var options = definition.Class == PropertyOptionsClass
            ? tPoseidon.Core.Game.Items.ItemComposingRules.Options(definition, _properties.ForItem(item.ItemUid))
            : definition.Options;
        return (options & NotExchangeableOption) == 0;
    }

    private static bool Holds(IReadOnlyList<Item> bag, TradeOfferSlot slot)
    {
        var stored = BagItemIds.Stored(slot.WireUid);
        return bag.FirstOrDefault(item => item.ItemUid == stored) is { } item
            && (uint)item.ItemIndex == slot.ItemId
            && slot.Count > 0
            && (item.ItemClass == TradeRules.CommonClass ? slot.Count <= item.Stack : slot.Count == 1);
    }

    private static TradeOffer OfferOf(ulong characterId, TradeSide side) => new(
        characterId,
        side.Money,
        side.Offered.Select(slot => new TradeSlot(BagItemIds.Stored(slot.WireUid), (int)slot.ItemId, (ushort)Math.Min(slot.Count, ushort.MaxValue))).ToList());

    private void Settle(InGameSessionState player, TradeSideResult result)
    {
        if (player.Stats is { } stats) stats.GrantMoney(result.Money - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.Trade);
        foreach (var change in result.Changes)
        {
            if (change.Kind == TradeChangeKind.Removed)
            {
                player.TimedItems.Forget(change.ItemUid);
            }
            else if (change.Kind == TradeChangeKind.Created && _items.FindByUid(change.ItemUid) is { } arrived && arrived.BreakedAt > Item.NeverBreaks)
            {
                player.TimedItems.Track(new tPoseidon.Core.Game.Items.TimedItem(arrived.ItemUid, arrived.ItemIndex, arrived.BreakedAt));
            }
        }
    }

    private static List<BagOperation> Operations(TradeSideResult result) => result.Changes
        .Select(change => new BagOperation(
            change.Kind switch
            {
                TradeChangeKind.StackChanged => QuestResultFrames.StackChanged,
                TradeChangeKind.Removed => QuestResultFrames.Removed,
                _ => QuestResultFrames.Created,
            },
            change.ItemClass, (uint)change.ItemIndex, BagItemIds.OnWire(change.ItemUid), change.Count, change.MixSlot))
        .ToList();

    private static void Detach(TradeSession session)
    {
        if (ReferenceEquals(session.Inviter.Player.Trade, session)) session.Inviter.Player.Trade = null;
        if (ReferenceEquals(session.Invitee.Player.Trade, session)) session.Invitee.Player.Trade = null;
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode) => SendAsync(player, opcode, Array.Empty<byte>());

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
