using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public enum ItemUseHelper
{
    NoHelper,
    Refuses,
    Money,
    Potion,
    Card,
    Misc,
    Use,
    Throw,
    SkillCard,
    Bullet,
}

public enum ItemUseRefusal
{
    NotUsable,
    BelowLevel,
    PotionNotNeeded,
}

public static class ItemUseRules
{
    public const int MoneyType = 1;
    public const int CardType = 11;
    public const int MiscType = 14;
    public const int ThrowType = 16;
    public const int SkillCardType = 28;
    public const int BulletType = 29;
    public const int FirstMonsterCardSubType = 18;
    public const int LastMonsterCardSubType = 27;

    public static bool IsMonsterCard(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Type == CardType && definition.SubType is >= FirstMonsterCardSubType and <= LastMonsterCardSubType;
    }

    public static ItemUseHelper HelperOf(int type) => type switch
    {
        MoneyType => ItemUseHelper.Money,
        ItemParamEntry.PotionType => ItemUseHelper.Potion,
        CardType => ItemUseHelper.Card,
        MiscType => ItemUseHelper.Misc,
        ItemParamEntry.UseItemType => ItemUseHelper.Use,
        ThrowType => ItemUseHelper.Throw,
        SkillCardType => ItemUseHelper.SkillCard,
        BulletType => ItemUseHelper.Bullet,
        >= 2 and <= 8 or 13 or >= 17 and <= 27 or 34 => ItemUseHelper.Refuses,
        _ => ItemUseHelper.NoHelper,
    };

    public static ItemUseRefusal? Check(ItemParamEntry definition, int level)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.IsUsable) return ItemUseRefusal.NotUsable;
        if (level < definition.MinLevel) return ItemUseRefusal.BelowLevel;
        return null;
    }

    public static bool PotionNotNeeded(ItemParamEntry definition, uint hp, uint maxHp, uint mp, uint maxMp)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return (definition.Hp > 0 && hp >= maxHp) || (definition.Mp > 0 && mp >= maxMp);
    }
}
