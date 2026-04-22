using System;
using System.Collections.Generic;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

internal static class PerfectionPatch
{
    internal static Func<ModConfig> GetConfig;

    // Postfix for Utility.getCraftedRecipesPercent.
    // Recalculates the result excluding DGF recipes so they don't affect the
    // crafting portion of the perfection score.
    internal static void GetCraftedRecipesPercent_Postfix(Farmer who, ref float __result)
    {
        var config = GetConfig();
        if (config.grabberMode != ModConfig.GrabberMode.Specialized || config.specializedGrabbersCountForPerfection)
            return;

        who ??= Game1.player;
        if (who == null) return;

        var allRecipes = CraftingRecipe.craftingRecipes;
        var dgfKeys = new HashSet<string>(ProgressionTracker.AllRecipeKeys);

        int dgfTotal = 0;
        foreach (string key in dgfKeys)
        {
            if (allRecipes.ContainsKey(key))
                dgfTotal++;
        }

        if (dgfTotal == 0) return;

        int adjustedTotal = allRecipes.Count - 1 - dgfTotal; // -1 for Wedding Ring
        if (adjustedTotal <= 0) return;

        float num = 0f;
        foreach (string key in allRecipes.Keys)
        {
            if (key == "Wedding Ring" || dgfKeys.Contains(key)) continue;
            if (who.craftingRecipes.TryGetValue(key, out int count) && count > 0)
                num += 1f;
        }

        __result = num / adjustedTotal;
    }

    // Postfix for Stats.checkForCraftingAchievements.
    // Grants the Craft Master achievement (id 22) if the player has crafted all
    // non-DGF recipes, even when DGF recipes are excluded from the count.
    internal static void CheckForCraftingAchievements_Postfix()
    {
        var config = GetConfig();
        if (config.grabberMode != ModConfig.GrabberMode.Specialized || config.specializedGrabbersCountForPerfection)
            return;

        if (Game1.player.achievements.Contains(22)) return;

        var allRecipes = CraftingRecipe.craftingRecipes;
        var dgfKeys = new HashSet<string>(ProgressionTracker.AllRecipeKeys);

        int dgfTotal = 0;
        foreach (string key in dgfKeys)
        {
            if (allRecipes.ContainsKey(key))
                dgfTotal++;
        }

        if (dgfTotal == 0) return;

        int adjustedTotal = allRecipes.Count - 1 - dgfTotal; // -1 for Wedding Ring
        int crafted = 0;
        foreach (string key in allRecipes.Keys)
        {
            if (key == "Wedding Ring" || dgfKeys.Contains(key)) continue;
            if (Game1.player.craftingRecipes.TryGetValue(key, out int count) && count > 0)
                crafted++;
        }

        if (crafted >= adjustedTotal)
            Game1.getAchievement(22);
    }
}
