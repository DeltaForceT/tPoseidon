using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public sealed class SkillCastResolver
{
    private const int DefaultCastDelayMs = 400;
    private const int DefaultDamage = 5;

    private readonly IEsaiiLookup _esaii;
    private readonly IEsActionLookup _esa;

    public SkillCastResolver(IEsaiiLookup esaii, IEsActionLookup esa)
    {
        _esaii = esaii;
        _esa = esa;
    }

    public int GetCastDelayMs(ISkill skill, int skillLevel)
    {
        var esaiiTable = TrimTableName(skill.EsaiiTableName, "ESAII_");
        if (esaiiTable.Length == 0) return DefaultCastDelayMs;

        var entry = _esaii.GetBySkillAndLevel(esaiiTable, skillLevel)
                    ?? _esaii.GetBySkillAndLevel(esaiiTable, 1);
        if (entry is null) return DefaultCastDelayMs;

        return WireLimits.SaturateInt32(entry.Delay * 1000f);
    }

    public int GetCooldownMs(ISkill skill, int skillLevel)
    {
        var esaiiTable = TrimTableName(skill.EsaiiTableName, "ESAII_");
        if (esaiiTable.Length == 0) return 0;

        var entry = _esaii.GetBySkillAndLevel(esaiiTable, skillLevel)
                    ?? _esaii.GetBySkillAndLevel(esaiiTable, 1);
        if (entry is null) return 0;

        return WireLimits.SaturateInt32(entry.CoolTime * 1000f);
    }

    public int GetMpCost(ISkill skill, int skillLevel)
    {
        var esaiiTable = TrimTableName(skill.EsaiiTableName, "ESAII_");
        if (esaiiTable.Length == 0) return 0;

        var entry = _esaii.GetBySkillAndLevel(esaiiTable, skillLevel)
                    ?? _esaii.GetBySkillAndLevel(esaiiTable, 1);
        return entry?.MpCon ?? 0;
    }

    public int GetRange(ISkill skill, int skillLevel)
    {
        var esaiiTable = TrimTableName(skill.EsaiiTableName, "ESAII_");
        if (esaiiTable.Length == 0) return 0;

        var entry = _esaii.GetBySkillAndLevel(esaiiTable, skillLevel)
                    ?? _esaii.GetBySkillAndLevel(esaiiTable, 1);
        return entry?.Range ?? 0;
    }

    public long ComputeDamage(ISkill skill, int skillLevel, long attackerStat, long defenderDp)
    {
        var esaTable = TrimTableName(skill.EsaTableName, "ESAction_");
        if (esaTable.Length == 0) return DefaultDamage;

        var entry = _esa.GetBySkillAndLevel(esaTable, skillLevel)
                    ?? _esa.GetBySkillAndLevel(esaTable, 1);
        if (entry is null) return DefaultDamage;

        var baseDamage = entry.Ratio1 + (attackerStat * entry.Ratio2 / 100);
        var raw = WireLimits.Saturate(baseDamage * entry.ApplyRatio);
        var afterDefense = raw - defenderDp;
        return Math.Max(1L, afterDefense);
    }

    private static string TrimTableName(string raw, string prefix)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return string.Empty;
        return trimmed.StartsWith(prefix, StringComparison.Ordinal)
            ? trimmed[prefix.Length..]
            : trimmed;
    }
}
