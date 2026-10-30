using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

public sealed record ExecutableIdentity(string ProductName, string Description, string FileName, string Version, byte[] Icon);

public static class ExecutableBranding
{
    public const string NewSectionName = ".tpres";

    private const int HeaderBytes = 4096;
    private const int DosHeaderBytes = 64;
    private const int PeOffsetField = 0x3C;
    private const int SectionHeaderBytes = 40;
    private const int MaxSections = 96;
    private const int ResourceDirectoryIndex = 2;
    private const int SecurityDirectoryIndex = 4;
    private const int DirectoryBytes = 8;
    private const ushort Pe32Magic = 0x10B;
    private const ushort Pe32PlusMagic = 0x20B;
    private const int Pe32DirectoryOffset = 96;
    private const int Pe32PlusDirectoryOffset = 112;
    private const uint SectionCharacteristics = 0x40000040;
    private const int CoffSectionCountOffset = 2;
    private const int CoffOptionalSizeOffset = 16;
    private const int CoffBytes = 20;
    private const int PeSignatureBytes = 4;
    private static readonly byte[] PeSignature = "PE\0\0"u8.ToArray();

    public static void Apply(string path, ExecutableIdentity identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(identity);
        var icons = IconResources.FromIco(identity.Icon);
        var version = VersionResource.Build(identity.ProductName, identity.Description, identity.FileName, identity.Version);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Replace(file, table =>
            {
                table.RemoveType(ResourceTable.IconType);
                table.RemoveType(ResourceTable.GroupIconType);
                table.RemoveType(ResourceTable.VersionType);
                for (var index = 0; index < icons.Images.Count; index++)
                {
                    table.Set(ResourceTable.IconType, (ushort)(index + 1), ResourceTable.NeutralLanguage, icons.Images[index]);
                }

                table.Set(ResourceTable.GroupIconType, 1, ResourceTable.NeutralLanguage, icons.Group);
                table.Set(ResourceTable.VersionType, 1, ResourceTable.EnglishLanguage, version);
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The program {Path.GetFileName(path)} could not be branded: {ex.Message}", ex);
        }
    }

    internal static ResourceTable Read(Stream file)
    {
        var header = ReadHeader(file);
        var layout = Layout.Of(header);
        return layout.ReadResources(file);
    }

    internal static void Replace(Stream file, Action<ResourceTable> change)
    {
        var header = ReadHeader(file);
        var layout = Layout.Of(header);
        var table = layout.ReadResources(file);
        change(table);
        var newVirtualAddress = Align(layout.SizeOfImage, layout.SectionAlignment);
        var data = table.Build(newVirtualAddress);
        var rawPointer = Align((uint)file.Length, layout.FileAlignment);
        var rawSize = Align((uint)data.Length, layout.FileAlignment);
        file.Seek(0, SeekOrigin.End);
        file.Write(new byte[rawPointer - file.Length]);
        file.Write(data);
        file.Write(new byte[rawSize - data.Length]);
        layout.AddSection(header, newVirtualAddress, (uint)data.Length, rawPointer, rawSize);
        file.Seek(0, SeekOrigin.Begin);
        file.Write(header);
        file.Flush();
    }

    private static byte[] ReadHeader(Stream file)
    {
        var header = new byte[(int)Math.Min(file.Length, HeaderBytes)];
        file.Seek(0, SeekOrigin.Begin);
        file.ReadExactly(header);
        return header;
    }

    private static uint Align(uint value, uint alignment) =>
        alignment == 0 ? value : (value + alignment - 1) / alignment * alignment;

    private static HostingException NotSupported(string why) =>
        new($"This program cannot be branded: {why}.", "Use a launcher or setup program from a released tPoseidon package.", null);

    private sealed class Layout
    {
        private readonly int _optional;
        private readonly int _directories;
        private readonly int _coff;
        private readonly int _sectionTable;
        private readonly List<(uint VirtualAddress, uint VirtualSize, uint RawPointer, uint RawSize)> _sections;
        private readonly uint _resourceRva;
        private readonly uint _resourceSize;

        private Layout(int coff, int optional, int directories, int sectionTable, List<(uint, uint, uint, uint)> sections, uint sizeOfImage, uint sectionAlignment, uint fileAlignment, uint resourceRva, uint resourceSize)
        {
            _resourceRva = resourceRva;
            _resourceSize = resourceSize;
            _coff = coff;
            _optional = optional;
            _directories = directories;
            _sectionTable = sectionTable;
            _sections = sections;
            SizeOfImage = sizeOfImage;
            SectionAlignment = sectionAlignment;
            FileAlignment = fileAlignment;
        }

        public uint SizeOfImage { get; }

        public uint SectionAlignment { get; }

        public uint FileAlignment { get; }

        public static Layout Of(byte[] header)
        {
            if (header.Length < DosHeaderBytes || header[0] != 'M' || header[1] != 'Z')
            {
                throw NotSupported("it is not a Windows program");
            }

            var coff = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(PeOffsetField)) + PeSignatureBytes;
            if (coff < DosHeaderBytes || coff + CoffBytes + 2 > header.Length || !header.AsSpan(coff - PeSignatureBytes, PeSignatureBytes).SequenceEqual(PeSignature))
            {
                throw NotSupported("it is not a Windows program");
            }

            var count = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(coff + CoffSectionCountOffset));
            var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(coff + CoffOptionalSizeOffset));
            var optional = coff + CoffBytes;
            var magic = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(optional));
            var directories = optional + (magic == Pe32PlusMagic ? Pe32PlusDirectoryOffset : Pe32DirectoryOffset);
            var sectionTable = optional + optionalSize;
            if ((magic != Pe32Magic && magic != Pe32PlusMagic) || count is 0 or >= MaxSections || sectionTable + (SectionHeaderBytes * (count + 1)) > header.Length || directories + (DirectoryBytes * (SecurityDirectoryIndex + 1)) > header.Length)
            {
                throw NotSupported("its headers are not in the expected form");
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(directories + (DirectoryBytes * SecurityDirectoryIndex) + 4)) != 0)
            {
                throw NotSupported("it carries a digital signature");
            }

            var sections = new List<(uint, uint, uint, uint)>();
            for (var index = 0; index < count; index++)
            {
                var at = sectionTable + (SectionHeaderBytes * index);
                sections.Add((
                    BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(at + 12)),
                    BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(at + 8)),
                    BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(at + 20)),
                    BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(at + 16))));
            }

            var firstRaw = sections.Where(section => section.Item3 != 0).Min(section => section.Item3);
            var slot = sectionTable + (SectionHeaderBytes * count);
            if (slot + SectionHeaderBytes > firstRaw || header.AsSpan(slot, SectionHeaderBytes).ContainsAnyExcept((byte)0))
            {
                throw NotSupported("there is no room for another section");
            }

            return new Layout(
                coff,
                optional,
                directories,
                sectionTable,
                sections,
                BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(optional + 56)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(optional + 32)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(optional + 36)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(directories + (DirectoryBytes * ResourceDirectoryIndex))),
                BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(directories + (DirectoryBytes * ResourceDirectoryIndex) + 4)));
        }

        public ResourceTable ReadResources(Stream file)
        {
            var rva = _resourceRva;
            var size = _resourceSize;
            if (rva == 0 || size == 0)
            {
                return ResourceTable.Parse([], 0);
            }

            var section = _sections.FirstOrDefault(each => rva >= each.VirtualAddress && rva < each.VirtualAddress + Math.Max(each.VirtualSize, each.RawSize));
            if (section.RawPointer == 0 || (long)section.RawPointer + (rva - section.VirtualAddress) + size > file.Length)
            {
                throw NotSupported("its resources are outside the file");
            }

            var bytes = new byte[size];
            file.Seek(section.RawPointer + (rva - section.VirtualAddress), SeekOrigin.Begin);
            file.ReadExactly(bytes);
            return ResourceTable.Parse(bytes, rva);
        }

        public void AddSection(byte[] header, uint virtualAddress, uint virtualSize, uint rawPointer, uint rawSize)
        {
            var slot = _sectionTable + (SectionHeaderBytes * _sections.Count);
            var name = Encoding.ASCII.GetBytes(NewSectionName);
            name.CopyTo(header, slot);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(slot + 8), virtualSize);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(slot + 12), virtualAddress);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(slot + 16), rawSize);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(slot + 20), rawPointer);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(slot + 36), SectionCharacteristics);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(_coff + CoffSectionCountOffset), (ushort)(_sections.Count + 1));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(_optional + 56), Align(virtualAddress + virtualSize, SectionAlignment));
            var initialized = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(_optional + 8));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(_optional + 8), initialized + rawSize);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(_optional + 64), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(_directories + (DirectoryBytes * ResourceDirectoryIndex)), virtualAddress);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(_directories + (DirectoryBytes * ResourceDirectoryIndex) + 4), virtualSize);
        }
    }
}

