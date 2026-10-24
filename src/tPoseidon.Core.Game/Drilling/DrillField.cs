using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public sealed class DrillField
{
    public const int PickTries = 100;
    public const int DepthUnit = 10;

    private readonly ZoneGeometry _geometry;
    private readonly DrillZoneEntry _zone;
    private readonly IReadOnlyDictionary<int, int> _natures;
    private readonly Dictionary<(int X, int Y), int> _laid = new();
    private readonly int _diggable;

    public DrillField(ZoneGeometry geometry, DrillZoneEntry zone, IReadOnlyDictionary<int, int> natures, Random rng)
    {
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _natures = natures ?? throw new ArgumentNullException(nameof(natures));
        ArgumentNullException.ThrowIfNull(rng);
        for (var y = 0; y < geometry.CellsY; y++)
        {
            for (var x = 0; x < geometry.CellsX; x++)
            {
                if (IsDiggable(x, y)) _diggable++;
            }
        }
        if (zone.HasItems) Fill(rng);
    }

    public int MapDepth => _zone.MaxItemDepth;

    public int LaidCount => _laid.Count;

    public int DiggableCount => _diggable;

    public int NatureAt(int cellX, int cellY) =>
        _geometry.HasGroundTypes && _geometry.ContainsCell(cellX, cellY) && _natures.TryGetValue(_geometry.GroundTypeAt(cellX, cellY), out var nature)
            ? nature
            : 0;

    public bool IsDiggable(int cellX, int cellY) =>
        _geometry.ContainsCell(cellX, cellY)
        && _geometry.AttributeAt(cellX, cellY) != ZoneGeometry.CellBlocked
        && NatureAt(cellX, cellY) != DrillRules.NotDiggableNature;

    public int? LaidDepth(int cellX, int cellY) => _laid.TryGetValue((cellX, cellY), out var depth) ? depth : null;

    public void Take(int cellX, int cellY, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (_laid.Remove((cellX, cellY)) && _zone.HasItems) Fill(rng);
    }

    private void Fill(Random rng)
    {
        if (_diggable == 0) return;
        while (_laid.Count * 100.0 / _diggable < _zone.ItemPercentage)
        {
            if (Pick(rng) is not { } cell) return;
            _laid[cell] = DepthUnit * rng.Next(_zone.MinItemDepth / DepthUnit, _zone.MaxItemDepth / DepthUnit + 1);
        }
    }

    private (int X, int Y)? Pick(Random rng)
    {
        for (var attempt = 0; attempt < PickTries; attempt++)
        {
            var x = rng.Next(_geometry.CellsX);
            var y = rng.Next(_geometry.CellsY);
            if (IsDiggable(x, y) && !_laid.ContainsKey((x, y))) return (x, y);
        }
        return null;
    }
}
