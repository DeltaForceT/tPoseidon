using System.Globalization;
using System.Text;
using System.Text.Json;

namespace tPoseidon.Core.Common.Launcher;

public sealed record NewsPost(string Id, string Title, DateOnly Date, string Text, bool Pinned, string? Picture)
{
    public NewsItem ToItem() =>
        new(Title, Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Text, Picture, Pinned);
}

public sealed record NewsDraft(string Title, DateOnly Date, string Text, bool Pinned, byte[]? NewPicture = null, bool RemovePicture = false);

public sealed class NewsStore
{
    public const string OrderFile = "order.json";
    public const string PostPattern = "*.txt";

    private const int MaxSlugLength = 40;
    private static readonly string[] PictureExtensions = [".png", ".jpg", ".jpeg"];
    private static readonly JsonSerializerOptions OrderOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _folder;

    public NewsStore(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = folder;
    }

    public static bool IsPictureName(string? name) =>
        !string.IsNullOrEmpty(name) && ClientPath.TryNormalize(name, out _) && !name.Contains('/', StringComparison.Ordinal)
        && PictureExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<NewsPost> Load()
    {
        if (!Directory.Exists(_folder))
        {
            return [];
        }

        var order = ReadOrder();
        var pinned = order.Pinned.ToHashSet(StringComparer.Ordinal);
        var posts = Directory.EnumerateFiles(_folder, PostPattern).Select(path => ReadPost(path, pinned)).ToList();
        var position = order.Order.Select((id, index) => (id, index)).GroupBy(entry => entry.id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().index, StringComparer.Ordinal);
        return posts
            .OrderByDescending(post => post.Pinned)
            .ThenBy(post => position.TryGetValue(post.Id, out var index) ? index : -1)
            .ThenByDescending(post => post.Date)
            .ThenBy(post => post.Title, StringComparer.Ordinal)
            .ToList();
    }