internal static class IconResources
{
    private const int HeaderBytes = 6;
    private const int IcoEntryBytes = 16;
    private const int GroupEntryBytes = 14;
    private const ushort IconKind = 1;

    public static (IReadOnlyList<byte[]> Images, byte[] Group) FromIco(byte[] ico)
    {
        ArgumentNullException.ThrowIfNull(ico);
        if (ico.Length < HeaderBytes || BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(0)) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2)) != IconKind)
        {
            throw new HostingException("The icon is not a Windows icon.");
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4));
        if (count == 0 || ico.Length < HeaderBytes + (IcoEntryBytes * count))
        {
            throw new HostingException("The icon has no pictures.");
        }

        var images = new List<byte[]>(count);
        var group = new byte[HeaderBytes + (GroupEntryBytes * count)];
        BinaryPrimitives.WriteUInt16LittleEndian(group.AsSpan(2), IconKind);
        BinaryPrimitives.WriteUInt16LittleEndian(group.AsSpan(4), count);
        for (var index = 0; index < count; index++)
        {
            var entry = ico.AsSpan(HeaderBytes + (IcoEntryBytes * index), IcoEntryBytes);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
            if ((long)offset + size > ico.Length)
            {
                throw new HostingException("The icon is damaged.");
            }

            images.Add(ico.AsSpan((int)offset, (int)size).ToArray());
            var target = group.AsSpan(HeaderBytes + (GroupEntryBytes * index), GroupEntryBytes);
            entry[..12].CopyTo(target);
            BinaryPrimitives.WriteUInt16LittleEndian(target[12..], (ushort)(index + 1));
        }

        return (images, group);
    }
}

