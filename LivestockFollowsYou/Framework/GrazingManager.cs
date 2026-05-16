using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace LivestockFollowsYou.Framework;

/// <summary>Manages grazing behavior for animals on walks. Detects player idle state,
/// finds nearby grass, steers animals to eat it, and applies mood boosts.</summary>
internal class GrazingManager
{
    private readonly Func<ModConfig> GetConfig;

    private Vector2 lastPlayerPosition;
    private int idleTicks;
    private bool wasIdle;

    private const int GrazeMoveSpeed = 2;
    private const float GrazeArrivalPixels = 48f;
    private const int EatingDurationTicks = 60;
    private const int GrassScanRadius = 5;

    public GrazingManager(Func<ModConfig> getConfig)
    {
        GetConfig = getConfig;
    }

    /// <summary>Called each tick. Handles idle detection and grazing state for walk animals.</summary>
    public void Update(IReadOnlyList<FollowingAnimal> followers)
    {
        var config = GetConfig();
        var player = Game1.player;

        bool playerMoved = Vector2.Distance(player.Position, lastPlayerPosition) > 2f;
        lastPlayerPosition = player.Position;

        if (playerMoved)
        {
            idleTicks = 0;

            if (wasIdle)
            {
                wasIdle = false;
                CancelAllGrazing(followers);
            }
            return;
        }

        idleTicks++;
        int requiredIdleTicks = config.GrazingIdleSeconds * 60;

        if (idleTicks < requiredIdleTicks)
            return;

        wasIdle = true;
        var location = player.currentLocation;
        if (location == null)
            return;

        for (int i = 0; i < followers.Count; i++)
        {
            var follow = followers[i];

            if (!follow.IsWalk || follow.HasGrazedThisStop)
                continue;

            if (follow.State == FollowState.Grazing)
            {
                UpdateGrazingAnimal(follow, location);
                continue;
            }

            if (follow.State != FollowState.FollowingPlayer)
                continue;

            if (idleTicks != requiredIdleTicks + i * 10)
                continue;

            var grassTile = FindNearestGrass(follow.Animal, player.Position, location, config);
            if (grassTile.HasValue)
            {
                follow.GrazeTarget = grassTile.Value;
                follow.State = FollowState.Grazing;
                follow.Path = null;
                follow.PathTarget = null;
                follow.FramesSinceProgress = 0;
                follow.ConsecutivePathFailures = 0;
                follow.EatingFrames = -1;
            }
        }
    }

    public void Reset()
    {
        idleTicks = 0;
        wasIdle = false;
    }

    private void UpdateGrazingAnimal(FollowingAnimal follow, GameLocation location)
    {
        var animal = follow.Animal;
        var target = follow.GrazeTarget;

        if (!target.HasValue)
        {
            FinishGrazing(follow);
            return;
        }

        Point goalTile = new((int)target.Value.X, (int)target.Value.Y);
        var result = AnimalSteering.SteerAlongPath(
            follow, goalTile, location, GrazeMoveSpeed, GrazeArrivalPixels);

        if (result == SteerResult.Stuck)
        {
            // Grass is unreachable (locked behind a fence, blocked by another animal, etc).
            // Give up on this grass and let the next idle tick pick a new target if any.
            FinishGrazing(follow);
            return;
        }

        if (result != SteerResult.Arrived)
            return;

        if (follow.EatingFrames < 0)
        {
            animal.Halt();
            animal.FacingDirection = 2;
            follow.EatingFrames = 0;
            return;
        }

        follow.EatingFrames++;

        if (follow.EatingFrames >= EatingDurationTicks)
        {
            ConsumeGrass(follow, location, target.Value);
            FinishGrazing(follow);
        }
    }

    private void ConsumeGrass(FollowingAnimal follow, GameLocation location, Vector2 tile)
    {
        var config = GetConfig();
        var animal = follow.Animal;

        if (location.terrainFeatures.TryGetValue(tile, out var feature) && feature is Grass grass)
        {
            Random r = Game1.random;
            if (r.NextDouble() < 0.5)
            {
                var hay = ItemRegistry.Create("(O)178");
                if (!Game1.player.addItemToInventoryBool(hay))
                {
                    Game1.getFarm()?.tryToAddHay(1);
                }
            }

            location.terrainFeatures.Remove(tile);

            animal.happiness.Value = (byte)Math.Min(255, animal.happiness.Value + config.GrazingHappinessBoost);
        }

        follow.HasGrazedThisStop = true;
    }

    private static void FinishGrazing(FollowingAnimal follow)
    {
        follow.GrazeTarget = null;
        follow.State = FollowState.FollowingPlayer;
        follow.EatingFrames = -1;
    }

    private static void CancelAllGrazing(IReadOnlyList<FollowingAnimal> followers)
    {
        for (int i = 0; i < followers.Count; i++)
        {
            var follow = followers[i];
            if (!follow.IsWalk)
                continue;

            if (follow.State == FollowState.Grazing)
            {
                follow.State = FollowState.FollowingPlayer;
                follow.GrazeTarget = null;
                follow.EatingFrames = -1;
            }

            follow.HasGrazedThisStop = false;
        }
    }

    private static Vector2? FindNearestGrass(FarmAnimal animal, Vector2 playerPosition, GameLocation location, ModConfig config)
    {
        Point animalTile = animal.TilePoint;
        Point playerTile = new((int)(playerPosition.X / 64f), (int)(playerPosition.Y / 64f));

        float maxDistFromPlayer = config.RubberBandDistance - 2;

        Vector2? nearest = null;
        float nearestDist = float.MaxValue;

        for (int dx = -GrassScanRadius; dx <= GrassScanRadius; dx++)
        {
            for (int dy = -GrassScanRadius; dy <= GrassScanRadius; dy++)
            {
                Point checkTile = new(animalTile.X + dx, animalTile.Y + dy);
                Vector2 checkVec = new(checkTile.X, checkTile.Y);

                if (Vector2.Distance(new Vector2(playerTile.X, playerTile.Y), checkVec) > maxDistFromPlayer)
                    continue;

                if (!location.terrainFeatures.TryGetValue(checkVec, out var feature) || feature is not Grass)
                    continue;

                float dist = Vector2.DistanceSquared(new Vector2(animalTile.X, animalTile.Y), checkVec);
                if (dist >= nearestDist)
                    continue;

                // Reachability: prefer a straight orthogonal line if there is one, otherwise BFS.
                bool reachable = AnimalPathfinder.HasLineOfSight(location, animal, animalTile, checkTile)
                    || AnimalPathfinder.FindPath(location, animal, animalTile, checkTile) != null;
                if (!reachable)
                    continue;

                nearestDist = dist;
                nearest = checkVec;
            }
        }

        return nearest;
    }
}
