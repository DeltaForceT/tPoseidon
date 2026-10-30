using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting.ClientScan;

public sealed class LibConfigRowFix : IClientFix
{
    private const string MapInfoTable = "MapInfoEx";
    private const string FileNameField = "FileName";
    private const char ClientSeparator = '\\';

    private readonly Func<string, ClientDataCheck> _check;
    private readonly Func<string, string?, ClientDataOutcome> _apply;
    private readonly Func<string, bool> _canRestore;
    private readonly Func<string, string?, ClientDataOutcome> _restore;

    public LibConfigRowFix(
        string id,
        string title,
        IReadOnlyList<LibConfigRowPatch> patches,
        Func<string, ClientDataCheck> check,
        Func<string, string?, ClientDataOutcome> apply,
        Func<string, bool> canRestore,
        Func<string, string?, ClientDataOutcome> restore)
    {
        Id = id;
        Title = title;
        Patches = patches;
        _check = check;
        _apply = apply;
        _canRestore = canRestore;
        _restore = restore;
    }

    public static LibConfigRowFix ShadowSanctuary { get; } = new(
        ShadowSanctuaryClientData.Id,
        ShadowSanctuaryClientData.Title,
        ShadowSanctuaryRows.Patches,
        ShadowSanctuaryClientData.Check,
        ShadowSanctuaryClientData.Apply,
        ShadowSanctuaryClientData.CanRestore,
        ShadowSanctuaryClientData.Restore);

    public static LibConfigRowFix ReferencedMaps { get; } = Create(
        ReferencedMapRows.Id,
        ReferencedMapRows.Title,
        ReferencedMapRows.Patches,
        new LibConfigRowPatchData(ReferencedMapRows.Patches, "maps the game data leads to"));

    public string Id { get; }

    public string Title { get; }

    public IReadOnlyList<LibConfigRowPatch> Patches { get; }

    public IReadOnlySet<string> MapFiles
    {
        get
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var patch in Patches.Where(patch => patch.Table == MapInfoTable))
            {
                var column = patch.Fields.ToList().IndexOf(FileNameField);
                foreach (var row in patch.Rows)
                {
                    files.Add(row[column][(row[column].LastIndexOf(ClientSeparator) + 1)..]);
                }
            }

            return files;
        }
    }

    public ClientDataCheck Check(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _check(context.Plan.LibConfig);
    }

    public bool CanRestore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _canRestore(context.Plan.LibConfig);
    }

    public FixOutcome Apply(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var outcome = _apply(context.Plan.LibConfig, context.ExecutablePath);
        return new FixOutcome(outcome.Changed, outcome.Message);
    }

    public FixOutcome Restore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var outcome = _restore(context.Plan.LibConfig, context.ExecutablePath);
        return new FixOutcome(outcome.Changed, outcome.Message);
    }

    private static LibConfigRowFix Create(string id, string title, IReadOnlyList<LibConfigRowPatch> patches, LibConfigRowPatchData data) => new(
        id,
        title,
        patches,
        data.Check,
        data.Apply,
        ShadowSanctuaryClientData.CanRestore,
        ShadowSanctuaryClientData.Restore);
}
