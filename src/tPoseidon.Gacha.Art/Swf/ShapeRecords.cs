namespace tPoseidon.Gacha.Art.Swf;

public abstract record ShapeRecord;

public sealed record StyleChangeRecord(StyleSet? NewStyles, int? Fill0, int? Fill1, int? Line, int? MoveX, int? MoveY) : ShapeRecord
{
    public bool HasMove => MoveX.HasValue;
}

public sealed record StraightEdgeRecord(int DeltaX, int DeltaY) : ShapeRecord;

public sealed record CurvedEdgeRecord(int ControlDeltaX, int ControlDeltaY, int AnchorDeltaX, int AnchorDeltaY) : ShapeRecord;

public static class ShapeRecordReader
{
    public static IReadOnlyList<ShapeRecord> Read(SwfReader reader, int shapeVersion, bool morph)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.Align();
        var fillBits = (int)reader.UBits(4);
        var lineBits = (int)reader.UBits(4);
        var records = new List<ShapeRecord>();
        while (true)
        {
            var isEdge = reader.Flag();
            if (!isEdge)
            {
                var newStyles = reader.Flag();
                var hasLine = reader.Flag();
                var hasFill1 = reader.Flag();
                var hasFill0 = reader.Flag();
                var hasMove = reader.Flag();
                if (!newStyles && !hasLine && !hasFill1 && !hasFill0 && !hasMove) break;
                int? moveX = null, moveY = null, fill0 = null, fill1 = null, line = null;
                if (hasMove)
                {
                    var bits = (int)reader.UBits(5);
                    moveX = reader.SBits(bits);
                    moveY = reader.SBits(bits);
                }
                if (hasFill0) fill0 = (int)reader.UBits(fillBits);
                if (hasFill1) fill1 = (int)reader.UBits(fillBits);
                if (hasLine) line = (int)reader.UBits(lineBits);
                StyleSet? styles = null;
                if (newStyles)
                {
                    if (morph) throw new SwfFormatException("morph shape records cannot carry new styles");
                    styles = ShapeStyleReader.ReadShapeStyles(reader, shapeVersion);
                    fillBits = (int)reader.UBits(4);
                    lineBits = (int)reader.UBits(4);
                }
                records.Add(new StyleChangeRecord(styles, fill0, fill1, line, moveX, moveY));
                continue;
            }
            var straight = reader.Flag();
            var numBits = (int)reader.UBits(4) + 2;
            if (straight)
            {
                var general = reader.Flag();
                int dx = 0, dy = 0;
                if (general)
                {
                    dx = reader.SBits(numBits);
                    dy = reader.SBits(numBits);
                }
                else if (reader.Flag())
                {
                    dy = reader.SBits(numBits);
                }
                else
                {
                    dx = reader.SBits(numBits);
                }
                records.Add(new StraightEdgeRecord(dx, dy));
            }
            else
            {
                records.Add(new CurvedEdgeRecord(reader.SBits(numBits), reader.SBits(numBits), reader.SBits(numBits), reader.SBits(numBits)));
            }
        }
        reader.Align();
        return records;
    }
}
