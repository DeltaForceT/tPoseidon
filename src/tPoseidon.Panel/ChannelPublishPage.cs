using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public sealed class ChannelPublishPage : StackPanel
{
    private readonly ChannelContext _context;
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _details = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _warning = new() { Foreground = Tokens.Warning, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBox _note = ChannelContext.Field("A short note for the history (optional)");
    private readonly Button _publish = ChannelContext.Small("Publish", primary: true);

    public ChannelPublishPage(ChannelContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Spacing = 12;
        _note.MaxLength = PublishHistory.MaxNoteLength;
        Children.Add(Tokens.Caption("Publishing builds the update list your players' launchers read. Nothing reaches players before you press Publish."));
        Children.Add(_summary);
        Children.Add(_details);
        Children.Add(_warning);
        Children.Add(ChannelContext.Labelled("Note", _note));
        Children.Add(ChannelContext.RowOf(_publish));
        _publish.Click += async (_, _) => await PublishAsync().ConfigureAwait(true);
        _publish.IsEnabled = false;
    }

    public TextBlock Summary => _summary;

    public TextBlock Details => _details;

    public TextBlock Warning => _warning;

    public TextBox NoteBox => _note;

    public Button PublishButton => _publish;

    public PublishOutcome? LastOutcome { get; private set; }

    public async Task RefreshAsync()
    {
        try
        {
            var guard = await _context.GuardAsync().ConfigureAwait(true);
            var preview = await Task.Run(() => UpdateChannel.Preview(_context.Paths, _context.Settings, guard)).ConfigureAwait(true);
            _summary.Text = preview.Changes.Describe();
            _details.Text = $"{preview.Manifest.Files.Count} files in total ({ChannelContext.Size(preview.Manifest.TotalBytes)}). Maintenance: {(preview.Manifest.Maintenance is null ? "off" : "on")}.";
            _warning.IsVisible = preview.OriginalNameMatches.Count > 0;
            _warning.Text = preview.OriginalNameMatches.Count == 0 ? "" : $"These files have the same name as files in the original client: {string.Join(", ", preview.OriginalNameMatches.Take(5))}{(preview.OriginalNameMatches.Count > 5 ? ", ..." : "")}. Only publish files you made yourself.";
            _publish.IsEnabled = true;
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _summary.Text = "Nothing can be published yet";
            _details.Text = "";
            _warning.IsVisible = false;
            _publish.IsEnabled = false;
            _context.Report(ex);
        }
    }

    public async Task<bool> PublishAsync()
    {
        _publish.IsEnabled = false;
        try
        {
            var note = _note.Text;
            LastOutcome = await Task.Run(() => UpdateChannel.Publish(_context.Paths, _context.Settings, note)).ConfigureAwait(true);
            var record = LastOutcome.Record;
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Published the launcher update '{LastOutcome.Manifest.Name}': {record.TotalFiles} files, {record.NewsPosts} news posts ({record.Added} added, {record.Changed} changed, {record.Removed} removed).");
            _context.Notice.Show(BannerKind.Success, "Launcher update published", $"{record.NewsPosts} news posts, {record.TotalFiles} files. Players get it the next time their launcher opens.");
            _note.Text = "";
            _context.RaiseChanged();
            await RefreshAsync().ConfigureAwait(true);
            return true;
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            _publish.IsEnabled = true;
            return false;
        }
    }
}
