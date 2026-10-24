using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed partial class ItemUseService
{
    private const int PercentBound = 100;

    private async Task OpenKeyBoxAsync(Item box, ItemParamEntry definition, ulong wireUid, CancellationToken ct)
    {
        if (_player.Keybox?.Invoke(box.ItemIndex) is not { } row || _player.Character() is not { } character)
        {
            await RefuseAsync($"key box {box.ItemIndex}: no {KeyboxEntry.TableName} row", ct).ConfigureAwait(false);
            return;
        }
        var key = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory).FirstOrDefault(held => row.KeyItemIds.Contains(held.ItemIndex));
        if (key is null && row.OnlyKey)
        {
            await RefuseAsync($"key box {box.ItemIndex}: none of the keys [{string.Join(",", row.KeyItemIds)}] is in the bag", ct).ConfigureAwait(false);
            return;
        }
        int reward;
        if (key is null)
        {
            reward = row.NormalItemId;
        }
        else
        {
            int roll;
            lock (_rng) roll = _rng.Next(PercentBound);
            reward = roll < row.RareBRatePercent ? row.RareItemIdB : row.RareItemIdA;
        }
        if (reward <= 0)
        {
            await RefuseAsync($"key box {box.ItemIndex}: no reward for {(key is null ? "no key" : "the key")}", ct).ConfigureAwait(false);
            return;
        }
        await DeliverBoxAsync(
            box, definition, character.CharacterId, wireUid, [new PresentGrant(reward, PresentRolls.SmallestCount)],
            key is null ? Enumerable.Empty<Item>() : [key], ct).ConfigureAwait(false);
    }
}
