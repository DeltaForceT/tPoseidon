using System.Text;

namespace tPoseidon.Gacha.Art.Swf;

public sealed class SwfLibrary
{
    private static readonly string[] ClipEventNames =
    [
        "keyUp", "keyDown", "mouseUp", "mouseDown", "mouseMove", "unload", "enterFrame", "load",
        "dragOver", "rollOut", "rollOver", "releaseOutside", "release", "press", "initialize", "data",
        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, "construct", "keyPress", "dragOut",
    ];

    private static readonly string[] ButtonConditionNames =
    [
        "idleToOverDown", "outDownToIdle", "outDownToOverDown", "overDownToOutDown",
        "overDownToOverUp", "overUpToOverDown", "overUpToIdle", "idleToOverUp",
    ];

    private static readonly string[] ButtonSoundNames = ["overUpToIdle", "idleToOverUp", "overUpToOverDown", "overDownToOverUp"];

    private readonly Dictionary<int, ShapeDefinition> _shapes = new();
    private readonly Dictionary<int, SpriteDefinition> _sprites = new();
    private readonly Dictionary<int, ButtonDefinition> _buttons = new();
    private readonly Dictionary<int, FontDefinition> _fonts = new();
    private readonly Dictionary<int, TextDefinition> _texts = new();
    private readonly Dictionary<int, EditTextDefinition> _editTexts = new();
    private readonly Dictionary<int, SoundDefinition> _sounds = new();
    private readonly Dictionary<int, BitmapDefinition> _bitmaps = new();
    private readonly Dictionary<string, int> _exports = new(StringComparer.Ordinal);
    private readonly Dictionary<int, List<(int Code, SwfTag Tag)>> _pendingButtonSounds = new();

    private SwfLibrary(SwfFile file)
    {
        File = file;
        Root = new SpriteDefinition(0, ReadTimeline(file.RootTags(), true));
    }

    public SwfFile File { get; }

    public SpriteDefinition Root { get; }

    public SwfColor Background { get; private set; } = new(255, 255, 255, 255);

    public SwfTag? JpegTables { get; private set; }

    public IReadOnlyDictionary<int, ShapeDefinition> Shapes => _shapes;

    public IReadOnlyDictionary<int, SpriteDefinition> Sprites => _sprites;

    public IReadOnlyDictionary<int, ButtonDefinition> Buttons => _buttons;

    public IReadOnlyDictionary<int, FontDefinition> Fonts => _fonts;

    public IReadOnlyDictionary<int, TextDefinition> Texts => _texts;

    public IReadOnlyDictionary<int, EditTextDefinition> EditTexts => _editTexts;

    public IReadOnlyDictionary<int, SoundDefinition> Sounds => _sounds;

    public IReadOnlyDictionary<int, BitmapDefinition> Bitmaps => _bitmaps;

    public IReadOnlyDictionary<string, int> Exports => _exports;

    public static SwfLibrary Load(SwfFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new SwfLibrary(file);
    }

    private List<TimelineFrame> ReadTimeline(IReadOnlyList<SwfTag> tags, bool root)
    {
        var frames = new List<TimelineFrame>();
        var labels = new List<string>();
        var operations = new List<DisplayOperation>();
        var sounds = new List<SoundStart>();
        var actions = false;
        foreach (var tag in tags)
        {
            var reader = File.Open(tag);
            switch (tag.Code)
            {
                case SwfTagCode.ShowFrame:
                    frames.Add(new TimelineFrame(labels.ToArray(), operations.ToArray(), sounds.ToArray(), actions));
                    labels.Clear();
                    operations.Clear();
                    sounds.Clear();
                    actions = false;
                    break;
                case SwfTagCode.FrameLabel:
                    labels.Add(reader.NullTerminatedString());
                    break;
                case SwfTagCode.PlaceObject:
                    operations.Add(ReadPlaceObject1(reader));
                    break;
                case SwfTagCode.PlaceObject2:
                    operations.Add(ReadPlaceObject2(reader, false));
                    break;
                case SwfTagCode.PlaceObject3:
                    operations.Add(ReadPlaceObject2(reader, true));
                    break;
                case SwfTagCode.RemoveObject:
                    reader.U16();
                    operations.Add(new RemoveOperation(reader.U16()));
                    break;
                case SwfTagCode.RemoveObject2:
                    operations.Add(new RemoveOperation(reader.U16()));
                    break;
                case SwfTagCode.StartSound:
                {
                    var id = reader.U16();
                    sounds.Add(new SoundStart(id, ReadSoundInfo(reader)));
                    break;
                }
                case SwfTagCode.DoAction:
                    actions = true;
                    break;
                default:
                    if (root) ReadDefinition(tag, reader);
                    break;
            }
        }
        if (labels.Count > 0 || operations.Count > 0 || sounds.Count > 0 || actions)
        {
            frames.Add(new TimelineFrame(labels.ToArray(), operations.ToArray(), sounds.ToArray(), actions));
        }
        return frames;
    }

