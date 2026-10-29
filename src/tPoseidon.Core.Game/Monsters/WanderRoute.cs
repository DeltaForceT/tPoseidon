using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed record WanderRoute(int DestinationX, int DestinationY, double Length, IReadOnlyList<(int X, int Y)> Points)
{
    public const int SearchNodeLimit = 4096;
    public const int MaxWaypoints = 128;

    private const int StraightStepCost = 2;
    private const int DiagonalStepCost = 3;
    private const int HeuristicWeight = 2;

    private const int ReplacementColumnOffset = 1;
    private static readonly int[] ReplacementRowOffsets = { 0, 1, -1, 2, -2, 3 };

    private static readonly (int X, int Y)[] Neighbours =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
        (1, 1), (1, -1), (-1, 1), (-1, -1),
    };

    public static WanderRoute? Find(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY) =>
        Route(geometry, fromX, fromY, toX, toY, keepsPoint: false);

    public static WanderRoute? FindToPoint(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY) =>
        Route(geometry, fromX, fromY, toX, toY, keepsPoint: true);

    public static IReadOnlyList<(int X, int Y)>? FindPathToPoint(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY) =>
        Plan(geometry, fromX, fromY, toX, toY, keepsPoint: true) is { } plan ? Waypoints(plan) : null;

    public static IReadOnlyList<(int X, int Y)>? FindWaypoints(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY) =>
        Plan(geometry, fromX, fromY, toX, toY, keepsPoint: false) is { } plan ? Waypoints(plan) : null;

    private static List<(int X, int Y)> Waypoints(RoutePlan plan)
    {
        var points = new List<(int X, int Y)>(plan.Cells.Count);
        for (var i = 1; i < plan.Cells.Count - 1; i++)
        {
            points.Add((ZoneGeometry.CentreOf(plan.Cells[i].X), ZoneGeometry.CentreOf(plan.Cells[i].Y)));
        }
        points.Add((plan.DestinationX, plan.DestinationY));
        return points;
    }

    private static WanderRoute? Route(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY, bool keepsPoint)
    {
        if (Plan(geometry, fromX, fromY, toX, toY, keepsPoint) is not { } plan) return null;

        var length = 0.0;
        var points = new List<(int X, int Y)>(plan.Cells.Count);
        double previousX = fromX;
        double previousY = fromY;
        for (var i = 1; i < plan.Cells.Count; i++)
        {
            var centreX = ZoneGeometry.CentreOf(plan.Cells[i].X);
            var centreY = ZoneGeometry.CentreOf(plan.Cells[i].Y);
            length += Distance(previousX, previousY, centreX, centreY);
            points.Add((centreX, centreY));
            previousX = centreX;
            previousY = centreY;
        }

        length += Distance(previousX, previousY, plan.DestinationX, plan.DestinationY);
        points.Add((plan.DestinationX, plan.DestinationY));
        return new WanderRoute(plan.DestinationX, plan.DestinationY, length, points);
    }

    private sealed record RoutePlan(List<(int X, int Y)> Cells, int DestinationX, int DestinationY);

    private static RoutePlan? Plan(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY, bool keepsPoint)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var startX = ZoneGeometry.CellOf(fromX);
        var startY = ZoneGeometry.CellOf(fromY);
        var goalX = ZoneGeometry.CellOf(toX);
        var goalY = ZoneGeometry.CellOf(toY);

        if (!geometry.ContainsCell(startX, startY)) return null;
        if (!geometry.ContainsCell(goalX, goalY)) return null;
        var goalIsFree = geometry.IsFreeDestination(goalX, goalY);
        if (startX == goalX && startY == goalY)
        {
            return keepsPoint && goalIsFree && (fromX != toX || fromY != toY)
                ? new RoutePlan([(startX, startY)], toX, toY)
                : null;
        }
        if (!goalIsFree && !TryReplaceDestination(geometry, ref goalX, ref goalY))
        {
            return null;
        }

        var cells = Search(geometry, startX, startY, goalX, goalY);
        if (cells is null || cells.Count - 1 > MaxWaypoints) return null;

        var destinationX = keepsPoint && goalIsFree ? toX : ZoneGeometry.CentreOf(goalX);
        var destinationY = keepsPoint && goalIsFree ? toY : ZoneGeometry.CentreOf(goalY);
        return new RoutePlan(cells, destinationX, destinationY);
    }

    private static bool TryReplaceDestination(ZoneGeometry geometry, ref int cellX, ref int cellY)
    {
        var column = cellX + ReplacementColumnOffset;
        foreach (var rowOffset in ReplacementRowOffsets)
        {
            var row = cellY + rowOffset;
            if (!geometry.ContainsCell(column, row)) continue;
            if (!geometry.IsFreeDestination(column, row)) continue;
            cellX = column;
            cellY = row;
            return true;
        }
        return false;
    }

    private static List<(int X, int Y)>? Search(ZoneGeometry geometry, int startX, int startY, int goalX, int goalY)
    {
        var width = geometry.CellsX;
        var start = startY * width + startX;
        var goal = goalY * width + goalX;

        var open = new PriorityQueue<int, int>();
        var costTo = new Dictionary<int, int> { [start] = 0 };
        var cameFrom = new Dictionary<int, int>();
        var closed = new HashSet<int>();
        open.Enqueue(start, Heuristic(startX, startY, goalX, goalY));

        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal) return Rebuild(cameFrom, start, goal, width);
            if (!closed.Add(current)) continue;
            if (closed.Count > SearchNodeLimit) return null;

            var currentX = current % width;
            var currentY = current / width;
            foreach (var (stepX, stepY) in Neighbours)
            {
                var nextX = currentX + stepX;
                var nextY = currentY + stepY;
                if (!geometry.ContainsCell(nextX, nextY)) continue;
                if (geometry.IsBlocked(nextX, nextY)) continue;

                var diagonal = stepX != 0 && stepY != 0;
                var next = nextY * width + nextX;
                if (closed.Contains(next)) continue;

                var cost = costTo[current] + (diagonal ? DiagonalStepCost : StraightStepCost);
                if (costTo.TryGetValue(next, out var known) && known <= cost) continue;

                costTo[next] = cost;
                cameFrom[next] = current;
                open.Enqueue(next, cost + Heuristic(nextX, nextY, goalX, goalY));
            }
        }
        return null;
    }

    private static int Heuristic(int cellX, int cellY, int goalX, int goalY)
        => (Math.Abs(cellX - goalX) + Math.Abs(cellY - goalY)) * HeuristicWeight;

    private static List<(int X, int Y)> Rebuild(Dictionary<int, int> cameFrom, int start, int goal, int width)
    {
        var cells = new List<(int X, int Y)>();
        var node = goal;
        while (true)
        {
            cells.Add((node % width, node / width));
            if (node == start) break;
            node = cameFrom[node];
        }
        cells.Reverse();
        return cells;
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
