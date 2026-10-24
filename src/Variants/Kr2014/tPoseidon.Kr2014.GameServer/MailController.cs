using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

internal sealed class MailState
{
    public Dictionary<uint, PostListRow> Box { get; } = new();
    public Dictionary<uint, int> Money { get; } = new();
    public int Busy;
}

internal static class MailError
{
    public const uint Pending = 1;
    public const uint OtherWork = 2;
    public const uint NotFound = 4;
    public const uint NoSuchItem = 5;
    public const uint CannotAttach = 6;
    public const uint ContentTooLong = 8;
    public const uint NameTooLong = 9;
    public const uint TooManyItems = 0x0A;
    public const uint NotEnoughMoney = 0x0B;
    public const uint WrongType = 0x0D;
    public const uint BagFull = 0x0F;
    public const uint WrongState = 0x10;
    public const uint BelowMinimum = 0x1C;
}

internal sealed class MailController
{
    public const int RecipientLength = 0x20;
    public const int TitleLength = 0x21;
    public const int ContentLength = 0x12D;
    public const int NotTradableOptions = 0x200 | 0x400;
    public const int FeeRateDivisor = 10000;
    public const int MinimumFeeRow = 5;
    public const int GiftFeeRow = 3;
    public const int PaymentFeeRow = 4;
    public const byte ListPageRows = PostRules.ListRows;

    private readonly MasterPosts _master;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IReadOnlyDictionary<int, int> _setup;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public MailController(MasterPosts master, IItemStore items, IItemParamLookup catalog, IReadOnlyDictionary<int, int> setup, Encoding text, string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public int Setup(int row) => _setup.TryGetValue(row, out var value) ? (ushort)value : 0;

    public int Fee(byte subType, int money, IEnumerable<(int Value, int Count)> items)
    {
        var rate = subType switch
        {
            PostRules.GiftLetter => Setup(GiftFeeRow) / (double)FeeRateDivisor,
            PostRules.PaymentRequest or PostRules.Payment => Setup(PaymentFeeRow) / (double)FeeRateDivisor,
            _ => 0.0,
        };
        long fee = 0;
        if (subType == PostRules.GiftLetter)
        {
            foreach (var (value, count) in items) fee += (long)Math.Truncate(value * (double)count * rate);
        }
        fee += (long)Math.Truncate(money * rate);
        var minimum = Setup(MinimumFeeRow);
        if (fee <= minimum) fee = minimum;
        return (int)Math.Min(fee, int.MaxValue);
    }

    public async Task SendLevelUpGiftsAsync(InGameSessionState player, Character character, int firstLevel, int lastLevel, IReadOnlyList<LevelUpGiftEntry> gifts)
    {
        var characterType = FriendRules.CharacterTypeOf(character);
        var sent = 0;
        for (var level = firstLevel; level <= lastLevel; level++)
        {
            foreach (var gift in gifts.Where(g => g.Fits(level, characterType)))
            {
                var items = gift.Items.Select(item => new GiftItem(
                    item.ItemId,
                    _catalog.GetById(item.ItemId)?.ObjectClass ?? 1,
                    (short)Math.Clamp(item.Count, short.MinValue, short.MaxValue),
                    0)).ToList();
                var result = await _master.SendGmGiftAsync(new GmGift(gift.Title, gift.Message, character.CharacterId, gift.Sender, items), default).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [mail] level {level} gift {gift.Id} for 0x{player.PlayerEntityId:X4}: {(result.Succeeded ? $"post {result.Value!.PostUid}" : $"error {result.Error}")}");
                if (result.Succeeded) sent++;
            }
        }
        if (sent > 0) await SyncAsync(player).ConfigureAwait(false);
    }

    public async Task<byte[]?> CountFrameAsync(ulong characterId) =>
        await _master.CountAsync(characterId, default).ConfigureAwait(false) is { } counts ? PostFrames.Counts(counts) : null;

    public async Task SyncAsync(InGameSessionState player)
    {
        if (player.Character is not { } character) return;
        if (Interlocked.CompareExchange(ref player.Mail.Busy, 1, 0) != 0) return;
        try
        {
            var cursor = player.Mail.Box.Count == 0 ? 0 : player.Mail.Box.Values.Max(row => row.DateSeq);
            if (await _master.SyncAsync(character.CharacterId, new PostSyncMessage(character.OwnerAccountId, cursor), default).ConfigureAwait(false) is not { } sync) return;
            var fresh = sync.Mail.Concat(sync.Gifts).ToList();
            if (fresh.Count == 0 && cursor != 0) return;
            foreach (var row in fresh) player.Mail.Box[row.PostUid] = row;
            await SendChunksAsync(player, PostFrames.Synced, PostFrames.SyncBody(sync, _text)).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref player.Mail.Busy, 0);
        }
    }

