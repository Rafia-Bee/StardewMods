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

    /// <summary>Current path of tiles from the animal's tile to the goal (goal included, start excluded).</summary>
    public Queue<Point> Path { get; set; }

    /// <summary>The tile we last pathed toward, so we can detect when the player moved enough to need a rebuild.</summary>
    public Point? PathTarget { get; set; }

    /// <summary>Frames since the path was last rebuilt; used to throttle rebuilds.</summary>
    public int FramesSincePathBuild { get; set; }

    /// <summary>Frames the animal's pixel position hasn't meaningfully changed while trying to follow.</summary>
    public int FramesSinceProgress { get; set; }

    /// <summary>Consecutive path rebuilds that didn't produce a path.</summary>
    public int ConsecutivePathFailures { get; set; }

    /// <summary>Tiles BFS is told to avoid because trying to step into them just stalled the animal.</summary>
    public HashSet<Point> AvoidTiles { get; } = new();

    /// <summary>Frames left before the avoid set is cleared so the animal can try those tiles again.</summary>
    public int AvoidExpiresFrames { get; set; }

    /// <summary>True if this animal is on a voluntary walk (Grazing Bell), false if purchase escort.</summary>
    public bool IsWalk { get; set; }

    /// <summary>Whether this animal has already eaten grass during the current idle stop.</summary>
    public bool HasGrazedThisStop { get; set; }

    /// <summary>Target grass tile the animal is walking toward (null when not grazing).</summary>
    public Vector2? GrazeTarget { get; set; }

    /// <summary>Current idle roaming activity when the player is standing still.</summary>
    public IdleActivity CurrentIdleActivity { get; set; }

    /// <summary>Ticks remaining for the current idle activity.</summary>
    public int IdleActivityTicksLeft { get; set; }

    public FollowingAnimal(FarmAnimal animal)
    {
        Animal = animal;
        State = FollowState.PendingSpawn;
    }
}

internal enum IdleActivity
{
    Walking,
    Pausing,
    Sitting,
    Eating
}

internal enum FollowState
{
    /// <summary>Purchased but not yet visible in the world.</summary>
    PendingSpawn,

    /// <summary>Walking behind the player.</summary>
    FollowingPlayer,

    /// <summary>On the farm, heading to the barn/coop door.</summary>
    HeadingToBarn,

    /// <summary>Animal is walking toward or eating a grass tile.</summary>
    Grazing
}

/// <summary>Core logic for animals following the player after purchase.</summary>
internal class AnimalFollowManager
{
    private readonly IMonitor Monitor;
    private readonly IModHelper Helper;
    private readonly Func<ModConfig> GetConfig;

    private readonly List<FollowingAnimal> following = new();

    private Vector2 lastPlayerPos;
    private int playerIdleTicks;

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

    /// <summary>Current list of following animals (read-only view for other managers).</summary>
    internal IReadOnlyList<FollowingAnimal> Followers => following;

