using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewValley;

namespace LoanableTractor.Framework
{
    /// <summary>
    /// Harmony patches for GameLocation.mailbox() and answerDialogueAction()
    /// to show a loan menu when the player interacts with an empty mailbox.
    /// </summary>
    internal static class MailboxOverrides
    {
        /*********
         ** Fields
         *********/
        /// <summary>Reference to the TractorLoanManager for checking loan state and executing loans.</summary>
        internal static TractorLoanManager LoanManager;

        /// <summary>The mod helper for translations.</summary>
        internal static StardewModdingAPI.IModHelper ModHelper;

        /// <summary>The monitor for logging.</summary>
        internal static StardewModdingAPI.IMonitor Monitor;

        /// <summary>The mod configuration.</summary>
        internal static ModConfig Config;

        /// <summary>Whether Mail Services Mod is installed (for combined dialogue).</summary>
        internal static bool MailServicesModInstalled;

        /*********
         ** Harmony Patches
         *********/

        /// <summary>
        /// Prefix patch for GameLocation.mailbox().
        /// Intercepts empty-mailbox interactions to show the loan tractor option.
        /// </summary>
        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.mailbox))]
        internal static class MailboxPatch
        {
            /// <summary>Prefix: show loan menu when mailbox is empty and conditions are met.</summary>
            internal static bool Prefix(GameLocation __instance)
            {
                try
                {
                    if (Game1.player.mailbox.Count > 0)
                        return true;

                    if (LoanManager == null || !LoanManager.CanLoan())
                        return true;

                    if (MailServicesModInstalled && Game1.player.ActiveObject != null)
                        return true;

                    ShowStandaloneLoanDialogue(__instance);
                    return false;
                }
                catch (Exception ex)
                {
                    Monitor?.Log($"Error in mailbox prefix: {ex}", StardewModdingAPI.LogLevel.Error);
                    return true;
                }
            }
        }

        /// <summary>
        /// Prefix patch for GameLocation.answerDialogueAction().
        /// Handles the player's response to the loan tractor and breakdown dialogues.
        /// </summary>
        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.answerDialogueAction))]
        internal static class AnswerDialoguePatch
        {
            /// <summary>Prefix: handle loan and breakdown dialogue responses.</summary>
            internal static bool Prefix(GameLocation __instance, string questionAndAnswer, ref bool __result)
            {
                try
                {
                    if (questionAndAnswer == null)
                        return true;

                    if (questionAndAnswer.StartsWith("LoanableTractor_MailboxMenu"))
                    {
                        string choice = questionAndAnswer.Replace("LoanableTractor_MailboxMenu_", "");

                        if (choice == "LoanableTractor_Loan")
                            HandleLoanChoice(__instance);

                        __result = true;
                        return false;
                    }

                    if (questionAndAnswer.StartsWith("LoanableTractor_BreakdownMenu"))
                    {
                        string choice = questionAndAnswer.Replace("LoanableTractor_BreakdownMenu_", "");

                        if (choice == "LoanableTractor_Fix")
                            HandleBreakdownFix();
                        else if (choice == "LoanableTractor_Skip")
                            HandleBreakdownSkip();

                        __result = true;
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Monitor?.Log($"Error in answerDialogueAction prefix: {ex}", StardewModdingAPI.LogLevel.Error);
                    return true;
                }
            }
        }

        /*********
         ** Private Methods
         *********/

        /// <summary>Show the standalone loan dialogue (no Mail Services Mod integration).</summary>
        private static void ShowStandaloneLoanDialogue(GameLocation location)
        {
            int cost = LoanManager.GetCurrentLoanCost();
            string loanText = ModHelper.Translation.Get("dialogue.mailbox.loan", new { cost });
            string cancelText = ModHelper.Translation.Get("dialogue.mailbox.cancel");

            bool ccComplete = TractorLoanManager.IsCommunityCenterComplete();
            string titleKey = ccComplete ? "dialogue.mailbox.title.pierre" : "dialogue.mailbox.title.joja";
            string title = ModHelper.Translation.Get(titleKey);

            if (Config != null && Config.EnableWeekendSurcharge && TractorLoanManager.IsWeekend())
            {
                string surchargeKey = ccComplete
                    ? "dialogue.mailbox.weekend_surcharge.pierre"
                    : "dialogue.mailbox.weekend_surcharge.joja";
                title += "^^" + ModHelper.Translation.Get(surchargeKey, new { percent = Config.WeekendSurchargePercent });
            }

            float seasonalDiscount = TractorLoanManager.GetSeasonalDiscount();
            if (seasonalDiscount > 0)
            {
                string season = Game1.currentSeason;
                string promoKey = ccComplete
                    ? $"dialogue.mailbox.seasonal_promo.pierre.{season}"
                    : $"dialogue.mailbox.seasonal_promo.joja.{season}";
                title += "^^" + ModHelper.Translation.Get(promoKey);
            }

            var responses = new List<Response>
            {
                new Response("LoanableTractor_Loan", loanText),
                new Response("LoanableTractor_Cancel", cancelText)
            };

            location.createQuestionDialogue(
                title,
                responses.ToArray(),
                "LoanableTractor_MailboxMenu"
            );
        }

        /// <summary>Handle the player choosing to loan a tractor. Checks for breakdown before executing.</summary>
        private static void HandleLoanChoice(GameLocation location)
        {
            int cost = LoanManager.GetCurrentLoanCost();
            if (Game1.player.Money < cost)
            {
                Game1.addHUDMessage(new HUDMessage(
                    ModHelper.Translation.Get("hud.tractor.insufficient.funds"), HUDMessage.error_type));
                return;
            }

            if (Config.EnableBreakdownChance && Game1.random.NextDouble() < Config.BreakdownChancePercent / 100.0)
            {
                var (featureKey, attachmentTypeName) = BreakdownManager.RollRandomFeature();
                BreakdownManager.PendingFeatureKey = featureKey;
                BreakdownManager.PendingAttachmentTypeName = attachmentTypeName;
                ShowBreakdownDialogue(location, featureKey);
            }
            else
            {
                LoanManager.ExecuteLoan();
            }
        }

        /// <summary>Handle the player choosing to fix the broken feature (stamina cost).</summary>
        private static void HandleBreakdownFix()
        {
            string featureKey = BreakdownManager.PendingFeatureKey;
            float currentStamina = Game1.player.Stamina;
            float cost = currentStamina * (Config.BreakdownStaminaCostPercent / 100f);
            Game1.player.Stamina = Math.Max(0, currentStamina - cost);

            BreakdownManager.ClearPending();

            if (LoanManager.ExecuteLoan())
            {
                string featureName = ModHelper.Translation.Get($"breakdown.feature.{featureKey}");
                bool ccComplete = TractorLoanManager.IsCommunityCenterComplete();
                string key = ccComplete ? "hud.tractor.breakdown.fixed.pierre" : "hud.tractor.breakdown.fixed.joja";
                Game1.addHUDMessage(new HUDMessage(
                    ModHelper.Translation.Get(key, new { feature = featureName }), HUDMessage.newQuest_type));
            }
            else
            {
                Game1.player.Stamina = currentStamina;
            }
        }

        /// <summary>Handle the player choosing to skip fixing (feature stays broken).</summary>
        private static void HandleBreakdownSkip()
        {
            string featureKey = BreakdownManager.PendingFeatureKey;
            BreakdownManager.ApplyPendingBreakdown();

            if (LoanManager.ExecuteLoan())
            {
                string featureName = ModHelper.Translation.Get($"breakdown.feature.{featureKey}");
                bool ccComplete = TractorLoanManager.IsCommunityCenterComplete();
                string key = ccComplete ? "hud.tractor.breakdown.active.pierre" : "hud.tractor.breakdown.active.joja";
                Game1.addHUDMessage(new HUDMessage(
                    ModHelper.Translation.Get(key, new { feature = featureName }), HUDMessage.newQuest_type));
            }
            else
            {
                BreakdownManager.Reset();
            }
        }

        /// <summary>Show the breakdown fix/skip dialogue.</summary>
        private static void ShowBreakdownDialogue(GameLocation location, string featureKey)
        {
            string featureName = ModHelper.Translation.Get($"breakdown.feature.{featureKey}");
            int staminaPercent = Config.BreakdownStaminaCostPercent;

            bool ccComplete = TractorLoanManager.IsCommunityCenterComplete();
            string messageKey = ccComplete ? "dialogue.breakdown.message.pierre" : "dialogue.breakdown.message.joja";
            string message = ModHelper.Translation.Get(messageKey, new { feature = featureName, percent = staminaPercent });

            string fixText = ModHelper.Translation.Get("dialogue.breakdown.fix", new { percent = staminaPercent });
            string skipText = ModHelper.Translation.Get("dialogue.breakdown.skip");

            var responses = new List<Response>
            {
                new Response("LoanableTractor_Fix", fixText),
                new Response("LoanableTractor_Skip", skipText)
            };

            location.createQuestionDialogue(message, responses.ToArray(), "LoanableTractor_BreakdownMenu");
        }
    }
}
