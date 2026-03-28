using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;

namespace LivestockFollowsYou.Framework;

/// <summary>Tracks the state of one animal being walked home.</summary>
internal class FollowingAnimal
{
    public FarmAnimal Animal { get; }
    public FollowState State { get; set; }
    public float SoundTimer { get; set; }
    public Vector2 LastPosition { get; set; }
    public int StuckTicks { get; set; }
    public int DirectionCooldown { get; set; }

    public FollowingAnimal(FarmAnimal animal)
    {
        Animal = animal;
        State = FollowState.PendingSpawn;
    }
}

internal enum FollowState
{
    /// <summary>Purchased but not yet visible in the world.</summary>
    PendingSpawn,

    /// <summary>Walking behind the player.</summary>
    FollowingPlayer,

    /// <summary>On the farm, heading to the barn/coop door.</summary>
    HeadingToBarn
}

/// <summary>Core logic for animals following the player after purchase.</summary>
internal class AnimalFollowManager
{
    private readonly IMonitor Monitor;
    private readonly IModHelper Helper;
    private readonly Func<ModConfig> GetConfig;

    private readonly List<FollowingAnimal> following = new();

    public AnimalFollowManager(IMonitor monitor, IModHelper helper, Func<ModConfig> getConfig)
    {
        Monitor = monitor;
        Helper = helper;
        GetConfig = getConfig;
    }

    /// <summary>Whether the given animal is currently being walked home by this mod.</summary>
    public bool IsFollowing(FarmAnimal animal)
    {
        return following.Any(f => f.Animal == animal);
    }

    /// <summary>Whether there are any animals currently following/pending.</summary>
    public bool HasFollowers => following.Count > 0;

    /// <summary>Queue an animal for follow after purchase.</summary>
    public void StartFollowing(FarmAnimal animal)
    {
        // Remove from the building interior so it doesn't exist in two places
        var interior = animal.homeInterior;
        if (interior != null)
            interior.animals.Remove(animal.myID.Value);

        var follow = new FollowingAnimal(animal);
        following.Add(follow);
        if (GetConfig().DebugLogging)
            Monitor.Log($"Queued {animal.displayName} ({animal.type.Value}) for follow.", LogLevel.Debug);

        // If the player is already outdoors (e.g. buying at an outdoor stall),
        // spawn the animal immediately instead of waiting for a location change
        var player = Game1.player;
        if (player.currentLocation is { IsOutdoors: true } location)
        {
            int idx = following.IndexOf(follow);
            float xOffset = (idx % 2 == 0 ? -1 : 1) * ((idx / 2) + 1) * 64f;
            var spawnPos = player.Position + new Vector2(xOffset, 320);
            SpawnAnimalInLocation(animal, location, spawnPos);
            follow.State = FollowState.FollowingPlayer;
            follow.SoundTimer = GetConfig().SoundIntervalSeconds;

            if (GetConfig().ShowNotifications)
            {
                Game1.addHUDMessage(new HUDMessage(
                    Helper.Translation.Get("hud.following", new { name = animal.displayName })));
            }
        }
    }

    /// <summary>Called each tick before the game updates. Sets movement directions for following animals.</summary>
    public void UpdateMovement(GameTime time)
    {
        if (!Game1.IsMasterGame || !Game1.shouldTimePass())
            return;

        var config = GetConfig();
        var player = Game1.player;

        for (int i = following.Count - 1; i >= 0; i--)
        {
            var follow = following[i];
            var animal = follow.Animal;

            // Check if the animal was delivered by vanilla barn-door code
            if (animal.IsHome)
            {
                OnDelivered(follow, wasAutoDelivered: false);
                following.RemoveAt(i);
                continue;
            }

            // Auto-deliver at configured time
            if (Game1.timeOfDay >= config.AutoDeliverTime && follow.State != FollowState.PendingSpawn)
            {
                ForceDeliver(follow);
                following.RemoveAt(i);
                continue;
            }

            // Only move animals that are spawned
            if (follow.State == FollowState.PendingSpawn)
                continue;

            // HeadingToBarn animals idle in front of their building
            // until the player enters the building (auto-delivered on building entry)
            if (follow.State == FollowState.HeadingToBarn)
                continue;

            // Chain following: animal 0 follows the player,
            // each subsequent animal follows the one ahead of it
            Vector2 target;
            if (i == 0)
                target = player.Position;
            else
                target = following[i - 1].Animal.Position;

            SteerAnimal(follow, target, config, time);

            // Sound
            if (config.AnimalSoundsWhileFollowing)
            {
                follow.SoundTimer -= (float)time.ElapsedGameTime.TotalSeconds;
                if (follow.SoundTimer <= 0f)
                {
                    animal.makeSound();
                    follow.SoundTimer = config.SoundIntervalSeconds;
                }
            }
        }
    }