    /// <summary>Queue an animal for follow after purchase.</summary>
    public void StartFollowing(FarmAnimal animal)
    {
        var interior = animal.homeInterior;
        if (interior != null)
            interior.animals.Remove(animal.myID.Value);

        var follow = new FollowingAnimal(animal);
        following.Add(follow);
        if (GetConfig().DebugLogging)
            Monitor.Log($"Queued {animal.displayName} ({animal.type.Value}) for follow.", LogLevel.Debug);

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

    /// <summary>Start a voluntary walk with an animal using the Grazing Bell.</summary>
    public bool StartWalk(FarmAnimal animal)
    {
        if (IsFollowing(animal))
            return false;

        if (animal.isBaby())
            return false;

        if (animal.ownerID.Value != Game1.player.UniqueMultiplayerID
            && animal.ownerID.Value != Game1.MasterPlayer.UniqueMultiplayerID)
            return false;

        if (Game1.timeOfDay >= GetConfig().AutoDeliverTime)
            return false;

        var interior = animal.homeInterior;
        if (interior != null)
            interior.animals.Remove(animal.myID.Value);
        animal.currentLocation?.animals.Remove(animal.myID.Value);

        var follow = new FollowingAnimal(animal) { IsWalk = true };
        var player = Game1.player;
        var location = player.currentLocation;

        if (location is { IsOutdoors: true })
        {
            int idx = following.Count;
            float xOffset = (idx % 2 == 0 ? -1 : 1) * ((idx / 2) + 1) * 64f;
            var spawnPos = player.Position + new Vector2(xOffset, 128);
            SpawnAnimalInLocation(animal, location, spawnPos);
            follow.State = FollowState.FollowingPlayer;
            follow.SoundTimer = GetConfig().SoundIntervalSeconds;
        }
        else
        {
            follow.State = FollowState.PendingSpawn;
        }

        following.Add(follow);

        if (GetConfig().DebugLogging)
            Monitor.Log($"Started walk with {animal.displayName}.", LogLevel.Debug);

        return true;
    }

    /// <summary>Attempt to send a walking animal home alone (requires friendship threshold).</summary>
    public SendHomeResult TrySendHome(FarmAnimal animal)
    {
        var follow = following.FirstOrDefault(f => f.Animal == animal);
        if (follow == null || !follow.IsWalk)
            return SendHomeResult.NotOnWalk;

        var config = GetConfig();
        if (animal.friendshipTowardFarmer.Value < config.MinFriendshipToSendHome)
            return SendHomeResult.InsufficientFriendship;

        ForceDeliver(follow, isWalkEnd: true);
        following.Remove(follow);
        return SendHomeResult.Success;
    }

    /// <summary>Whether any walk animals are currently following.</summary>
    public bool HasWalkAnimals => following.Any(f => f.IsWalk);

    /// <summary>Called each tick before the game updates. Sets movement directions for following animals.</summary>
    public void UpdateMovement(GameTime time)
    {
        if (!Game1.shouldTimePass())
            return;

        var config = GetConfig();
        var player = Game1.player;

        bool playerMoved = Vector2.Distance(player.Position, lastPlayerPos) > 2f;
        lastPlayerPos = player.Position;

        if (playerMoved)
            playerIdleTicks = 0;
        else
            playerIdleTicks++;

        // ~1 second (60 ticks)
        bool playerIdle = playerIdleTicks > 60;

        for (int i = following.Count - 1; i >= 0; i--)
        {
            var follow = following[i];
            var animal = follow.Animal;

            if (!follow.IsWalk && animal.IsHome)
            {
                OnDelivered(follow, wasAutoDelivered: false);
                following.RemoveAt(i);
                continue;
            }

            if (Game1.timeOfDay >= config.AutoDeliverTime && follow.State != FollowState.PendingSpawn)
            {
                ForceDeliver(follow);
                following.RemoveAt(i);
                continue;
            }

            if (follow.State == FollowState.PendingSpawn)
                continue;

            if (follow.State == FollowState.HeadingToBarn)
                continue;

            if (follow.State == FollowState.Grazing)
                continue;

            // Clear external PathFindControllers (e.g. Autonomals) that override movement
            if (animal.controller != null)
                animal.controller = null;

            if (follow.IsWalk && playerIdle)
            {
                animal.speed = DefaultAnimalSpeed;
                UpdateIdleRoaming(follow, config, Game1.player);
                continue;
            }

            Vector2 target = player.Position;

            SteerAnimal(follow, target, config, time);

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
        var config = GetConfig();

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

            // Any cached path is from the previous map; throw it out.
            follow.Path = null;
            follow.PathTarget = null;
            follow.FramesSincePathBuild = 0;
            follow.FramesSinceProgress = 0;
            follow.ConsecutivePathFailures = 0;
            follow.AvoidTiles.Clear();
            follow.AvoidExpiresFrames = 0;

            if (follow.State == FollowState.PendingSpawn)
            {
                if (!newLocation.IsOutdoors)
                    continue;

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

            if (follow.State == FollowState.HeadingToBarn)
                continue;

            if (follow.State == FollowState.Grazing)
            {
                follow.State = FollowState.FollowingPlayer;
                follow.GrazeTarget = null;
            }

            oldLocation.animals.Remove(animal.myID.Value);

            if (follow.IsWalk)
            {
                SpawnAnimalInLocation(animal, newLocation, Game1.player.Position);
                continue;
            }

            if (animal.home != null && newLocation == animal.home.GetParentLocation())
            {
                follow.State = FollowState.HeadingToBarn;

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

    private const int DefaultAnimalSpeed = 2;
    private const int PathRebuildInterval = 30;
    private const int MaxStallFrames = 20;
    private const int MaxConsecutivePathFailures = 3;
    private const int AvoidExpiryFrames = 180;

    private void SteerAnimal(FollowingAnimal follow, Vector2 target, ModConfig config, GameTime time)
    {
        var animal = follow.Animal;
        var player = Game1.player;
        var location = animal.currentLocation;

        float tileDistanceToPlayer = Vector2.Distance(animal.Position, player.Position) / 64f;

        animal.speed = DefaultAnimalSpeed;

        if (tileDistanceToPlayer < 1.5f)
        {
            animal.Halt();
            follow.Path = null;
            follow.PathTarget = null;
            follow.FramesSinceProgress = 0;
            follow.ConsecutivePathFailures = 0;
            return;
        }

        // Only teleport when the animal is genuinely lost (well past the rubber band).
        if (tileDistanceToPlayer > config.RubberBandDistance * 1.6f)
        {
            TeleportNearPlayer(follow);
            follow.Path = null;
            follow.PathTarget = null;
            follow.FramesSinceProgress = 0;
            follow.ConsecutivePathFailures = 0;
            return;
        }

        Point animalTile = animal.TilePoint;
        Point goal = player.TilePoint;

        // Age out avoid tiles so a blocker that's since moved doesn't stay banned forever.
        if (follow.AvoidTiles.Count > 0)
        {
            follow.AvoidExpiresFrames--;
            if (follow.AvoidExpiresFrames <= 0)
                follow.AvoidTiles.Clear();
        }

        bool needRebuild =
            follow.Path == null
            || follow.Path.Count == 0
            || follow.PathTarget != goal
            || follow.FramesSincePathBuild >= PathRebuildInterval;

        if (needRebuild)
        {
            Queue<Point> path;
            if (AnimalPathfinder.HasLineOfSight(location, animal, animalTile, goal, follow.AvoidTiles))
            {
                path = BuildStraightPath(animalTile, goal);
            }
            else
            {
                path = AnimalPathfinder.FindPath(location, animal, animalTile, goal, follow.AvoidTiles);
            }

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
        {
            TeleportNearPlayer(follow);
            follow.ConsecutivePathFailures = 0;
            follow.FramesSinceProgress = 0;
            return;
        }

        if (follow.Path == null || follow.Path.Count == 0)
        {
            animal.Halt();
            return;
        }

        // Advance past any waypoints we've already reached.
        while (follow.Path.Count > 0 && follow.Path.Peek() == animalTile)
            follow.Path.Dequeue();

        if (follow.Path.Count == 0)
        {
            animal.Halt();
            return;
        }

        Point waypoint = follow.Path.Peek();
        int desiredDir = GetDirToWaypoint(animalTile, waypoint, animal.Position);

        // Pre-check whether vanilla MovePosition would refuse this move. If we set a
        // movement flag toward a blocked tile, vanilla's bounce logic has a 60% chance
        // of flipping us to the opposite direction every tick (see FarmAnimal.MovePosition),
        // which produces a visible up/down or left/right walk-in-place glitch. Halting
        // and not setting any flag avoids that entirely.
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

        // Stall detection by pixel position. Works for both "blocked at next step" and
        // "vanilla refused our move" cases.
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
            // Ban the tile we were trying to step into so BFS finds a detour next rebuild.
            if (follow.Path != null && follow.Path.Count > 0)
                follow.AvoidTiles.Add(follow.Path.Peek());
            follow.AvoidExpiresFrames = AvoidExpiryFrames;

            follow.Path = null;
            follow.PathTarget = null;
            follow.FramesSinceProgress = 0;
            follow.FramesSincePathBuild = PathRebuildInterval;
            follow.ConsecutivePathFailures++;
        }
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

        // Same tile or diagonal (shouldn't happen with 4-connected BFS). Aim at pixel offset.
        Vector2 wpPixel = new(to.X * 64f + 32f, to.Y * 64f + 32f);
        Vector2 diff = wpPixel - animalPos;
        if (System.Math.Abs(diff.X) > System.Math.Abs(diff.Y))
            return diff.X > 0 ? 1 : 3;
        return diff.Y > 0 ? 2 : 0;
    }

    /// <summary>Teleport an animal to a position near the player with horizontal spread for multiple animals.</summary>
    private void TeleportNearPlayer(FollowingAnimal follow)
    {
        var animal = follow.Animal;
        var player = Game1.player;

        int idx = following.IndexOf(follow);
        float xOffset = (idx % 2 == 0 ? -1 : 1) * ((idx / 2) + 1) * 64f;
        Vector2 teleportPos = player.Position + new Vector2(xOffset, 192f);

        animal.Position = teleportPos;
        animal.Halt();
        follow.StuckTicks = 0;
        follow.DirectionCooldown = 0;
        follow.LastPosition = teleportPos;

        if (GetConfig().DebugLogging)
            Monitor.Log($"{animal.displayName} teleported near player.", LogLevel.Trace);
    }

    /// <summary>Custom idle roaming: animals walk short distances, pause, sit, and do eating animations
    /// within the rubber band radius while the player stands still.</summary>
    private void UpdateIdleRoaming(FollowingAnimal follow, ModConfig config, Farmer player)
    {
        var animal = follow.Animal;
        float distToPlayer = Vector2.Distance(animal.Position, player.Position) / 64f;

        follow.LastPosition = animal.Position;
        follow.StuckTicks = 0;

        if (distToPlayer > config.RubberBandDistance + 2)
        {
            TeleportNearPlayer(follow);
            follow.IdleActivityTicksLeft = 0;
            return;
        }

        if (follow.CurrentIdleActivity != IdleActivity.Walking)
        {
            animal.Halt();
            animal.controller = null;
        }
        else
        {
            animal.controller = null;
        }

        if (follow.CurrentIdleActivity == IdleActivity.Walking && distToPlayer > config.RubberBandDistance - 2)
        {
            Vector2 diff = player.Position - animal.Position;
            int dir = Math.Abs(diff.X) > Math.Abs(diff.Y)
                ? (diff.X > 0 ? 1 : 3)
                : (diff.Y > 0 ? 2 : 0);

            animal.Halt();
            SetMovingDirection(animal, dir);
            follow.IdleActivityTicksLeft = 0;
        }

        if (follow.CurrentIdleActivity == IdleActivity.Sitting)
        {
            var animalData = animal.GetAnimalData();
            bool useDouble = animalData?.UseDoubleUniqueAnimationFrames ?? false;
            bool flipRight = animalData?.UseFlippedRightForLeft ?? false;

            int sitFrame;
            if (useDouble)
            {
                sitFrame = animal.FacingDirection switch
                {
                    0 => 20, 1 => 18, 2 => 16, 3 => 22, _ => 16
                };
            }
            else
            {
                sitFrame = animal.FacingDirection switch
                {
                    0 => 15, 1 => 14, 2 => 13, 3 => (flipRight ? 14 : 12), _ => 13
                };
            }

            animal.Sprite.currentFrame = sitFrame;
            animal.Sprite.UpdateSourceRect();
        }

        if (follow.CurrentIdleActivity == IdleActivity.Eating)
        {
            var animalData = animal.GetAnimalData();
            int baseFrame = 16;
            if (!animal.Sprite.textureUsesFlippedRightForLeft)
                baseFrame += 4;
            if (animalData?.UseDoubleUniqueAnimationFrames ?? false)
                baseFrame += 4;

            int cycleFrame = (follow.IdleActivityTicksLeft / 40) % 2 == 0 ? baseFrame : baseFrame + 1;
            animal.Sprite.currentFrame = cycleFrame;
            animal.Sprite.UpdateSourceRect();
        }

        follow.IdleActivityTicksLeft--;
        if (follow.IdleActivityTicksLeft <= 0)
        {
            PickNextIdleActivity(follow, config, player);
        }
    }

    private void PickNextIdleActivity(FollowingAnimal follow, ModConfig config, Farmer player)
    {
        var animal = follow.Animal;
        var random = Game1.random;
        float distToPlayer = Vector2.Distance(animal.Position, player.Position) / 64f;

        animal.isEating.Value = false;

        int roll = random.Next(8);

        if (roll < 2)
        {
            follow.CurrentIdleActivity = IdleActivity.Walking;
            follow.IdleActivityTicksLeft = random.Next(60, 150);

            int dir;
            if (distToPlayer > config.RubberBandDistance - 3)
            {
                Vector2 diff = player.Position - animal.Position;
                dir = Math.Abs(diff.X) > Math.Abs(diff.Y)
                    ? (diff.X > 0 ? 1 : 3)
                    : (diff.Y > 0 ? 2 : 0);
            }
            else
            {
                dir = random.Next(4);
            }

            animal.Halt();
            SetMovingDirection(animal, dir);
        }
        else if (roll < 4)
        {
            follow.CurrentIdleActivity = IdleActivity.Pausing;
            follow.IdleActivityTicksLeft = random.Next(180, 360); // 3s - 6s
            animal.Halt();
            animal.FacingDirection = random.Next(4);
        }
        else if (roll < 6)
        {
            follow.CurrentIdleActivity = IdleActivity.Sitting;
            follow.IdleActivityTicksLeft = random.Next(240, 420); // 4s - 7s
            animal.Halt();
            animal.FacingDirection = random.Next(4);
        }
        else
        {
            follow.CurrentIdleActivity = IdleActivity.Eating;
            follow.IdleActivityTicksLeft = random.Next(150, 270); // 2.5s - 4.5s
            animal.Halt();
            animal.FacingDirection = 2; // face down
        }
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

    private void ForceDeliver(FollowingAnimal follow, bool isDayEnd = false, bool isWalkEnd = false)
    {
        var animal = follow.Animal;

        animal.speed = DefaultAnimalSpeed;

        animal.currentLocation?.animals.Remove(animal.myID.Value);

        var interior = animal.homeInterior;
        if (interior != null && !interior.animals.ContainsKey(animal.myID.Value))
        {
            interior.animals.Add(animal.myID.Value, animal);
            animal.currentLocation = interior;
            animal.setRandomPosition(interior);
        }

        if (GetConfig().ShowNotifications)
        {
            string key;
            if (isWalkEnd)
                key = "hud.walk_sent_home";
            else if (isDayEnd)
                key = "hud.auto_delivered";
            else if (follow.IsWalk)
                key = "hud.walk_delivered";
            else
                key = "hud.delivered";

            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get(key, new { name = animal.displayName })));
        }

        if (GetConfig().DebugLogging)
            Monitor.Log($"Delivered {animal.displayName} to {interior?.Name ?? "unknown"}.", LogLevel.Debug);
    }

    private void OnDelivered(FollowingAnimal follow, bool wasAutoDelivered)
    {
        var animal = follow.Animal;

        animal.speed = DefaultAnimalSpeed;

        var interior = animal.homeInterior;
        if (interior != null)
            animal.setRandomPosition(interior);

        if (GetConfig().ShowNotifications)
        {
            Game1.addHUDMessage(new HUDMessage(
                Helper.Translation.Get("hud.delivered", new { name = animal.displayName })));
        }
    }
}

internal enum SendHomeResult
{
    Success,
    NotOnWalk,
    InsufficientFriendship
}
