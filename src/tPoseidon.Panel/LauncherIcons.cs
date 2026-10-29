using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;

namespace tPoseidon.Panel;

public static class LauncherIcons
{
    private static readonly int[] Sides = [256, 48, 32, 16];

    public static byte[] FromLogo(byte[] logo)
    {
        ArgumentNullException.ThrowIfNull(logo);
        IconMaker.Dimensions(logo);
        try
        {
            using var source = new Bitmap(new MemoryStream(logo));
            var pictures = Sides.Select(side => Render(source, side)).ToList();
            return IconMaker.FromPngs(pictures);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            throw new HostingException($"The logo could not be read: {ex.Message}", "Choose another PNG picture.", ex);
        }
    }

    public static PresetAssets Preset() => new(Asset("logo-1024.png"), Asset("launcher-background.png"), Asset("tposeidon.ico"));

    private static byte[] Asset(string name)
    {
        using var stream = AssetLoader.Open(PanelAssets.Uri(name));
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static byte[] Render(Bitmap source, int side)
    {
        using var target = new RenderTargetBitmap(new PixelSize(side, side), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            var scale = Math.Min((double)side / source.PixelSize.Width, (double)side / source.PixelSize.Height);
            var width = source.PixelSize.Width * scale;
            var height = source.PixelSize.Height * scale;
            var destination = new Rect((side - width) / 2, (side - height) / 2, width, height);
            context.DrawImage(source, new Rect(0, 0, source.PixelSize.Width, source.PixelSize.Height), destination);
        }

        using var memory = new MemoryStream();
        target.Save(memory);
        return memory.ToArray();
    }
}
