using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public sealed class ChannelContext
{
    public const double FieldHeight = 30;

    private OriginalFileGuard? _guard;

    public ChannelContext(InstallPaths paths, HostSettings settings, Banner notice)
    {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Notice = notice ?? throw new ArgumentNullException(nameof(notice));
    }

    public InstallPaths Paths { get; }

    public HostSettings Settings { get; }

    public Banner Notice { get; }

    public string NewsFolder => Path.Combine(Paths.Updates, ManifestBuilder.NewsFolder);

    public event Action? Changed;

    public static TextBox Field(string watermark) =>
        new() { Watermark = watermark, Height = FieldHeight, MinHeight = 0, VerticalContentAlignment = VerticalAlignment.Center };

    public static Button Small(string text, bool primary = false)
    {
        var button = new Button { Content = text, Height = FieldHeight, MinHeight = 0, Padding = new Thickness(14, 0), VerticalContentAlignment = VerticalAlignment.Center };
        button.Classes.Add(primary ? "primary" : "secondary");
        return button;
    }

    public static StackPanel Labelled(string label, params Control[] controls)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(Tokens.Caption(label));
        foreach (var control in controls)
        {
            stack.Children.Add(control);
        }

        return stack;
    }

    public static StackPanel RowOf(params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var control in controls)
        {
            row.Children.Add(control);
        }

        return row;
    }

    public static Bitmap? Picture(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return new Bitmap(new MemoryStream(bytes));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    public static string Size(long bytes) =>
        bytes >= 1048576 ? $"{bytes / 1048576.0:0.#} MB" : bytes >= 1024 ? $"{bytes / 1024.0:0.#} KB" : $"{bytes} B";

    public async Task<OriginalFileGuard> GuardAsync() =>
        _guard ??= await Task.Run(() => UpdateChannel.OriginalGuard(Settings)).ConfigureAwait(true);

    public void ForgetGuard() => _guard = null;

    public void RaiseChanged() => Changed?.Invoke();

    public void Report(Exception error)
    {
        if (error is HostingException hosting)
        {
            Notice.Show(BannerKind.Error, hosting.Summary, hosting.Hint);
            LogHub.Write(LogHub.PanelSource, LogLevel.Error, hosting.Message);
            return;
        }

        Notice.Show(BannerKind.Error, error.Message);
        LogHub.Write(LogHub.PanelSource, LogLevel.Error, error.Message);
    }

    public static bool IsExpected(Exception error) =>
        error is HostingException or InvalidDataException or IOException or UnauthorizedAccessException;

    public static IBrush Tint(FileState state) => state switch
    {
        FileState.Added => Tokens.Success,
        FileState.Changed => Tokens.Accent,
        FileState.Removed => Tokens.Danger,
        _ => Tokens.TextSecondary,
    };
}
