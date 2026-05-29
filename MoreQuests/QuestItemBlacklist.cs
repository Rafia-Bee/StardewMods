using System;
using System.Collections.Generic;
using MoreQuestsFramework;
using StardewValley;

namespace MoreQuests;

/// Items that should never show up as a crop / flower / gem quest objective. Two tiers:
/// some are off-limits forever (endgame or extremely rare), others only in Year 1 when the
/// player realistically can't spare them yet. Wired into the framework's item pools via
/// ItemResolver.QuestPoolExclusion, so every generator that pulls from those pools (board
/// requests, festival and cooking ingredient lists) skips them automatically. Rewards are
/// untouched, so these items can still be handed to the player as a prize.
internal static class QuestItemBlacklist
{
    // Never requested, any year. Add modded endgame ids here.
    private static readonly HashSet<string> AlwaysExcluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "(O)889", // Qi Fruit
        "(O)890", // Qi Bean
        "(O)74",  // Prismatic Shard
        "(O)FlashShifter.StardewValleyExpandedCP_Galdoran_Gem" // Galdoran Gem (SVE)
    };

    // Obtainable in Year 1 but too precious or out of reach to hand over that early. Fair game from Year 2 on.
    private static readonly HashSet<string> Year1Excluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "(O)454", // Ancient Fruit
        "(O)417", // Sweet Gem Berry
        "(O)829", // Ginger
        "(O)832", // Pineapple
        "(O)815", // Taro Root
        "(O)266", // Red Cabbage
        "(O)248", // Garlic
        "(O)252", // Rhubarb
        "(O)268", // Starfruit
        "(O)489", // Artichoke
        "(O)90",  // Cactus Fruit
        "(O)284"  // Beet
    };

    public static bool IsExcluded(ResolvedItem item) => IsExcluded(item.QualifiedItemId);

    public static bool IsExcluded(string qualifiedItemId)
    {
        if (string.IsNullOrEmpty(qualifiedItemId))
            return false;
        if (AlwaysExcluded.Contains(qualifiedItemId))
            return true;
        if (Game1.year < 2 && Year1Excluded.Contains(qualifiedItemId))
            return true;
        return false;
    }
}
