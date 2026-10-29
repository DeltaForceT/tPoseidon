using System.IO.Compression;
using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

public sealed record LauncherKitFiles(string LauncherFolder, string InstallerExecutable)
{
    public const string FolderName = "launcher-kit";
    public const string LauncherExecutable = "tPoseidon.Launcher.exe";
    public const string InstallerFileName = "tPoseidon.Launcher.Installer.exe";

    public static LauncherKitFiles Locate(string kitFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kitFolder);
        var launcher = Path.Combine(kitFolder, "launcher");
        var installer = Path.Combine(kitFolder, "installer", InstallerFileName);
        if (!File.Exists(Path.Combine(launcher, LauncherExecutable)) || !File.Exists(installer))
        {
            throw new HostingException(
                "The launcher pieces are not part of this install.",
                $"Use a released tPoseidon package, or publish the launcher and its installer into {kitFolder}.",
                null);
        }

        return new LauncherKitFiles(launcher, installer);
    }
}

public sealed record LauncherBuildResult(string SetupPath, string ZipPath);

public enum LauncherBuildStep
{
    Copying,
    Branding,
    PackingZip,
    PackingInstaller,
    WritingInstaller,
    Done,
}

public sealed record LauncherBuildProgress(LauncherBuildStep Step, double Fraction);

public static class LauncherBuilder
{
    public const string SetupLabel = " Setup";
    public const string ExecutableExtension = ".exe";
    public const string SetupSuffix = SetupLabel + ExecutableExtension;
    public const string InstallNotes = "HOW TO INSTALL.txt";
    public const string TestSuffix = "-test-this-pc";

    public static string SetupFileName(string serverName, bool testOnThisComputer = false) =>
        FileNames.Safe(serverName) + SetupLabel + (testOnThisComputer ? TestSuffix : "") + ExecutableExtension;

    public static string ZipFileName(string serverName, string version, bool testOnThisComputer = false) =>
        FileNames.Safe(serverName).Replace(' ', '-') + "-launcher-" + version + (testOnThisComputer ? TestSuffix : "") + ".zip";

    public const double CopyingStart = 0;
    public const double BrandingStart = 0.3;
    public const double PackingInstallerStart = 0.35;
    public const double PackingZipStart = 0.6;
    public const double WritingInstallerStart = 0.85;
    public const double Finished = 1;

    public static string ExecutableName(string serverName) => FileNames.Safe(serverName) + ".exe";

    public static LauncherBuildResult Build(LauncherBuildRequest request, LauncherKitFiles kit, string outputFolder, IProgress<LauncherBuildProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        var spec = request.Spec.Validated();
        var output = Path.GetFullPath(outputFolder);
        var work = Path.Combine(Path.GetTempPath(), "tposeidon-launcher-build-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(output);
            var stage = Path.Combine(work, FileNames.Safe(spec.ServerName));
            CopyFolder(kit.LauncherFolder, stage, part => Report(progress, LauncherBuildStep.Copying, CopyingStart, BrandingStart, part));
            Report(progress, LauncherBuildStep.Branding, BrandingStart, PackingInstallerStart, 0);
            var executable = ExecutableName(spec.ServerName);
            File.Move(Path.Combine(stage, LauncherKitFiles.LauncherExecutable), Path.Combine(stage, executable));
            Branding.Write(Path.Combine(stage, BrandingSpec.FolderName), spec, request.Logo, request.Background, request.Icon);
            File.WriteAllText(Path.Combine(stage, InstallNotes), Notes(spec, executable));

            var payloadZip = Path.Combine(work, "payload.zip");
            Pack(stage, payloadZip, null, part => Report(progress, LauncherBuildStep.PackingInstaller, PackingInstallerStart, PackingZipStart, part));
            var zipTarget = Path.Combine(output, ZipFileName(spec.ServerName, request.Version, spec.TestOnThisComputer));
            var zipTemporary = zipTarget + ".tmp";
            Pack(stage, zipTemporary, Path.GetFileName(stage), part => Report(progress, LauncherBuildStep.PackingZip, PackingZipStart, WritingInstallerStart, part));
            File.Move(zipTemporary, zipTarget, overwrite: true);

            Report(progress, LauncherBuildStep.WritingInstaller, WritingInstallerStart, Finished, 0);
            var setupTarget = Path.Combine(output, SetupFileName(spec.ServerName, spec.TestOnThisComputer));
            InstallerPayload.Write(kit.InstallerExecutable, payloadZip, new InstallerHeader(spec.ServerName, request.Version, executable, $"{BrandingSpec.FolderName}/{spec.Icon}", $"{BrandingSpec.FolderName}/{spec.Logo}", spec.AccentColor), setupTarget);
            Report(progress, LauncherBuildStep.Done, Finished, Finished, 1);
            return new LauncherBuildResult(setupTarget, zipTarget);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new HostingException($"The launcher could not be built: {ex.Message}", "Pick a folder you can write to and make sure no earlier build is open.", ex);
        }
        finally
        {
            if (Directory.Exists(work))
            {
                Directory.Delete(work, recursive: true);
            }
        }
    }

    private static string Notes(BrandingSpec spec, string executable) =>
        $"{spec.ServerName}{Environment.NewLine}{Environment.NewLine}" +
        $"1. Unpack this folder anywhere you can write to (not Program Files).{Environment.NewLine}" +
        $"2. Open {executable}.{Environment.NewLine}" +
        $"3. Choose your game client folder in Options, then log in.{Environment.NewLine}" +
        (spec.TestOnThisComputer ? $"{Environment.NewLine}This is a test launcher. It only works on the computer that runs the server. Do not give it to players.{Environment.NewLine}" : "");

    private static void Report(IProgress<LauncherBuildProgress>? progress, LauncherBuildStep step, double from, double to, double part) =>
        progress?.Report(new LauncherBuildProgress(step, from + (to - from) * part));

    private static void CopyFolder(string source, string target, Action<double> advance)
    {
        Directory.CreateDirectory(target);
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        for (var index = 0; index < files.Length; index++)
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, files[index]));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(files[index], destination);
            advance((index + 1d) / files.Length);
        }
    }

    private static void Pack(string source, string target, string? root, Action<double> advance)
    {
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        using var archive = ZipFile.Open(target, ZipArchiveMode.Create);
        for (var index = 0; index < files.Length; index++)
        {
            var name = Path.GetRelativePath(source, files[index]).Replace(Path.DirectorySeparatorChar, '/');
            archive.CreateEntryFromFile(files[index], root is null ? name : root + "/" + name, CompressionLevel.Optimal);
            advance((index + 1d) / files.Length);
        }
    }
}
