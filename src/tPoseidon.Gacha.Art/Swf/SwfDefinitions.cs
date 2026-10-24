namespace tPoseidon.Gacha.Art.Swf;

public sealed record ShapeDefinition(int Id, int Version, SwfRect Bounds, SwfRect? EndBounds, IReadOnlyList<ShapeLayer> Layers)
{
    public bool IsMorph => EndBounds.HasValue;
}

public sealed record SoundInfo(bool SyncStop, bool SyncNoMultiple, int? InPoint, int? OutPoint, int? Loops);

public sealed record SoundStart(int SoundId, SoundInfo Info);

public abstract record DisplayOperation(int Depth);

public sealed record PlaceOperation(
    int Depth,
    bool Move,
    int? CharacterId,
    SwfMatrix? Matrix,
    SwfColorTransform? ColorTransform,
    int? Ratio,
    string? Name,
    int? ClipDepth,
    int? BlendMode,
    IReadOnlyList<SwfFilter>? Filters,
    bool? CacheAsBitmap,
    IReadOnlyList<string> ClipEvents) : DisplayOperation(Depth);

public sealed record RemoveOperation(int Depth) : DisplayOperation(Depth);

public sealed record TimelineFrame(IReadOnlyList<string> Labels, IReadOnlyList<DisplayOperation> Operations, IReadOnlyList<SoundStart> Sounds, bool HasActions);

public sealed record SpriteDefinition(int Id, IReadOnlyList<TimelineFrame> Frames);

public sealed record ButtonRecord(bool Up, bool Over, bool Down, bool Hit, int CharacterId, int Depth, SwfMatrix Matrix, SwfColorTransform ColorTransform, int? BlendMode, IReadOnlyList<SwfFilter>? Filters);

public sealed record ButtonDefinition(int Id, bool TrackAsMenu, IReadOnlyList<ButtonRecord> Records, IReadOnlyList<string> ActionConditions, IReadOnlyDictionary<string, SoundStart> Sounds);

public sealed record FontDefinition(int Id, string Name, bool Bold, bool Italic, int GlyphCount, IReadOnlyList<int> Codes);

public sealed record GlyphEntry(int Index, int Advance);

public sealed record TextRun(int? FontId, int? Height, SwfColor? Color, int? X, int? Y, IReadOnlyList<GlyphEntry> Glyphs, string Text);

public sealed record TextDefinition(int Id, SwfRect Bounds, SwfMatrix Matrix, IReadOnlyList<TextRun> Runs);

public sealed record EditTextDefinition(
    int Id,
    SwfRect Bounds,
    bool WordWrap,
    bool Multiline,
    bool Password,
    bool ReadOnly,
    bool AutoSize,
    bool NoSelect,
    bool Border,
    bool Html,
    bool UseOutlines,
    int? FontId,
    int? FontHeight,
    SwfColor? Color,
    int? MaxLength,
    int? Align,
    int? LeftMargin,
    int? RightMargin,
    int? Indent,
    int? Leading,
    string VariableName,
    string? InitialText);

public sealed record SoundDefinition(int Id, int Format, int Rate, bool SixteenBit, bool Stereo, uint SampleCount, int DataOffset, int DataLength);

public sealed record BitmapDefinition(int Id, int TagCode, int Offset, int Length);
