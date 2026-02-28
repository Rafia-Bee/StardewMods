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
        /// Handles the player's response to the loan tractor dialogue.
        /// </summary>
        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.answerDialogueAction))]
        internal static class AnswerDialoguePatch
        {
            /// <summary>Prefix: handle loan dialogue responses.</summary>
            internal static bool Prefix(GameLocation __instance, string questionAndAnswer, ref bool __result)
            {
                try
                {
                    if (questionAndAnswer == null || !questionAndAnswer.StartsWith("LoanableTractor_MailboxMenu"))
                        return true;

                    string choice = questionAndAnswer.Replace("LoanableTractor_MailboxMenu_", "");

                    if (choice == "LoanableTractor_Loan")
                    {
                        LoanManager.ExecuteLoan();
                        __result = true;
                        return false;
                    }

                    __result = true;
                    return false;
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
    }
}
