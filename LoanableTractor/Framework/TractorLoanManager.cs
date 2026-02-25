using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;

namespace LoanableTractor.Framework
{
    /// <summary>Manages tractor loan lifecycle: spawning, despawning, tracking, and fee collection.</summary>
    internal class TractorLoanManager
    {
        /*********
         ** Constants
         *********/
        /// <summary>The mod data key used to mark a horse as a loaned tractor.</summary>
        public const string LoanedTractorModDataKey = "RafiaBee.LoanableTractor";

        /// <summary>The mod data value indicating an active loan.</summary>
        private const string LoanedValue = "loaned";

        /// <summary>The mod data key Tractor Mod uses to identify tractors.</summary>
        public const string TractorModDataKey = "Pathoschild.TractorMod";

        /// <summary>The name Tractor Mod assigns to tractor horses.</summary>
        private const string TractorName = "Tractor";

        /// <summary>The asset name for the tractor spritesheet (loaded by Tractor Mod via content pipeline).</summary>
        private const string TractorSpriteAsset = "Mods/Pathoschild.TractorMod/Tractor";

        /*********
         ** Fields
         *********/
        private readonly IMonitor Monitor;
        private readonly ModConfig Config;
        private readonly IModHelper Helper;
        private readonly LoyaltyTracker Loyalty;

        /*********
         ** Properties
         *********/
        /// <summary>Whether a tractor has been loaned today (single-day mode).</summary>
        public bool LoanActiveToday { get; set; }

        /// <summary>Number of remaining loan days for multi-day loans.</summary>
        public int RemainingLoanDays { get; set; }

        /// <summary>GUIDs of all active loaned tractors.</summary>
        public List<Guid> ActiveTractorIds { get; set; } = new();

        /*********
         ** Public Methods
         *********/
        public TractorLoanManager(IMonitor monitor, ModConfig config, IModHelper helper, LoyaltyTracker loyalty)
        {
            this.Monitor = monitor;
            this.Config = config;
            this.Helper = helper;
            this.Loyalty = loyalty;
        }

        /// <summary>Check if a tractor loan can be started right now.</summary>
        public bool CanLoan()
        {
            if (this.LoanActiveToday)
                return false;

            if (!this.Config.AllowLoanWithGarage && this.PlayerHasGarage())
                return false;

            if (!Game1.player.mailReceived.Contains("RafiaBee.LoanableTractor_Intro"))
                return false;

            return true;
        }

        /// <summary>Get the current loan cost per day, accounting for loyalty discounts.</summary>
        public int GetCurrentLoanCost()
        {
            int baseCost = this.Config.LoanCostPerDay;
            float discount = this.Loyalty?.GetCurrentDiscount() ?? 0f;
            return Math.Max(0, (int)(baseCost * (1f - discount)));
        }

        /// <summary>Execute a tractor loan: deduct gold, spawn tractor, set tracking flags.</summary>
        public bool ExecuteLoan(int days = -1)
        {
            if (days < 1)
                days = this.Config.MaxLoanDays;

            days = Math.Clamp(days, 1, this.Config.MaxLoanDays);

            int costPerDay = this.GetCurrentLoanCost();
            int totalCost = this.Config.ChargeUpfront ? costPerDay * days : costPerDay;

            if (Game1.player.Money < totalCost)
            {
                Game1.addHUDMessage(new HUDMessage(
                    this.Helper.Translation.Get("hud.tractor.insufficient.funds"), HUDMessage.error_type));
                return false;
            }

            Game1.player.Money -= totalCost;

            bool spawned = this.SpawnTractor();
            if (!spawned)
            {
                Game1.player.Money += totalCost;
                this.Monitor.Log("Failed to spawn loaned tractor — refunding gold.", LogLevel.Warn);
                return false;
            }

            this.LoanActiveToday = true;
            this.RemainingLoanDays = days;

            Game1.addHUDMessage(new HUDMessage(
                this.Helper.Translation.Get("hud.tractor.delivered"), HUDMessage.achievement_type));

            this.Monitor.Log($"Tractor loaned for {days} day(s) at {costPerDay}g/day (total: {totalCost}g).", LogLevel.Info);
            return true;
        }

