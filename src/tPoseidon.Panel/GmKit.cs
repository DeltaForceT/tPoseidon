using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel;

public sealed record GmConfirm(string Question, string Detail, string Yes);

public sealed class GmChecks
{
    private readonly List<string> _problems = [];

    public bool Ok => _problems.Count == 0;

    public IReadOnlyList<string> Problems => _problems;

    public string Text(TextBox box, string label, int maxBytes = 0, bool required = true)
    {
        ArgumentNullException.ThrowIfNull(box);
        var text = box.Text?.Trim() ?? "";
        if (required && text.Length == 0)
        {
            return Bad(box, $"{label} is empty.");
        }

        if (maxBytes > 0 && GameMasterToolCommands.ClientText().GetByteCount(text) > maxBytes)
        {
            return Bad(box, $"{label} is too long (at most {maxBytes} bytes in the game's text encoding).");
        }

        Good(box);
        return text;
    }

    public string Number(TextBox box, string label, long minimum, long maximum)
    {
        ArgumentNullException.ThrowIfNull(box);
        var text = box.Text?.Trim() ?? "";
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
        {
            return Bad(box, string.Create(CultureInfo.InvariantCulture, $"{label} must be a whole number from {minimum} to {maximum}."));
        }

        Good(box);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public string Time(TextBox box, string label)
    {
        ArgumentNullException.ThrowIfNull(box);
        var text = box.Text?.Trim() ?? "";
        if (!GameMasterToolCommands.TryParseTime(text, out var unixSeconds))
        {
            return Bad(box, $"{label} must be a date and time like 2026-10-24 20:00.");
        }

        Good(box);
        return unixSeconds.ToString(CultureInfo.InvariantCulture);
    }

    public void Require(bool condition, TextBox? box, string problem)
    {
        if (condition)
        {
            return;
        }

        if (box is not null)
        {
            Bad(box, problem);
        }
        else
        {
            _problems.Add(problem);
        }
    }

    private string Bad(TextBox box, string problem)
    {
        if (!box.Classes.Contains("bad"))
        {
            box.Classes.Add("bad");
        }

        _problems.Add(problem);
        return "";
    }

    private static void Good(TextBox box) => box.Classes.Remove("bad");
}

public sealed class GmKit
{
    public const int PickerRows = 200;

    private readonly List<Button> _actions = [];

    public GmKit(GmModel model, Window owner)
    {
        Model = model;
        Owner = owner;
        Model.Changed += () => Dispatcher.UIThread.Post(Refresh);
    }

    public GmModel Model { get; }

    public Window Owner { get; }

    public static TextBox Box(string watermark, double width = double.NaN, string text = "") =>
        new() { Watermark = watermark, Text = text, Width = width, Height = 28, MinHeight = 0, Padding = new Thickness(8, 4), VerticalContentAlignment = VerticalAlignment.Center, FontSize = Tokens.FontSizeBody };

    public static TextBox Lines(string watermark, double height = 64) =>
        new() { Watermark = watermark, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = height, MinHeight = 0, Padding = new Thickness(8, 5), FontSize = Tokens.FontSizeBody };

