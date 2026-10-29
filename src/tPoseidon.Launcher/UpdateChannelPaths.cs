namespace tPoseidon.Launcher;

public static class UpdateChannelPaths
{
    public const string Manifest = "/launcher/manifest.json";
    public const string Banner = "/launcher/banner";
    private const string FilesPrefix = "/launcher/files/";
    private const string NewsPicturePrefix = "/launcher/news-picture/";

    public static string NewsPicture(string name) => NewsPicturePrefix + Uri.EscapeDataString(name);

    public static string File(string relative) =>
        FilesPrefix + string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));
}
