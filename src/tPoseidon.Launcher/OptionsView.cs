using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Ui;

namespace tPoseidon.Launcher;

public sealed class OptionsView : StackPanel
{
    private const double FieldHeight = 32;

    private readonly LauncherModel _model;
    private readonly ComboBox _resolution = Combo();
    private readonly CheckBox _fullScreen = new() { Content = "Full Screen Mode" };
    private readonly CheckBox _useEffect = new() { Content = "Use Effect" };
    private readonly CheckBox _useMapEffect = new() { Content = "Use Map Effect" };
    private readonly CheckBox _soundOff = new() { Content = "Sound Off" };
    private readonly ComboBox _sampleRate = Combo();
    private readonly ComboBox _bits = Combo();
    private readonly ComboBox _channels = Combo();
    private readonly ComboBox _format = Combo();
    private readonly ComboBox _quality = Combo();
    private readonly Button _save = new() { Content = "Save options", Height = FieldHeight, MinHeight = 0, Padding = new Thickness(16, 0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock _profile = Tokens.Caption();
    private bool _loading;

    public OptionsView(LauncherModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Spacing = 8;
        Fill(_resolution, ClientSetup.Resolutions.Select(choice => choice.Label));
        Fill(_sampleRate, ClientSetup.SampleRates.Select(rate => rate.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Fill(_bits, ClientSetup.BitDepths.Select(bits => $"{bits} Bit"));
        Fill(_channels, ClientSetup.Channels.Select(choice => choice.Label));
        Fill(_format, ClientSetup.ScreenShotFormats);
        Fill(_quality, ClientSetup.Qualities.Select(choice => choice.Label));
        _save.Classes.Add("primary");
        _save.Click += (_, _) => Save();
        _soundOff.IsCheckedChanged += (_, _) => Enable();
        _format.SelectionChanged += (_, _) => Enable();

        Children.Add(new TextBlock { Text = "Client options", FontWeight = FontWeight.SemiBold, Foreground = Tokens.TextSecondary, Margin = new Thickness(0, 6, 0, 0) });
        Children.Add(_profile);
        Children.Add(Pair(Labelled("Resolution", _resolution), _fullScreen));
        Children.Add(Pair(_useEffect, _useMapEffect));
        Children.Add(Pair(_soundOff, new Border()));
        Children.Add(Pair(Labelled("Sample Rate", _sampleRate), Labelled("Bits", _bits)));
        Children.Add(Pair(Labelled("Channel", _channels), new Border()));
        Children.Add(Pair(Labelled("Screen shot format", _format), Labelled("JPG option", _quality)));
        Children.Add(_save);
        IsVisible = _model.OptionsAvailable;
        if (IsVisible)
        {
            Reload();
        }
    }

    internal ComboBox Resolution => _resolution;

    internal CheckBox FullScreen => _fullScreen;

    internal CheckBox UseEffect => _useEffect;

    internal CheckBox UseMapEffect => _useMapEffect;

    internal CheckBox SoundOff => _soundOff;

    internal ComboBox SampleRate => _sampleRate;

    internal ComboBox Bits => _bits;

    internal ComboBox Channels => _channels;

    internal ComboBox Format => _format;

    internal ComboBox Quality => _quality;

    internal Button SaveButton => _save;

    public void Reload()
    {
        if (!_model.OptionsAvailable)
        {
            return;
        }

        var setup = _model.LoadClientSetup();
        _loading = true;
        _resolution.SelectedIndex = ClientSetup.Resolutions.ToList().FindIndex(choice => choice.Width == setup.Width && choice.Height == setup.Height);
        _fullScreen.IsChecked = setup.FullScreen;
        _useEffect.IsChecked = setup.Use3DEffect;
        _useMapEffect.IsChecked = setup.UseMapEffect;
        _soundOff.IsChecked = !setup.UseSound;
        _sampleRate.SelectedIndex = ClientSetup.SampleRates.ToList().IndexOf(setup.SoundFrequency);
        _bits.SelectedIndex = ClientSetup.BitDepths.ToList().IndexOf(setup.SoundBits);
        _channels.SelectedIndex = setup.ChannelChoice;
        _format.SelectedIndex = ClientSetup.ScreenShotFormats.ToList().IndexOf(setup.ScreenShotFormat);
        _quality.SelectedIndex = ClientSetup.Qualities.ToList().FindIndex(choice => choice.Value == setup.JpgQuality);
        _profile.Text = $"Saved for the {(_model.SetupProfileName() == ClientSetupProfile.English ? "English" : "Korean")} client mode.";
        _loading = false;
        Enable();
    }

    public ClientSetup Collect() => new(
        ClientSetup.Resolutions[_resolution.SelectedIndex].Width,
        ClientSetup.Resolutions[_resolution.SelectedIndex].Height,
        _fullScreen.IsChecked == true,
        _soundOff.IsChecked != true,
        ClientSetup.SampleRates[_sampleRate.SelectedIndex],
        ClientSetup.BitDepths[_bits.SelectedIndex],
        _channels.SelectedIndex,
        _useEffect.IsChecked == true,
        _useMapEffect.IsChecked == true,
        ClientSetup.ScreenShotFormats[_format.SelectedIndex],
        ClientSetup.Qualities[_quality.SelectedIndex].Value);

    public void Save()
    {
        if (!_loading && _model.SaveClientSetup(Collect()))
        {
            Reload();
        }
    }

    private static ComboBox Combo() =>
        new() { Height = FieldHeight, MinHeight = 0, HorizontalAlignment = HorizontalAlignment.Stretch };

    private static void Fill(ComboBox box, IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            box.Items.Add(item);
        }
    }

    private static StackPanel Labelled(string label, Control field)
    {
        var stack = new StackPanel { Spacing = 3 };
        stack.Children.Add(Tokens.Caption(label));
        stack.Children.Add(field);
        return stack;
    }

    private static Grid Pair(Control left, Control right)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        left.VerticalAlignment = VerticalAlignment.Bottom;
        right.VerticalAlignment = VerticalAlignment.Bottom;
        right.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private void Enable()
    {
        var sound = _soundOff.IsChecked != true;
        _sampleRate.IsEnabled = sound;
        _bits.IsEnabled = sound;
        _channels.IsEnabled = sound;
        _quality.IsEnabled = _format.SelectedIndex >= 0 && ClientSetup.ScreenShotFormats[_format.SelectedIndex] == "JPG";
    }
}
