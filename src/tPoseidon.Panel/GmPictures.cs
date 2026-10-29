using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using tPoseidon.Core.ClientArt;

namespace tPoseidon.Panel;

public sealed record GmArt(string File, int Frame, bool Standing)
{
    public static GmArt Icon(string file, int frame) => new(file, frame, false);

    public static GmArt Sprite(string file) => new(file, -1, true);

    public ClientPicture? Load(ClientArtLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return Standing ? library.Sprite(File) : library.Icon(File, Frame);
    }
}

public static class GmBitmaps
{
    private static readonly ConditionalWeakTable<ClientPicture, WriteableBitmap> Made = new();

    public static WriteableBitmap Of(ClientPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        return Made.GetValue(picture, Make);
    }

    private static WriteableBitmap Make(ClientPicture picture)
    {
        var bitmap = new WriteableBitmap(new PixelSize(picture.Width, picture.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = bitmap.Lock();
        var row = new byte[picture.Width * 4];
        for (var y = 0; y < picture.Height; y++)
        {
            for (var x = 0; x < picture.Width; x++)
            {
                var at = (y * picture.Width + x) * 4;
                var alpha = picture.Rgba[at + 3];
                row[x * 4] = (byte)(picture.Rgba[at + 2] * alpha / 255);
                row[x * 4 + 1] = (byte)(picture.Rgba[at + 1] * alpha / 255);
                row[x * 4 + 2] = (byte)(picture.Rgba[at] * alpha / 255);
                row[x * 4 + 3] = alpha;
            }

            System.Runtime.InteropServices.Marshal.Copy(row, 0, frame.Address + y * frame.RowBytes, row.Length);
        }

        return bitmap;
    }
}

public sealed class GmPictureSlot : Border
{
    private readonly GmModel _model;
    private readonly Image _image = new() { Stretch = Stretch.Fill, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly double _box;
    private readonly int _maxZoom;
    private GmArt? _art;
    private bool _attached;
    private Task _ready = Task.CompletedTask;

    public GmPictureSlot(GmModel model, double box, int maxZoom, GmArt? art = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _box = box;
        _maxZoom = maxZoom;
        _art = art;
        Width = box;
        Height = box;
        Child = _image;
        RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.None);
        AttachedToVisualTree += (_, _) =>
        {
            _attached = true;
            _ready = Refresh();
        };
        DetachedFromVisualTree += (_, _) => _attached = false;
    }

    public Task Ready => _ready;

    public GmArt? Art => _art;

    public IImage? Picture => _image.Source;

    public void Show(GmArt? art)
    {
        _art = art;
        _image.Source = null;
        if (_attached)
        {
            _ready = Refresh();
        }
    }

    private async Task Refresh()
    {
        var art = _art;
        if (art is null || _model.Art is not { } library || _image.Source is not null)
        {
            return;
        }

        var picture = await Task.Run(() => art.Load(library)).ConfigureAwait(true);
        if (picture is null || !_attached || !ReferenceEquals(art, _art))
        {
            return;
        }

        var zoom = Math.Max(1, Math.Min(_maxZoom, (int)Math.Min(_box / picture.Width, _box / picture.Height)));
        var fits = picture.Width * zoom <= _box && picture.Height * zoom <= _box;
        _image.Stretch = fits ? Stretch.Fill : Stretch.Uniform;
        _image.Width = fits ? picture.Width * zoom : _box;
        _image.Height = fits ? picture.Height * zoom : _box;
        _image.Source = GmBitmaps.Of(picture);
    }
}
