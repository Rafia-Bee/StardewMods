using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace LivestockFollowsYou.Framework;

/// <summary>Manages grazing behavior for animals on walks. Detects player idle state,
/// finds nearby grass, steers animals to eat it, and applies mood boosts.</summary>
internal class GrazingManager
{
    private readonly IMonitor Monitor;
    private readonly IModHelper Helper;
    private readonly Func<ModConfig> GetConfig;

    private Vector2 lastPlayerPosition;
    private int idleTicks;
    private bool wasIdle;

    private const int GrazeMoveSpeed = 2;
    private const int EatingDurationTicks = 60;
    private const int GrassScanRadius = 5;

    /// <summary>Per-animal tick counter for the eating animation pause.</summary>
    private readonly Dictionary<long, int> eatingTimers = new();

    public GrazingManager(IMonitor monitor, IModHelper helper, Func<ModConfig> getConfig)
    {
        Monitor = monitor;
        Helper = helper;
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

            var grassTile = FindNearestGrass(follow.Animal.Position, player.Position, location, config);
            if (grassTile.HasValue)
            {
                follow.GrazeTarget = grassTile.Value;
                follow.State = FollowState.Grazing;
                eatingTimers.Remove(follow.Animal.myID.Value);
            }
        }
    }

    public void Reset()
    {
        idleTicks = 0;
        wasIdle = false;
        eatingTimers.Clear();
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

        Vector2 targetWorldPos = target.Value * 64f + new Vector2(32f, 32f);
        float distance = Vector2.Distance(animal.Position, targetWorldPos);

        if (distance > 48f)
        {
            SteerToward(animal, targetWorldPos);
            return;
        }

        long animalId = animal.myID.Value;
        if (!eatingTimers.ContainsKey(animalId))
        {
            animal.Halt();
            animal.FacingDirection = 2;
            eatingTimers[animalId] = 0;
            return;
        }

        eatingTimers[animalId]++;

        if (eatingTimers[animalId] >= EatingDurationTicks)
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

    private void FinishGrazing(FollowingAnimal follow)
    {
        follow.GrazeTarget = null;
        follow.State = FollowState.FollowingPlayer;
        eatingTimers.Remove(follow.Animal.myID.Value);
    }

    private void CancelAllGrazing(IReadOnlyList<FollowingAnimal> followers)
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
                eatingTimers.Remove(follow.Animal.myID.Value);
            }

            follow.HasGrazedThisStop = false;
        }
    }

    private Vector2? FindNearestGrass(Vector2 animalPosition, Vector2 playerPosition, GameLocation location, ModConfig config)
    {
        Vector2 animalTile = new Vector2(
            (int)(animalPosition.X / 64f),
            (int)(animalPosition.Y / 64f)
        );

        Vector2 playerTile = new Vector2(
            (int)(playerPosition.X / 64f),
            (int)(playerPosition.Y / 64f)
        );

        float maxDistFromPlayer = config.RubberBandDistance - 2;

        Vector2? nearest = null;
        float nearestDist = float.MaxValue;

        for (int dx = -GrassScanRadius; dx <= GrassScanRadius; dx++)
        {
            for (int dy = -GrassScanRadius; dy <= GrassScanRadius; dy++)
            {
                var checkTile = new Vector2(animalTile.X + dx, animalTile.Y + dy);

                if (Vector2.Distance(playerTile, checkTile) > maxDistFromPlayer)
                    continue;

                if (location.terrainFeatures.TryGetValue(checkTile, out var feature) && feature is Grass)
                {
                    float dist = Vector2.DistanceSquared(animalTile, checkTile);
                    if (dist < nearestDist && IsPathWalkable(animalTile, checkTile, location))
                    {
                        nearestDist = dist;
                        nearest = checkTile;
                    }
                }
            }
        }

        return nearest;
    }

    /// <summary>Checks that tiles along a straight line from start to end are passable (no water, cliffs, etc.).</summary>
    private static bool IsPathWalkable(Vector2 startTile, Vector2 endTile, GameLocation location)
    {
        int steps = (int)Math.Max(Math.Abs(endTile.X - startTile.X), Math.Abs(endTile.Y - startTile.Y));
        if (steps == 0)
            return true;

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            int tileX = (int)Math.Round(startTile.X + (endTile.X - startTile.X) * t);
            int tileY = (int)Math.Round(startTile.Y + (endTile.Y - startTile.Y) * t);

            if (location.isWaterTile(tileX, tileY))
                return false;

            if (!location.isTilePassable(new xTile.Dimensions.Location(tileX * 64, tileY * 64), Game1.viewport))
                return false;
        }

        return true;
    }

    private static void SteerToward(FarmAnimal animal, Vector2 target)
    {
        Vector2 diff = target - animal.Position;

        int dir;
        if (Math.Abs(diff.X) > Math.Abs(diff.Y))
            dir = diff.X > 0 ? 1 : 3;
        else
            dir = diff.Y > 0 ? 2 : 0;

        if (animal.FacingDirection != dir || !animal.isMoving())
        {
            animal.Halt();
            switch (dir)
            {
                case 0: animal.SetMovingUp(true); break;
                case 1: animal.SetMovingRight(true); break;
                case 2: animal.SetMovingDown(true); break;
                case 3: animal.SetMovingLeft(true); break;
            }
        }

        animal.speed = GrazeMoveSpeed;
    }
}
