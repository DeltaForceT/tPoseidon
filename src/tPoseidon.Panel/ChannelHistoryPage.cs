using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public sealed class ChannelHistoryPage : StackPanel
{
    private readonly ChannelContext _context;
    private readonly ListBox _list = new() { MinHeight = 300, MaxHeight = 460 };
    private readonly TextBlock _empty = new() { Text = "Nothing has been published yet.", Foreground = Tokens.TextMuted };

    public ChannelHistoryPage(ChannelContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Spacing = 10;
        Children.Add(Tokens.Caption("Every publish, newest first. Saved in the install folder as launcher-history.json."));
        Children.Add(new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = _list });
        Children.Add(_empty);
        Reload();
    }

    public ListBox Entries => _list;

    public IReadOnlyList<PublishRecord> Records { get; private set; } = [];

    public static string Describe(PublishRecord record) =>
        $"{record.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}   {record.NewsPosts} news posts{(record.BannerChanged ? ", banner changed" : "")}, {record.Added} files added, {record.Changed} changed, {record.Removed} removed ({record.TotalFiles} files in total){(record.Note is null ? "" : "   " + record.Note)}";

    public void Reload()
    {
        try
        {
            Records = PublishHistory.Load(_context.Paths);
        }
        catch (HostingException ex)
        {
            Records = [];
            _context.Report(ex);
        }

        _list.Items.Clear();
        foreach (var record in Records)
        {
            _list.Items.Add(Describe(record));
        }

        _empty.IsVisible = Records.Count == 0;
    }
}
