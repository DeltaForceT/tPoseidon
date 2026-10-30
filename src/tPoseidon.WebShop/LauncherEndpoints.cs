using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.WebShop;

public sealed class LauncherEndpoints
{
    public const string UpdatesVariable = "MT_UPDATES_DIR";
    public const string ManifestPath = "/launcher/manifest.json";
    public const string FilesPrefix = "/launcher/files/";
    public const string SignaturePath = "/launcher/manifest.sig";
    public const string LauncherFilesPrefix = "/launcher/launcher-files/";
    public const string BannerPath = "/launcher/banner";
    public const string NewsPicturePrefix = "/launcher/news-picture/";

    private readonly string? _updates;

    public LauncherEndpoints(string? updatesFolder)
    {
        _updates = string.IsNullOrWhiteSpace(updatesFolder) ? null : Path.GetFullPath(updatesFolder);
    }

    public static LauncherEndpoints FromEnvironment() =>
        new(Environment.GetEnvironmentVariable(UpdatesVariable));

    public async Task ManifestAsync(HttpContext context)
    {
        if (LoadManifest() is not { } manifest)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.Headers.CacheControl = "no-cache";
        context.Response.ContentType = "application/json";
        await context.Response.Body.WriteAsync(manifest.Json, context.RequestAborted).ConfigureAwait(false);
    }

    public async Task SignatureAsync(HttpContext context)
    {
        var path = _updates is null ? null : Path.Combine(_updates, ManifestSignature.SignatureFile);
        if (path is null || !File.Exists(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.Headers.CacheControl = "no-cache";
        context.Response.ContentType = "application/json";
        await context.Response.SendFileAsync(path, context.RequestAborted).ConfigureAwait(false);
    }

    public async Task BannerAsync(HttpContext context)
    {
        if (LoadManifest() is not { Manifest.Banner: { } banner } || _updates is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var path = Path.Combine(_updates, banner);
        if (!File.Exists(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = banner.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.SendFileAsync(path, context.RequestAborted).ConfigureAwait(false);
    }

    public async Task NewsPictureAsync(HttpContext context, string name)
    {
        if (_updates is null || LoadManifest()?.Manifest.News.FirstOrDefault(item => string.Equals(item.Picture, name, StringComparison.Ordinal)) is not { Picture: { } picture })
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var path = Path.Combine(_updates, ManifestBuilder.NewsFolder, picture);
        if (!File.Exists(path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = picture.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.SendFileAsync(path, context.RequestAborted).ConfigureAwait(false);
    }

    public Task FileAsync(HttpContext context, string relative) =>
        ServeAsync(context, relative, manifest => manifest.Files, file => file.Group is null
            ? Path.Combine(_updates!, ManifestBuilder.ClientFolder)
            : Path.Combine(_updates!, ManifestBuilder.PacksFolder, file.Group));

    public Task LauncherFileAsync(HttpContext context, string relative) =>
        ServeAsync(context, relative, manifest => manifest.Launcher?.Files ?? [], _ => Path.Combine(_updates!, ManifestBuilder.LauncherFolder));

    private async Task ServeAsync(HttpContext context, string relative, Func<LauncherManifest, IReadOnlyList<ManifestFile>> list, Func<ManifestFile, string> sourceOf)
    {
        if (_updates is null || !ClientPath.TryNormalize(relative, out var normalized))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var listed = LoadManifest() is { } loaded ? list(loaded.Manifest).FirstOrDefault(file => string.Equals(file.Path, normalized, StringComparison.Ordinal)) : null;
        if (listed is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var path = ClientPath.Resolve(sourceOf(listed), listed.Path);
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (info.Length != listed.Size)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }

        long start = 0;
        context.Response.Headers.AcceptRanges = "bytes";
        context.Response.ContentType = "application/octet-stream";
        var range = context.Request.GetTypedHeaders().Range?.Ranges.FirstOrDefault();
        if (range is { From: { } from } && from < info.Length)
        {
            start = from;
            context.Response.StatusCode = StatusCodes.Status206PartialContent;
            context.Response.Headers.ContentRange = new ContentRangeHeaderValue(start, info.Length - 1, info.Length).ToString();
        }
        else if (range is not null)
        {
            context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            return;
        }

        context.Response.ContentLength = info.Length - start;
        await context.Response.SendFileAsync(path, start, info.Length - start, context.RequestAborted).ConfigureAwait(false);
    }

    private (LauncherManifest Manifest, byte[] Json)? LoadManifest()
    {
        if (_updates is null)
        {
            return null;
        }

        var path = Path.Combine(_updates, ManifestBuilder.ManifestFile);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = File.ReadAllBytes(path);
        return (LauncherManifest.Parse(json), json);
    }
}
