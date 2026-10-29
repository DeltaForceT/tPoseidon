using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.Panel;

public sealed class ChannelNewsPage : Grid
{
    private readonly ChannelContext _context;
    private readonly NewsStore _store;
    private readonly ListBox _list = new() { MinHeight = 200 };
    private readonly Button _new = ChannelContext.Small("New post");
    private readonly Button _remove = ChannelContext.Small("Remove");
    private readonly Button _up = ChannelContext.Small("Move up");
    private readonly Button _down = ChannelContext.Small("Move down");
    private readonly Button _pin = ChannelContext.Small("Pin");
    private readonly TextBox _title = ChannelContext.Field("Title");
    private readonly TextBox _date = ChannelContext.Field("yyyy-MM-dd");
    private readonly TextBox _text = new() { Watermark = "What players should read", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 110, MaxLength = LauncherManifest.MaxTextLength };
    private readonly CheckBox _pinned = new() { Content = "Pinned: shown above the other posts" };
    private readonly TextBlock _pictureName = Tokens.Caption("No picture");
    private readonly Button _choosePicture = ChannelContext.Small("Choose picture...");
    private readonly Button _removePicture = ChannelContext.Small("Remove picture");
    private readonly Button _save = ChannelContext.Small("Save post", primary: true);
    private readonly Border _previewHost = new();
    private IReadOnlyList<NewsPost> _posts = [];
    private string? _editing;
    private byte[]? _pendingPicture;
    private bool _removeExistingPicture;
    private bool _loading;

