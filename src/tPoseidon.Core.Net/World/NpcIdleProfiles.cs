using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed record NpcIdleProfile(NpcActionScriptEntry? Script, TimeSpan WaitWithoutMotion);

public sealed class NpcIdleProfiles
{
    public static readonly TimeSpan WaitWithoutMotion = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan SkillShopWaitWithoutMotion = TimeSpan.FromSeconds(5);

    private readonly INpcActionScriptLookup _npcMotions;
    private readonly INpcActionScriptLookup _shopMotions;
    private readonly INpcActionScriptLookup _skillShopMotions;
    private readonly IReadOnlySet<int> _shopNpcTypes;
    private readonly IReadOnlySet<int> _skillShopNpcTypes;

    public NpcIdleProfiles(
        INpcActionScriptLookup npcMotions,
        INpcActionScriptLookup shopMotions,
        INpcActionScriptLookup skillShopMotions,
        IReadOnlySet<int> shopNpcTypes,
        IReadOnlySet<int> skillShopNpcTypes)
    {
        ArgumentNullException.ThrowIfNull(npcMotions);
        ArgumentNullException.ThrowIfNull(shopMotions);
        ArgumentNullException.ThrowIfNull(skillShopMotions);
        ArgumentNullException.ThrowIfNull(shopNpcTypes);
        ArgumentNullException.ThrowIfNull(skillShopNpcTypes);
        _npcMotions = npcMotions;
        _shopMotions = shopMotions;
        _skillShopMotions = skillShopMotions;
        _shopNpcTypes = shopNpcTypes;
        _skillShopNpcTypes = skillShopNpcTypes;
    }

    public NpcIdleProfile For(NpcEntity npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (_skillShopNpcTypes.Contains(npc.ChrType))
        {
            return new NpcIdleProfile(_skillShopMotions.GetByType(npc.ChrType), SkillShopWaitWithoutMotion);
        }
        if (_shopNpcTypes.Contains(npc.ChrType))
        {
            return new NpcIdleProfile(_shopMotions.GetByType(npc.ChrType), WaitWithoutMotion);
        }
        return new NpcIdleProfile(_npcMotions.GetByType(npc.ChrType), WaitWithoutMotion);
    }
}
