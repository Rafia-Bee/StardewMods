using System;
using HarmonyLib;
using LoanableTractor.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace LoanableTractor
{
    /// <summary>Main entry point for the Loanable Tractor mod.</summary>
    public class ModEntry : Mod
    {
        /*********
         ** Fields
         *********/
        /// <summary>The mod configuration.</summary>
        private ModConfig Config;

        /// <summary>Manages tractor spawn/despawn and loan state.</summary>
        private TractorLoanManager LoanManager;

        /// <summary>Handles SMAPI native mail delivery for the intro letter.</summary>
        private MailManager Mail;

        /// <summary>Tracks loan history and loyalty tier.</summary>
        private LoyaltyTracker Loyalty;

        /// <summary>Registers GMCM config options.</summary>
        private ConfigMenuHelper ConfigMenu;

        /// <summary>Save data key for persisting loan state.</summary>
        private const string SaveDataKey = "LoanableTractor_SaveData";

        /*********
         ** Public Methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();

            this.Loyalty = new LoyaltyTracker(this.Monitor, helper);
            this.LoanManager = new TractorLoanManager(this.Monitor, this.Config, helper, this.Loyalty);
            this.Mail = new MailManager(this.Monitor, helper, this.Config, this.LoanManager);
            this.ConfigMenu = new ConfigMenuHelper(this.Monitor, helper, this.ModManifest);

            MailboxOverrides.LoanManager = this.LoanManager;
            MailboxOverrides.ModHelper = helper;
            MailboxOverrides.Monitor = this.Monitor;
            MailboxOverrides.MailServicesModInstalled = helper.ModRegistry.IsLoaded("Digus.MailServicesMod");

            this.Mail.RegisterAssetEditor();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStartedEarly;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.GameLoop.Saving += this.OnSaving;
            helper.Events.World.NpcListChanged += this.OnNpcListChanged;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Player.Warped += this.OnPlayerWarped;

            this.ApplyHarmonyPatches();
            this.RegisterConsoleCommands(helper);

            this.Monitor.Log("Loanable Tractor mod initialized.", LogLevel.Info);
        }

        /*********
         ** Event Handlers
         *********/

        /// <summary>Raised after the game is launched. Registers GMCM, validates dependencies, gets APIs.</summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            if (!this.Helper.ModRegistry.IsLoaded("Pathoschild.TractorMod"))
            {
                this.Monitor.Log("Tractor Mod is not installed! This mod requires Tractor Mod to function.", LogLevel.Error);
                return;
            }

            this.ConfigMenu.Register(this.Config);
        }

        /// <summary>Raised after a save is loaded. Restores loan state and registers mail.</summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            this.LoadSaveData();
        }

        /// <summary>
        /// Raised EARLY at the start of each day (before Tractor Mod's DayStarted).
        /// Hides loaned tractors from Tractor Mod's cleanup.
        /// </summary>
        private void OnDayStartedEarly(object sender, DayStartedEventArgs e)
        {
            TractorModCompatPatches.HideLoanedTractorsFromCleanup();
        }

        /// <summary>Raised at the start of each day. Handles multi-day loan continuations.</summary>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            TractorModCompatPatches.RestoreLoanedTractorsAfterCleanup();

            this.Mail.TryDeliverIntroMail();

            if (this.LoanManager.RemainingLoanDays > 0)
                this.LoanManager.OnDayStarted();
        }

        /// <summary>Raised at the end of each day. Despawns tractors and processes end-of-day logic.</summary>
        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            this.LoanManager.OnDayEnding();
        }

        /// <summary>Raised before save. Persist loan state to save data.</summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            this.WriteSaveData();
        }

        /// <summary>Raised when the player returns to the title screen. Cleans up state.</summary>
        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            this.LoanManager.Reset();
        }

        /// <summary>Track NPC additions/removals to detect loaned tractor disappearance.</summary>
        private void OnNpcListChanged(object sender, NpcListChangedEventArgs e)
        {
            if (!this.LoanManager.LoanActiveToday)
                return;

            foreach (var npc in e.Removed)
            {
                if (npc is StardewValley.Characters.Horse horse
                    && horse.modData.ContainsKey(TractorLoanManager.LoanedTractorModDataKey))
                {
                    this.Monitor.Log($"Loaned tractor removed from {e.Location.Name}: HorseId={horse.HorseId}", LogLevel.Trace);
                }
            }
        }

        /// <summary>Runs dismount survival watchdog and periodic tractor safety checks.</summary>
        private int _tickCounter;
        private bool _wasRidingLoanedTractor;
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!this.LoanManager.LoanActiveToday || !Context.IsWorldReady)
                return;

            var mount = Game1.player?.mount;
            bool isRidingLoaned = mount != null && mount.modData.ContainsKey(TractorLoanManager.LoanedTractorModDataKey);

            if (this._wasRidingLoanedTractor && !isRidingLoaned)
                this.LoanManager.EnsureTractorSurvival();

            this._wasRidingLoanedTractor = isRidingLoaned;

            _tickCounter++;
            if (_tickCounter >= 60)
            {
                _tickCounter = 0;
                this.LoanManager.EnsureTractorSurvival();
            }
        }

        /// <summary>Track warps: make loaned tractor follow if the player was mounted at warp time.</summary>
        private void OnPlayerWarped(object sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer || !this.LoanManager.LoanActiveToday)
                return;

            if (this._wasRidingLoanedTractor)
                this.LoanManager.HandlePlayerWarped(e);
        }

        /*********
         ** Private Methods
         *********/

        /// <summary>Apply Harmony patches for mailbox interaction.</summary>
        private void ApplyHarmonyPatches()
        {
            try
            {
                var harmony = new Harmony(this.ModManifest.UniqueID);
                harmony.PatchAll(typeof(ModEntry).Assembly);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error applying Harmony patches: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Register debug console commands.</summary>
        private void RegisterConsoleCommands(IModHelper helper)
        {
            helper.ConsoleCommands.Add(
                "loanable_tractor",
                "Loanable Tractor commands.\n\nUsage:\n  loanable_tractor status\n  loanable_tractor loan [days]\n  loanable_tractor return\n  loanable_tractor reset",
                this.HandleConsoleCommand
            );
        }

        /// <summary>Handle console commands.</summary>
        private void HandleConsoleCommand(string command, string[] args)
        {
            if (args.Length == 0)
            {
                this.Monitor.Log("Usage: loanable_tractor <status|loan|return|reset>", LogLevel.Info);
                return;
            }

            switch (args[0].ToLower())
            {
                case "status":
                    if (this.LoanManager.LoanActiveToday || this.LoanManager.RemainingLoanDays > 0)
                    {
                        this.Monitor.Log(this.Helper.Translation.Get("command.status.active",
                            new
                            {
                                days = this.LoanManager.RemainingLoanDays,
                                total = this.Loyalty.TotalLoansCompleted,
                                tier = this.Loyalty.GetTierName()
                            }), LogLevel.Info);
                    }
                    else
                    {
                        this.Monitor.Log(this.Helper.Translation.Get("command.status.inactive",
                            new
                            {
                                total = this.Loyalty.TotalLoansCompleted,
                                tier = this.Loyalty.GetTierName()
                            }), LogLevel.Info);
                    }
                    break;

                case "loan":
                    int days = 1;
                    if (args.Length > 1 && int.TryParse(args[1], out int parsedDays))
                        days = Math.Clamp(parsedDays, 1, this.Config.MaxLoanDays);

                    if (this.LoanManager.ExecuteLoan(days))
                        this.Monitor.Log(this.Helper.Translation.Get("command.loan.success", new { days }), LogLevel.Info);
                    break;

                case "return":
                    this.LoanManager.Reset();
                    this.Monitor.Log(this.Helper.Translation.Get("command.return.success"), LogLevel.Info);
                    break;

                case "reset":
                    this.LoanManager.Reset();
                    this.Loyalty.Reset();
                    this.WriteSaveData();
                    this.Monitor.Log(this.Helper.Translation.Get("command.reset.success"), LogLevel.Info);
                    break;

                default:
                    this.Monitor.Log("Unknown command. Usage: loanable_tractor <status|loan|return|reset>", LogLevel.Warn);
                    break;
            }
        }

        /// <summary>Load persisted loan data from the save file.</summary>
        private void LoadSaveData()
        {
            try
            {
                var data = this.Helper.Data.ReadSaveData<LoanSaveData>(SaveDataKey);
                if (data != null)
                {
                    this.Loyalty.TotalLoansCompleted = data.TotalLoansCompleted;
                    this.Loyalty.CurrentLoyaltyTier = data.CurrentLoyaltyTier;
                    this.LoanManager.RemainingLoanDays = data.RemainingLoanDays;
                    this.LoanManager.LoanActiveToday = data.LoanActiveToday;
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error loading save data: {ex}", LogLevel.Error);
            }
        }

        /// <summary>Persist loan data to the save file.</summary>
        private void WriteSaveData()
        {
            try
            {
                var data = new LoanSaveData
                {
                    TotalLoansCompleted = this.Loyalty.TotalLoansCompleted,
                    CurrentLoyaltyTier = this.Loyalty.CurrentLoyaltyTier,
                    RemainingLoanDays = this.LoanManager.RemainingLoanDays,
                    LoanActiveToday = this.LoanManager.LoanActiveToday
                };

                this.Helper.Data.WriteSaveData(SaveDataKey, data);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error writing save data: {ex}", LogLevel.Error);
            }
        }
    }

    /// <summary>Serializable save data for loan state persistence.</summary>
    internal class LoanSaveData
    {
        public int TotalLoansCompleted { get; set; }
        public int CurrentLoyaltyTier { get; set; }
        public bool LoanActiveToday { get; set; }
        public int RemainingLoanDays { get; set; }
    }
}