        /// <summary>Handle daily multi-day loan charging at start of day.</summary>
        public void OnDayStarted()
        {
            if (this.RemainingLoanDays <= 0)
                return;

            if (!this.Config.ChargeUpfront)
            {
                int cost = this.GetCurrentLoanCost();
                if (Game1.player.Money < cost)
                {
                    this.DespawnAllLoanedTractors();
                    this.RemainingLoanDays = 0;
                    this.LoanActiveToday = false;
                    Game1.addHUDMessage(new HUDMessage(
                        this.Helper.Translation.Get("hud.tractor.repossessed"), HUDMessage.error_type));
                    this.Monitor.Log("Tractor repossessed — insufficient funds.", LogLevel.Info);
                    return;
                }
                Game1.player.Money -= cost;
            }

            this.SpawnTractor();
            this.LoanActiveToday = true;
        }

        /// <summary>Handle end-of-day cleanup: despawn tractors, decrement loan counter.</summary>
        public void OnDayEnding()
        {
            if (!this.LoanActiveToday && this.RemainingLoanDays <= 0)
                return;

            if (this.Config.EnableLateReturnPenalty && this.LoanActiveToday && Game1.timeOfDay >= 2400)
            {
                int penalty = this.Config.LateReturnPenalty;
                Game1.player.Money = Math.Max(0, Game1.player.Money - penalty);
            }

            this.DespawnAllLoanedTractors();

            if (this.RemainingLoanDays > 0)
                this.RemainingLoanDays--;

            if (this.RemainingLoanDays <= 0)
            {
                this.LoanActiveToday = false;
                this.Loyalty?.RecordLoanCompleted();

                Game1.addHUDMessage(new HUDMessage(
                    this.Helper.Translation.Get("hud.tractor.collected"), HUDMessage.achievement_type));
            }
        }

        /// <summary>Reset all loan state (used on return to title or debug reset).</summary>
        public void Reset()
        {
            this.LoanActiveToday = false;
            this.RemainingLoanDays = 0;
            this.DespawnAllLoanedTractors();
            this.ActiveTractorIds.Clear();
        }

        /// <summary>Find a loaned tractor by searching all game locations (including mounted).</summary>
        public Horse FindLoanedTractor()
        {
            var mount = Game1.player?.mount;
            if (mount != null && mount.modData.ContainsKey(LoanedTractorModDataKey))
                return mount;
            foreach (var location in Game1.locations)
            {
                foreach (var npc in location.characters)
                {
                    if (npc is Horse horse && horse.modData.ContainsKey(LoanedTractorModDataKey))
                        return horse;
                }
            }

            return null;
        }

        /// <summary>
        /// Handle player warp: if the player was riding the loaned tractor but arrived unmounted,
        /// warp the tractor to the player's new location.
        /// </summary>
        public void HandlePlayerWarped(WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer || !this.LoanActiveToday)
                return;

            var mount = Game1.player?.mount;

            if (mount != null && mount.modData.ContainsKey(LoanedTractorModDataKey))
                return;

            // Player arrived without mount — warp the tractor to them
            Horse tractor = this.FindLoanedTractor();
            if (tractor != null && tractor != Game1.player?.mount)
            {
                var tile = Game1.player.Tile;
                this.PlaceTractorAtTile(tractor, e.NewLocation, tile);

                this.Monitor.Log($"Warped loaned tractor to follow player to {e.NewLocation.Name} at ({tile.X}, {tile.Y}).", LogLevel.Trace);
            }
        }

        /// <summary>
        /// Watchdog: called every tick to ensure the loaned tractor hasn't been lost.
        /// If the player just dismounted and the tractor isn't in any location, re-add it.
        /// </summary>
        public void EnsureTractorSurvival()
        {
            if (!this.LoanActiveToday || this.ActiveTractorIds.Count == 0)
                return;

            var mount = Game1.player?.mount;
            if (mount != null && mount.modData.ContainsKey(LoanedTractorModDataKey))
                return;

            Horse existingTractor = null;
            foreach (var location in Game1.locations)
            {
                foreach (var npc in location.characters)
                {
                    if (npc is Horse horse && horse.modData.ContainsKey(LoanedTractorModDataKey))
                    {
                        existingTractor = horse;
                        break;
                    }
                }
                if (existingTractor != null)
                    break;
            }

            if (existingTractor != null)
                return;

            var player = Game1.player;
            if (player?.currentLocation == null)
                return;

            var tractorId = this.ActiveTractorIds.Count > 0 ? this.ActiveTractorIds[0] : Guid.NewGuid();
            var tractor = this.CreateLoanedTractor(tractorId);
            tractor.DefaultPosition = player.Tile;

            this.PlaceTractorAtTile(tractor, player.currentLocation, player.Tile);

            if (!this.ActiveTractorIds.Contains(tractorId))
            {
                this.ActiveTractorIds.Clear();
                this.ActiveTractorIds.Add(tractorId);
            }

            this.Monitor.Log($"Recovered lost loaned tractor — re-spawned at player's position in {player.currentLocation.Name}.", LogLevel.Warn);
        }

