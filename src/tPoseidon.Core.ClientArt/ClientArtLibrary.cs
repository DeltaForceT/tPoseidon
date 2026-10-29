using tPoseidon.Core.Common;

namespace tPoseidon.Core.ClientArt;

public sealed class ClientArtLibrary
{
    public const int PictureLimit = 4096;
    public const int OpenFileLimit = 8;
    private const int SpriteFrame = -1;

    private readonly string _clientFolder;
    private readonly Action<string> _unreadable;
    private readonly object _gate = new();
    private readonly Dictionary<(string File, int Frame), LinkedListNode<(string File, int Frame, ClientPicture? Picture)>> _pictures = [];
    private readonly LinkedList<(string File, int Frame, ClientPicture? Picture)> _pictureOrder = new();
    private readonly Dictionary<string, LinkedListNode<(string File, NriFile Sprite)>> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<(string File, NriFile Sprite)> _fileOrder = new();
    private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);

    public ClientArtLibrary(string clientFolder, Action<string> unreadable)
    {
        ArgumentException.ThrowIfNullOrEmpty(clientFolder);
        ArgumentNullException.ThrowIfNull(unreadable);
        _clientFolder = clientFolder;
        _unreadable = unreadable;
    }

    public ClientPicture? Icon(string file, int frame)
    {
        ArgumentNullException.ThrowIfNull(file);
        return frame < 0 ? null : Fetch(file, frame);
    }

    public ClientPicture? Sprite(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Fetch(file, SpriteFrame);
    }

    private ClientPicture? Fetch(string file, int frame)
    {
        if (file.Length == 0) return null;
        var key = (ClientFiles.Normalize(file).ToUpperInvariant(), frame);
        lock (_gate)
        {
            if (_pictures.TryGetValue(key, out var known))
            {
                _pictureOrder.Remove(known);
                _pictureOrder.AddFirst(known);
                return known.Value.Picture;
            }

            var picture = Load(file, frame);
            _pictures[key] = _pictureOrder.AddFirst((key.Item1, frame, picture));
            while (_pictures.Count > PictureLimit)
            {
                var last = _pictureOrder.Last!;
                _pictures.Remove((last.Value.File, last.Value.Frame));
                _pictureOrder.RemoveLast();
            }

            return picture;
        }
    }

    private ClientPicture? Load(string file, int frame)
    {
        try
        {
            var sprite = Open(file);
            return frame == SpriteFrame ? sprite.ReadStanding() : sprite.ReadImage(frame);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            if (_reported.Add(file))
            {
                _unreadable($"The picture file {file} could not be read: {ex.Message}");
            }

            return null;
        }
    }

    private NriFile Open(string file)
    {
        if (_files.TryGetValue(file, out var known))
        {
            _fileOrder.Remove(known);
            _fileOrder.AddFirst(known);
            return known.Value.Sprite;
        }

        var path = ClientFiles.Locate(ClientFiles.Combine(_clientFolder, file))
            ?? throw new FileNotFoundException("The file is not in the client folder.");
        var sprite = NriFile.Parse(File.ReadAllBytes(path));
        _files[file] = _fileOrder.AddFirst((file, sprite));
        while (_files.Count > OpenFileLimit)
        {
            _files.Remove(_fileOrder.Last!.Value.File);
            _fileOrder.RemoveLast();
        }

        return sprite;
    }
}
