using tPoseidon.Core.Common;
using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.Hosting;

public sealed record PublishPreview(LauncherManifest Manifest, ManifestChanges Changes, IReadOnlyList<string> OriginalNameMatches, LauncherPackageInfo? Launcher);

public sealed record PublishOutcome(LauncherManifest Manifest, PublishRecord Record);

public static class UpdateChannel
{
    public static OriginalFileGuard OriginalGuard(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            return OriginalFileGuard.FromClientFolder(settings.ClientFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"Could not read the client folder {settings.ClientFolder}: {ex.Message}", ex);
        }
    }

    public static PublishPreview Preview(InstallPaths paths, HostSettings settings, OriginalFileGuard? guard = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        var manifest = Build(paths, settings);
        var changes = ManifestChanges.Compare(LoadPublished(paths), manifest);
        var original = guard ?? OriginalGuard(settings);
        var matches = changes.Files.Where(file => file.State != FileState.Removed && original.Matches(file.Path)).Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return new PublishPreview(manifest, changes, matches, ReadLauncher(paths));
    }

    public static PublishOutcome Publish(InstallPaths paths, HostSettings settings, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        if (note is { Length: > PublishHistory.MaxNoteLength })
        {
            throw new HostingException($"The note can be at most {PublishHistory.MaxNoteLength} characters.");
        }

        Directory.CreateDirectory(Path.Combine(paths.Updates, ManifestBuilder.ClientFolder));
        Directory.CreateDirectory(Path.Combine(paths.Updates, ManifestBuilder.NewsFolder));
        Directory.CreateDirectory(Path.Combine(paths.Updates, ManifestBuilder.PacksFolder));
        var target = Path.Combine(paths.Updates, ManifestBuilder.ManifestFile);
        LauncherManifest manifest;
        ManifestChanges changes;
        try
        {
            manifest = Build(paths, settings);
            changes = ManifestChanges.Compare(LoadPublished(paths), manifest);
            var json = manifest.ToJson();
            var key = SigningKeyStore.EnsureCreated(paths);
            var signature = ManifestSignature.Sign(json, key.Pair, key.Rotations);
            var signatureTarget = Path.Combine(paths.Updates, ManifestSignature.SignatureFile);
            var temporary = target + ".tmp";
            var signatureTemporary = signatureTarget + ".tmp";
            File.WriteAllBytes(temporary, json);
            File.WriteAllBytes(signatureTemporary, signature);
            File.Move(signatureTemporary, signatureTarget, overwrite: true);
            File.Move(temporary, target, overwrite: true);
        }
        catch (IOException ex)
        {
            throw new HostingException($"Could not publish the update folder {paths.Updates}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HostingException($"Could not publish the update folder {paths.Updates}: {ex.Message}", ex);
        }

        var record = PublishRecord.From(DateTimeOffset.UtcNow, note, manifest, changes);
        PublishHistory.Append(paths, record);
        return new PublishOutcome(manifest, record);
    }

    public static LauncherPackageInfo? ReadLauncher(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        try
        {
            return LauncherPackageInfo.Read(paths.Updates);
        }
        catch (InvalidDataException ex)
        {
            throw new HostingException($"The launcher update information in {paths.Updates} is not valid: {ex.Message}", "Choose the launcher zip again.", ex);
        }
    }

    private static LauncherManifest Build(InstallPaths paths, HostSettings settings)
    {
        try
        {
            return ManifestBuilder.Build(paths.Updates, settings.Launch.WorldName, BuildIdentity.Current.Version);
        }
        catch (InvalidDataException ex)
        {
            throw new HostingException($"The update folder {paths.Updates} cannot be published: {ex.Message}", "Fix the file named above and publish again.", ex);
        }
        catch (IOException ex)
        {
            throw new HostingException($"Could not read the update folder {paths.Updates}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HostingException($"Could not read the update folder {paths.Updates}: {ex.Message}", ex);
        }
    }

    private static LauncherManifest? LoadPublished(InstallPaths paths)
    {
        var path = Path.Combine(paths.Updates, ManifestBuilder.ManifestFile);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return LauncherManifest.Parse(File.ReadAllBytes(path));
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"Could not read the published manifest {path}: {ex.Message}", ex);
        }
    }
}