    private void ReadDefinition(SwfTag tag, SwfReader reader)
    {
        switch (tag.Code)
        {
            case SwfTagCode.SetBackgroundColor:
                Background = SwfColor.ReadRgb(reader);
                break;
            case SwfTagCode.JpegTables:
                JpegTables = tag;
                break;
            case SwfTagCode.DefineShape:
                AddShape(reader, 1);
                break;
            case SwfTagCode.DefineShape2:
                AddShape(reader, 2);
                break;
            case SwfTagCode.DefineShape3:
                AddShape(reader, 3);
                break;
            case SwfTagCode.DefineShape4:
                AddShape(reader, 4);
                break;
            case SwfTagCode.DefineMorphShape:
                AddMorphShape(reader);
                break;
            case SwfTagCode.DefineBits:
            case SwfTagCode.DefineBitsJpeg2:
            case SwfTagCode.DefineBitsJpeg3:
            case SwfTagCode.DefineBitsLossless:
            case SwfTagCode.DefineBitsLossless2:
            {
                var id = reader.U16();
                _bitmaps[id] = new BitmapDefinition(id, tag.Code, tag.Offset, tag.Length);
                break;
            }
            case SwfTagCode.DefineSound:
                AddSound(reader);
                break;
            case SwfTagCode.DefineSprite:
            {
                var id = reader.U16();
                reader.U16();
                var inner = SwfFile.ReadTags(File.Data, reader.Position, reader.End);
                _sprites[id] = new SpriteDefinition(id, ReadTimeline(inner, false));
                break;
            }
            case SwfTagCode.DefineButton2:
                AddButton(reader);
                break;
            case SwfTagCode.DefineButtonSound:
                AddButtonSound(reader);
                break;
            case SwfTagCode.DefineFont2:
            case SwfTagCode.DefineFont3:
                AddFont(reader);
                break;
            case SwfTagCode.DefineText:
                AddText(reader, false);
                break;
            case SwfTagCode.DefineText2:
                AddText(reader, true);
                break;
            case SwfTagCode.DefineEditText:
                AddEditText(reader);
                break;
            case SwfTagCode.ExportAssets:
            {
                var count = reader.U16();
                for (var i = 0; i < count; i++)
                {
                    var id = reader.U16();
                    _exports[reader.NullTerminatedString()] = id;
                }
                break;
            }
            default:
                break;
        }
    }

    private void AddShape(SwfReader reader, int version)
    {
        var id = reader.U16();
        var bounds = SwfRect.Read(reader);
        if (version == 4)
        {
            SwfRect.Read(reader);
            reader.U8();
        }
        var styles = ShapeStyleReader.ReadShapeStyles(reader, version);
        var records = ShapeRecordReader.Read(reader, version, false);
        _shapes[id] = new ShapeDefinition(id, version, bounds, null, ShapeGeometryBuilder.Build(styles, records, null));
    }

    private void AddMorphShape(SwfReader reader)
    {
        var id = reader.U16();
        var startBounds = SwfRect.Read(reader);
        var endBounds = SwfRect.Read(reader);
        var offset = (int)reader.U32();
        var endEdgesAt = reader.Position + offset;
        var styles = ShapeStyleReader.ReadMorphStyles(reader);
        var start = ShapeRecordReader.Read(reader, 3, true);
        reader.Seek(endEdgesAt);
        var end = ShapeRecordReader.Read(reader, 3, true);
        _shapes[id] = new ShapeDefinition(id, 3, startBounds, endBounds, ShapeGeometryBuilder.Build(styles, start, end));
    }

