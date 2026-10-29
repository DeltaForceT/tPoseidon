using Avalonia;
using Avalonia.Controls;
using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.Panel;

public sealed class ChannelMaintenancePage : StackPanel
{
    private readonly ChannelContext _context;
    private readonly CheckBox _on = new() { Content = "Maintenance mode is on" };
    private readonly TextBox _message = new() { Watermark = "Back at 18:00 UTC", AcceptsReturn = false, Height = ChannelContext.FieldHeight, MinHeight = 0, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center, MaxLength = LauncherManifest.MaxMaintenanceLength };
    private readonly Button _save = ChannelContext.Small("Save", primary: true);

    public ChannelMaintenancePage(ChannelContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Spacing = 12;
        Children.Add(Tokens.Caption("While maintenance is on, the launcher shows the message, the server's status turns to Maintenance and players cannot start the game. Turn it off and publish to let them in again."));
        Children.Add(_on);
        Children.Add(ChannelContext.Labelled("Message players see", _message));
        Children.Add(ChannelContext.RowOf(_save));
        _save.Click += (_, _) => Save();
        Reload();
    }

    public CheckBox SwitchBox => _on;

    public TextBox MessageBox => _message;

    public void Reload()
    {
        try
        {
            var channel = ChannelSettings.Read(_context.Paths.Updates);
            _on.IsChecked = !string.IsNullOrWhiteSpace(channel.Maintenance);
            _message.Text = channel.Maintenance ?? "";
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
        }
    }

    public bool Save()
    {
        var message = (_message.Text ?? "").Trim();
        if (_on.IsChecked == true && message.Length == 0)
        {
            _context.Notice.Show(BannerKind.Error, "Write the message players will see.", "Or turn maintenance off.");
            return false;
        }

        try
        {
            var channel = ChannelSettings.Read(_context.Paths.Updates);
            (channel with { Maintenance = _on.IsChecked == true ? message : null }).Write(_context.Paths.Updates);
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return false;
        }

        _context.Notice.Show(BannerKind.Success, _on.IsChecked == true ? "Maintenance is set" : "Maintenance is off", "Players see the change after you publish.");
        _context.RaiseChanged();
        return true;
    }
}