    public static Control Field(string label, Control control)
    {
        var stack = new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 10, 6), VerticalAlignment = VerticalAlignment.Bottom };
        stack.Children.Add(new TextBlock { Text = label, FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary });
        control.HorizontalAlignment = HorizontalAlignment.Left;
        stack.Children.Add(control);
        return stack;
    }

    public static WrapPanel Line(params Control[] controls)
    {
        var line = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in controls)
        {
            if (control is Button or ToggleButton or ComboBox)
            {
                control.Margin = new Thickness(0, 0, 8, 6);
                control.VerticalAlignment = VerticalAlignment.Bottom;
            }

            line.Children.Add(control);
        }

        return line;
    }

    public static TextBlock Hint(string text) =>
        new() { Text = text, FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextMuted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };

    public static Border Section(string title, string? hint, params Control[] rows)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 0, 0, 2) });
        if (hint is not null)
        {
            stack.Children.Add(Hint(hint));
        }

        foreach (var row in rows)
        {
            stack.Children.Add(row);
        }

        return new Border
        {
            Background = Tokens.Surface1,
            BorderBrush = Tokens.BorderSubtle,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 6),
            Child = stack,
        };
    }

    public static Control Tab(string description, params (string Heading, Control[] Sections)[] groups)
    {
        var page = new StackPanel { Spacing = 10 };
        page.Children.Add(new TextBlock { Text = description, Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap });
        foreach (var (heading, sections) in groups)
        {
            page.Children.Add(new TextBlock { Text = heading.ToUpperInvariant(), FontSize = Tokens.FontSizeCaption, FontWeight = FontWeight.SemiBold, Foreground = Tokens.Accent, Margin = new Thickness(2, 8, 0, -2) });
            foreach (var section in sections)
            {
                page.Children.Add(section);
            }
        }

        return page;
    }

    public static ComboBox Choice(IEnumerable<string> items, double width = double.NaN) =>
        new() { ItemsSource = items.ToList(), SelectedIndex = 0, Height = 28, MinHeight = 0, Width = width, FontSize = Tokens.FontSizeBody, VerticalContentAlignment = VerticalAlignment.Center };

    public static Button Plain(string text, bool primary = false)
    {
        var button = new Button { Content = text, Height = 28, MinHeight = 0, Padding = new Thickness(12, 0), VerticalContentAlignment = VerticalAlignment.Center };
        button.Classes.Add(primary ? "primary" : "secondary");
        if (!primary)
        {
            button.Foreground = Tokens.TextPrimary;
        }

        return button;
    }

    public static Control Presets(TextBox target, params (string Label, string Value)[] presets)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 10, 6), VerticalAlignment = VerticalAlignment.Bottom };
        foreach (var (label, value) in presets)
        {
            var chip = new Button { Content = label, Height = 24, MinHeight = 0, Padding = new Thickness(8, 0), FontSize = Tokens.FontSizeCaption, VerticalContentAlignment = VerticalAlignment.Center, Foreground = Tokens.TextSecondary };
            chip.Classes.Add("flat");
            chip.Click += (_, _) => target.Text = value;
            row.Children.Add(chip);
        }

        return row;
    }

    public static string Quote(string argument) =>
        argument.Length == 0 || argument.Any(char.IsWhiteSpace) ? "\"" + argument + "\"" : argument;

    public Button Tool(string label, string command, Func<GmChecks, IReadOnlyList<string>> build, Func<IReadOnlyList<string>, GmConfirm?>? confirm = null, Action<GmOutcome>? done = null, bool primary = false)
    {
        Model.Wire(command);
        return Act(label, build, confirm, async arguments =>
        {
            var outcome = await Model.ToolAsync($"{label} · {command} {string.Join(" ", arguments.Select(Quote))}".TrimEnd(), command, arguments).ConfigureAwait(true);
            if (outcome is not null)
            {
                done?.Invoke(outcome);
            }
        }, primary);
    }

    public Button Penalty(string label, string subcommand, Func<GmChecks, IReadOnlyList<string>> build, Func<IReadOnlyList<string>, GmConfirm?>? confirm = null, Action<GmOutcome>? done = null, bool primary = false)
    {
        Model.Wire(subcommand);
        return Act(label, build, confirm, async arguments =>
        {
            var outcome = await Model.PenaltyAsync($"{label} · {subcommand} {string.Join(" ", arguments.Select(Quote))}".TrimEnd(), subcommand, arguments).ConfigureAwait(true);
            if (outcome is not null)
            {
                done?.Invoke(outcome);
            }
        }, primary);
    }

    public Button Accounts(string label, string wired, Func<GmChecks, IReadOnlyList<string>> build, Func<IReadOnlyList<string>, Func<IGmAccountBook, GmOutcome>> action, Func<IReadOnlyList<string>, GmConfirm?>? confirm = null, Action<GmOutcome>? done = null, bool primary = false)
    {
        Model.Wire(wired);
        return Act(label, build, confirm, async arguments =>
        {
            var outcome = await Model.AccountsAsync($"{label} · {wired} {string.Join(" ", arguments.Select(Quote))}".TrimEnd(), action(arguments)).ConfigureAwait(true);
            if (outcome is not null)
            {
                done?.Invoke(outcome);
            }
        }, primary);
    }

    private Button Act(string label, Func<GmChecks, IReadOnlyList<string>> build, Func<IReadOnlyList<string>, GmConfirm?>? confirm, Func<IReadOnlyList<string>, Task> run, bool primary)
    {
        var button = Plain(label, primary);
        button.IsEnabled = !Model.Busy;
        button.Click += async (_, _) =>
        {
            var checks = new GmChecks();
            var arguments = build(checks);
            if (!checks.Ok)
            {
                await Model.Fail(label, "Check the marked fields. " + string.Join(" ", checks.Problems)).ConfigureAwait(true);
                return;
            }

            if (confirm?.Invoke(arguments) is { } question && !await AskWindow.ConfirmAsync(Owner, question.Question, question.Detail, question.Yes).ConfigureAwait(true))
            {
                return;
            }

            await run(arguments).ConfigureAwait(true);
        };
        _actions.Add(button);
        return button;
    }

    private void Refresh()
    {
        foreach (var button in _actions)
        {
            button.IsEnabled = !Model.Busy;
        }
    }
}

