using System;
using StardewModdingAPI;

namespace LoanableTractor.Framework
{
    /// <summary>Tracks loan history and loyalty tier milestones for the Joja Loyalty Program.</summary>
    internal class LoyaltyTracker
    {
        /*********
         ** Fields
         *********/
        private readonly IMonitor Monitor;
        private readonly IModHelper Helper;

        /*********
         ** Properties
         *********/
        /// <summary>Total number of loans completed by the player.</summary>
        public int TotalLoansCompleted { get; set; }

        /// <summary>Current loyalty tier (0=None, 1=Bronze, 2=Silver, 3=Gold).</summary>
        public int CurrentLoyaltyTier { get; set; }

        /*********
         ** Public Methods
         *********/
        public LoyaltyTracker(IMonitor monitor, IModHelper helper)
        {
            this.Monitor = monitor;
            this.Helper = helper;
        }

        /// <summary>Get the current discount multiplier based on loyalty tier.</summary>
        public float GetCurrentDiscount()
        {
            return this.CurrentLoyaltyTier switch
            {
                1 => 0.10f, // Bronze — 10%
                2 => 0.20f, // Silver — 20%
                3 => 0.30f, // Gold   — 30%
                _ => 0.00f  // None
            };
        }

        /// <summary>Get the display name for the current loyalty tier.</summary>
        public string GetTierName()
        {
            return this.CurrentLoyaltyTier switch
            {
                1 => "Bronze",
                2 => "Silver",
                3 => "Gold",
                _ => "None"
            };
        }

        /// <summary>Record a completed loan and check for milestone advancement.</summary>
        public void RecordLoanCompleted()
        {
            this.TotalLoansCompleted++;
            this.Monitor.Log($"Loan completed. Total: {this.TotalLoansCompleted}", LogLevel.Trace);
            this.CheckMilestones();
        }

        /// <summary>Reset all loyalty data.</summary>
        public void Reset()
        {
            this.TotalLoansCompleted = 0;
            this.CurrentLoyaltyTier = 0;
            this.Monitor.Log("Loyalty data reset.", LogLevel.Trace);
        }

        /*********
         ** Private Methods
         *********/

        /// <summary>Check whether the player has reached a new loyalty milestone.</summary>
        private void CheckMilestones()
        {
            int previousTier = this.CurrentLoyaltyTier;
            bool ccComplete = TractorLoanManager.IsCommunityCenterComplete();
            string suffix = ccComplete ? ".pierre" : "";

            if (this.TotalLoansCompleted >= 50 && this.CurrentLoyaltyTier < 3)
            {
                this.CurrentLoyaltyTier = 3;
                this.ShowMilestoneMessage($"loyalty.gold{suffix}");
            }
            else if (this.TotalLoansCompleted >= 25 && this.CurrentLoyaltyTier < 2)
            {
                this.CurrentLoyaltyTier = 2;
                this.ShowMilestoneMessage($"loyalty.silver{suffix}");
            }
            else if (this.TotalLoansCompleted >= 10 && this.CurrentLoyaltyTier < 1)
            {
                this.CurrentLoyaltyTier = 1;
                this.ShowMilestoneMessage($"loyalty.bronze{suffix}");
            }

            if (this.CurrentLoyaltyTier != previousTier)
                this.Monitor.Log($"Loyalty tier advanced to {this.GetTierName()} (tier {this.CurrentLoyaltyTier}).", LogLevel.Info);
        }

        /// <summary>Show a HUD message for a loyalty milestone.</summary>
        private void ShowMilestoneMessage(string translationKey)
        {
            string message = this.Helper.Translation.Get(translationKey);
            StardewValley.Game1.addHUDMessage(
                new StardewValley.HUDMessage(message, StardewValley.HUDMessage.achievement_type));
        }
    }
}
