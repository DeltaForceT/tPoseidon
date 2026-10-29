using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed partial class ItemUseService
{
    public async Task MakeSecretMapAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!SecretMapFrames.TryReadRequest(payload.Span, out var uids))
        {
            Console.WriteLine($"{_logTag} {_remote()}   secret map request of {payload.Length} bytes not read");
            return;
        }
        if (_player.Character() is not { } character) return;
        var pieces = new List<(Item Item, ItemParamEntry Definition)>(uids.Count);
        foreach (var uid in uids)
        {
            if (Held(character, uid) is not { } held)
            {
                Console.WriteLine($"{_logTag} {_remote()}   secret map: 0x{BagItemIds.Stored(uid):X} is not in the bag");
                return;
            }
            pieces.Add(held);
        }
        if (!SecretMapRules.IsCompleteSet(pieces.Select(piece => piece.Item.ItemIndex).ToList()))
        {
            Console.WriteLine($"{_logTag} {_remote()}   secret map: the request is not the sixteen secret cards");
            return;
        }
        if (_catalog.GetById(SecretMapRules.MapItemId) is not { } mapDefinition)
        {
            Console.WriteLine($"{_logTag} {_remote()}   secret map: item {SecretMapRules.MapItemId} is not in the item table");
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var freed = pieces.Count(piece => piece.Item.Stack <= 1);
        if (!mapDefinition.IsStackable && PickupRules.BagCapacity - bag.Count + freed < 1)
        {
            Console.WriteLine($"{_logTag} {_remote()}   secret map: no bag slot for the map");
            return;
        }
        var operations = new List<BagOperation>(pieces.Count + 1);
        foreach (var piece in pieces) operations.Add(TakeOne(piece.Item, piece.Definition));
        operations.Add(Grant(character.CharacterId, new PresentGrant(SecretMapRules.MapItemId, 1)));
        await SendAsync(Opcode.S2C_SECRET_MAP_MADE, SecretMapFrames.Made(operations), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_remote()}   secret map made from {pieces.Count} secret cards");
    }
}
