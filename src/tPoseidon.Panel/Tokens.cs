using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace tPoseidon.Panel;

public static class Glyph
{
    public const string Play = "";
    public const string Stop = "";
    public const string Restart = "";
    public const string Log = "";
    public const string More = "";
    public const string Client = "";
    public const string Copy = "";
    public const string Clear = "";
    public const string Check = "";
    public const string Warn = "";
    public const string Error = "";
    public const string Info = "";
    public const string Shield = "";

    public const string Admin = "";

    private const double ViewBox = 16;

    private static readonly Dictionary<string, (string Data, bool Filled)> Shapes = new(StringComparer.Ordinal)
    {
        [Play] = ("M4.5,2.5 L13,8 L4.5,13.5 Z", false),
        [Stop] = ("M3.5,3.5 H12.5 V12.5 H3.5 Z", false),
        [Restart] = ("M13.2,8 A5.2,5.2 0 1 1 11.6,4.25 M12,1.6 V4.6 H9", false),
        [Log] = ("M4,1.5 H9.5 L12.5,4.5 V14.5 H4 Z M9.5,1.5 V4.5 H12.5 M6,8 H10.5 M6,10.5 H10.5", false),
        [More] = ("M2.5,8 A1,1 0 1 0 4.5,8 A1,1 0 1 0 2.5,8 Z M7,8 A1,1 0 1 0 9,8 A1,1 0 1 0 7,8 Z M11.5,8 A1,1 0 1 0 13.5,8 A1,1 0 1 0 11.5,8 Z", true),
        [Client] = ("M4.5,4.5 H11.5 A3.5,3.5 0 0 1 15,8 V10 A2,2 0 0 1 11.6,11.4 L10.2,10 H5.8 L4.4,11.4 A2,2 0 0 1 1,10 V8 A3.5,3.5 0 0 1 4.5,4.5 Z M4.5,6.5 V9 M3.25,7.75 H5.75 M10.5,7 V7.01 M12,8.5 V8.51", false),
        [Copy] = ("M5.5,5.5 H13.5 V14.5 H5.5 Z M3,11.5 H2.5 V1.5 H10.5 V2", false),
        [Clear] = ("M2.5,4 H13.5 M6,4 V2.5 H10 V4 M4,4 L4.8,14.5 H11.2 L12,4 M6.8,6.5 V12 M9.2,6.5 V12", false),
        [Check] = ("M2.5,8.5 L6,12 L13.5,4.5", false),
        [Warn] = ("M8,1.8 L15,14 H1 Z M8,6 V9.6 M8,11.6 V11.9", false),
        [Error] = ("M1.5,8 A6.5,6.5 0 1 0 14.5,8 A6.5,6.5 0 1 0 1.5,8 M5.6,5.6 L10.4,10.4 M10.4,5.6 L5.6,10.4", false),
        [Info] = ("M1.5,8 A6.5,6.5 0 1 0 14.5,8 A6.5,6.5 0 1 0 1.5,8 M8,7.2 V11.6 M8,4.6 V4.9", false),
        [Shield] = ("M8,1.5 L13.5,3.5 V8 C13.5,11.2 11,13.5 8,14.5 C5,13.5 2.5,11.2 2.5,8 V3.5 Z", false),
        [Admin] = ("M8,1.8 A2.9,2.9 0 1 0 8,7.6 A2.9,2.9 0 1 0 8,1.8 Z M2.5,14.5 C2.5,11.4 5,9.6 8,9.6 C11,9.6 13.5,11.4 13.5,14.5", false),
    };

    public static bool UsesFont { get; } = OperatingSystem.IsWindows();

    public static Control Drawn(string glyph, double size, IBrush foreground, out Path path)
    {
        var (data, filled) = Shapes[glyph];
        path = new Path
        {
            Data = Geometry.Parse(data),
            Stroke = filled ? null : foreground,
            Fill = filled ? foreground : null,
            StrokeThickness = 1.4,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        var canvas = new Canvas { Width = ViewBox, Height = ViewBox };
        canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas };
    }

    public static void Restyle(Path path, string glyph, IBrush foreground)
    {
        var (data, filled) = Shapes[glyph];
        path.Data = Geometry.Parse(data);
        path.Stroke = filled ? null : foreground;
        path.Fill = filled ? foreground : null;
    }
}

public sealed class GlyphView : Decorator
{
    private readonly TextBlock? _text;
    private readonly Path? _path;
    private string _glyph;
    private IBrush _foreground;

