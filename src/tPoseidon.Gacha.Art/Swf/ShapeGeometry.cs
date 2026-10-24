namespace tPoseidon.Gacha.Art.Swf;

public readonly record struct MorphPoint(int X, int Y, int EndX, int EndY)
{
    public bool SameStart(MorphPoint other) => X == other.X && Y == other.Y;

    public static MorphPoint Middle(MorphPoint a, MorphPoint b) =>
        new((a.X + b.X) / 2, (a.Y + b.Y) / 2, (a.EndX + b.EndX) / 2, (a.EndY + b.EndY) / 2);
}

public sealed record ShapeEdge(MorphPoint From, MorphPoint? Control, MorphPoint To)
{
    public ShapeEdge Reversed() => new(To, Control, From);
}

public enum PathVerb
{
    Move,
    Line,
    Curve,
    Close,
}

public readonly record struct PathCommand(PathVerb Verb, MorphPoint Point, MorphPoint Control);

public sealed record StyledPath(int Style, IReadOnlyList<PathCommand> Commands);

public sealed record ShapeLayer(StyleSet Styles, IReadOnlyList<StyledPath> Fills, IReadOnlyList<StyledPath> Lines);

public static class ShapeGeometryBuilder
{
    public static IReadOnlyList<ShapeLayer> Build(StyleSet styles, IReadOnlyList<ShapeRecord> start, IReadOnlyList<ShapeRecord>? end)
    {
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(start);
        var layers = new List<ShapeLayer>();
        var layer = new LayerBuilder(styles);
        int x = 0, y = 0, endX = 0, endY = 0, fill0 = 0, fill1 = 0, line = 0, cursor = 0;
        foreach (var record in start)
        {
            if (record is StyleChangeRecord change)
            {
                if (change.NewStyles is { } newStyles)
                {
                    layer.FinishInto(layers);
                    layer = new LayerBuilder(newStyles);
                    fill0 = fill1 = line = 0;
                }
                if (change.Fill0 is { } f0) fill0 = f0;
                if (change.Fill1 is { } f1) fill1 = f1;
                if (change.Line is { } l) line = l;
                if (change.HasMove)
                {
                    x = change.MoveX!.Value;
                    y = change.MoveY!.Value;
                }
                if (end is null)
                {
                    endX = x;
                    endY = y;
                }
                else if (cursor < end.Count && end[cursor] is StyleChangeRecord endChange)
                {
                    cursor++;
                    if (endChange.HasMove)
                    {
                        endX = endChange.MoveX!.Value;
                        endY = endChange.MoveY!.Value;
                    }
                }
                continue;
            }
            var from = new MorphPoint(x, y, endX, endY);
            ShapeRecord endRecord = record;
            if (end is not null)
            {
                while (cursor < end.Count && end[cursor] is StyleChangeRecord stray)
                {
                    cursor++;
                    if (stray.HasMove)
                    {
                        endX = stray.MoveX!.Value;
                        endY = stray.MoveY!.Value;
                        from = new MorphPoint(x, y, endX, endY);
                    }
                }
                if (cursor >= end.Count) throw new SwfFormatException("morph end shape has fewer edges than the start shape");
                endRecord = end[cursor++];
            }
            var edge = MakeEdge(from, record, endRecord);
            layer.Add(edge, fill0, fill1, line);
            x = edge.To.X;
            y = edge.To.Y;
            endX = edge.To.EndX;
            endY = edge.To.EndY;
        }
        layer.FinishInto(layers);
        return layers;
    }

    private static ShapeEdge MakeEdge(MorphPoint from, ShapeRecord startRecord, ShapeRecord endRecord)
    {
        var (startControl, startTo) = Deltas(from.X, from.Y, startRecord);
        var (endControl, endTo) = Deltas(from.EndX, from.EndY, endRecord);
        var to = new MorphPoint(startTo.X, startTo.Y, endTo.X, endTo.Y);
        if (startControl is null && endControl is null) return new ShapeEdge(from, null, to);
        var sc = startControl ?? ((from.X + startTo.X) / 2, (from.Y + startTo.Y) / 2);
        var ec = endControl ?? ((from.EndX + endTo.X) / 2, (from.EndY + endTo.Y) / 2);
        return new ShapeEdge(from, new MorphPoint(sc.X, sc.Y, ec.X, ec.Y), to);
    }

