using System.Text.Json;
using System.Text.Json.Serialization;
using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.Hosting;

public sealed record ServerFile(string Name, string Address, string? WebUrl, string? Note, string? Color = null)
{
    public const string FormatName = "tposeidon-server";
    public const int CurrentVersion = 1;
    public const string Extension = ".tposeidon-server";
    public const int MaxNameLength = 60;
    public const int MaxNoteLength = 500;
    public const int MaxFileBytes = 16 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ServerFile ForServer(HostSettings settings, string name, string? webUrl, string? note, string? color = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Network.Audience == PlayerAudience.Local)
        {
            throw new HostingException(
                "This server only accepts players on this computer, so there is nothing to share yet.",
                "Run tposeidon setup again and choose LAN or the internet as the audience, then open the ports (see docs/install/hosting-securely.md).",
                null);
        }

        if (!settings.ShopEnabled)
        {
            throw new HostingException(
                "Friends log in through the item shop's web port, and the item shop is turned off.",
                "Turn the shop on in tposeidon.json (\"shopEnabled\": true) and restart the servers.",
                null);
        }

        if (settings.Ports.Lobby != ClientAddressPatch.ClientLobbyPort)
        {
            throw new HostingException(
                $"The KR 2014 client always connects to lobby port {ClientAddressPatch.ClientLobbyPort}, but this server's lobby port is {settings.Ports.Lobby}.",
                $"Set the lobby port back to {ClientAddressPatch.ClientLobbyPort} in tposeidon.json.",
                null);
        }

        var address = settings.Network.PublicAddress.Trim();
        var web = string.IsNullOrWhiteSpace(webUrl) ? $"http://{address}:{settings.Ports.Shop}" : webUrl.Trim();
        return new ServerFile(name.Trim(), address, web, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), string.IsNullOrWhiteSpace(color) ? null : color.Trim()).Validated();
    }

    public static ServerFile Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new HostingException($"This is not a tPoseidon server file: {ex.Message}", ex);
        }

        if (document is null || !string.Equals(document.Format, FormatName, StringComparison.Ordinal))
        {
            throw new HostingException("This is not a tPoseidon server file.");
        }

        if (document.Version != CurrentVersion)
        {
            throw new HostingException($"This server file is version {document.Version}, and this launcher reads version {CurrentVersion}.", "Update the launcher, or ask the server owner to export the file again.", null);
        }

        return new ServerFile(document.Name ?? "", document.Address ?? "", document.WebUrl, document.Note, document.Color).Validated();
    }

    public static ServerFile Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxFileBytes)
            {
                throw new HostingException($"{path} is too big to be a server file.");
            }

            return Parse(File.ReadAllText(path));
        }
        catch (IOException ex)
        {
            throw new HostingException($"Could not read {path}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HostingException($"Could not read {path}: {ex.Message}", ex);
        }
    }

    public string ToJson()
    {
        var valid = Validated();
        return JsonSerializer.Serialize(new Document(FormatName, CurrentVersion, valid.Name, valid.Address, valid.WebUrl, valid.Note, valid.Color), JsonOptions);
    }

    public void Write(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = ToJson();
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, json + Environment.NewLine);
            File.Move(temporary, path, overwrite: true);
        }
        catch (IOException ex)
        {
            throw new HostingException($"Could not write {path}: {ex.Message}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HostingException($"Could not write {path}: {ex.Message}", ex);
        }
    }

    public Uri TicketEndpoint(int shopPort) => PlayTicketClient.Endpoint(Address, shopPort, WebUrl);

    public Uri WebAddress(int shopPort, string path) => new(PlayTicketClient.Endpoint(Address, shopPort, WebUrl), path);

    public ServerFile Validated()
    {
        var name = Name?.Trim() ?? "";
        var address = Address?.Trim() ?? "";
        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            throw new HostingException($"A server needs a name of 1 to {MaxNameLength} characters.");
        }

        if (address.Length == 0 || address.Any(c => char.IsWhiteSpace(c) || c is '/' or ':' or '\\'))
        {
            throw new HostingException($"'{address}' is not a server address.", "Use the server's IPv4 address or host name, without http:// and without a port.", null);
        }

        var note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            throw new HostingException($"The note can be at most {MaxNoteLength} characters.");
        }

        string? web = null;
        if (!string.IsNullOrWhiteSpace(WebUrl))
        {
            web = WebUrl.Trim();
            if (!Uri.TryCreate(web, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                throw new HostingException($"'{web}' is not a web address.", "Use something like https://play.example.org:9990.", null);
            }
        }

        if (!string.IsNullOrWhiteSpace(Color) && !LauncherManifest.IsColor(Color.Trim()))
        {
            throw new HostingException($"'{Color}' is not a colour like #3B82F6.");
        }

        return new ServerFile(name, address, web, note, string.IsNullOrWhiteSpace(Color) ? null : Color.Trim());
    }

    private sealed record Document(string? Format, int Version, string? Name, string? Address, string? WebUrl, string? Note, string? Color);
}
