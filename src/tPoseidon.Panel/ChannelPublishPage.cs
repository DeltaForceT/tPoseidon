using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;

namespace tPoseidon.Panel;

public sealed class ChannelPublishPage : StackPanel
{
    private readonly ChannelContext _context;
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _details = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _warning = new() { Foreground = Tokens.Warning, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBox _note = ChannelContext.Field("A short note for the history (optional)");
    private readonly Button _publish = ChannelContext.Small("Publish", primary: true);
    private readonly TextBlock _launcherInfo = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _launcherWarning = new() { Foreground = Tokens.Warning, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly Button _chooseLauncher = ChannelContext.Small("Choose a launcher zip...");
    private readonly Button _dropLauncher = ChannelContext.Small("Stop handing out a launcher");
    private readonly TextBlock _keyInfo = new() { Foreground = Tokens.TextSecondary, TextWrapping = TextWrapping.Wrap };
    private readonly Button _changeKey = ChannelContext.Small("Change the signing key...");

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
        Children.Add(Tokens.Caption("Launcher update"));
        Children.Add(_launcherInfo);
        Children.Add(_launcherWarning);
        Children.Add(ChannelContext.RowOf(_chooseLauncher, _dropLauncher));
        Children.Add(_keyInfo);
        Children.Add(ChannelContext.RowOf(_changeKey));
        _publish.Click += async (_, _) => await PublishAsync().ConfigureAwait(true);
        _chooseLauncher.Click += async (_, _) => await ChooseLauncherAsync().ConfigureAwait(true);
        _dropLauncher.Click += async (_, _) => await DropLauncherAsync().ConfigureAwait(true);
        _changeKey.Click += async (_, _) => await ChangeKeyAsync(ConfirmKeyChangeAsync).ConfigureAwait(true);
        _publish.IsEnabled = false;
    }

    public TextBlock Summary => _summary;

    public TextBlock Details => _details;

    public TextBlock Warning => _warning;

    public TextBox NoteBox => _note;

    public Button PublishButton => _publish;

    public TextBlock LauncherInfo => _launcherInfo;

    public TextBlock LauncherWarning => _launcherWarning;

    public Button DropLauncherButton => _dropLauncher;

    public TextBlock KeyInfo => _keyInfo;

    public Button ChangeKeyButton => _changeKey;

    public PublishOutcome? LastOutcome { get; private set; }

    public async Task<bool> UseLauncherAsync(string zipPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        try
        {
            SigningKeyStore.EnsureCreated(_context.Paths);
            var import = await Task.Run(() => LauncherUpdatePackage.Import(_context.Paths, zipPath)).ConfigureAwait(true);
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Prepared the launcher update {import.Info.Version} ({import.Files} files).");
            _context.Notice.Show(import.Info.TestOnThisComputer ? BannerKind.Warning : BannerKind.Success, import.Info.TestOnThisComputer ? "A test launcher was added" : "The launcher update is ready", import.Info.TestOnThisComputer ? LauncherUpdatePackage.TestLauncherWarning : "It goes out with the next publish. Branded launchers that are older update themselves.");
            await RefreshAsync().ConfigureAwait(true);
            return true;
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return false;
        }
    }

    public async Task<bool> DropLauncherAsync()
    {
        try
        {
            await Task.Run(() => LauncherUpdatePackage.Remove(_context.Paths)).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            return true;
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return false;
        }
    }

    public async Task<bool> ChangeKeyAsync(Func<Task<bool>> confirm)
    {
        ArgumentNullException.ThrowIfNull(confirm);
        if (!SigningKeyStore.Exists(_context.Paths) || !await confirm().ConfigureAwait(true))
        {
            return false;
        }

        try
        {
            var key = await Task.Run(() => SigningKeyStore.Rotate(_context.Paths)).ConfigureAwait(true);
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Changed the launcher signing key to {key.Fingerprint}.");
            _context.Notice.Show(BannerKind.Success, "The signing key was changed", "Publish again so the new key signs your updates. Launchers you already handed out follow the change.");
            ShowKey();
            return true;
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return false;
        }
    }

    public async Task RefreshAsync()
    {
        ShowKey();
        try
        {
            var guard = await _context.GuardAsync().ConfigureAwait(true);
            var preview = await Task.Run(() => UpdateChannel.Preview(_context.Paths, _context.Settings, guard)).ConfigureAwait(true);
            _summary.Text = preview.Changes.Describe();
            _details.Text = $"{preview.Manifest.Files.Count} files in total ({ChannelContext.Size(preview.Manifest.TotalBytes)}). Maintenance: {(preview.Manifest.Maintenance is null ? "off" : "on")}.";
            _warning.IsVisible = preview.OriginalNameMatches.Count > 0;
            _warning.Text = preview.OriginalNameMatches.Count == 0 ? "" : $"These files have the same name as files in the original client: {string.Join(", ", preview.OriginalNameMatches.Take(5))}{(preview.OriginalNameMatches.Count > 5 ? ", ..." : "")}. Only publish files you made yourself.";
            _publish.IsEnabled = true;
            ShowLauncher(preview.Launcher, preview.Manifest.Launcher);
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

    private async Task ChooseLauncherAsync()
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the launcher zip the panel built",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Launcher zip") { Patterns = ["*.zip"] }],
        }).ConfigureAwait(true);
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await UseLauncherAsync(path).ConfigureAwait(true);
        }
    }

    private void ShowLauncher(LauncherPackageInfo? package, LauncherRelease? release)
    {
        _dropLauncher.IsEnabled = package is not null;
        _launcherWarning.IsVisible = package is { TestOnThisComputer: true };
        _launcherWarning.Text = package is { TestOnThisComputer: true } ? LauncherUpdatePackage.TestLauncherWarning : "";
        _launcherInfo.Text = package is null || release is null
            ? "No launcher update goes out. Choose the zip the launcher wizard built for this server to let your players' launchers update themselves."
            : $"Version {package.Version} goes out: {release.Files.Count} files ({ChannelContext.Size(release.Files.Sum(file => file.Size))}). Launchers that are older download it, check it and restart into it.";
    }

    private Task<bool> ConfirmKeyChangeAsync() =>
        AskWindow.ConfirmAsync(TopLevel.GetTopLevel(this) as Window, "Change the signing key?", "Launchers you already handed out follow the change automatically. Keep your backup of the old key until all your players have updated.", "Change the key");

    private void ShowKey()
    {
        try
        {
            var key = SigningKeyStore.Load(_context.Paths);
            _keyInfo.Text = key is null
                ? "Your updates are signed with a key that is created the first time you publish or build a launcher. The key is stored with your server files and is part of the backup."
                : $"Your updates are signed. Key {key.Fingerprint}{(key.Rotations.Count == 0 ? "" : $", changed {key.Rotations.Count} time(s)")}. The key file is part of the backup; keep it private.";
            _changeKey.IsEnabled = key is not null;
        }
        catch (HostingException ex)
        {
            _keyInfo.Text = ex.Summary;
            _changeKey.IsEnabled = false;
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
            if (UpdateChannel.ReadLauncher(_context.Paths) is { TestOnThisComputer: true })
            {
                _context.Notice.Show(BannerKind.Warning, "A test launcher was published", LauncherUpdatePackage.TestLauncherWarning);
            }
            else
            {
                _context.Notice.Show(BannerKind.Success, "Launcher update published", $"{record.NewsPosts} news posts, {record.TotalFiles} files. Players get it the next time their launcher opens.");
            }

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
