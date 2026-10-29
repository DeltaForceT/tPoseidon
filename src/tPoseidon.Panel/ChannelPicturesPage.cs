using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public sealed class ChannelPicturesPage : StackPanel
{
    public const int RecommendedWidth = 630;
    public const int RecommendedHeight = 210;

    private readonly ChannelContext _context;
    private readonly Image _image = new() { Stretch = Stretch.UniformToFill };
    private readonly Border _frame = new() { Width = RecommendedWidth, Height = RecommendedHeight, CornerRadius = new CornerRadius(8), ClipToBounds = true, Background = Tokens.Surface2, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
    private readonly TextBlock _info = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly Button _choose = ChannelContext.Small("Choose or replace banner...");
    private readonly Button _remove = ChannelContext.Small("Remove banner");

    public ChannelPicturesPage(ChannelContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Spacing = 12;
        _frame.Child = _image;
        Children.Add(Tokens.Caption($"The banner is the picture at the top of the launcher. A PNG or JPG of at most 2 MB, at least {RecommendedWidth} x {RecommendedHeight} pixels. News pictures are added to each post on the News page."));
        Children.Add(_frame);
        Children.Add(_info);
        Children.Add(ChannelContext.RowOf(_choose, _remove));
        _choose.Click += async (_, _) => await ChooseAsync().ConfigureAwait(true);
        _remove.Click += (_, _) => RemoveBanner();
        Reload();
    }

    public Image BannerImage => _image;

    public TextBlock Info => _info;

    public Button RemoveButton => _remove;

    public void Reload()
    {
        var path = UpdateFolder.BannerPath(_context.Paths);
        _remove.IsEnabled = path is not null;
        if (path is null)
        {
            _image.Source = null;
            _info.Text = "No banner: the launcher shows the server's accent colour.";
            return;
        }

        try
        {
            Describe(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _image.Source = null;
            _context.Report(ex);
        }
    }

    public bool SetBanner(byte[] picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        try
        {
            var info = UpdateFolder.SetBanner(_context.Paths, picture);
            Describe(picture);
            if (info.Width < RecommendedWidth || info.Height < RecommendedHeight)
            {
                _context.Notice.Show(BannerKind.Warning, "Banner saved, but it is small", $"The launcher shows it at {RecommendedWidth} x {RecommendedHeight}, so a {info.Width} x {info.Height} picture looks blurry.");
            }
            else
            {
                _context.Notice.Show(BannerKind.Success, "Banner saved", "Players see it after you publish.");
            }

            _remove.IsEnabled = true;
            _context.RaiseChanged();
            return true;
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return false;
        }
    }

    public void RemoveBanner()
    {
        try
        {
            UpdateFolder.RemoveBanner(_context.Paths);
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return;
        }

        Reload();
        _context.RaiseChanged();
    }

    private void Describe(byte[] picture)
    {
        _image.Source = ChannelContext.Picture(picture);
        try
        {
            var info = PictureInfo.Inspect(picture);
            _info.Text = $"{info.Format} {info.Width} x {info.Height}, {ChannelContext.Size(picture.Length)}";
        }
        catch (InvalidDataException ex)
        {
            _info.Text = ex.Message;
        }
    }

    private async Task ChooseAsync()
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the banner",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Pictures") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }],
        }).ConfigureAwait(true);
        if (files.Count > 0)
        {
            await using var stream = await files[0].OpenReadAsync().ConfigureAwait(true);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory).ConfigureAwait(true);
            SetBanner(memory.ToArray());
        }
    }
}
