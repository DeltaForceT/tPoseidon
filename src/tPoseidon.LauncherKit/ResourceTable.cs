using System.Buffers.Binary;
using System.Text;
using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

internal readonly record struct ResourceKey(ushort? Id, string? Name)
{
    public static ResourceKey Of(ushort id) => new(id, null);

    public static ResourceKey Of(string name) => new(null, name);
}

internal sealed record ResourceData(byte[] Bytes, uint CodePage);

internal sealed class ResourceTable
{
    public const ushort IconType = 3;
    public const ushort GroupIconType = 14;
    public const ushort VersionType = 16;
    public const ushort NeutralLanguage = 0;
    public const ushort EnglishLanguage = 0x0409;

    private const int DirectoryBytes = 16;
    private const int EntryBytes = 8;
    private const int DataEntryBytes = 16;
    private const int NamedCountOffset = 12;
    private const int IdCountOffset = 14;
    private const uint HighBit = 0x80000000;
    private const int MaxDepth = 3;
    private const int MaxEntries = 4096;

    private readonly Dictionary<ResourceKey, Dictionary<ResourceKey, Dictionary<ushort, ResourceData>>> _types = [];

    public IEnumerable<ResourceKey> Types => _types.Keys;

    public IReadOnlyDictionary<ResourceKey, Dictionary<ushort, ResourceData>> Names(ushort type) =>
        _types.TryGetValue(ResourceKey.Of(type), out var names) ? names : new Dictionary<ResourceKey, Dictionary<ushort, ResourceData>>();

    public void RemoveType(ushort type) => _types.Remove(ResourceKey.Of(type));

    public void Set(ushort type, ushort id, ushort language, byte[] bytes)
    {
        if (!_types.TryGetValue(ResourceKey.Of(type), out var names))
        {
            names = [];
            _types[ResourceKey.Of(type)] = names;
        }

        if (!names.TryGetValue(ResourceKey.Of(id), out var languages))
        {
            languages = [];
            names[ResourceKey.Of(id)] = languages;
        }

        languages[language] = new ResourceData(bytes, 0);
    }

    public static ResourceTable Parse(ReadOnlySpan<byte> section, uint sectionRva)
    {
        var table = new ResourceTable();
        if (section.IsEmpty)
        {
            return table;
        }

        var seen = 0;
        var root = ReadDirectory(section, 0, 1, ref seen);
        foreach (var (typeKey, typeTarget) in root)
        {
            var names = new Dictionary<ResourceKey, Dictionary<ushort, ResourceData>>();
            foreach (var (nameKey, nameTarget) in ReadDirectory(section, typeTarget, 2, ref seen))
            {
                var languages = new Dictionary<ushort, ResourceData>();
                foreach (var (languageKey, leaf) in ReadDirectory(section, nameTarget, MaxDepth, ref seen))
                {
                    if (languageKey.Id is not { } language || (long)leaf + DataEntryBytes > section.Length)
                    {
                        throw Unsupported();
                    }

                    var entry = section.Slice((int)leaf, DataEntryBytes);
                    var start = (long)BinaryPrimitives.ReadUInt32LittleEndian(entry) - sectionRva;
                    var size = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
                    if (start < 0 || start + size > section.Length)
                    {
                        throw Unsupported();
                    }

                    languages[language] = new ResourceData(section.Slice((int)start, (int)size).ToArray(), BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]));
                }

                names[nameKey] = languages;
            }

