using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace LivestockFollowsYou.Framework;

internal enum SteerResult
{
    /// <summary>Animal is walking along the path (or paused at a temporarily blocked step).</summary>
    Moving,
    /// <summary>Animal is within the arrival distance of the goal.</summary>
    Arrived,
    /// <summary>The path has failed enough consecutive rebuilds that the caller should give up or fall back.</summary>
    Stuck
}

/// <summary>Shared per-tick steering for any following animal walking toward a tile goal.
/// Builds and follows a BFS path, pre-checks collision so vanilla's bounce-flip can't fire,
/// and bans repeatedly-blocked waypoints via the animal's AvoidTiles set.</summary>
internal static class AnimalSteering
{
    private const int PathRebuildInterval = 30;
    private const int MaxStallFrames = 20;
    private const int MaxConsecutivePathFailures = 3;
    private const int AvoidExpiryFrames = 180;

    public static SteerResult SteerAlongPath(
        FollowingAnimal follow,
        Point goal,
        GameLocation location,
        int speed,
        float arrivalPixels)
    {
        var animal = follow.Animal;
        animal.speed = speed;

        Vector2 goalCenter = new(goal.X * 64f + 32f, goal.Y * 64f + 32f);
        float distance = Vector2.Distance(animal.Position, goalCenter);

        if (distance < arrivalPixels)
        {
            animal.Halt();
            follow.Path = null;
            follow.PathTarget = null;
            follow.FramesSinceProgress = 0;
            follow.ConsecutivePathFailures = 0;
            return SteerResult.Arrived;
        }

        if (follow.AvoidTiles.Count > 0)
        {
            follow.AvoidExpiresFrames--;
            if (follow.AvoidExpiresFrames <= 0)
                follow.AvoidTiles.Clear();
        }

        Point animalTile = animal.TilePoint;

        bool needRebuild =
            follow.Path == null
            || follow.Path.Count == 0
            || follow.PathTarget != goal
            || follow.FramesSincePathBuild >= PathRebuildInterval;

        if (needRebuild)
        {
            Queue<Point> path = AnimalPathfinder.HasLineOfSight(location, animal, animalTile, goal, follow.AvoidTiles)
                ? BuildStraightPath(animalTile, goal)
                : AnimalPathfinder.FindPath(location, animal, animalTile, goal, follow.AvoidTiles);

            if (path != null && path.Count > 0)
            {
                follow.Path = path;
                follow.PathTarget = goal;
            }
            else
            {
                follow.Path = null;
                follow.PathTarget = null;
                follow.ConsecutivePathFailures++;
            }

            follow.FramesSincePathBuild = 0;
        }
        else
        {
            follow.FramesSincePathBuild++;
        }

        if (follow.ConsecutivePathFailures >= MaxConsecutivePathFailures)
            return SteerResult.Stuck;

        if (follow.Path == null || follow.Path.Count == 0)
        {
            animal.Halt();
            return SteerResult.Moving;
        }

        while (follow.Path.Count > 0 && follow.Path.Peek() == animalTile)
            follow.Path.Dequeue();

        if (follow.Path.Count == 0)
        {
            animal.Halt();
            return SteerResult.Moving;
        }

        Point waypoint = follow.Path.Peek();
        int desiredDir = GetDirToWaypoint(animalTile, waypoint, animal.Position);

        // Pre-check what vanilla MovePosition would do. Setting a movement flag toward a
        // blocked tile triggers vanilla's 60% chance to flip us to the opposite direction
        // (see FarmAnimal.MovePosition), which produces a visible walk-in-place glitch.
        var nextRect = animal.nextPosition(desiredDir);
        bool blocked = location.isCollidingPosition(
            nextRect, Game1.viewport,
            isFarmer: false, damagesFarmer: 0, glider: false, animal, pathfinding: false);

        if (blocked)
        {
            animal.Halt();
            if (animal.FacingDirection != desiredDir)
                animal.faceDirection(desiredDir);
        }
        else if (animal.FacingDirection != desiredDir || !animal.isMoving())
        {
            animal.Halt();
            SetMovingDirection(animal, desiredDir);
        }

        if (Vector2.Distance(animal.Position, follow.LastPosition) < 0.5f)
        {
            follow.FramesSinceProgress++;
        }
        else
        {
            follow.FramesSinceProgress = 0;
            follow.LastPosition = animal.Position;
            follow.ConsecutivePathFailures = 0;
        }

        if (follow.FramesSinceProgress > MaxStallFrames)
        {
            if (follow.Path != null && follow.Path.Count > 0)
                follow.AvoidTiles.Add(follow.Path.Peek());
            follow.AvoidExpiresFrames = AvoidExpiryFrames;

            follow.Path = null;
            follow.PathTarget = null;
            follow.FramesSinceProgress = 0;
            follow.FramesSincePathBuild = PathRebuildInterval;
            follow.ConsecutivePathFailures++;
        }

        return SteerResult.Moving;
    }

    private static Queue<Point> BuildStraightPath(Point start, Point goal)
    {
        var q = new Queue<Point>();
        int dx = System.Math.Sign(goal.X - start.X);
        int dy = System.Math.Sign(goal.Y - start.Y);
        Point cur = start;
        while (cur != goal)
        {
            cur = new Point(cur.X + dx, cur.Y + dy);
            q.Enqueue(cur);
        }
        return q;
    }

    private static int GetDirToWaypoint(Point from, Point to, Vector2 animalPos)
    {
        int dx = to.X - from.X;
        int dy = to.Y - from.Y;

        if (dx != 0 && dy == 0)
            return dx > 0 ? 1 : 3;
        if (dy != 0 && dx == 0)
            return dy > 0 ? 2 : 0;

        Vector2 wpPixel = new(to.X * 64f + 32f, to.Y * 64f + 32f);
        Vector2 diff = wpPixel - animalPos;
        if (System.Math.Abs(diff.X) > System.Math.Abs(diff.Y))
            return diff.X > 0 ? 1 : 3;
        return diff.Y > 0 ? 2 : 0;
    }

    private static void SetMovingDirection(FarmAnimal animal, int dir)
    {
        switch (dir)
        {
            case 0: animal.SetMovingUp(true); break;
            case 1: animal.SetMovingRight(true); break;
            case 2: animal.SetMovingDown(true); break;
            case 3: animal.SetMovingLeft(true); break;
        }
    }
}