    public GlyphView(string glyph, double size, IBrush foreground)
    {
        _glyph = glyph;
        _foreground = foreground;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
        if (Glyph.UsesFont)
        {
            _text = new TextBlock { Text = glyph, FontFamily = Tokens.IconFont, FontSize = size, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            Child = _text;
        }
        else
        {
            Child = Glyph.Drawn(glyph, size, foreground, out var path);
            _path = path;
        }
    }

    public string Symbol
    {
        get => _glyph;
        set
        {
            _glyph = value;
            Apply();
        }
    }

    public IBrush Foreground
    {
        get => _foreground;
        set
        {
            _foreground = value;
            Apply();
        }
    }

    private void Apply()
    {
        if (_text is not null)
        {
            _text.Text = _glyph;
            _text.Foreground = _foreground;
        }
        else if (_path is not null)
        {
            Glyph.Restyle(_path, _glyph, _foreground);
        }
    }
}

public static class Tokens
{
    public static readonly FontFamily UiFont = OperatingSystem.IsWindows() ? new("Segoe UI Variable Text, Segoe UI") : FontFamily.Default;
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly FontFamily MonoFont = new(
        OperatingSystem.IsWindows() ? "Cascadia Mono, Consolas, monospace"
        : OperatingSystem.IsMacOS() ? "Menlo, Monaco, monospace"
        : "DejaVu Sans Mono, Liberation Mono, monospace");
    public static readonly IBrush Surface0 = Brush("#14171C");
    public static readonly IBrush Surface1 = Brush("#1B1F26");
    public static readonly IBrush Surface2 = Brush("#222730");
    public static readonly IBrush Surface3 = Brush("#2B313C");
    public static readonly IBrush BorderSubtle = Brush("#2A303B");
    public static readonly IBrush TextPrimary = Brush("#E6E9EF");
    public static readonly IBrush TextSecondary = Brush("#9AA3B2");
    public static readonly IBrush TextMuted = Brush("#6B7585");
    public static readonly IBrush Accent = Brush("#5B9DFF");
    public static readonly IBrush AccentHover = Brush("#7DB2FF");
    public static readonly IBrush AccentPressed = Brush("#4A88E6");
    public static readonly IBrush AccentSoft = Brush("#1F3A66");
    public static readonly IBrush OnAccent = Brush("#0B1B33");
    public static readonly IBrush Success = Brush("#4ADE80");
    public static readonly IBrush Warning = Brush("#FBBF24");
    public static readonly IBrush Danger = Brush("#F87171");
    public static readonly IBrush SuccessTint = Brush("#1A2F26");
    public static readonly IBrush WarningTint = Brush("#2E2814");
    public static readonly IBrush DangerTint = Brush("#32191C");
    public static readonly IBrush InfoTint = Brush("#17283F");
    public const double FontSizeBody = 12;
    public const double FontSizeCaption = 11;
    public const double HeaderHeight = 48;
    public const double StatusBarHeight = 24;
    public const double RowHeight = 36;

    public static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    public static TextBlock Caption(string text = "") =>
        new() { Text = text, FontSize = FontSizeCaption, Foreground = TextSecondary, VerticalAlignment = VerticalAlignment.Center };

    public static GlyphView Glyphs(string glyph, double size = 12, IBrush? foreground = null) =>
        new(glyph, size, foreground ?? TextPrimary);

