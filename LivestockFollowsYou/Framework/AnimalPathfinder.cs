using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace LivestockFollowsYou.Framework;

/// <summary>Tile-based BFS pathfinder for following animals. Returns a queue of adjacent waypoints from start (exclusive) to goal (inclusive).</summary>
internal static class AnimalPathfinder
{
    private const int MaxNodes = 1500;

    public static Queue<Point> FindPath(GameLocation location, FarmAnimal animal, Point start, Point goal)
    {
        if (location == null)
            return null;
        if (start == goal)
            return new Queue<Point>();

        var cameFrom = new Dictionary<Point, Point>();
        var queue = new Queue<Point>();
        var visited = new HashSet<Point> { start };
        queue.Enqueue(start);

        int expanded = 0;
        while (queue.Count > 0 && expanded < MaxNodes)
        {
            Point current = queue.Dequeue();
            expanded++;

            foreach (Point next in GetNeighbors(current))
            {
                if (!visited.Add(next))
                    continue;

                // Always accept the goal tile even if it has the player on it.
                bool isGoal = next == goal;
                if (!isGoal && !IsPassable(location, animal, next))
                    continue;

                cameFrom[next] = current;
                if (isGoal)
                    return Reconstruct(cameFrom, start, goal);

                queue.Enqueue(next);
            }
        }

        return null;
    }

    /// <summary>True when the straight tile line from start to goal has no blockers, so BFS can be skipped.</summary>
    public static bool HasLineOfSight(GameLocation location, FarmAnimal animal, Point start, Point goal)
    {
        if (location == null)
            return false;
        if (start == goal)
            return true;

        int dx = goal.X - start.X;
        int dy = goal.Y - start.Y;

        // Only allow straight orthogonal lines.
        if (dx != 0 && dy != 0)
            return false;

        int stepX = System.Math.Sign(dx);
        int stepY = System.Math.Sign(dy);
        int steps = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy));

        for (int i = 1; i <= steps; i++)
        {
            Point t = new(start.X + stepX * i, start.Y + stepY * i);
            if (t == goal)
                return true;
            if (!IsPassable(location, animal, t))
                return false;
        }
        return true;
    }

    private static IEnumerable<Point> GetNeighbors(Point p)
    {
        yield return new Point(p.X, p.Y - 1);
        yield return new Point(p.X + 1, p.Y);
        yield return new Point(p.X, p.Y + 1);
        yield return new Point(p.X - 1, p.Y);
    }

    private static bool IsPassable(GameLocation location, FarmAnimal animal, Point tile)
    {
        Vector2 v = new(tile.X, tile.Y);

        if (!location.isTileOnMap(v))
            return false;

        if (!location.isTilePassable(v))
            return false;

        if (location.objects.TryGetValue(v, out var obj) && obj != null && !obj.isPassable())
            return false;

        if (location.terrainFeatures.TryGetValue(v, out var tf) && tf != null && !tf.isPassable())
            return false;

        if (location.getBuildingAt(v) != null)
            return false;

        if (location.getLargeTerrainFeatureAt(tile.X, tile.Y) != null)
            return false;

        return true;
    }

    private static Queue<Point> Reconstruct(Dictionary<Point, Point> cameFrom, Point start, Point goal)
    {
        var stack = new Stack<Point>();
        Point cur = goal;
        while (cur != start)
        {
            stack.Push(cur);
            if (!cameFrom.TryGetValue(cur, out var prev))
                return null;
            cur = prev;
        }

        var path = new Queue<Point>(stack.Count);
        while (stack.Count > 0)
            path.Enqueue(stack.Pop());
        return path;
    }
}