    public NewsPost Save(string? id, NewsDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var title = string.Join(' ', draft.Title.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var text = draft.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        Validate(title, text);
        var picture = draft.NewPicture is null ? null : PictureInfo.Inspect(draft.NewPicture, LauncherManifest.MaxNewsPictureBytes);
        Directory.CreateDirectory(_folder);

        var existing = id is null ? null : Load().FirstOrDefault(post => post.Id == id)
            ?? throw new InvalidDataException("That news post no longer exists.");
        var newId = existing is null ? NewId(draft.Date, title) : RenamedId(existing.Id, draft.Date);
        var pictureName = existing?.Picture;
        if (existing is not null && newId != existing.Id)
        {
            Rename(existing.Id, newId);
            pictureName = pictureName is null ? null : newId + Path.GetExtension(pictureName);
        }

        if (draft.RemovePicture || picture is not null)
        {
            DeletePictures(newId);
            pictureName = null;
        }

        if (picture is not null)
        {
            pictureName = newId + picture.Extension;
            WriteAtomic(Path.Combine(_folder, pictureName), draft.NewPicture!);
        }

        WriteAtomic(Path.Combine(_folder, newId + ".txt"), Encoding.UTF8.GetBytes(text.Length == 0 ? title : title + "\n" + text));
        var order = ReadOrder();
        var renamed = order with
        {
            Order = order.Order.Select(entry => entry == id ? newId : entry).ToList(),
            Pinned = order.Pinned.Where(entry => entry != id && entry != newId).Concat(draft.Pinned ? [newId] : []).ToList(),
        };
        WriteOrder(renamed);
        return new NewsPost(newId, title, draft.Date, text, draft.Pinned, pictureName);
    }

    public void Delete(string id)
    {
        RequireId(id);
        File.Delete(Path.Combine(_folder, id + ".txt"));
        DeletePictures(id);
        var order = ReadOrder();
        WriteOrder(new OrderDocument(order.Order.Where(entry => entry != id).ToList(), order.Pinned.Where(entry => entry != id).ToList()));
    }

    public void SetPinned(string id, bool pinned)
    {
        RequireId(id);
        var order = ReadOrder();
        WriteOrder(order with { Pinned = order.Pinned.Where(entry => entry != id).Concat(pinned ? [id] : []).ToList() });
    }

    public void Move(string id, int direction)
    {
        RequireId(id);
        var posts = Load();
        var index = posts.ToList().FindIndex(post => post.Id == id);
        if (index < 0)
        {
            throw new InvalidDataException("That news post no longer exists.");
        }

        var step = Math.Sign(direction);
        var target = index + step;
        if (step == 0 || target < 0 || target >= posts.Count || posts[target].Pinned != posts[index].Pinned)
        {
            return;
        }

        var ids = posts.Select(post => post.Id).ToList();
        (ids[index], ids[target]) = (ids[target], ids[index]);
        WriteOrder(new OrderDocument(ids, ReadOrder().Pinned));
    }

    public string PicturePath(NewsPost post) =>
        Path.Combine(_folder, post.Picture ?? throw new InvalidOperationException("This post has no picture."));

    private static void Validate(string title, string text)
    {
        if (title.Length == 0)
        {
            throw new InvalidDataException("A news post needs a title.");
        }

        if (title.Length > LauncherManifest.MaxTitleLength)
        {
            throw new InvalidDataException($"The title can be at most {LauncherManifest.MaxTitleLength} characters.");
        }

        if (text.Length > LauncherManifest.MaxTextLength)
        {
            throw new InvalidDataException($"The text can be at most {LauncherManifest.MaxTextLength} characters.");
        }
    }

    private static string RenamedId(string id, DateOnly date)
    {
        var slug = HasDatePrefix(id) ? id[11..] : id;
        return Prefix(date) + slug;
    }

    private static bool HasDatePrefix(string id) =>
        id.Length > 11 && id[10] == ' ' && DateOnly.TryParseExact(id[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static string Prefix(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " ";

    private static string Slug(string title)
    {
        var builder = new StringBuilder();
        foreach (var letter in title.ToLowerInvariant())
        {
            if (letter is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(letter);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');
        slug = slug.Length > MaxSlugLength ? slug[..MaxSlugLength].Trim('-') : slug;
        return slug.Length == 0 ? "post" : slug;
    }

    private static void RequireId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id != Path.GetFileName(id))
        {
            throw new InvalidDataException("That is not a news post.");
        }
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
    }

    private string NewId(DateOnly date, string title)
    {
        var baseId = Prefix(date) + Slug(title);
        var candidate = baseId;
        for (var number = 2; File.Exists(Path.Combine(_folder, candidate + ".txt")); number++)
        {
            candidate = $"{baseId}-{number}";
        }

        return candidate;
    }

    private void Rename(string from, string to)
    {
        var target = Path.Combine(_folder, to + ".txt");
        if (File.Exists(target))
        {
            throw new InvalidDataException("Another news post already has that date and name.");
        }

        File.Move(Path.Combine(_folder, from + ".txt"), target);
        foreach (var extension in PictureExtensions)
        {
            var picture = Path.Combine(_folder, from + extension);
            if (File.Exists(picture))
            {
                File.Move(picture, Path.Combine(_folder, to + extension), overwrite: true);
            }
        }
    }

    private void DeletePictures(string id)
    {
        foreach (var extension in PictureExtensions)
        {
            File.Delete(Path.Combine(_folder, id + extension));
        }
    }

    private NewsPost ReadPost(string path, HashSet<string> pinned)
    {
        var id = Path.GetFileNameWithoutExtension(path);
        var lines = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', 2);
        var date = HasDatePrefix(id)
            ? DateOnly.ParseExact(id[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : DateOnly.FromDateTime(File.GetLastWriteTimeUtc(path));
        var picture = PictureExtensions.Select(extension => id + extension).FirstOrDefault(name => File.Exists(Path.Combine(_folder, name)) && new FileInfo(Path.Combine(_folder, name)).Length <= LauncherManifest.MaxNewsPictureBytes);
        return new NewsPost(id, lines[0].Trim(), date, lines.Length > 1 ? lines[1].Trim() : "", pinned.Contains(id), picture);
    }

    private OrderDocument ReadOrder()
    {
        var path = Path.Combine(_folder, OrderFile);
        if (!File.Exists(path))
        {
            return new OrderDocument([], []);
        }

        try
        {
            var document = JsonSerializer.Deserialize<OrderDocument>(File.ReadAllText(path), OrderOptions);
            return new OrderDocument(document?.Order ?? [], document?.Pinned ?? []);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path} is not valid: {ex.Message}", ex);
        }
    }

    private void WriteOrder(OrderDocument document) =>
        WriteAtomic(Path.Combine(_folder, OrderFile), JsonSerializer.SerializeToUtf8Bytes(document, OrderOptions));

    private sealed record OrderDocument(List<string> Order, List<string> Pinned);
}