    /// <summary>Called when the player warps between locations. Transfers following animals.</summary>
    public void OnPlayerWarped(GameLocation oldLocation, GameLocation newLocation)
    {
        if (!Game1.IsMasterGame)
            return;

        var config = GetConfig();

        // If player enters a building interior, auto-deliver any animals destined for it
        if (newLocation is AnimalHouse enteredHouse)
        {
            for (int i = following.Count - 1; i >= 0; i--)
            {
                if (following[i].Animal.homeInterior == enteredHouse)
                {
                    ForceDeliver(following[i]);
                    following.RemoveAt(i);
                }
            }
            return;
        }

        foreach (var follow in following)
        {
            var animal = follow.Animal;

            if (follow.State == FollowState.PendingSpawn)
            {
                // Spawn in the first outdoor location after purchase
                if (!newLocation.IsOutdoors)
                    continue;

                // Spawn south of the player, offset each animal horizontally
                // so multiple purchases don't stack on top of each other
                int pendingIndex = following.IndexOf(follow);
                float xOffset = (pendingIndex % 2 == 0 ? -1 : 1) * ((pendingIndex / 2) + 1) * 64f;
                var spawnPos = Game1.player.Position + new Vector2(xOffset, 320);
                SpawnAnimalInLocation(animal, newLocation, spawnPos);
                follow.State = FollowState.FollowingPlayer;
                follow.SoundTimer = config.SoundIntervalSeconds;

                if (config.ShowNotifications)
                {
                    Game1.addHUDMessage(new HUDMessage(
                        Helper.Translation.Get("hud.following", new { name = animal.displayName })));
                }
                continue;
            }

            // Animals already parked at their building stay put when the player leaves
            if (follow.State == FollowState.HeadingToBarn)
                continue;

            // Transfer from old location to new
            oldLocation.animals.Remove(animal.myID.Value);

            // Switch to heading-to-barn if we arrived at the building's parent location
            if (animal.home != null && newLocation == animal.home.GetParentLocation())
            {
                follow.State = FollowState.HeadingToBarn;

                // Spawn spread out in front of the building door instead of at the player
                Building home = animal.home;
                Vector2 doorPos = new Vector2(
                    (home.tileX.Value + home.animalDoor.X) * 64f,
                    (home.tileY.Value + home.animalDoor.Y + 2) * 64f
                );
                int sameBuildingCount = 0;
                for (int j = 0; j < following.Count; j++)
                {
                    if (following[j] != follow && following[j].Animal.home == home
                        && following[j].State == FollowState.HeadingToBarn)
                        sameBuildingCount++;
                }
                float xSpread = (sameBuildingCount % 2 == 0 ? -1 : 1) * ((sameBuildingCount / 2) + 1) * 64f;
                SpawnAnimalInLocation(animal, newLocation, doorPos + new Vector2(xSpread, 0));
            }
            else
            {
                SpawnAnimalInLocation(animal, newLocation, Game1.player.Position);
            }
        }
    }

    /// <summary>Force-deliver all remaining animals (called on day end).</summary>
    public void DeliverAll()
    {
        foreach (var follow in following)
            ForceDeliver(follow, isDayEnd: true);
        following.Clear();
    }

    /// <summary>Clean up all state (called when returning to title).</summary>
    public void Reset()
    {
        following.Clear();
    }

    /// <summary>Clean up stray animals in non-home locations after loading a save.</summary>
    public void CleanupStrays()
    {
        foreach (var location in Game1.locations)
        {
            if (location is Farm)
                continue;

            // Don't touch building interiors (AnimalHouse)
            if (location.GetParentLocation() != null)
                continue;

            var strays = location.animals.Values
                .Where(a => a.homeInterior != null)
                .ToList();

            foreach (var animal in strays)
            {
                location.animals.Remove(animal.myID.Value);
                if (!animal.homeInterior.animals.ContainsKey(animal.myID.Value))
                {
                    animal.homeInterior.animals.Add(animal.myID.Value, animal);
                    animal.currentLocation = animal.homeInterior;
                    animal.setRandomPosition(animal.homeInterior);
                }
                if (GetConfig().DebugLogging)
                    Monitor.Log($"Cleaned up stray animal {animal.displayName} from {location.Name}.", LogLevel.Info);
            }
        }
    }

