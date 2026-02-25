using StardewValley;
using StardewValley.Characters;

namespace LoanableTractor.Framework
{
    /// <summary>
    /// Prevents Tractor Mod from cleaning up loaned tractors during its DayStarted event.
    /// Temporarily removes/restores the Pathoschild.TractorMod modData key.
    /// </summary>
    internal static class TractorModCompatPatches
    {
        /// <summary>
        /// Hide loaned tractors from Tractor Mod's cleanup.
        /// Removes the TractorMod modData key so IsTractor() returns false.
        /// </summary>
        public static void HideLoanedTractorsFromCleanup()
        {
            foreach (var location in Game1.locations)
            {
                foreach (var npc in location.characters)
                {
                    if (npc is Horse horse && horse.modData.ContainsKey(TractorLoanManager.LoanedTractorModDataKey))
                    {
                        if (horse.modData.ContainsKey(TractorLoanManager.TractorModDataKey))
                        {
                            horse.modData.Remove(TractorLoanManager.TractorModDataKey);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Restore loaned tractors after Tractor Mod's cleanup.
        /// Re-adds the TractorMod modData key so the tractor functions normally.
        /// </summary>
        public static void RestoreLoanedTractorsAfterCleanup()
        {
            foreach (var location in Game1.locations)
            {
                foreach (var npc in location.characters)
                {
                    if (npc is Horse horse && horse.modData.ContainsKey(TractorLoanManager.LoanedTractorModDataKey))
                    {
                        if (!horse.modData.ContainsKey(TractorLoanManager.TractorModDataKey))
                        {
                            horse.modData[TractorLoanManager.TractorModDataKey] = "1";
                        }
                    }
                }
            }
        }
    }
}
