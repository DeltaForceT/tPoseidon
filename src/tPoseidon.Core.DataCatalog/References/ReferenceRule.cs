using System.Text.RegularExpressions;

namespace tPoseidon.Core.DataCatalog.References;

public enum ReferenceTarget
{
    Table,
    Item,
    Monster,
    Skill,
    Map,
    Quest,
    MonsterDropRow,
    TacticsGroup,
}

public sealed record ReferenceRule(Regex Tables, Regex Columns, ReferenceTarget Target)
{
    public static ReferenceRule Of(string tables, string columns, ReferenceTarget target) =>
        new(new Regex($"^(?:{tables})$", RegexOptions.CultureInvariant | RegexOptions.Compiled), new Regex($"^(?:{columns})$", RegexOptions.CultureInvariant | RegexOptions.Compiled), target);
}

public sealed record UnresolvedReference(string Table, int Row, string Column, string Value, ReferenceTarget Target)
{
    public string Describe() => $"{Table} row {Row} {Column} = {Value}: no {Target}";
}

public sealed record ReferenceReport(int Checked, IReadOnlyList<UnresolvedReference> Unresolved, IReadOnlyList<UnresolvedReference> CaseOnly)
{
    public IReadOnlyList<IGrouping<(string Table, string Column, ReferenceTarget Target), UnresolvedReference>> ByColumn() =>
        Unresolved.GroupBy(item => (item.Table, item.Column, item.Target)).ToArray();
}
