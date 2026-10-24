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
    if (options.Product == PackOptions.DrillEggProduct)
    {
        using var drillEgg = new DrillEggPackageBuilder(RepoRoot.Path, options, releaseLock);
        Console.WriteLine($"{ReleaseManifest.DrillEggProduct} {drillEgg.Identity}");
        var eggs = new List<PackageResult>();
        foreach (var rid in options.Rids)
        {
            foreach (var package in await drillEgg.BuildAsync(rid, cancellation.Token).ConfigureAwait(false))
            {
                eggs.Add(package);
                Console.WriteLine($"{rid}: {package.Path} ({package.Bytes} bytes)");
                Console.WriteLine($"{rid}: sha256 {package.Sha256}");
            }
        }

        var eggManifest = Path.Combine(drillEgg.Output, ReleaseManifest.FileName(ReleaseManifest.DrillEggFilePrefix, options.Version));
        new ReleaseManifest(ReleaseManifest.DrillEggProduct, drillEgg.Identity, eggs).Write(eggManifest, DateTimeOffset.UtcNow);
        Console.WriteLine($"manifest: {eggManifest}");
        return 0;
    }

    var builder = new PackageBuilder(RepoRoot.Path, options, releaseLock);
    Console.WriteLine($"{ReleaseManifest.Product} {builder.Identity}");
    var packages = new List<PackageResult>();
    foreach (var rid in options.Rids)
    {
        var package = await builder.BuildAsync(rid, cancellation.Token).ConfigureAwait(false);
        packages.Add(package);
        Console.WriteLine($"{rid}: {package.Path}");
        Console.WriteLine($"{rid}: sha256 {package.Sha256}");
    }

    var manifestPath = Path.Combine(builder.Output, ReleaseManifest.FileName(ReleaseManifest.FilePrefix, options.Version));
    new ReleaseManifest(ReleaseManifest.Product, builder.Identity, packages).Write(manifestPath, DateTimeOffset.UtcNow);
    Console.WriteLine($"manifest: {manifestPath}");
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or IOException or HttpRequestException or InvalidOperationException)
{
    Console.Error.WriteLine("error: " + ex.Message);
    return 1;
}
