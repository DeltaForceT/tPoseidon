using tPoseidon.Core.Common;

namespace tPoseidon.Hosting.ClientScan;

public sealed class Md3PointsFix : IClientFix
{
    public const string FixId = "map-points";
    public const string BackupSuffix = ".tposeidon-original";

    private readonly ClientPatchCatalog _catalog;

    public Md3PointsFix(ClientPatchCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public string Id => FixId;

    public string Title => "Add the missing map points";

    public bool CanRestore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _catalog.Points.Select(patch => patch.MapFile).Distinct(StringComparer.OrdinalIgnoreCase)
            .Any(file => Locate(context, file) is { } path && File.Exists(path + BackupSuffix));
    }

    public FixOutcome Apply(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var changed = new List<string>();
        foreach (var group in _catalog.Points.GroupBy(patch => patch.MapFile, StringComparer.OrdinalIgnoreCase))
        {
            if (Locate(context, group.Key) is not { } path)
            {
                continue;
            }

            var original = File.ReadAllBytes(path);
            var document = Md3Document.Parse(original)
                ?? throw new HostingException($"{Path.GetFileName(path)} is not a map file tPoseidon can read.");
            var added = 0;
            foreach (var patch in group)
            {
                var existing = document.Points.FindIndex(point => point.Type == patch.Point.Type && point.Id == patch.Point.Id && point.MapId == patch.Point.MapId);
                if (existing >= 0 && document.Points[existing] == patch.Point)
                {
                    continue;
                }

                if (existing >= 0)
                {
                    document.Points[existing] = patch.Point;
                }
                else
                {
                    document.Points.Add(patch.Point);
                }

                added++;
            }

            if (added == 0)
            {
                continue;
            }

            Write(path, original, document.ToBytes());
            changed.Add($"{Path.GetFileName(path)} ({added})");
        }

        return changed.Count == 0
            ? new FixOutcome(false, "The map points are already in place.")
            : new FixOutcome(true, $"Added map points to {string.Join(", ", changed)}. The originals are kept with {BackupSuffix} added to their names.");
    }

    public FixOutcome Restore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var restored = new List<string>();
        foreach (var file in _catalog.Points.Select(patch => patch.MapFile).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Locate(context, file) is not { } path || !File.Exists(path + BackupSuffix))
            {
                continue;
            }

            try
            {
                File.Move(path + BackupSuffix, path, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new PlayFailure(PlayFailureReason.ClientRunning, $"Could not restore {path}: {ex.Message}", "Close the game client, then try again.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new PlayFailure(PlayFailureReason.ClientNotWritable, $"Could not restore {path}: {ex.Message}", "Give your account write access to the client folder.", ex);
            }

            restored.Add(Path.GetFileName(path));
        }

        return restored.Count == 0
            ? new FixOutcome(false, "No map files were changed by the scanner.")
            : new FixOutcome(true, $"Restored {string.Join(", ", restored)}.");
    }

    private static string? Locate(ClientScanContext context, string file) =>
        ClientFiles.Locate(Path.Combine(context.MapFolder, file));

    private static void Write(string path, byte[] original, byte[] patched)
    {
        var backup = path + BackupSuffix;
        try
        {
            if (!File.Exists(backup))
            {
                File.WriteAllBytes(backup, original);
            }

            File.WriteAllBytes(path, patched);
        }
        catch (IOException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientRunning, $"Could not update {path}: {ex.Message}", "Close the game client, then try again.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientNotWritable, $"Could not update {path}: {ex.Message}", "Give your account write access to the client folder.", ex);
        }
    }
}
