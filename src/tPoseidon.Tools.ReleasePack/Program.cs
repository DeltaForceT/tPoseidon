using tPoseidon.Core.Common;
using tPoseidon.Tools.ReleasePack;

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
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or HttpRequestException or InvalidOperationException)
{
    Console.Error.WriteLine("error: " + ex.Message);
    return 1;
}
