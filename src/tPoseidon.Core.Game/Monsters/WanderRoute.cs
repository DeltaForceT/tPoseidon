using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public readonly record struct RouteLeg(int FromX, int FromY, int ToX, int ToY, double StartDistance, double EndDistance);

public sealed record WanderRoute(int DestinationX, int DestinationY, double Length, IReadOnlyList<(int X, int Y)> Points)
{
    public const int SearchNodeLimit = 4096;
    public const int MaxWaypoints = 128;
    public const int FreeDestinationRadius = 24;
    public const int FreeDestinationCandidates = 6;

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
        Route(geometry, fromX, fromY, toX, toY, keepsPoint: false, nearestFree: false);

    public static WanderRoute? FindToPoint(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY) =>
        Route(geometry, fromX, fromY, toX, toY, keepsPoint: true, nearestFree: false);

    public static WanderRoute? FindToApproachPoint(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY) =>
        Route(geometry, fromX, fromY, toX, toY, keepsPoint: true, nearestFree: true);

    public static IReadOnlyList<(int X, int Y)>? FindPathToPoint(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY) =>
        Plan(geometry, fromX, fromY, toX, toY, keepsPoint: true, nearestFree: false) is { } plan ? Waypoints(plan) : null;

    public static IReadOnlyList<(int X, int Y)>? FindWaypoints(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY) =>
        Plan(geometry, fromX, fromY, toX, toY, keepsPoint: false, nearestFree: false) is { } plan ? Waypoints(plan) : null;

    public const int LineSampleStep = 4;

    public static IReadOnlyList<RouteLeg> Legs(ZoneGeometry geometry, int fromX, int fromY, IReadOnlyList<(int X, int Y)> points)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(points);
        var legs = new List<RouteLeg>();
        if (points.Count == 0) return legs;

        var travelled = new double[points.Count];
        double previousX = fromX;
        double previousY = fromY;
        var total = 0.0;
        for (var i = 0; i < points.Count; i++)
        {
            total += Distance(previousX, previousY, points[i].X, points[i].Y);
            travelled[i] = total;
            previousX = points[i].X;
            previousY = points[i].Y;
        }

