using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using tPoseidon.Core.Common;

namespace tPoseidon.Tools.ReleasePack;

internal static class PackageTools
{
    private const uint ElfMagic = 0x464C457F;
    private const uint MachO64Magic = 0xFEEDFACF;
    private const uint MachO64MagicSwapped = 0xCFFAEDFE;
    private const uint MachOFatMagic = 0xBEBAFECA;
    private const int CommitHashLength = 40;

    public static void Publish(string repoRoot, string project, string rid, string output, BuildIdentity identity, IEnumerable<string> properties)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            WorkingDirectory = repoRoot,
        };
        foreach (var argument in new[]
        {
            "publish", Path.Combine(repoRoot, project),
            "--configuration", "Release",
            "--runtime", rid,
            "--self-contained", "true",
            "--output", output,
            "--nologo",
            "--verbosity", "quiet",
            "-p:Version=" + identity.Version,
            "-p:SourceRevisionId=" + identity.Commit,
            "-p:BuildTag=" + identity.Tag,
            "-p:DebugType=none",
            "-p:DebugSymbols=false",
            "-p:SatelliteResourceLanguages=en",
        }.Concat(properties))
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("dotnet publish did not start.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet publish {project} -r {rid} failed (exit {process.ExitCode}).");
        }
    }

    public static string GitHead(string repoRoot)
    {
        var info = new ProcessStartInfo("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            WorkingDirectory = repoRoot,
        };
        info.ArgumentList.Add("rev-parse");
        info.ArgumentList.Add("HEAD");

        string commit;
        try
        {
            using var process = Process.Start(info) ?? throw new InvalidOperationException("git did not start.");
            commit = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"git rev-parse HEAD failed (exit {process.ExitCode}) in {repoRoot}.");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException("git is needed to record the commit of a release build: " + ex.Message, ex);
        }

        if (commit.Length != CommitHashLength || !commit.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException($"git rev-parse HEAD returned '{commit}', not a commit hash.");
        }

        return commit.ToLowerInvariant();
    }

    public static async Task<string> HashOfAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)).ToLowerInvariant();
    }

    public static UnixFileMode ModeFor(string path)
    {
        Span<byte> magic = stackalloc byte[4];
        using (var stream = File.OpenRead(path))
        {
            if (stream.Read(magic) < magic.Length)
            {
                return PackageArchive.RegularMode;
            }
        }

        var value = BitConverter.ToUInt32(magic);
        return value is ElfMagic or MachO64Magic or MachO64MagicSwapped or MachOFatMagic
            ? PackageArchive.ExecutableMode
            : PackageArchive.RegularMode;
    }

    public static string DotnetRoot()
    {
        var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        var root = new DirectoryInfo(Path.TrimEndingDirectorySeparator(runtimeDirectory)).Parent?.Parent?.Parent
            ?? throw new InvalidOperationException($"Cannot find the .NET install from {runtimeDirectory}.");
        if (!File.Exists(Path.Combine(root.FullName, "LICENSE.txt")))
        {
            throw new InvalidOperationException($"{root.FullName} has no LICENSE.txt; cannot include the .NET license.");
        }

        return root.FullName;
    }

    public static async Task<List<(string Source, string Entry)>> LicensesAsync(IEnumerable<RemoteFile> licenses, string cache, CancellationToken ct)
    {
        using var downloads = new VerifiedDownloads(cache);
        var files = new List<(string Source, string Entry)>();
        foreach (var license in licenses)
        {
            var url = license.Url ?? throw new InvalidDataException($"No URL for license {license.Path}.");
            files.Add((await downloads.FetchAsync(url, license.Sha256, ct).ConfigureAwait(false), "licenses/" + license.Path));
        }

        var dotnetRoot = DotnetRoot();
        files.Add((Path.Combine(dotnetRoot, "LICENSE.txt"), "licenses/dotnet/LICENSE.txt"));
        files.Add((Path.Combine(dotnetRoot, "ThirdPartyNotices.txt"), "licenses/dotnet/ThirdPartyNotices.txt"));
        return files;
    }
}