internal static class VersionResource
{
    private const uint Signature = 0xFEEF04BD;
    private const uint StructureVersion = 0x00010000;
    private const uint FlagsMask = 0x3F;
    private const uint WindowsNt = 0x00040004;
    private const uint ApplicationType = 1;
    private const int FixedInfoBytes = 52;
    private const ushort TextBlock = 1;
    private const ushort BinaryBlock = 0;
    private const string LanguageTable = "040904B0";
    private const ushort EnglishUs = 0x0409;
    private const ushort UnicodeCodePage = 0x04B0;
    private const int Alignment = 4;
    private const int VersionParts = 4;

    public static byte[] Build(string productName, string description, string fileName, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        var numbers = Numbers(version);
        var fixedInfo = new byte[FixedInfoBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo, Signature);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(4), StructureVersion);
        var high = (numbers[0] << 16) | numbers[1];
        var low = (numbers[2] << 16) | numbers[3];
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(8), high);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(12), low);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(16), high);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(20), low);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(24), FlagsMask);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(32), WindowsNt);
        BinaryPrimitives.WriteUInt32LittleEndian(fixedInfo.AsSpan(36), ApplicationType);

        var strings = new[]
        {
            Block(TextBlock, "CompanyName", Text(productName), (productName.Length + 1)),
            Block(TextBlock, "FileDescription", Text(description), (description.Length + 1)),
            Block(TextBlock, "FileVersion", Text(version), (version.Length + 1)),
            Block(TextBlock, "InternalName", Text(fileName), (fileName.Length + 1)),
            Block(TextBlock, "OriginalFilename", Text(fileName), (fileName.Length + 1)),
            Block(TextBlock, "ProductName", Text(productName), (productName.Length + 1)),
            Block(TextBlock, "ProductVersion", Text(version), (version.Length + 1)),
        };
        var table = Block(TextBlock, LanguageTable, [], 0, strings);
        var stringInfo = Block(TextBlock, "StringFileInfo", [], 0, table);
        var translation = new byte[4];
        BinaryPrimitives.WriteUInt16LittleEndian(translation, EnglishUs);
        BinaryPrimitives.WriteUInt16LittleEndian(translation.AsSpan(2), UnicodeCodePage);
        var variableInfo = Block(TextBlock, "VarFileInfo", [], 0, Block(BinaryBlock, "Translation", translation, translation.Length));
        return Block(BinaryBlock, "VS_VERSION_INFO", fixedInfo, fixedInfo.Length, stringInfo, variableInfo);
    }

    private static uint[] Numbers(string version)
    {
        var core = version.Split(LauncherVersions.BuildSeparator)[0].Split(LauncherVersions.LabelSeparator)[0];
        var result = new uint[VersionParts];
        var parts = core.Split('.');
        for (var index = 0; index < VersionParts && index < parts.Length; index++)
        {
            result[index] = uint.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? Math.Min(value, ushort.MaxValue) : 0;
        }

        return result;
    }

    private static byte[] Text(string value) => Encoding.Unicode.GetBytes(value + "\0");

    private static byte[] Block(ushort type, string key, byte[] value, int valueLength, params byte[][] children)
    {
        using var stream = new MemoryStream();
        stream.Write(new byte[6]);
        stream.Write(Encoding.Unicode.GetBytes(key + "\0"));
        Pad(stream);
        stream.Write(value);
        foreach (var child in children)
        {
            Pad(stream);
            stream.Write(child);
        }

        var bytes = stream.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0), (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), (ushort)valueLength);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), type);
        return bytes;
    }

    private static void Pad(MemoryStream stream)
    {
        while (stream.Length % Alignment != 0)
        {
            stream.WriteByte(0);
        }
    }
}