    private static ((int X, int Y)? Control, (int X, int Y) To) Deltas(int x, int y, ShapeRecord record) => record switch
    {
        StraightEdgeRecord s => (null, (x + s.DeltaX, y + s.DeltaY)),
        CurvedEdgeRecord c => ((x + c.ControlDeltaX, y + c.ControlDeltaY), (x + c.ControlDeltaX + c.AnchorDeltaX, y + c.ControlDeltaY + c.AnchorDeltaY)),
        _ => throw new SwfFormatException("morph edge pairs an edge with a style change"),
    };

    private sealed class LayerBuilder
    {
        private readonly StyleSet _styles;
        private readonly SortedDictionary<int, List<ShapeEdge>> _fills = new();
        private readonly SortedDictionary<int, List<List<ShapeEdge>>> _lines = new();
        private int _edgeCount;

        public LayerBuilder(StyleSet styles) => _styles = styles;

        public void Add(ShapeEdge edge, int fill0, int fill1, int line)
        {
            _edgeCount++;
            if (fill1 > 0) Fill(fill1).Add(edge);
            if (fill0 > 0) Fill(fill0).Add(edge.Reversed());
            if (line > 0)
            {
                if (!_lines.TryGetValue(line, out var subpaths))
                {
                    subpaths = new List<List<ShapeEdge>>();
                    _lines[line] = subpaths;
                }
                if (subpaths.Count > 0 && subpaths[^1][^1].To.SameStart(edge.From)) subpaths[^1].Add(edge);
                else subpaths.Add(new List<ShapeEdge> { edge });
            }
        }

        public void FinishInto(List<ShapeLayer> layers)
        {
            if (_edgeCount == 0 && layers.Count > 0) return;
            var fills = new List<StyledPath>();
            foreach (var (style, edges) in _fills)
            {
                fills.Add(new StyledPath(style, Link(edges)));
            }
            var lines = new List<StyledPath>();
            foreach (var (style, subpaths) in _lines)
            {
                var noClose = style <= _styles.Lines.Count && _styles.Lines[style - 1].Start.NoClose;
                var commands = new List<PathCommand>();
                foreach (var subpath in subpaths)
                {
                    commands.Add(new PathCommand(PathVerb.Move, subpath[0].From, default));
                    foreach (var edge in subpath) commands.Add(Segment(edge));
                    if (!noClose && subpath.Count > 1 && subpath[^1].To.SameStart(subpath[0].From)) commands.Add(new PathCommand(PathVerb.Close, default, default));
                }
                lines.Add(new StyledPath(style, commands));
            }
            layers.Add(new ShapeLayer(_styles, fills, lines));
        }

        private List<ShapeEdge> Fill(int style)
        {
            if (!_fills.TryGetValue(style, out var edges))
            {
                edges = new List<ShapeEdge>();
                _fills[style] = edges;
            }
            return edges;
        }

        private static List<PathCommand> Link(List<ShapeEdge> edges)
        {
            var byStart = new Dictionary<(int, int), List<int>>();
            for (var i = 0; i < edges.Count; i++)
            {
                var key = (edges[i].From.X, edges[i].From.Y);
                if (!byStart.TryGetValue(key, out var list))
                {
                    list = new List<int>();
                    byStart[key] = list;
                }
                list.Add(i);
            }
            var used = new bool[edges.Count];
            var commands = new List<PathCommand>();
            for (var i = 0; i < edges.Count; i++)
            {
                if (used[i]) continue;
                used[i] = true;
                var first = edges[i];
                commands.Add(new PathCommand(PathVerb.Move, first.From, default));
                commands.Add(Segment(first));
                var current = first.To;
                while (!current.SameStart(first.From))
                {
                    var next = NextUnused(byStart, used, current, i);
                    if (next < 0) break;
                    used[next] = true;
                    commands.Add(Segment(edges[next]));
                    current = edges[next].To;
                }
                commands.Add(new PathCommand(PathVerb.Close, default, default));
            }
            return commands;
        }

        private static int NextUnused(Dictionary<(int, int), List<int>> byStart, bool[] used, MorphPoint at, int after)
        {
            if (!byStart.TryGetValue((at.X, at.Y), out var candidates)) return -1;
            var fallback = -1;
            foreach (var candidate in candidates)
            {
                if (used[candidate]) continue;
                if (candidate > after) return candidate;
                if (fallback < 0) fallback = candidate;
            }
            return fallback;
        }

        private static PathCommand Segment(ShapeEdge edge) =>
            edge.Control is { } control
                ? new PathCommand(PathVerb.Curve, edge.To, control)
                : new PathCommand(PathVerb.Line, edge.To, default);
    }
}
