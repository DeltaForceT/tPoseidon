using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Game.Guild;

public static class GuildEmblemIcon
{
    public static uint? Resolve(IItemStore items, IItemPropertyStore properties, ulong characterId, ulong itemUid)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(properties);
        var item = items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(held => held.ItemUid == itemUid);
        if (item is null) return null;
        var look = properties.ForItem(itemUid).FirstOrDefault(property => property.Type == ItemComposingRules.AppearanceType);
        return look.Type == ItemComposingRules.AppearanceType ? look.Extra : (uint)item.ItemIndex;
    }
}
