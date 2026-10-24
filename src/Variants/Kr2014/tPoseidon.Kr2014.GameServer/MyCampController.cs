using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.MyCamp;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

internal sealed class MyCampController : IDisposable
{
    public const string DefaultSlotName = "mycamp 1";

    private readonly IMyCampStore _store;
    private readonly Func<ushort, int> _zoneType;
    private readonly IReadOnlyDictionary<ushort, ZoneGeometry> _geometry;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly Func<InGameSessionState, Opcode, byte[], Task> _sendToOthersOnMap;
    private readonly Func<InGameSessionState, Task> _showPlayers;
    private readonly Func<ushort> _newEntityId;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Dictionary<ulong, MyCampShell> _byOwner = new();
    private readonly Dictionary<ulong, MyCampTransfer> _transfers = new();
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Random _rng = new();

    public MyCampController(
        IMyCampStore store, Func<ushort, int> zoneType, IReadOnlyDictionary<ushort, ZoneGeometry> geometry,
        Func<IReadOnlyCollection<InGameSessionState>> sessions, Func<ushort, Opcode, byte[], Task> sendToMap,
        Func<InGameSessionState, Opcode, byte[], Task> sendToOthersOnMap, Func<InGameSessionState, Task> showPlayers,
        Func<ushort> newEntityId, Encoding text, string logTag)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _zoneType = zoneType ?? throw new ArgumentNullException(nameof(zoneType));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _sendToOthersOnMap = sendToOthersOnMap ?? throw new ArgumentNullException(nameof(sendToOthersOnMap));
        _showPlayers = showPlayers ?? throw new ArgumentNullException(nameof(showPlayers));
        _newEntityId = newEntityId ?? throw new ArgumentNullException(nameof(newEntityId));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public void Dispose() => _sync.Dispose();

    public IReadOnlyList<(ushort HouseId, ushort X, ushort Y, byte[] Spawn)> HousesOn(ushort mapId)
    {
        lock (_byOwner)
        {
            return _byOwner.Values.Where(shell => shell.FieldMap == mapId)
                .Select(shell => (shell.HouseId, shell.HouseX, shell.HouseY, MyCampFrames.House(shell, shell.OwnerId, _text)))
                .ToArray();
        }
    }

