using tPoseidon.Core.Common;
using tPoseidon.Tools.ReleasePack;

if (args.Length > 0 && args[0] is "make-key" or "sign")
{
    return SigningCommands.Run(args);
}

var options = PackOptions.Parse(args);
if (options is null)
{
    PackOptions.PrintUsage();
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

try
{
    var releaseLock = ReleaseLock.Load();
    var builder = new PackageBuilder(RepoRoot.Path, options, releaseLock);
    Console.WriteLine($"{ReleaseManifest.Product} {builder.Identity}");
    var packages = new List<PackageResult>();
    foreach (var rid in options.Rids)
    {
        var package = await builder.BuildAsync(rid, cancellation.Token).ConfigureAwait(false);
        packages.Add(package);
        Console.WriteLine($"{rid}: {package.Path}");
        Console.WriteLine($"{rid}: sha256 {package.Sha256}");

        if (PackageBuilder.HasLauncher(rid))
        {
            var launcher = await builder.BuildLauncherAsync(rid, cancellation.Token).ConfigureAwait(false);
            packages.Add(launcher);
            Console.WriteLine($"{rid} launcher: {launcher.Path}");
            Console.WriteLine($"{rid} launcher: sha256 {launcher.Sha256}");
        }
    }

    var manifestPath = Path.Combine(builder.Output, ReleaseManifest.FileName(ReleaseManifest.FilePrefix, options.Version));
    new ReleaseManifest(builder.Identity, packages).Write(manifestPath, DateTimeOffset.UtcNow);
    Console.WriteLine($"manifest: {manifestPath}");
    if (options.SignKey is { } keyFile)
    {
        var files = packages.Select(package => package.Path).Append(manifestPath).ToList();
        foreach (var written in ReleaseSigning.Sign(files, builder.Output, keyFile))
        {
            Console.WriteLine($"signed: {written}");
        }
    }

    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or HttpRequestException or InvalidOperationException)
{
    Console.Error.WriteLine("error: " + ex.Message);
    return 1;
}