public sealed record GmPick(string Id, string Text, GmArt? Art = null);

public sealed class GmPicker : StackPanel
{
    public const double IconRowBox = 36;
    public const double SpriteRowBox = 56;
    public const double PreviewBox = 120;
    public const int PreviewZoom = 4;

    private readonly GmModel _model;
    private readonly Func<GameCatalog, string, IEnumerable<GmPick>> _query;
    private readonly ListBox _list = new() { Height = 150, Background = Tokens.Surface0, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), FontSize = Tokens.FontSizeBody };
    private readonly TextBlock _count = new() { FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextMuted, TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private readonly GmPictureSlot? _preview;
    private GameCatalog? _catalog;

    public GmPicker(GmModel model, string what, string idLabel, Func<GameCatalog, string, IEnumerable<GmPick>> query, bool pictures, params Control[] filters)
    {
        _model = model;
        _query = query;
        Spacing = 4;
        Search = GmKit.Box($"Search {what} by name or id", 240);
        Id = GmKit.Box(idLabel, 110);
        _list.ItemTemplate = new FuncDataTemplate<GmPick>((pick, _) => Row(pick), true);
        Search.TextChanged += (_, _) => Restart();
        Search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Flush();
            }
        };
        _debounce.Tick += (_, _) => Flush();
        _list.SelectionChanged += (_, _) =>
        {
            if (_list.SelectedItem is GmPick pick)
            {
                Id.Text = pick.Id;
                _preview?.Show(pick.Art);
                Picked?.Invoke(pick);
            }
        };
        var line = new List<Control> { GmKit.Field("Search", Search) };
        line.AddRange(filters);
        line.Add(GmKit.Field(idLabel, Id));
        Children.Add(GmKit.Line([.. line]));
        if (pictures)
        {
            _preview = new GmPictureSlot(model, PreviewBox, PreviewZoom);
            var frame = new Border { Child = _preview, Background = Tokens.Surface0, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            body.Children.Add(_list);
            Grid.SetColumn(frame, 1);
            body.Children.Add(frame);
            Children.Add(body);
        }
        else
        {
            Children.Add(_list);
        }

        Children.Add(_count);
        AttachedToVisualTree += async (_, _) =>
        {
            if (_catalog is not null)
            {
                return;
            }

            _count.Text = "Reading the game data...";
            _catalog = await _model.CatalogAsync().ConfigureAwait(true);
            Show();
        };
    }

    public event Action<GmPick>? Picked;

    public TextBox Search { get; }

    public TextBox Id { get; }

    public void Flush()
    {
        _debounce.Stop();
        Show();
    }

    public void Restart()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private Control Row(GmPick pick)
    {
        var text = new TextBlock { Text = pick.Text, Margin = new Thickness(8, 3), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        if (pick.Art is not { } art)
        {
            return text;
        }

        var box = art.Standing ? SpriteRowBox : IconRowBox;
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        line.Children.Add(new GmPictureSlot(_model, box, 1, art));
        line.Children.Add(text);
        return line;
    }

    private void Show()
    {
        if (_catalog is null)
        {
            return;
        }

        var matches = _query(_catalog, Search.Text ?? "").Take(GmKit.PickerRows + 1).ToList();
        _list.ItemsSource = matches.Take(GmKit.PickerRows).ToList();
        _count.Text = _catalog.Problem
            ?? (matches.Count == 0 ? "Nothing matches. You can still type an id."
            : matches.Count > GmKit.PickerRows ? string.Create(CultureInfo.InvariantCulture, $"Showing the first {GmKit.PickerRows} matches; type more to narrow it down.")
            : string.Create(CultureInfo.InvariantCulture, $"{matches.Count} match(es)."));
    }
}