            table._types[typeKey] = names;
        }

        return table;
    }

    public byte[] Build(uint sectionRva)
    {
        var root = new Directory();
        foreach (var (typeKey, names) in _types)
        {
            var typeDirectory = new Directory();
            foreach (var (nameKey, languages) in names)
            {
                var nameDirectory = new Directory();
                foreach (var (language, data) in languages)
                {
                    nameDirectory.Entries.Add(new Entry(ResourceKey.Of(language), null, new Leaf(data)));
                }

                typeDirectory.Entries.Add(new Entry(nameKey, nameDirectory, null));
            }

            root.Entries.Add(new Entry(typeKey, typeDirectory, null));
        }

        var directories = new List<Directory>();
        var leaves = new List<Leaf>();
        var queue = new Queue<Directory>();
        queue.Enqueue(root);
        var position = 0;
        while (queue.Count > 0)
        {
            var directory = queue.Dequeue();
            directory.Sort();
            directory.Offset = position;
            position += DirectoryBytes + (EntryBytes * directory.Entries.Count);
            directories.Add(directory);
            foreach (var entry in directory.Entries)
            {
                if (entry.Sub is not null)
                {
                    queue.Enqueue(entry.Sub);
                }
                else
                {
                    leaves.Add(entry.Data!);
                }
            }
        }

        foreach (var leaf in leaves)
        {
            leaf.EntryOffset = position;
            position += DataEntryBytes;
        }

        var strings = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var name in directories.SelectMany(directory => directory.Entries).Select(entry => entry.Key.Name).OfType<string>())
        {
            if (!strings.ContainsKey(name))
            {
                strings[name] = position;
                position += sizeof(ushort) + (Encoding.Unicode.GetByteCount(name));
            }
        }

        position = Align(position, 4);
        foreach (var leaf in leaves)
        {
            leaf.DataOffset = position;
            position = Align(position + leaf.Data.Bytes.Length, 4);
        }

        var output = new byte[position];
        foreach (var directory in directories)
        {
            var named = directory.Entries.Count(entry => entry.Key.Name is not null);
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(directory.Offset + NamedCountOffset), (ushort)named);
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(directory.Offset + IdCountOffset), (ushort)(directory.Entries.Count - named));
            for (var index = 0; index < directory.Entries.Count; index++)
            {
                var entry = directory.Entries[index];
                var at = directory.Offset + DirectoryBytes + (EntryBytes * index);
                var name = entry.Key.Name is { } text ? HighBit | (uint)strings[text] : entry.Key.Id!.Value;
                var target = entry.Sub is { } sub ? HighBit | (uint)sub.Offset : (uint)entry.Data!.EntryOffset;
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(at), name);
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(at + 4), target);
            }
        }

        foreach (var (name, offset) in strings)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(offset), (ushort)name.Length);
            Encoding.Unicode.GetBytes(name, output.AsSpan(offset + sizeof(ushort)));
        }

        foreach (var leaf in leaves)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(leaf.EntryOffset), sectionRva + (uint)leaf.DataOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(leaf.EntryOffset + 4), (uint)leaf.Data.Bytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(leaf.EntryOffset + 8), leaf.Data.CodePage);
            leaf.Data.Bytes.CopyTo(output, leaf.DataOffset);
        }

        return output;
    }

    private static List<(ResourceKey Key, uint Target)> ReadDirectory(ReadOnlySpan<byte> section, uint offset, int depth, ref int seen)
    {
        if ((long)offset + DirectoryBytes > section.Length)
        {
            throw Unsupported();
        }

        var at = (int)offset;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(section[(at + NamedCountOffset)..]) + BinaryPrimitives.ReadUInt16LittleEndian(section[(at + IdCountOffset)..]);
        seen += count;
        if (seen > MaxEntries || at + DirectoryBytes + (EntryBytes * count) > section.Length)
        {
            throw Unsupported();
        }

        var entries = new List<(ResourceKey, uint)>(count);
        for (var index = 0; index < count; index++)
        {
            var entry = section[(at + DirectoryBytes + (EntryBytes * index))..];
            var name = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            var target = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            var isDirectory = (target & HighBit) != 0;
            if (isDirectory == (depth == MaxDepth))
            {
                throw Unsupported();
            }

            entries.Add((KeyOf(section, name), isDirectory ? target & ~HighBit : target));
        }

        return entries;
    }

    private static ResourceKey KeyOf(ReadOnlySpan<byte> section, uint name)
    {
        if ((name & HighBit) == 0)
        {
            return ResourceKey.Of((ushort)name);
        }

        var at = (int)(name & ~HighBit);
        if (at + sizeof(ushort) > section.Length)
        {
            throw Unsupported();
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(section[at..]);
        var end = at + sizeof(ushort) + (length * sizeof(char));
        if (end > section.Length)
        {
            throw Unsupported();
        }

        return ResourceKey.Of(Encoding.Unicode.GetString(section[(at + sizeof(ushort))..end]));
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    private static HostingException Unsupported() =>
        new("The program's resource table has a layout this builder does not handle.", "Use a launcher or setup program from a released tPoseidon package.", null);

    private sealed class Directory
    {
        public List<Entry> Entries { get; } = [];

        public int Offset { get; set; }

        public void Sort() => Entries.Sort((left, right) => Compare(left.Key, right.Key));

        private static int Compare(ResourceKey left, ResourceKey right)
        {
            if (left.Name is not null || right.Name is not null)
            {
                return left.Name is null ? 1 : right.Name is null ? -1 : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            }

            return left.Id!.Value.CompareTo(right.Id!.Value);
        }
    }

    private sealed record Entry(ResourceKey Key, Directory? Sub, Leaf? Data);

    private sealed class Leaf
    {
        public Leaf(ResourceData data)
        {
            Data = data;
        }

        public ResourceData Data { get; }

        public int EntryOffset { get; set; }

        public int DataOffset { get; set; }
    }
}
