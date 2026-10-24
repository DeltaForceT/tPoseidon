namespace tPoseidon.Core.LibConfig;

public sealed record MonsterDebuffResistEntry(int Id, IReadOnlyList<int> Values)
{
    public static readonly IReadOnlyList<string> Columns = new[]
    {
        "Stun", "Paralysis", "Stone", "MagicPoison", "Poison", "RestrictAvoid", "RestrictAttack", "RestrictSkill", "RestrictHeal",
        "AP", "AC", "DX", "MA", "MD", "DA", "LK", "DP", "HV", "SPD", "Cancler", "APResist", "GPResist", "MAResist",
        "ThunderResist", "AirResist", "WaterResist", "EarthResist", "FireResist", "DarkResist", "LightResist", "Dimension",
    };
}