    public Task OpenAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        if (!MyCampFrames.TryReadOpen(payload.Span, _text, out var request))
        {
            Log(st, $"open of {payload.Length} bytes not read");
            return;
        }
        _geometry.TryGetValue(st.MapId, out var geometry);
        var inRegion = geometry?.Regions.Any(region => Inside(region, st.PlayerX, st.PlayerY)) ?? false;
        var refusal = MyCampRules.OpenRefusal(
            st.IsDead || st.Trade is not null || st.Mission is not null,
            Shell(character.CharacterId) is not null,
            _zoneType(st.MapId),
            MyCampRules.IsShellZone(st.MapId),
            inRegion,
            TileAt(geometry, st.PlayerX, st.PlayerY));
        var slot = refusal is null ? SlotOf(character.CharacterId) : null;
        if (refusal is null && slot is null) refusal = MyCampRules.OpenBusy;
        if (refusal is { } code)
        {
            await SendAsync(st, Opcode.S2C_MYCAMP_OPEN_REFUSED, new[] { code }).ConfigureAwait(false);
            Log(st, $"open refused: {code} (map {st.MapId} type {_zoneType(st.MapId)}, at {st.PlayerX},{st.PlayerY})");
            return;
        }
        var shell = new MyCampShell(character.CharacterId, character.OwnerAccountId, character.Name, slot!.SlotCode, slot.SlotName,
            FreeShellZone(), _newEntityId(), st.MapId, st.PlayerX, st.PlayerY, DateTime.Now)
        {
            Mode = request.WithChat ? MyCampRules.Chat : MyCampRules.Decorate,
        };
        if (request.WithChat) shell.SetChat(request.Chat.Title, request.Chat.MaxGuests, request.Chat.HasPassword, request.Chat.Password);
        shell.LoadLayout(Records(shell, guest: false));
        lock (_byOwner) _byOwner[character.CharacterId] = shell;
        await _sendToOthersOnMap(st, Opcode.S2C_PLAYER_LEFT_MAP, MyCampFrames.ObjectId(st.PlayerEntityId)).ConfigureAwait(false);
        st.MyCampReturn = (st.MapId, st.PlayerX, st.PlayerY);
        st.MapId = shell.ShellZone;
        st.PlayerX = MyCampRules.OwnerX;
        st.PlayerY = MyCampRules.OwnerY;
        await _sendToMap(shell.FieldMap, Opcode.S2C_PLAYER_SPAWN_POST_WARP, MyCampFrames.House(shell, shell.OwnerId, _text)).ConfigureAwait(false);
        await SendAsync(st, Opcode.S2C_MYCAMP_OWNER_ENTER, MyCampFrames.OwnerEnter(shell, _text)).ConfigureAwait(false);
        Log(st, $"opens camp (slot {shell.SlotCode}, mode {shell.Mode}, house 0x{shell.HouseId:X4} on map {shell.FieldMap}, {shell.Layout.Count} items)");
    });

    public Task OwnerReadyAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character || ShellOfOwner(st) is not { } shell || payload.Length < 6) return;
        st.PlayerX = (ushort)BitConverter.ToInt16(payload.Span[2..4]);
        st.PlayerY = (ushort)BitConverter.ToInt16(payload.Span[4..6]);
        shell.Ready = true;
        await SendAsync(st, Opcode.S2C_MYCAMP_OWNER_READY, Array.Empty<byte>()).ConfigureAwait(false);
        await _showPlayers(st).ConfigureAwait(false);
        await StartTransferAsync(st, character.CharacterId, shell.Layout.Values.ToArray(), forGuest: false).ConfigureAwait(false);
    });

    public Task TransferNextAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || !_transfers.TryGetValue(character.CharacterId, out var transfer)) return;
        if (transfer.Done)
        {
            await SendAsync(st, Opcode.S2C_MYCAMP_ITEMS_END, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        await SendAsync(st, Opcode.S2C_MYCAMP_ITEMS, MyCampFrames.Items(transfer.NextChunk(MyCampRules.TransferChunkBytes))).ConfigureAwait(false);
    });

    public Task StartRecycleListAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        var slot = SlotOf(character.CharacterId);
        var records = Records(character.OwnerAccountId, slot?.SlotCode, guest: false);
        await SendAsync(st, Opcode.S2C_RECYCLE_MYCAMP_TIME, RecycleFrames.MyCampListTime(DateTime.Now)).ConfigureAwait(false);
        _transfers[character.CharacterId] = new MyCampTransfer(records, MyCampTransferKind.Recycle);
        await SendAsync(st, Opcode.S2C_MYCAMP_ITEM_COUNT, MyCampFrames.ItemCount(records.Count)).ConfigureAwait(false);
        Log(st, $"recycle list of {records.Count} camp items started");
    });

    public Task DropRecycleListAsync(InGameSessionState st) => Locked(() =>
    {
        if (st.Character is { } character && _transfers.TryGetValue(character.CharacterId, out var transfer) && transfer.Kind == MyCampTransferKind.Recycle)
        {
            _transfers.Remove(character.CharacterId);
        }
        return Task.CompletedTask;
    });

    public Task TransferEndAsync(InGameSessionState st, bool aborted) => Locked(async () =>
    {
        if (st.Character is not { } character || !_transfers.Remove(character.CharacterId, out var transfer)) return;
        if (transfer.Kind == MyCampTransferKind.Recycle)
        {
            if (!aborted) await SendAsync(st, Opcode.S2C_RECYCLE_MYCAMP_READY, RecycleFrames.MyCampListReady(character.Name, _text)).ConfigureAwait(false);
            return;
        }
        if (!transfer.ForGuest)
        {
            if (ShellOfOwner(st) is { } own) await SendAsync(st, Opcode.S2C_MYCAMP_MODE, new[] { own.Mode }).ConfigureAwait(false);
            return;
        }
        if (GuestShell(character.CharacterId) is not { } shell) return;
        if (aborted)
        {
            shell.RemoveGuest(character.CharacterId);
            await SendAsync(st, Opcode.S2C_MYCAMP_ENTER_REFUSED, new[] { MyCampRules.EnterFailed }).ConfigureAwait(false);
            return;
        }
        await _sendToOthersOnMap(st, Opcode.S2C_PLAYER_LEFT_MAP, MyCampFrames.ObjectId(st.PlayerEntityId)).ConfigureAwait(false);
        st.MyCampReturn = (st.MapId, st.PlayerX, st.PlayerY);
        st.MapId = shell.ShellZone;
        st.PlayerX = MyCampRules.OwnerX;
        st.PlayerY = MyCampRules.OwnerY;
        await SendAsync(st, Opcode.S2C_MYCAMP_GUEST_ENTER, MyCampFrames.GuestEnter(shell, st.PlayerX, st.PlayerY, _text)).ConfigureAwait(false);
        await _sendToMap(shell.FieldMap, Opcode.S2C_MYCAMP_GUEST_COUNT, MyCampFrames.GuestCount(shell)).ConfigureAwait(false);
        Log(st, $"enters the camp of {shell.OwnerName} ({shell.Guests.Count} guests)");
    });

    public Task GuestLoadedAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || GuestShell(character.CharacterId) is not { } shell || st.MapId != shell.ShellZone || shell.Mode == MyCampRules.Decorate) return;
        await _showPlayers(st).ConfigureAwait(false);
        await SendAsync(st, Opcode.S2C_MYCAMP_GUEST_LOADED, Array.Empty<byte>()).ConfigureAwait(false);
    });

    public Task EnterAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character || !MyCampFrames.TryReadEnter(payload.Span, _text, out var request)) return;
        MyCampShell? shell;
        lock (_byOwner) shell = _byOwner.Values.FirstOrDefault(candidate => candidate.HouseId == request.HouseId && candidate.FieldMap == st.MapId);
        var refusal = MyCampRules.IsShellZone(st.MapId) || shell is null || GuestShell(character.CharacterId) is not null
            ? MyCampRules.EnterFailed
            : shell.EnterRefusal(character.CharacterId, request.Password, st.GameMasterAttribute, DateTimeOffset.UtcNow);
        if (refusal is { } code)
        {
            await SendAsync(st, Opcode.S2C_MYCAMP_ENTER_REFUSED, new[] { code }).ConfigureAwait(false);
            Log(st, $"enter of house 0x{request.HouseId:X4} refused: {code}");
            return;
        }
        shell!.AddGuest(character.CharacterId);
        await StartTransferAsync(st, character.CharacterId, shell.Placed(), forGuest: true).ConfigureAwait(false);
    });

    public Task LeaveAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || !MyCampRules.IsShellZone(st.MapId))
        {
            await SendAsync(st, Opcode.S2C_MYCAMP_LEAVE_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        if (Shell(character.CharacterId) is { } own)
        {
            await CloseAsync(own).ConfigureAwait(false);
            return;
        }
        if (GuestShell(character.CharacterId) is { } shell) await GuestOutAsync(st, shell, null).ConfigureAwait(false);
    });

    public Task CloseRequestAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || Shell(character.CharacterId) is not { } shell)
        {
            await SendAsync(st, Opcode.S2C_MYCAMP_CLOSE_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        await CloseAsync(shell).ConfigureAwait(false);
    });

    public Task ForceCloseAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || Shell(character.CharacterId) is not { } shell) return;
        await CloseAsync(shell).ConfigureAwait(false);
    });

    public Task MoveNewItemAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character || ShellOfOwner(st) is not { } shell || payload.Length < 10)
        {
            await SendAsync(st, Opcode.S2C_MYCAMP_NEW_ITEM_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        var uid = BitConverter.ToUInt64(payload.Span[2..10]);
        var item = _store.FindItem(uid);
        if (item is null || item.AccountId != character.OwnerAccountId || item.ItemWhere != MyCampItemWhere.New || !_store.MoveItemToInventory(uid))
        {
            await SendAsync(st, Opcode.S2C_MYCAMP_NEW_ITEM_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        shell.Apply(new[] { new MyCampItemRecord(uid, 0, (ushort)item.ItemIndex, 0, MyCampRules.StateInventory) });
        await SendAsync(st, Opcode.S2C_MYCAMP_NEW_ITEM_MOVED, MyCampFrames.NewItemMoved(uid, (ushort)item.ItemIndex, 0)).ConfigureAwait(false);
    });

    public Task ModeAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } character || ShellOfOwner(st) is not { } shell || !MyCampFrames.TryReadMode(payload.Span, _text, out var request)) return;
        if (request.Mode > MyCampRules.Shop) return;
        shell.Mode = request.Mode;
        if (request.Chat is { } chat)
        {
            shell.SetChat(chat.Title, chat.MaxGuests, chat.HasPassword, chat.Password);
            Save(shell);
            await SendAsync(st, Opcode.S2C_MYCAMP_MODE, new[] { shell.Mode }).ConfigureAwait(false);
            await _sendToMap(shell.FieldMap, Opcode.S2C_MYCAMP_HOUSE_UPDATE, MyCampFrames.HouseUpdate(shell, MyCampRules.HouseFlagState | MyCampRules.HouseFlagChat, _text)).ConfigureAwait(false);
        }
        else
        {
            foreach (var guest in shell.Guests.ToArray())
            {
                if (Session(guest) is { } guestSt) await GuestOutAsync(guestSt, shell, MyCampRules.KickReasonModeChange).ConfigureAwait(false);
            }
            shell.LoadLayout(Records(shell, guest: false));
            await _sendToMap(shell.FieldMap, Opcode.S2C_MYCAMP_HOUSE_UPDATE, MyCampFrames.HouseUpdate(shell, MyCampRules.HouseFlagState, _text)).ConfigureAwait(false);
            await StartTransferAsync(st, character.CharacterId, shell.Layout.Values.ToArray(), forGuest: false).ConfigureAwait(false);
        }
        Log(st, $"camp mode {shell.Mode}");
    });

    public Task ChatInfoAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (ShellOfOwner(st) is not { } shell || !MyCampFrames.TryReadChatInfo(payload.Span, _text, out var chat)) return;
        shell.SetChat(chat.Title, chat.MaxGuests, chat.HasPassword, chat.Password);
        await _sendToMap(shell.FieldMap, Opcode.S2C_MYCAMP_HOUSE_UPDATE, MyCampFrames.HouseUpdate(shell, MyCampRules.HouseFlagChat, _text)).ConfigureAwait(false);
        await _sendToMap(shell.ShellZone, Opcode.S2C_MYCAMP_CHAT_INFO, MyCampFrames.ChatInfo(shell, _text)).ConfigureAwait(false);
    });

    public Task RenameAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(() =>
    {
        if (ShellOfOwner(st) is not { } shell || !MyCampFrames.TryReadText(payload.Span, 2, _text, out var name))
        {
            Log(st, "slot name not read");
            return Task.CompletedTask;
        }
        shell.SlotName = name.Length <= MyCampRules.TitleLength ? name : name[..MyCampRules.TitleLength];
        _store.UpdateSlotName(shell.SlotCode, shell.SlotName);
        return Task.CompletedTask;
    });

    public Task ModelAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (ShellOfOwner(st) is not { } shell || payload.Length < 2) return;
        shell.Model = BitConverter.ToUInt16(payload.Span[..2]);
        await _sendToMap(shell.FieldMap, Opcode.S2C_MYCAMP_HOUSE_UPDATE, MyCampFrames.HouseUpdate(shell, MyCampRules.HouseFlagModel, _text)).ConfigureAwait(false);
    });

    public Task SaveObjectsAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(() =>
    {
        if (ShellOfOwner(st) is not { } shell) return Task.CompletedTask;
        if (!MyCampFrames.TryReadItems(payload.Span, out var records))
        {
            Log(st, $"object save of {payload.Length} bytes not read");
            return Task.CompletedTask;
        }
        shell.Apply(records.Where(record => shell.Layout.ContainsKey(record.ItemUid)));
        return Task.CompletedTask;
    });

    public Task ShoutAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (ShellOfOwner(st) is not { } shell || !MyCampFrames.TryReadText(payload.Span, 0, _text, out var text) || text.Length == 0) return;
        await _sendToMap(shell.ShellZone, Opcode.S2C_MYCAMP_SHOUT, MyCampFrames.Text(text, _text)).ConfigureAwait(false);
    });

    public Task KickAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (ShellOfOwner(st) is not { } shell || payload.Length < 2) return;
        var target = BitConverter.ToUInt16(payload.Span[..2]);
        var guest = _sessions().FirstOrDefault(session => session.PlayerEntityId == target && session.Character is { } c && shell.Guests.Contains(c.CharacterId));
        if (target == st.PlayerEntityId || guest?.Character is not { } guestCharacter)
        {
            await SendAsync(st, Opcode.S2C_MYCAMP_KICK_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        await SendAsync(st, Opcode.S2C_MYCAMP_KICKED_OK, Array.Empty<byte>()).ConfigureAwait(false);
        await _sendToMap(shell.ShellZone, Opcode.S2C_MYCAMP_GUEST_KICKED, MyCampFrames.ObjectId(target)).ConfigureAwait(false);
        shell.Ban(guestCharacter.CharacterId, DateTimeOffset.UtcNow);
        await GuestOutAsync(guest, shell, null).ConfigureAwait(false);
        Log(st, $"kicks {guestCharacter.Name} out of the camp");
    });

    public Task LeftAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        _transfers.Remove(character.CharacterId);
        if (Shell(character.CharacterId) is { } own)
        {
            await CloseAsync(own).ConfigureAwait(false);
            return;
        }
        if (GuestShell(character.CharacterId) is { } shell)
        {
            shell.RemoveGuest(character.CharacterId);
            await _sendToMap(shell.FieldMap, Opcode.S2C_MYCAMP_GUEST_COUNT, MyCampFrames.GuestCount(shell)).ConfigureAwait(false);
        }
    });

    private async Task StartTransferAsync(InGameSessionState st, ulong characterId, IReadOnlyCollection<MyCampItemRecord> records, bool forGuest)
    {
        _transfers[characterId] = new MyCampTransfer(records, forGuest);
        await SendAsync(st, Opcode.S2C_MYCAMP_ITEM_COUNT, MyCampFrames.ItemCount(records.Count)).ConfigureAwait(false);
    }

    private async Task CloseAsync(MyCampShell shell)
    {
        Save(shell);
        lock (_byOwner) _byOwner.Remove(shell.OwnerId);
        await _sendToMap(shell.FieldMap, Opcode.S2C_ENTITY_DESPAWN, MyCampFrames.ObjectId(shell.HouseId)).ConfigureAwait(false);
        foreach (var guest in shell.Guests.ToArray())
        {
            if (Session(guest) is { } guestSt) await GuestOutAsync(guestSt, shell, MyCampRules.CloseReasonSave).ConfigureAwait(false);
        }
        if (Session(shell.OwnerId) is { } owner)
        {
            _transfers.Remove(shell.OwnerId);
            if (owner.MapId == shell.ShellZone) await ReturnAsync(owner, shell, null).ConfigureAwait(false);
            await SendAsync(owner, Opcode.S2C_MYCAMP_CLOSED, Array.Empty<byte>()).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} [mycamp] camp of {shell.OwnerName} closed ({shell.Placed().Count} placed items saved)");
    }

    private async Task GuestOutAsync(InGameSessionState guest, MyCampShell shell, byte? kickReason)
    {
        if (guest.Character is not { } character) return;
        shell.RemoveGuest(character.CharacterId);
        _transfers.Remove(character.CharacterId);
        if (guest.MapId == shell.ShellZone) await ReturnAsync(guest, shell, kickReason).ConfigureAwait(false);
        await _sendToMap(shell.FieldMap, Opcode.S2C_MYCAMP_GUEST_COUNT, MyCampFrames.GuestCount(shell)).ConfigureAwait(false);
    }

    private async Task ReturnAsync(InGameSessionState st, MyCampShell shell, byte? kickReason)
    {
        await _sendToOthersOnMap(st, Opcode.S2C_PLAYER_LEFT_MAP, MyCampFrames.ObjectId(st.PlayerEntityId)).ConfigureAwait(false);
        var (zone, x, y) = st.MyCampReturn ?? (shell.FieldMap, shell.HouseX, shell.HouseY);
        st.MyCampReturn = null;
        if (kickReason is { } reason) await SendAsync(st, Opcode.S2C_MYCAMP_KICK_OUT, MyCampFrames.Kicked(zone, shell.HouseX, shell.HouseY, reason)).ConfigureAwait(false);
        else await SendAsync(st, Opcode.S2C_MYCAMP_RETURN, MyCampFrames.Return(zone, shell.HouseX, shell.HouseY)).ConfigureAwait(false);
        await SendAsync(st, Opcode.S2C_MYCAMP_LEFT, Array.Empty<byte>()).ConfigureAwait(false);
        st.MapId = zone;
        st.PlayerX = x;
        st.PlayerY = y;
        if (st.RequestZoneChange is { } requestZoneChange)
        {
            await requestZoneChange(new ZoneChangeRequest(zone, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, (shell.HouseX, shell.HouseY)), default).ConfigureAwait(false);
        }
    }

    private void Save(MyCampShell shell)
    {
        var placed = shell.Placed().Select(record => new MyCampDeco
        {
            SlotCode = shell.SlotCode,
            ItemUid = record.ItemUid,
            DecoX = record.X,
            DecoY = record.Y,
            DecoZ = record.Z,
            DecoDirection = record.Direction,
            ParentsUid = record.ParentsUid,
        }).ToArray();
        _store.ReplaceDeco(shell.SlotCode, placed);
    }

    private List<MyCampItemRecord> Records(MyCampShell shell, bool guest) => Records(shell.AccountId, shell.SlotCode, guest);

    private List<MyCampItemRecord> Records(uint accountId, ulong? slotCode, bool guest)
    {
        var items = _store.GetItemsForAccount(accountId).ToDictionary(item => item.ItemUid);
        var deco = slotCode is { } code ? _store.GetDecoForSlot(code) : Array.Empty<MyCampDeco>();
        var placedUids = _store.GetPlacedItemUidsForAccount(accountId);
        var records = new List<MyCampItemRecord>();
        foreach (var row in deco)
        {
            if (!items.TryGetValue(row.ItemUid, out var item) || item.ItemWhere == MyCampItemWhere.Deleted) continue;
            records.Add(new MyCampItemRecord(row.ItemUid, row.ParentsUid, (ushort)item.ItemIndex, 0, MyCampRules.StatePlaced, row.DecoX, row.DecoY, row.DecoZ, row.DecoDirection));
        }
        if (guest) return records;
        var inventory = items.Values.Where(item => item.ItemWhere == MyCampItemWhere.Inventory && !placedUids.Contains(item.ItemUid))
            .OrderBy(item => item.ItemUid).Take(MyCampRules.InventoryCap).ToArray();
        records.AddRange(inventory.Select(item => new MyCampItemRecord(item.ItemUid, 0, (ushort)item.ItemIndex, 0, MyCampRules.StateInventory)));
        records.AddRange(items.Values.Where(item => item.ItemWhere == MyCampItemWhere.New && !placedUids.Contains(item.ItemUid))
            .OrderBy(item => item.ItemUid).Take(MyCampRules.ItemCap - inventory.Length)
            .Select(item => new MyCampItemRecord(item.ItemUid, 0, (ushort)item.ItemIndex, 0, MyCampRules.StateNew)));
        return records;
    }

    private MyCampSlot? SlotOf(ulong characterId) =>
        _store.GetSlotsForCharacter(characterId).OrderByDescending(slot => slot.SlotUpToDate).FirstOrDefault();

    private ushort FreeShellZone()
    {
        lock (_byOwner)
        {
            var used = _byOwner.Values.Select(shell => shell.ShellZone).ToHashSet();
            var zone = MyCampRules.ShellZoneBase;
            while (used.Contains(zone)) zone++;
            return zone;
        }
    }

    private MyCampShell? Shell(ulong ownerId)
    {
        lock (_byOwner) return _byOwner.GetValueOrDefault(ownerId);
    }

    private MyCampShell? ShellOfOwner(InGameSessionState st) =>
        st.Character is { } character && Shell(character.CharacterId) is { } shell && st.MapId == shell.ShellZone ? shell : null;

    private MyCampShell? GuestShell(ulong guestId)
    {
        lock (_byOwner) return _byOwner.Values.FirstOrDefault(shell => shell.Guests.Contains(guestId));
    }

    private InGameSessionState? Session(ulong characterId) =>
        _sessions().FirstOrDefault(session => session.Character?.CharacterId == characterId);

    private static bool Inside(ZoneRegion region, int x, int y) =>
        x >= Math.Min(region.X1, region.X2) && x <= Math.Max(region.X1, region.X2) && y >= Math.Min(region.Y1, region.Y2) && y <= Math.Max(region.Y1, region.Y2);

    private static byte TileAt(ZoneGeometry? geometry, int x, int y)
    {
        if (geometry is null) return 0;
        var cellX = ZoneGeometry.CellOf(x);
        var cellY = ZoneGeometry.CellOf(y);
        if (cellX < 0 || cellY < 0 || cellX >= geometry.CellsX || cellY >= geometry.CellsY) return ZoneGeometry.CellBlocked;
        return geometry.AttributeAt(cellX, cellY);
    }

    private async Task Locked(Func<Task> action)
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }

    private void Log(InGameSessionState st, string what) =>
        Console.WriteLine($"{_logTag} [mycamp] 0x{st.PlayerEntityId:X4} {what}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