    private void SpawnAnimalInLocation(FarmAnimal animal, GameLocation location, Vector2 position)
    {
        animal.Position = position;
        animal.currentLocation = location;

        if (!location.animals.ContainsKey(animal.myID.Value))
            location.animals.Add(animal.myID.Value, animal);
    }

    private void SteerAnimal(FollowingAnimal follow, Vector2 target, ModConfig config, GameTime time)
    {
        var animal = follow.Animal;
        Vector2 diff = target - animal.Position;
        float tileDistance = diff.Length() / 64f;

        // Rubber-band: teleport closer if too far behind
        if (tileDistance > config.RubberBandDistance)
        {
            Vector2 direction = Vector2.Normalize(diff);
            animal.Position = target - direction * 3 * 64f;
            follow.StuckTicks = 0;
            return;
        }

        // Close enough: idle
        if (tileDistance < 1.5f)
        {
            animal.Halt();
            follow.StuckTicks = 0;
            return;
        }

        // Stuck detection: if position hasn't changed, teleport past the obstacle
        if (Vector2.Distance(animal.Position, follow.LastPosition) < 1f)
        {
            follow.StuckTicks++;
            if (follow.StuckTicks > 30)
            {
                // Teleport 2 tiles toward the target to skip the obstacle
                Vector2 direction = Vector2.Normalize(diff);
                animal.Position += direction * 128f;
                follow.StuckTicks = 0;
                follow.DirectionCooldown = 0;
                return;
            }
        }
        else
        {
            follow.StuckTicks = 0;
        }
        follow.LastPosition = animal.Position;

        // Tick down direction change cooldown
        if (follow.DirectionCooldown > 0)
            follow.DirectionCooldown--;

        // Determine desired facing direction
        int desiredDir;
        if (Math.Abs(diff.X) > Math.Abs(diff.Y))
            desiredDir = diff.X > 0 ? 1 : 3;
        else
            desiredDir = diff.Y > 0 ? 2 : 0;

        // When stuck, freeze facing direction to prevent rapid left-right flipping
        if (follow.StuckTicks > 0 && animal.isMoving())
            desiredDir = animal.FacingDirection;

        // Direction debounce: hold current direction during cooldown
        if (follow.DirectionCooldown > 0 && desiredDir != animal.FacingDirection)
            desiredDir = animal.FacingDirection;

        // Only call Halt() + SetMoving when direction changes to preserve walk animation
        if (animal.FacingDirection != desiredDir || !animal.isMoving())
        {
            animal.Halt();
            switch (desiredDir)
            {
                case 0: animal.SetMovingUp(true); break;
                case 1: animal.SetMovingRight(true); break;
                case 2: animal.SetMovingDown(true); break;
                case 3: animal.SetMovingLeft(true); break;
            }
            follow.DirectionCooldown = 15;
        }

        // Gentle speed boost when falling behind
        float speedBoost = Math.Max(0, (tileDistance - 3f) * 0.3f * config.FollowSpeedMultiplier);
        animal.addedSpeed = Math.Min(speedBoost, 2f);
    }

    private void ForceDeliver(FollowingAnimal follow, bool isDayEnd = false)
    {
        var animal = follow.Animal;

        // Remove from whichever location it's in
        animal.currentLocation?.animals.Remove(animal.myID.Value);

        // Place in home building
        var interior = animal.homeInterior;
        if (interior != null && !interior.animals.ContainsKey(animal.myID.Value))
        {
            interior.animals.Add(animal.myID.Value, animal);
            animal.currentLocation = interior;
            animal.setRandomPosition(interior);
        }

        if (GetConfig().ShowNotifications)
        {
            string key = isDayEnd ? "hud.auto_delivered" : "hud.delivered";
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get(key, new { name = animal.displayName })));
        }

        if (GetConfig().DebugLogging)
            Monitor.Log($"Delivered {animal.displayName} to {interior?.Name ?? "unknown"}.", LogLevel.Debug);
    }

    private void OnDelivered(FollowingAnimal follow, bool wasAutoDelivered)
    {
        var animal = follow.Animal;

        // Reposition to a valid tile inside the building so the animal
        // doesn't end up in the black zone near the door warp
        var interior = animal.homeInterior;
        if (interior != null)
            animal.setRandomPosition(interior);

        if (GetConfig().ShowNotifications)
        {
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.delivered", new { name = animal.displayName })));
        }

        if (GetConfig().DebugLogging)
            Monitor.Log($"{animal.displayName} entered barn via door.", LogLevel.Debug);
    }
}