    public ChannelNewsPage(ChannelContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _store = new NewsStore(context.NewsFolder);
        _title.MaxLength = LauncherManifest.MaxTitleLength;
        _date.MaxLength = 10;
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(300)));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        var left = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 16, 0) };
        left.Children.Add(Tokens.Caption("News posts, in the order players see them"));
        left.Children.Add(new Border { Background = Tokens.Surface1, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = _list });
        left.Children.Add(ChannelContext.RowOf(_new, _remove));
        left.Children.Add(ChannelContext.RowOf(_up, _down, _pin));
        var form = new StackPanel { Spacing = 10 };
        form.Children.Add(ChannelContext.Labelled("Title", _title));
        form.Children.Add(ChannelContext.Labelled("Date", _date));
        form.Children.Add(ChannelContext.Labelled("Text", _text));
        form.Children.Add(_pinned);
        form.Children.Add(ChannelContext.Labelled("Picture (optional)", ChannelContext.RowOf(_choosePicture, _removePicture), _pictureName));
        form.Children.Add(ChannelContext.RowOf(_save));
        form.Children.Add(Tokens.Caption("As the launcher shows it"));
        form.Children.Add(_previewHost);
        SetColumn(form, 1);
        Children.Add(left);
        Children.Add(form);
        Wire();
        Reload();
        NewPost();
    }

    public IReadOnlyList<NewsPost> Posts => _posts;

    public ListBox PostList => _list;

    public TextBox TitleBox => _title;

    public TextBox DateBox => _date;

    public TextBox TextBox => _text;

    public CheckBox PinnedBox => _pinned;

    public Button SaveButton => _save;

    public Button PinButton => _pin;

    public Border Preview => _previewHost;

    public string? EditingId => _editing;

    public void Reload(string? select = null)
    {
        try
        {
            _posts = _store.Load();
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _posts = [];
            _context.Report(ex);
        }

        _loading = true;
        _list.Items.Clear();
        foreach (var post in _posts)
        {
            _list.Items.Add((post.Pinned ? "Pinned  " : "") + post.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "  " + post.Title);
        }

        _list.SelectedIndex = select is null ? -1 : _posts.ToList().FindIndex(post => post.Id == select);
        _loading = false;
        RefreshButtons();
    }

    public void NewPost()
    {
        _editing = null;
        _pendingPicture = null;
        _removeExistingPicture = false;
        _loading = true;
        _list.SelectedIndex = -1;
        _title.Text = "";
        _date.Text = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _text.Text = "";
        _pinned.IsChecked = false;
        _loading = false;
        RefreshForm();
    }

    public void Select(string id)
    {
        var post = _posts.FirstOrDefault(entry => entry.Id == id);
        if (post is null)
        {
            return;
        }

        _editing = post.Id;
        _pendingPicture = null;
        _removeExistingPicture = false;
        _loading = true;
        _list.SelectedIndex = _posts.ToList().IndexOf(post);
        _title.Text = post.Title;
        _date.Text = post.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _text.Text = post.Text;
        _pinned.IsChecked = post.Pinned;
        _loading = false;
        RefreshForm();
    }

    public void SetPicture(byte[] picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        try
        {
            var info = PictureInfo.Inspect(picture, LauncherManifest.MaxNewsPictureBytes);
            _pendingPicture = picture;
            _removeExistingPicture = false;
            _context.Notice.Hide();
            _pictureName.Text = $"{info.Format} {info.Width} x {info.Height}, {ChannelContext.Size(picture.Length)}";
        }
        catch (InvalidDataException ex)
        {
            _context.Notice.Show(BannerKind.Error, ex.Message, "Choose a PNG or JPG picture of at most 2 MB.");
        }

        RefreshPreview();
    }

    public void RemovePicture()
    {
        _pendingPicture = null;
        _removeExistingPicture = true;
        RefreshForm();
    }

    public bool Save()
    {
        if (!DateOnly.TryParseExact((_date.Text ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            _context.Notice.Show(BannerKind.Error, "The date must look like 2026-10-29.");
            return false;
        }

        try
        {
            var post = _store.Save(_editing, new NewsDraft(_title.Text ?? "", date, _text.Text ?? "", _pinned.IsChecked == true, _pendingPicture, _removeExistingPicture));
            Reload(post.Id);
            Select(post.Id);
            _context.Notice.Show(BannerKind.Success, "Post saved", "Players see it after you publish.");
            _context.RaiseChanged();
            return true;
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return false;
        }
    }

    public void RemoveSelected()
    {
        if (_editing is null)
        {
            return;
        }

        try
        {
            _store.Delete(_editing);
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return;
        }

        Reload();
        NewPost();
        _context.RaiseChanged();
    }

    public void MoveSelected(int direction)
    {
        var id = _editing;
        if (id is null)
        {
            return;
        }

        try
        {
            _store.Move(id, direction);
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return;
        }

        Reload(id);
        _context.RaiseChanged();
    }

    public void TogglePin()
    {
        if (_editing is not { } id || _posts.FirstOrDefault(post => post.Id == id) is not { } post)
        {
            return;
        }

        try
        {
            _store.SetPinned(id, !post.Pinned);
        }
        catch (Exception ex) when (ChannelContext.IsExpected(ex))
        {
            _context.Report(ex);
            return;
        }

        Reload(id);
        Select(id);
        _context.RaiseChanged();
    }

    private void Wire()
    {
        _list.SelectionChanged += (_, _) =>
        {
            if (!_loading && _list.SelectedIndex >= 0)
            {
                Select(_posts[_list.SelectedIndex].Id);
            }
        };
        foreach (var box in new[] { _title, _date, _text })
        {
            box.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty && !_loading)
                {
                    RefreshPreview();
                }
            };
        }

        _pinned.IsCheckedChanged += (_, _) =>
        {
            if (!_loading)
            {
                RefreshPreview();
            }
        };
        _new.Click += (_, _) => NewPost();
        _remove.Click += (_, _) => RemoveSelected();
        _up.Click += (_, _) => MoveSelected(-1);
        _down.Click += (_, _) => MoveSelected(1);
        _pin.Click += (_, _) => TogglePin();
        _save.Click += (_, _) => Save();
        _removePicture.Click += (_, _) => RemovePicture();
        _choosePicture.Click += async (_, _) => await ChoosePictureAsync().ConfigureAwait(true);
    }

    private void RefreshForm()
    {
        var existing = _posts.FirstOrDefault(post => post.Id == _editing);
        _pictureName.Text = existing?.Picture is { } name && !_removeExistingPicture ? name : "No picture";
        RefreshButtons();
        RefreshPreview();
    }

    private void RefreshButtons()
    {
        var has = _editing is not null;
        var post = _posts.FirstOrDefault(entry => entry.Id == _editing);
        _remove.IsEnabled = has;
        _up.IsEnabled = has;
        _down.IsEnabled = has;
        _pin.IsEnabled = has;
        _pin.Content = post?.Pinned == true ? "Unpin" : "Pin";
        _removePicture.IsEnabled = _pendingPicture is not null || (post?.Picture is not null && !_removeExistingPicture);
    }

    private void RefreshPreview()
    {
        var existing = _posts.FirstOrDefault(post => post.Id == _editing);
        byte[]? picture = _pendingPicture;
        if (picture is null && existing?.Picture is not null && !_removeExistingPicture)
        {
            picture = TryRead(_store.PicturePath(existing));
        }

        var title = string.IsNullOrWhiteSpace(_title.Text) ? "Title" : _title.Text.Trim();
        _previewHost.Child = NewsCard.Create(title, (_date.Text ?? "").Trim(), (_text.Text ?? "").Trim(), _pinned.IsChecked == true, ChannelContext.Picture(picture));
        RefreshButtons();
    }

    private byte[]? TryRead(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _context.Report(ex);
            return null;
        }
    }

    private async Task ChoosePictureAsync()
    {
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the news picture",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Pictures") { Patterns = ["*.png", "*.jpg", "*.jpeg"] }],
        }).ConfigureAwait(true);
        if (files.Count > 0)
        {
            await using var stream = await files[0].OpenReadAsync().ConfigureAwait(true);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory).ConfigureAwait(true);
            SetPicture(memory.ToArray());
        }
    }
}
