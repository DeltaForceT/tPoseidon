using tPoseidon.Core.Game.PlayerShop;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting.GameMaster;

public sealed record ItemCategory(byte Id, string Name);

public static class ItemCategories
{
    public static IReadOnlyList<ItemCategory> All { get; } =
    [
        new(PlayerShopSearchRules.WeaponCategory, "Weapons"),
        new(PlayerShopSearchRules.GunCategory, "Guns"),
        new(PlayerShopSearchRules.HatCategory, "Hats"),
        new(PlayerShopSearchRules.ShieldCategory, "Shields"),
        new(PlayerShopSearchRules.CapeCategory, "Capes"),
        new(PlayerShopSearchRules.AccessoryCategory, "Accessories"),
        new(PlayerShopSearchRules.ArmourCategory, "Belts and shoes"),
        new(PlayerShopSearchRules.PetCategory, "Pets"),
        new(PlayerShopSearchRules.HeadCategory, "Head items"),
        new(PlayerShopSearchRules.FaceCategory, "Face items"),
        new(PlayerShopSearchRules.DrillCategory, "Drills"),
        new(PlayerShopSearchRules.FashionCategory, "Fashion"),
        new(PlayerShopSearchRules.PotionCategory, "Potions"),
        new(PlayerShopSearchRules.CardCategory, "Cards"),
        new(PlayerShopSearchRules.OtherCategory, "Other items"),
    ];

    public static ItemCategory Of(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var id = PlayerShopSearchRules.CategoryOf(definition);
        return All.First(category => category.Id == id);
    }
}