        /// <summary>
        /// Create a new Horse entity configured as a loaned tractor with all required
        /// Tractor Mod metadata. Centralizes initialization to avoid duplication.
        /// </summary>
        private Horse CreateLoanedTractor(Guid tractorId)
        {
            var tractor = new Horse(tractorId, 0, 0)
            {
                Name = TractorName
            };

            tractor.modData[TractorModDataKey] = "1";
            tractor.onFootstepAction = _ => { };
            tractor.hideFromAnimalSocialMenu.Value = true;
            tractor.ownerId.Value = 0;
            tractor.modData[LoanedTractorModDataKey] = LoanedValue;

            return tractor;
        }

        /// <summary>
        /// Place a tractor at a specific tile in a location using Tractor Mod's SetLocation pattern.
        /// Handles removal from previous location, addition to new location, and position setup.
        /// </summary>
        private void PlaceTractorAtTile(Horse tractor, GameLocation location, Vector2 tile)
        {
            tractor.currentLocation?.characters.Remove(tractor);
            tractor.currentLocation = null;
            location.addCharacter(tractor);
            tractor.currentLocation = location;
            tractor.isCharging = false;
            tractor.speed = 2;
            tractor.blockedInterval = 0;
            tractor.position.X = tile.X * Game1.tileSize;
            tractor.position.Y = tile.Y * Game1.tileSize;

            this.ApplyTractorTexture(tractor);
        }

        /// <summary>Apply the tractor spritesheet so it renders as a tractor immediately.</summary>
        public void ApplyTractorTexture(Horse tractor)
        {
            try
            {
                tractor.Sprite.LoadTexture(TractorSpriteAsset, syncTextureName: false);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Could not apply tractor texture: {ex.Message}", LogLevel.Trace);
            }
        }

        /// <summary>Check whether the player owns a tractor garage building.</summary>
        public bool PlayerHasGarage()
        {
            var farm = Game1.getFarm();
            if (farm == null)
                return false;

            return farm.buildings.Any(b =>
                b.buildingType.Value != null
                && b.buildingType.Value.Contains("Tractor", StringComparison.OrdinalIgnoreCase));
        }

        /*********
         ** Private Methods
         *********/
        /// <summary>Spawn a tractor near the player's mailbox on the farm.</summary>
        private bool SpawnTractor()
        {
            try
            {
                var farm = Game1.getFarm();
                if (farm == null)
                {
                    this.Monitor.Log("Cannot spawn tractor — farm not found.", LogLevel.Error);
                    return false;
                }

                Vector2 spawnTile = this.GetSpawnTile(farm);

                Guid tractorId = Guid.NewGuid();
                var tractor = this.CreateLoanedTractor(tractorId);
                tractor.DefaultPosition = spawnTile;

                this.PlaceTractorAtTile(tractor, farm, spawnTile);

                this.ActiveTractorIds.Add(tractorId);
                return true;
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error spawning loaned tractor: {ex}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>Get a valid tile near the mailbox to spawn the tractor.</summary>
        private Vector2 GetSpawnTile(GameLocation farm)
        {
            // The farm mailbox is at tile (68, 16) on the standard farm
            Point mailboxTile = new Point(68, 16);

            Vector2[] offsets = new[]
            {
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(-1, 0),
                new Vector2(0, -1),
                new Vector2(2, 0),
                new Vector2(0, 2)
            };

            foreach (var offset in offsets)
            {
                var candidate = new Vector2(mailboxTile.X + offset.X, mailboxTile.Y + offset.Y);
                if (!farm.IsTileOccupiedBy(candidate) && farm.isTilePassable(candidate))
                    return candidate;
            }

            return new Vector2(mailboxTile.X, mailboxTile.Y);
        }

        /// <summary>Remove all loaned tractors from all game locations.</summary>
        private void DespawnAllLoanedTractors()
        {
            try
            {
                foreach (var location in Game1.locations)
                {
                    var toRemove = location.characters
                        .Where(npc => npc is Horse horse && horse.modData.ContainsKey(LoanedTractorModDataKey))
                        .ToList();

                    foreach (var character in toRemove)
                    {
                        location.characters.Remove(character);
                    }
                }

                this.ActiveTractorIds.Clear();
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error despawning loaned tractors: {ex}", LogLevel.Error);
            }
        }
    }
}