    public async Task OpenListAsync(InGameSessionState player, ReadOnlyMemory<byte> body)
    {
        if (player.Character is not { } character || !PostFrames.TryReadListType(body.Span, out var listType)) return;
        if (Interlocked.CompareExchange(ref player.Mail.Busy, 1, 0) != 0)
        {
            await SendAsync(player, PostFrames.ListFailed, PostFrames.Error(MailError.Pending)).ConfigureAwait(false);
            return;
        }
        try
        {
            var result = await _master.ListAsync(character.CharacterId, new PostListMessage(character.OwnerAccountId, listType, 0, ListPageRows), default).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await SendAsync(player, PostFrames.ListFailed, PostFrames.Error((uint)result.Error)).ConfigureAwait(false);
                return;
            }
            foreach (var row in result.Value!) player.Mail.Box[row.PostUid] = row;
            await SendChunksAsync(player, PostFrames.Listed, PostFrames.ListBody(listType, result.Value!, _text)).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref player.Mail.Busy, 0);
        }
    }

    public async Task OpenPostAsync(InGameSessionState player, ReadOnlyMemory<byte> body)
    {
        if (player.Character is not { } character || !PostFrames.TryReadOpen(body.Span, out var postType, out var postUid)) return;
        if (Interlocked.CompareExchange(ref player.Mail.Busy, 1, 0) != 0)
        {
            await SendAsync(player, PostFrames.OpenFailed, PostFrames.Error(MailError.Pending)).ConfigureAwait(false);
            return;
        }
        try
        {
            if (!player.Mail.Box.TryGetValue(postUid, out var row))
            {
                await SendAsync(player, PostFrames.OpenFailed, PostFrames.Error(MailError.NotFound)).ConfigureAwait(false);
                return;
            }
            var request = new PostDetailMessage(character.OwnerAccountId, postUid, postType);
            if (postType == PostRules.GiftBox)
            {
                var gift = await _master.GiftDetailAsync(character.CharacterId, request, default).ConfigureAwait(false);
                if (!gift.Succeeded)
                {
                    await SendAsync(player, PostFrames.OpenFailed, PostFrames.Error((uint)gift.Error)).ConfigureAwait(false);
                    return;
                }
                await SendAsync(player, PostFrames.Opened, PostFrames.GiftDetailBody(gift.Value!, _text)).ConfigureAwait(false);
                return;
            }
            var detail = await _master.DetailAsync(character.CharacterId, request, default).ConfigureAwait(false);
            if (!detail.Succeeded)
            {
                await SendAsync(player, PostFrames.OpenFailed, PostFrames.Error((uint)detail.Error)).ConfigureAwait(false);
                return;
            }
            player.Mail.Money[postUid] = detail.Value!.Money;
            if (row.PostSubType == PostRules.NormalLetter && row.PostState == PostRules.Unread)
            {
                player.Mail.Box[postUid] = row with { PostState = PostRules.Read };
            }
            await SendAsync(player, PostFrames.Opened, PostFrames.DetailBody(detail.Value, _text)).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref player.Mail.Busy, 0);
        }
    }

    public async Task TakeAsync(InGameSessionState player, ReadOnlyMemory<byte> body)
    {
        if (player.Character is not { } character || player.Stats is not { } stats || !PostFrames.TryReadPostUid(body.Span, out var postUid)) return;
        if (Interlocked.CompareExchange(ref player.Mail.Busy, 1, 0) != 0)
        {
            await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error(MailError.OtherWork)).ConfigureAwait(false);
            return;
        }
        try
        {
            if (!player.Mail.Box.TryGetValue(postUid, out var row))
            {
                await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error(MailError.NotFound)).ConfigureAwait(false);
                return;
            }
            if (row.PostType == PostRules.MailBox && row.PostSubType is PostRules.NormalLetter or 6)
            {
                await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error(MailError.WrongType)).ConfigureAwait(false);
                return;
            }
            if (row.PostState is PostRules.Read or PostRules.Deleted)
            {
                await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error(MailError.WrongState)).ConfigureAwait(false);
                return;
            }
            var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
            if (row.PostType == PostRules.GiftBox)
            {
                await TakeGiftAsync(player, character, row, bag).ConfigureAwait(false);
                return;
            }
            if (row.PostSubType == PostRules.PaymentRequest && row.PostState == PostRules.Unread
                && player.Mail.Money.TryGetValue(postUid, out var price) && stats.Money < price)
            {
                await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error(MailError.NotEnoughMoney)).ConfigureAwait(false);
                return;
            }
            if (bag.Count >= tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity)
            {
                await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error(MailError.BagFull)).ConfigureAwait(false);
                return;
            }
            var result = await _master.ReceiveAsync(new PostReceiveRequest(
                postUid, row.PostState, character.OwnerAccountId, character.CharacterId, character.Level, character.Name, string.Empty, string.Empty), default).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error((uint)result.Error)).ConfigureAwait(false);
                return;
            }
            var received = result.Value!;
            if (received.MoneyChange != 0)
            {
                stats.GrantMoney((int)Math.Clamp(received.MoneyChange, int.MinValue, int.MaxValue), tPoseidon.Core.Game.Stats.StatChangeSource.Item);
                await SendAsync(player, Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money)).ConfigureAwait(false);
            }
            var operations = received.Items.Select(item => new BagOperation(
                item.Change == PostItemChange.Stacked ? QuestResultFrames.StackChanged : QuestResultFrames.Created,
                item.ItemClass, (uint)item.ItemIndex, BagItemIds.OnWire(item.ItemUid), item.Total, item.ItemSlot)).ToList();
            player.Mail.Box[postUid] = row with { PostState = PostRules.Read };
            await SendAsync(player, PostFrames.Taken, PostFrames.TakenBody(postUid, operations)).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [mail] 0x{player.PlayerEntityId:X4} takes post {postUid}: {operations.Count} items, money {received.MoneyChange:+#;-#;0}");
        }
        finally
        {
            Volatile.Write(ref player.Mail.Busy, 0);
        }
        await SyncAsync(player).ConfigureAwait(false);
    }

    private async Task TakeGiftAsync(InGameSessionState player, Character character, PostListRow row, IReadOnlyList<Item> bag)
    {
        var detail = await _master.GiftDetailAsync(character.CharacterId, new PostDetailMessage(character.OwnerAccountId, row.PostUid, PostRules.GiftBox), default).ConfigureAwait(false);
        if (!detail.Succeeded)
        {
            await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error((uint)detail.Error)).ConfigureAwait(false);
            return;
        }
        if (bag.Count + detail.Value!.Items.Count > tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity)
        {
            await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error(MailError.BagFull)).ConfigureAwait(false);
            return;
        }
        var result = await _master.ReceiveGiftAsync(new GiftReceiveRequest(
            row.PostUid, character.OwnerAccountId, character.CharacterId, character.Level, Array.Empty<GiftItemOption>()), default).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await SendAsync(player, PostFrames.TakeFailed, PostFrames.Error((uint)result.Error)).ConfigureAwait(false);
            return;
        }
        var operations = result.Value!.Select(item => new BagOperation(
            QuestResultFrames.Created, item.ItemClass, (uint)item.ItemIndex, BagItemIds.OnWire(item.ItemUid), item.Count)).ToList();
        player.Mail.Box[row.PostUid] = row with { PostState = PostRules.Read };
        await SendAsync(player, PostFrames.Taken, PostFrames.TakenBody(row.PostUid, operations)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [mail] 0x{player.PlayerEntityId:X4} takes gift {row.PostUid}: {operations.Count} items");
    }

    public async Task SendPostAsync(InGameSessionState player, ReadOnlyMemory<byte> body)
    {
        if (player.Character is not { } character || player.Stats is not { } stats) return;
        if (!PostFrames.TryReadSend(body.Span, _text, out var form) || form is null) return;
        if (Interlocked.CompareExchange(ref player.Mail.Busy, 1, 0) != 0)
        {
            await SendAsync(player, PostFrames.SendFailed, PostFrames.Error(MailError.WrongState)).ConfigureAwait(false);
            return;
        }
        var charged = 0;
        try
        {
            var refusal = Validate(character, form, out var attached, out var fee);
            if (refusal is { } code)
            {
                await SendAsync(player, PostFrames.SendFailed, PostFrames.Error(code)).ConfigureAwait(false);
                return;
            }
            if (form.PostSubType == PostRules.GiftLetter)
            {
                if (stats.Money - form.Money < 0 || stats.Money - form.Money - fee < 0)
                {
                    await SendAsync(player, PostFrames.SendFailed, PostFrames.Error(MailError.NotEnoughMoney)).ConfigureAwait(false);
                    return;
                }
                charged = form.Money + fee;
                stats.GrantMoney(-charged, tPoseidon.Core.Game.Stats.StatChangeSource.Item);
                await SendAsync(player, Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money)).ConfigureAwait(false);
            }
            var result = await _master.SendAsync(new PostSendRequest(
                form.PostSubType, form.Title, form.Content, form.PostSubType == PostRules.NormalLetter ? 0 : fee, form.Money,
                character.OwnerAccountId, character.CharacterId, character.Name, character.Level, form.ToCharName,
                attached.Select(a => new PostSendItem(a.Held.ItemUid, a.Count)).ToList()), default).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                if (charged != 0)
                {
                    stats.GrantMoney(charged, tPoseidon.Core.Game.Stats.StatChangeSource.Item);
                    await SendAsync(player, Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money)).ConfigureAwait(false);
                }
                await SendAsync(player, PostFrames.SendFailed, PostFrames.Error((uint)result.Error)).ConfigureAwait(false);
                return;
            }
            var operations = attached.Select(a => a.Count < a.Held.WeightUnits
                ? new BagOperation(QuestResultFrames.StackChanged, a.Held.ItemClass, (uint)a.Held.ItemIndex, BagItemIds.OnWire(a.Held.ItemUid), (ushort)(a.Held.Stack - a.Count), a.Held.ItemSlot)
                : new BagOperation(QuestResultFrames.Removed, a.Held.ItemClass, (uint)a.Held.ItemIndex, BagItemIds.OnWire(a.Held.ItemUid), a.Held.Stack, a.Held.ItemSlot)).ToList();
            await SendAsync(player, PostFrames.Sent, PostFrames.SentBody(operations)).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [mail] 0x{player.PlayerEntityId:X4} sends post {result.Value!.PostUid} (sub type {form.PostSubType}) to '{form.ToCharName}': {attached.Count} items, money {form.Money}, fee {fee}");
        }
        finally
        {
            Volatile.Write(ref player.Mail.Busy, 0);
        }
        await SyncAsync(player).ConfigureAwait(false);
    }

    private uint? Validate(Character character, PostSendForm form, out List<(Item Held, ushort Count)> attached, out int fee)
    {
        attached = new List<(Item, ushort)>();
        fee = 0;
        if (form.Items.Count >= PostFrames.MaxAttachments) return MailError.TooManyItems;
        var values = new List<(int Value, int Count)>();
        foreach (var item in form.Items)
        {
            var held = _items.FindByUid(BagItemIds.Stored(item.ItemUid));
            if (held is null || held.CharacterId != character.CharacterId || held.SlotKind != SlotKind.Inventory) return MailError.NoSuchItem;
            var definition = _catalog.GetById(held.ItemIndex);
            if (held.BreakedAt > new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)) return MailError.CannotAttach;
            if (definition is not null && (definition.Options & NotTradableOptions) != 0) return MailError.CannotAttach;
            if (item.Count == 0 || item.Count > held.WeightUnits) return MailError.TooManyItems;
            attached.Add((held, item.Count));
            values.Add((definition?.Value ?? 0, item.Count));
        }
        if (_text.GetByteCount(form.Title) > TitleLength) return MailError.NoSuchItem;
        if (_text.GetByteCount(form.Content) > ContentLength) return MailError.ContentTooLong;
        if (_text.GetByteCount(form.ToCharName) > RecipientLength) return MailError.NameTooLong;
        if (form.PostSubType is PostRules.PaidGift or PostRules.GmGift or 6) return MailError.WrongType;
        if (form.PostSubType == PostRules.NormalLetter && form.Money > 0) return MailError.WrongType;
        fee = Fee(form.PostSubType, form.Money, values);
        if (form.PostSubType == PostRules.NormalLetter) fee = 0;
        if (form.PostSubType is not PostRules.NormalLetter and not PostRules.GiftLetter && form.Money < Setup(MinimumFeeRow)) return MailError.BelowMinimum;
        return null;
    }

    public async Task DeleteAsync(InGameSessionState player, ReadOnlyMemory<byte> body)
    {
        if (player.Character is not { } character || !PostFrames.TryReadPostUid(body.Span, out var postUid)) return;
        if (Interlocked.CompareExchange(ref player.Mail.Busy, 1, 0) != 0)
        {
            await SendAsync(player, PostFrames.DeleteFailed, PostFrames.Error(MailError.Pending)).ConfigureAwait(false);
            return;
        }
        try
        {
            if (!player.Mail.Box.ContainsKey(postUid))
            {
                await SendAsync(player, PostFrames.DeleteFailed, PostFrames.Error(MailError.NotFound)).ConfigureAwait(false);
                return;
            }
            var last = player.Mail.Box.Values.Min(row => row.DateSeq);
            var result = await _master.DeleteAsync(character.CharacterId, new PostDeleteMessage(character.OwnerAccountId, postUid, last), default).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await SendAsync(player, PostFrames.DeleteFailed, PostFrames.Error((uint)result.Error)).ConfigureAwait(false);
                return;
            }
            player.Mail.Box.Remove(postUid);
            player.Mail.Money.Remove(postUid);
            if (result.Value is { } next) player.Mail.Box[next.PostUid] = next;
            await SendAsync(player, PostFrames.Deleted, PostFrames.DeletedBody(postUid, result.Value, _text)).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref player.Mail.Busy, 0);
        }
    }

    private async Task SendChunksAsync(InGameSessionState player, ushort opcode, byte[] body)
    {
        foreach (var chunk in PostFrames.Chunks(body)) await SendAsync(player, opcode, chunk).ConfigureAwait(false);
    }

    private Task SendAsync(InGameSessionState player, ushort opcode, byte[] payload) => SendAsync(player, (Opcode)opcode, payload);

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