        var anchorX = fromX;
        var anchorY = fromY;
        var anchorIndex = -1;
        while (anchorIndex < points.Count - 1)
        {
            var reach = anchorIndex + 1;
            for (var candidate = points.Count - 1; candidate > anchorIndex + 1; candidate--)
            {
                if (!IsClearLine(geometry, anchorX, anchorY, points[candidate].X, points[candidate].Y)) continue;
                reach = candidate;
                break;
            }
            var startDistance = anchorIndex < 0 ? 0.0 : travelled[anchorIndex];
            legs.Add(new RouteLeg(anchorX, anchorY, points[reach].X, points[reach].Y, startDistance, travelled[reach]));
            anchorX = points[reach].X;
            anchorY = points[reach].Y;
            anchorIndex = reach;
        }
        return legs;
    }

    public static bool IsClearLine(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var length = Distance(fromX, fromY, toX, toY);
        var samples = Math.Max(1, (int)Math.Ceiling(length / LineSampleStep));
        var lastCellX = ZoneGeometry.CellOf(fromX);
        var lastCellY = ZoneGeometry.CellOf(fromY);
        for (var i = 1; i <= samples; i++)
        {
            var x = (int)Math.Round(fromX + (toX - fromX) * (double)i / samples);
            var y = (int)Math.Round(fromY + (toY - fromY) * (double)i / samples);
            var cellX = ZoneGeometry.CellOf(x);
            var cellY = ZoneGeometry.CellOf(y);
            if (cellX == lastCellX && cellY == lastCellY) continue;
            if (!geometry.ContainsCell(cellX, cellY) || geometry.IsBlocked(cellX, cellY)) return false;
            if (cellX != lastCellX && cellY != lastCellY
                && (geometry.IsBlocked(cellX, lastCellY) || geometry.IsBlocked(lastCellX, cellY)))
            {
                return false;
            }
            lastCellX = cellX;
            lastCellY = cellY;
        }
        return true;
    }

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

    private static WanderRoute? Route(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY, bool keepsPoint, bool nearestFree)
    {
        if (Plan(geometry, fromX, fromY, toX, toY, keepsPoint, nearestFree) is not { } plan) return null;

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

    private static RoutePlan? Plan(ZoneGeometry geometry, int fromX, int fromY, int toX, int toY, bool keepsPoint, bool nearestFree)
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
        List<(int X, int Y)>? cells = null;
        var goalReachable = goalIsFree;
        if (goalIsFree)
        {
            cells = Search(geometry, startX, startY, goalX, goalY);
        }
        else
        {
            var blockedX = goalX;
            var blockedY = goalY;
            goalReachable = TryReplaceDestination(geometry, ref goalX, ref goalY);
            if (goalReachable)
            {
                cells = Search(geometry, startX, startY, goalX, goalY);
            }
            if (cells is null && nearestFree)
            {
                foreach (var (candidateX, candidateY) in NearestFreeCells(geometry, startX, startY, blockedX, blockedY))
                {
                    if (candidateX == startX && candidateY == startY) return null;
                    cells = Search(geometry, startX, startY, candidateX, candidateY);
                    if (cells is null) continue;
                    goalX = candidateX;
                    goalY = candidateY;
                    break;
                }
            }
        }
        if (cells is not null && cells.Count - 1 > MaxWaypoints) return null;
        if (cells is null && goalReachable)
        {
            var greedy = Greedy(geometry, startX, startY, ZoneGeometry.CellOf(toX), ZoneGeometry.CellOf(toY));
            if (greedy is null || greedy.Count - 1 > MaxWaypoints) return null;
            var last = greedy[^1];
            var reachedGoal = last.X == ZoneGeometry.CellOf(toX) && last.Y == ZoneGeometry.CellOf(toY);
            return new RoutePlan(
                greedy,
                reachedGoal && keepsPoint && goalIsFree ? toX : ZoneGeometry.CentreOf(last.X),
                reachedGoal && keepsPoint && goalIsFree ? toY : ZoneGeometry.CentreOf(last.Y));
        }
        if (cells is null) return null;

        var destinationX = keepsPoint && goalIsFree ? toX : ZoneGeometry.CentreOf(goalX);
        var destinationY = keepsPoint && goalIsFree ? toY : ZoneGeometry.CentreOf(goalY);
        return new RoutePlan(cells, destinationX, destinationY);
    }

    private static bool Closed(ZoneGeometry geometry, int cellX, int cellY) =>
        !geometry.ContainsCell(cellX, cellY) || geometry.IsBlocked(cellX, cellY);

    private static List<(int X, int Y)>? Greedy(ZoneGeometry geometry, int startX, int startY, int goalX, int goalY)
    {
        if (!geometry.ContainsCell(goalX, goalY) || geometry.IsBlocked(goalX, goalY)) return null;
        var cells = new List<(int X, int Y)> { (startX, startY) };
        var current = (X: startX, Y: startY);
        var previous = current;
        while (current != (goalX, goalY))
        {
            var deltaX = goalX - current.X;
            var deltaY = goalY - current.Y;
            var signX = Math.Sign(deltaX);
            var signY = Math.Sign(deltaY);
            var candidate = (X: current.X + signX, Y: current.Y + signY);
            if (Closed(geometry, candidate.X, candidate.Y))
            {
                candidate = deltaX == 0 ? (current.X - 1, candidate.Y)
                    : deltaY == 0 ? (candidate.X, current.Y - 1)
                    : (current.X, candidate.Y);
                if (Closed(geometry, candidate.X, candidate.Y))
                {
                    candidate = deltaX == 0 ? (current.X + 1, current.Y + signY)
                        : deltaY == 0 ? (current.X + signX, current.Y + 1)
                        : (current.X + signX, current.Y);
                    if (Closed(geometry, candidate.X, candidate.Y)) return cells.Count < 2 ? null : cells;
                }
            }
            if (candidate == previous) return cells.Count < 2 ? null : cells;
            cells.Add(candidate);
            previous = current;
            current = candidate;
            if (cells.Count > MaxWaypoints + 1) return null;
        }
        return cells;
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

    private static IEnumerable<(int X, int Y)> NearestFreeCells(ZoneGeometry geometry, int startX, int startY, int blockedX, int blockedY)
    {
        for (var radius = 1; radius <= FreeDestinationRadius; radius++)
        {
            var ring = new List<(int X, int Y)>();
            for (var offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (var offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != radius) continue;
                    var cellX = blockedX + offsetX;
                    var cellY = blockedY + offsetY;
                    if (geometry.ContainsCell(cellX, cellY) && geometry.IsFreeDestination(cellX, cellY)) ring.Add((cellX, cellY));
                }
            }
            if (ring.Count == 0) continue;
            ring.Sort((a, b) => SquaredCellDistance(a, startX, startY).CompareTo(SquaredCellDistance(b, startX, startY)));
            foreach (var cell in ring.Take(FreeDestinationCandidates)) yield return cell;
            yield break;
        }
    }

    private static long SquaredCellDistance((int X, int Y) cell, int fromX, int fromY)
    {
        long dx = cell.X - fromX;
        long dy = cell.Y - fromY;
        return dx * dx + dy * dy;
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