    public static Button IconButton(string glyph, string tip, IBrush? foreground = null)
    {
        var button = new Button
        {
            Content = Glyphs(glyph, 12, foreground),
            Width = 26,
            Height = 26,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Foreground = foreground ?? TextPrimary,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add("flat");
        ToolTip.SetTip(button, tip);
        return button;
    }

    public static Button ToolbarButton(string glyph, string text, string tip)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        content.Children.Add(Glyphs(glyph));
        content.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var button = new Button { Content = content, Height = 26, MinHeight = 0, Padding = new Thickness(8, 0), Foreground = TextPrimary, VerticalContentAlignment = VerticalAlignment.Center };
        button.Classes.Add("flat");
        ToolTip.SetTip(button, tip);
        return button;
    }
}

public static class Skins
{
    public static void Register(Styles styles)
    {
        Button("flat", Brushes.Transparent, Tokens.Surface2, Tokens.Surface3, null, null, Brushes.Transparent);
        Button("primary", Tokens.Accent, Tokens.AccentHover, Tokens.AccentPressed, Tokens.OnAccent, Tokens.OnAccent, Brushes.Transparent);
        Button("secondary", Tokens.Surface2, Tokens.Surface3, Tokens.BorderSubtle, null, null, Tokens.BorderSubtle);
        Toggle("chip");
        Field();
        Row();

        void Button(string cls, IBrush normal, IBrush hover, IBrush pressed, IBrush? fg, IBrush? fgHover, IBrush border)
        {
            styles.Add(Presenter($"Button.{cls}", normal, fg, border));
            styles.Add(Presenter($"Button.{cls}:pointerover", hover, fgHover, border));
            styles.Add(Presenter($"Button.{cls}:pressed", pressed, fgHover, border));
            styles.Add(Presenter($"Button.{cls}:disabled", Tokens.Surface2, Tokens.TextMuted, Tokens.BorderSubtle));
        }

        void Toggle(string cls)
        {
            styles.Add(Presenter($"ToggleButton.{cls}", Tokens.Surface2, null, Tokens.BorderSubtle));
            styles.Add(Presenter($"ToggleButton.{cls}:pointerover", Tokens.Surface3, null, Tokens.BorderSubtle));
            styles.Add(Presenter($"ToggleButton.{cls}:checked", Tokens.AccentSoft, Tokens.TextPrimary, Tokens.Accent));
            styles.Add(Presenter($"ToggleButton.{cls}:checked:pointerover", Tokens.AccentSoft, Tokens.TextPrimary, Tokens.AccentHover));
        }

        void Field()
        {
            styles.Add(Border("TextBox", Tokens.Surface1, Tokens.BorderSubtle));
            styles.Add(Border("TextBox:pointerover", Tokens.Surface1, Tokens.Surface3));
            styles.Add(Border("TextBox:focus", Tokens.Surface1, Tokens.Accent));
            styles.Add(Border("TextBox.bad", Tokens.Surface1, Tokens.Danger));
            styles.Add(Border("TextBox.bad:pointerover", Tokens.Surface1, Tokens.Danger));
            styles.Add(Border("TextBox.bad:focus", Tokens.Surface1, Tokens.Danger));
        }

        void Row()
        {
            styles.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(0)), new Setter(Layoutable.MinHeightProperty, 0.0) } });
        }

        Style Presenter(string selector, IBrush background, IBrush? foreground, IBrush border)
        {
            var parts = selector.Split(':', 2);
            var cls = parts[0].Split('.');
            var style = new Style(x => Build(x, cls[0], cls[1], parts.Length > 1 ? parts[1] : null).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"));
            style.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, background));
            style.Setters.Add(new Setter(ContentPresenter.BorderBrushProperty, border));
            style.Setters.Add(new Setter(ContentPresenter.CornerRadiusProperty, new CornerRadius(6)));
            if (foreground is not null)
            {
                style.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, foreground));
            }

            return style;
        }

        Style Border(string selector, IBrush background, IBrush border)
        {
            var parts = selector.Split(':', 2);
            var head = parts[0].Split('.');
            var style = new Style(x => Build(x, head[0], head.Length > 1 ? head[1] : null, parts.Length > 1 ? parts[1] : null).Template().OfType<Avalonia.Controls.Border>().Name("PART_BorderElement"));
            style.Setters.Add(new Setter(Avalonia.Controls.Border.BackgroundProperty, background));
            style.Setters.Add(new Setter(Avalonia.Controls.Border.BorderBrushProperty, border));
            return style;
        }
    }

    private static Selector Build(Selector? from, string type, string? cls, string? pseudo)
    {
        var s = type switch
        {
            "Button" => from.OfType<Avalonia.Controls.Button>(),
            "ToggleButton" => from.OfType<ToggleButton>(),
            _ => from.OfType<TextBox>(),
        };
        if (cls is not null)
        {
            s = s.Class(cls);
        }

        if (pseudo is not null)
        {
            foreach (var p in pseudo.Split(':'))
            {
                s = s.Class(":" + p);
            }
        }

        return s;
    }
}

public sealed class Banner : Border
{
    private readonly GlyphView _icon = Tokens.Glyphs(Glyph.Info, 14);
    private readonly TextBlock _title = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap, FontSize = Tokens.FontSizeCaption };

    public Banner()
    {
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(12, 10);
        BorderThickness = new Thickness(1);
        IsVisible = false;
        _icon.VerticalAlignment = VerticalAlignment.Top;
        _icon.Margin = new Thickness(0, 2, 10, 0);
        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(_title);
        text.Children.Add(_detail);
        var dock = new DockPanel();
        DockPanel.SetDock(_icon, Dock.Left);
        dock.Children.Add(_icon);
        dock.Children.Add(text);
        Child = dock;
    }

    public void Show(BannerKind kind, string title, string? detail = null)
    {
        var (glyph, tint, accent) = kind switch
        {
            BannerKind.Success => (Glyph.Check, Tokens.SuccessTint, Tokens.Success),
            BannerKind.Warning => (Glyph.Warn, Tokens.WarningTint, Tokens.Warning),
            BannerKind.Error => (Glyph.Error, Tokens.DangerTint, Tokens.Danger),
            _ => (Glyph.Info, Tokens.InfoTint, Tokens.Accent),
        };
        _icon.Symbol = glyph;
        _icon.Foreground = accent;
        Background = tint;
        BorderBrush = accent;
        _title.Text = title;
        _detail.Text = detail ?? "";
        _detail.IsVisible = !string.IsNullOrEmpty(detail);
        IsVisible = true;
    }

    public void Hide() => IsVisible = false;
}

public enum BannerKind
{
    Info,
    Success,
    Warning,
    Error,
}