    private void AddSound(SwfReader reader)
    {
        var id = reader.U16();
        var format = (int)reader.UBits(4);
        var rate = (int)reader.UBits(2);
        var sixteen = reader.Flag();
        var stereo = reader.Flag();
        reader.Align();
        var samples = reader.U32();
        _sounds[id] = new SoundDefinition(id, format, rate, sixteen, stereo, samples, reader.Position, reader.Remaining);
    }

    private void AddButton(SwfReader reader)
    {
        var id = reader.U16();
        var trackAsMenu = (reader.U8() & 1) != 0;
        var actionOffset = reader.U16();
        var actionsAt = actionOffset == 0 ? -1 : reader.Position - 2 + actionOffset;
        var records = new List<ButtonRecord>();
        while (true)
        {
            var flags = reader.U8();
            if (flags == 0) break;
            var characterId = reader.U16();
            var depth = reader.U16();
            var matrix = SwfMatrix.Read(reader);
            var colorTransform = SwfColorTransform.Read(reader, true);
            IReadOnlyList<SwfFilter>? filters = (flags & 0x10) != 0 ? SwfFilterReader.ReadList(reader) : null;
            int? blend = (flags & 0x20) != 0 ? reader.U8() : null;
            records.Add(new ButtonRecord((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0, characterId, depth, matrix, colorTransform, blend, filters));
        }
        var conditions = new List<string>();
        if (actionsAt >= 0)
        {
            reader.Seek(actionsAt);
            while (reader.Remaining >= 4)
            {
                var start = reader.Position;
                var size = reader.U16();
                var high = reader.U8();
                var mask = (high << 8) | reader.U8();
                for (var bit = 0; bit < ButtonConditionNames.Length; bit++)
                {
                    if ((mask & (0x8000 >> bit)) != 0) conditions.Add(ButtonConditionNames[bit]);
                }
                var key = (mask >> 1) & 0x7F;
                if (key != 0) conditions.Add($"keyPress:{key}");
                if ((mask & 1) != 0) conditions.Add("overDownToIdle");
                if (size == 0) break;
                reader.Seek(start + size);
            }
        }
        var sounds = new Dictionary<string, SoundStart>(StringComparer.Ordinal);
        if (_buttons.TryGetValue(id, out var earlier))
        {
            foreach (var (name, sound) in earlier.Sounds) sounds[name] = sound;
        }
        _buttons[id] = new ButtonDefinition(id, trackAsMenu, records, conditions.Distinct(StringComparer.Ordinal).ToArray(), sounds);
    }

    private void AddButtonSound(SwfReader reader)
    {
        var id = reader.U16();
        var sounds = new Dictionary<string, SoundStart>(StringComparer.Ordinal);
        foreach (var name in ButtonSoundNames)
        {
            var soundId = reader.U16();
            if (soundId != 0) sounds[name] = new SoundStart(soundId, ReadSoundInfo(reader));
        }
        if (_buttons.TryGetValue(id, out var button))
        {
            _buttons[id] = button with { Sounds = sounds };
        }
        else
        {
            _buttons[id] = new ButtonDefinition(id, false, [], [], sounds);
        }
    }

    private void AddFont(SwfReader reader)
    {
        var id = reader.U16();
        var flags = reader.U8();
        var wideOffsets = (flags & 0x08) != 0;
        var wideCodes = (flags & 0x04) != 0;
        var italic = (flags & 0x02) != 0;
        var bold = (flags & 0x01) != 0;
        reader.U8();
        var nameLength = reader.U8();
        var name = reader.FixedString(nameLength).TrimEnd('\0');
        var glyphs = reader.U16();
        var codes = new List<int>(glyphs);
        if (glyphs > 0)
        {
            var tableStart = reader.Position;
            reader.Skip(glyphs * (wideOffsets ? 4 : 2));
            var codeTableOffset = wideOffsets ? (int)reader.U32() : reader.U16();
            reader.Seek(tableStart + codeTableOffset);
            for (var i = 0; i < glyphs; i++) codes.Add(wideCodes ? reader.U16() : reader.U8());
        }
        _fonts[id] = new FontDefinition(id, name, bold, italic, glyphs, codes);
    }

    private void AddText(SwfReader reader, bool withAlpha)
    {
        var id = reader.U16();
        var bounds = SwfRect.Read(reader);
        var matrix = SwfMatrix.Read(reader);
        var glyphBits = reader.U8();
        var advanceBits = reader.U8();
        var runs = new List<TextRun>();
        int? currentFont = null;
        while (true)
        {
            var flags = reader.U8();
            if (flags == 0) break;
            int? font = null, height = null, x = null, y = null;
            SwfColor? color = null;
            if ((flags & 0x08) != 0) font = reader.U16();
            if ((flags & 0x04) != 0) color = withAlpha ? SwfColor.ReadRgba(reader) : SwfColor.ReadRgb(reader);
            if ((flags & 0x01) != 0) x = reader.S16();
            if ((flags & 0x02) != 0) y = reader.S16();
            if ((flags & 0x08) != 0) height = reader.U16();
            if (font.HasValue) currentFont = font;
            var count = reader.U8();
            var glyphs = new List<GlyphEntry>(count);
            for (var i = 0; i < count; i++)
            {
                glyphs.Add(new GlyphEntry((int)reader.UBits(glyphBits), reader.SBits(advanceBits)));
            }
            reader.Align();
            runs.Add(new TextRun(font, height, color, x, y, glyphs, Decode(currentFont, glyphs)));
        }
        _texts[id] = new TextDefinition(id, bounds, matrix, runs);
    }

    private string Decode(int? fontId, IReadOnlyList<GlyphEntry> glyphs)
    {
        if (fontId is not { } id || !_fonts.TryGetValue(id, out var font)) return string.Empty;
        var text = new StringBuilder(glyphs.Count);
        foreach (var glyph in glyphs)
        {
            if (glyph.Index >= 0 && glyph.Index < font.Codes.Count) text.Append(char.ConvertFromUtf32(font.Codes[glyph.Index]));
        }
        return text.ToString();
    }

    private void AddEditText(SwfReader reader)
    {
        var id = reader.U16();
        var bounds = SwfRect.Read(reader);
        var first = reader.U8();
        var second = reader.U8();
        var hasText = (first & 0x80) != 0;
        var wordWrap = (first & 0x40) != 0;
        var multiline = (first & 0x20) != 0;
        var password = (first & 0x10) != 0;
        var readOnly = (first & 0x08) != 0;
        var hasColor = (first & 0x04) != 0;
        var hasMaxLength = (first & 0x02) != 0;
        var hasFont = (first & 0x01) != 0;
        var hasFontClass = (second & 0x80) != 0;
        var autoSize = (second & 0x40) != 0;
        var hasLayout = (second & 0x20) != 0;
        var noSelect = (second & 0x10) != 0;
        var border = (second & 0x08) != 0;
        var html = (second & 0x02) != 0;
        var outlines = (second & 0x01) != 0;
        int? fontId = hasFont ? reader.U16() : null;
        if (hasFontClass) reader.NullTerminatedString();
        int? fontHeight = hasFont || hasFontClass ? reader.U16() : null;
        SwfColor? color = hasColor ? SwfColor.ReadRgba(reader) : null;
        int? maxLength = hasMaxLength ? reader.U16() : null;
        int? align = null, left = null, right = null, indent = null, leading = null;
        if (hasLayout)
        {
            align = reader.U8();
            left = reader.U16();
            right = reader.U16();
            indent = reader.U16();
            leading = reader.S16();
        }
        var variable = reader.NullTerminatedString();
        var initial = hasText ? reader.NullTerminatedString() : null;
        _editTexts[id] = new EditTextDefinition(id, bounds, wordWrap, multiline, password, readOnly, autoSize, noSelect, border, html, outlines, fontId, fontHeight, color, maxLength, align, left, right, indent, leading, variable, initial);
    }

    private static PlaceOperation ReadPlaceObject1(SwfReader reader)
    {
        var character = reader.U16();
        var depth = reader.U16();
        var matrix = SwfMatrix.Read(reader);
        SwfColorTransform? colorTransform = reader.AtEnd ? null : SwfColorTransform.Read(reader, false);
        return new PlaceOperation(depth, false, character, matrix, colorTransform, null, null, null, null, null, null, []);
    }

    private PlaceOperation ReadPlaceObject2(SwfReader reader, bool third)
    {
        var flags = reader.U8();
        var flags2 = third ? reader.U8() : (byte)0;
        var hasClipActions = (flags & 0x80) != 0;
        var hasClipDepth = (flags & 0x40) != 0;
        var hasName = (flags & 0x20) != 0;
        var hasRatio = (flags & 0x10) != 0;
        var hasColorTransform = (flags & 0x08) != 0;
        var hasMatrix = (flags & 0x04) != 0;
        var hasCharacter = (flags & 0x02) != 0;
        var move = (flags & 0x01) != 0;
        var hasImage = (flags2 & 0x10) != 0;
        var hasClassName = (flags2 & 0x08) != 0;
        var hasCache = (flags2 & 0x04) != 0;
        var hasBlend = (flags2 & 0x02) != 0;
        var hasFilters = (flags2 & 0x01) != 0;
        var depth = reader.U16();
        if (hasClassName || (hasImage && hasCharacter)) reader.NullTerminatedString();
        int? character = hasCharacter ? reader.U16() : null;
        SwfMatrix? matrix = hasMatrix ? SwfMatrix.Read(reader) : null;
        SwfColorTransform? colorTransform = hasColorTransform ? SwfColorTransform.Read(reader, true) : null;
        int? ratio = hasRatio ? reader.U16() : null;
        var name = hasName ? reader.NullTerminatedString() : null;
        int? clipDepth = hasClipDepth ? reader.U16() : null;
        var filters = hasFilters ? SwfFilterReader.ReadList(reader) : null;
        int? blend = hasBlend ? reader.U8() : null;
        bool? cache = hasCache ? reader.U8() != 0 : null;
        var events = hasClipActions ? ReadClipEvents(reader) : [];
        return new PlaceOperation(depth, move, character, matrix, colorTransform, ratio, name, clipDepth, blend, filters, cache, events);
    }

    private string[] ReadClipEvents(SwfReader reader)
    {
        var wide = File.Version >= 6;
        reader.U16();
        ReadEventMask(reader, wide);
        var names = new List<string>();
        while (!reader.AtEnd)
        {
            var mask = ReadEventMask(reader, wide);
            if (mask == 0) break;
            var size = (int)reader.U32();
            for (var bit = 0; bit < ClipEventNames.Length; bit++)
            {
                if ((mask & (1u << (31 - bit))) != 0 && ClipEventNames[bit].Length > 0) names.Add(ClipEventNames[bit]);
            }
            reader.Skip(size);
        }
        return names.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static uint ReadEventMask(SwfReader reader, bool wide)
    {
        var low = reader.U16();
        var value = (uint)(((low & 0xFF) << 24) | ((low >> 8) << 16));
        if (wide)
        {
            var high = reader.U16();
            value |= (uint)(((high & 0xFF) << 8) | (high >> 8));
        }
        return value;
    }

    private static SoundInfo ReadSoundInfo(SwfReader reader)
    {
        var flags = reader.U8();
        var stop = (flags & 0x20) != 0;
        var noMultiple = (flags & 0x10) != 0;
        var hasEnvelope = (flags & 0x08) != 0;
        var hasLoops = (flags & 0x04) != 0;
        var hasOut = (flags & 0x02) != 0;
        var hasIn = (flags & 0x01) != 0;
        int? inPoint = hasIn ? (int)reader.U32() : null;
        int? outPoint = hasOut ? (int)reader.U32() : null;
        int? loops = hasLoops ? reader.U16() : null;
        if (hasEnvelope)
        {
            var points = reader.U8();
            reader.Skip(points * 8);
        }
        return new SoundInfo(stop, noMultiple, inPoint, outPoint, loops);
    }
}
