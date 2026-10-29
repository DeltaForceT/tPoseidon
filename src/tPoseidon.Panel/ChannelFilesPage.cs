using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public sealed class ChannelFilesPage : StackPanel
{
    private readonly ChannelContext _context;
    private readonly ListBox _list = new() { MinHeight = 260, MaxHeight = 380 };
    private readonly TextBox _target = ChannelContext.Field("Folder inside the client, for example data/item (optional)");
    private readonly Button _addFiles = ChannelContext.Small("Add files...");
    private readonly Button _addFolder = ChannelContext.Small("Add folder...");
    private readonly Button _remove = ChannelContext.Small("Remove selected");
    private readonly TextBlock _summary = Tokens.Caption();
    private readonly TextBlock _warning = new() { Foreground = Tokens.Warning, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private IReadOnlyList<FileChange> _files = [];
    private HashSet<string> _matches = [];

    public ChannelFilesPage(ChannelContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        Spacing = 10;
        Children.Add(Tokens.Caption("Files here are copied into your players' client folders with the same paths. Drop files or folders anywhere on this page. Only publish files you made or may give away, never the game's own files."));
        Children.Add(ChannelContext.Labelled("Put added files into", _target));
        Children.Add(ChannelContext.RowOf(_addFiles, _addFolder, _remove));
        Children.Add(_warning);
        Children.Add(new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = _list });
        Children.Add(_summary);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        _addFiles.Click += async (_, _) => await PickAsync(folders: false).ConfigureAwait(true);
        _addFolder.Click += async (_, _) => await PickAsync(folders: true).ConfigureAwait(true);
        _remove.Click += async (_, _) => await RemoveSelectedAsync().ConfigureAwait(true);
        _list.SelectionChanged += (_, _) => _remove.IsEnabled = SelectedChange is { State: not FileState.Removed };
        _remove.IsEnabled = false;
    }

    public ListBox FileList => _list;

    public TextBox TargetBox => _target;

    public TextBlock Warning => _warning;

    public TextBlock Summary => _summary;

    public IReadOnlyList<FileChange> Files => _files;

    private FileChange? SelectedChange => _list.SelectedItem is ListBoxItem { Tag: FileChange change } ? change : null;

    public async Task RefreshAsync()
    {
        try
        {
            var guard = await _context.GuardAsync().ConfigureAwait(true);
            var preview = await Task.Run(() => UpdateChannel.Preview(_context.Paths, _context.Settings, guard)).ConfigureAwait(true);
            Show(preview);
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
        }
    }

    public async Task<AddOutcome?> AddAsync(IReadOnlyList<string> sources)
    {
        AddOutcome outcome;
        var target = _target.Text;
        try
        {
            outcome = await Task.Run(() => UpdateFolder.AddClientFiles(_context.Paths, sources, target)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return null;
        }

        await RefreshAsync().ConfigureAwait(true);
        if (outcome.Refused.Count > 0)
        {
            _context.Notice.Show(BannerKind.Warning, $"{outcome.Copied} files added, {outcome.Refused.Count} refused", string.Join(Environment.NewLine, outcome.Refused.Take(5)));
        }
        else
        {
            _context.Notice.Show(BannerKind.Success, $"{outcome.Copied} files added", "Players get them after you publish.");
        }

        _context.RaiseChanged();
        return outcome;
    }

    public async Task<bool> RemoveSelectedAsync()
    {
        if (SelectedChange is not { State: not FileState.Removed } change)
        {
            return false;
        }

        try
        {
            await Task.Run(() => UpdateFolder.RemoveFile(_context.Paths, change.Group, change.Path)).ConfigureAwait(true);
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return false;
        }

        await RefreshAsync().ConfigureAwait(true);
        _context.RaiseChanged();
        return true;
    }

    private static Control Row(FileChange change, bool matchesOriginal)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,90,90") };
        var name = (change.Group is null ? "" : $"[{change.Group}] ") + change.Path;
        var path = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = change.State == FileState.Removed ? Tokens.TextMuted : Tokens.TextPrimary };
        var size = new TextBlock { Text = ChannelContext.Size(change.Size), Foreground = Tokens.TextSecondary, HorizontalAlignment = HorizontalAlignment.Right };
        var state = new TextBlock { Text = StateText(change.State), Foreground = ChannelContext.Tint(change.State), HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(size, 1);
        Grid.SetColumn(state, 2);
        grid.Children.Add(path);
        grid.Children.Add(size);
        grid.Children.Add(state);
        if (!matchesOriginal)
        {
            return grid;
        }

        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(grid);
        stack.Children.Add(new TextBlock { Text = "Same file name as a file in the original client. Publish only your own files.", Foreground = Tokens.Warning, FontSize = Tokens.FontSizeCaption });
        return stack;
    }

    private static string StateText(FileState state) => state switch
    {
        FileState.Added => "New",
        FileState.Changed => "Changed",
        FileState.Removed => "Removed",
        _ => "Published",
    };

    private void Show(PublishPreview preview)
    {
        _files = preview.Changes.Files;
        _matches = preview.OriginalNameMatches.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _list.Items.Clear();
        foreach (var change in _files.OrderBy(file => file.Group, StringComparer.Ordinal).ThenBy(file => file.Path, StringComparer.Ordinal))
        {
            _list.Items.Add(new ListBoxItem { Content = Row(change, change.State != FileState.Removed && _matches.Contains(change.Path)), Tag = change });
        }

        var live = _files.Where(file => file.State != FileState.Removed).ToList();
        _summary.Text = $"{(live.Count == 1 ? "1 file" : $"{live.Count} files")}, {ChannelContext.Size(live.Sum(file => file.Size))}. Since the last publish: {preview.Changes.Added} new, {preview.Changes.Changed} changed, {preview.Changes.Removed} removed.";
        _warning.IsVisible = _matches.Count > 0;
        _warning.Text = _matches.Count == 0 ? "" : $"{_matches.Count} listed files have the same name as a file in the original client: {string.Join(", ", _matches.Take(3))}{(_matches.Count > 3 ? ", ..." : "")}. Only publish files you made yourself.";
        _remove.IsEnabled = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var paths = e.Data.GetFiles()?.Select(item => item.TryGetLocalPath()).OfType<string>().ToList() ?? [];
        if (paths.Count > 0)
        {
            await AddAsync(paths).ConfigureAwait(true);
        }
    }

    private async Task PickAsync(bool folders)
    {
        var storage = TopLevel.GetTopLevel(this)!.StorageProvider;
        IReadOnlyList<IStorageItem> picked = folders
            ? await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a folder to add", AllowMultiple = true }).ConfigureAwait(true)
            : await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose files to add", AllowMultiple = true }).ConfigureAwait(true);
        var paths = picked.Select(item => item.TryGetLocalPath()).OfType<string>().ToList();
        if (paths.Count > 0)
        {
            await AddAsync(paths).ConfigureAwait(true);
        }
    }
}
