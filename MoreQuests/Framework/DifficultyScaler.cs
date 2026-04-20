using System;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework;

/// <summary>
/// Calculates quest difficulty tier based on player skill levels and time played.
/// </summary>
internal sealed class DifficultyScaler
{
    public DifficultyTier GetTier(QuestCategory category)
    {
        int year = Game1.year;
        int relevantSkill = GetRelevantSkillLevel(category);

        if (year >= 3 && relevantSkill >= 10)
            return DifficultyTier.Expert;
        if (year >= 2 && relevantSkill >= 7)
            return DifficultyTier.Advanced;
        if (relevantSkill >= 4)
            return DifficultyTier.Intermediate;

        return DifficultyTier.Beginner;
    }

    public int ScaleQuantity(int baseAmount, DifficultyTier tier)
    {
        return tier switch
        {
            DifficultyTier.Beginner => baseAmount,
            DifficultyTier.Intermediate => (int)(baseAmount * 1.5),
            DifficultyTier.Advanced => baseAmount * 2,
            DifficultyTier.Expert => baseAmount * 3,
            _ => baseAmount
        };
    }

    public int ScaleRewardGold(int baseGold, DifficultyTier tier)
    {
        return tier switch
        {
            DifficultyTier.Beginner => baseGold,
            DifficultyTier.Intermediate => (int)(baseGold * 1.5),
            DifficultyTier.Advanced => baseGold * 2,
            DifficultyTier.Expert => (int)(baseGold * 3.5),
            _ => baseGold
        };
    }

    public int GetMinQuality(DifficultyTier tier)
    {
        return tier switch
        {
            DifficultyTier.Beginner => 0,      // any quality
            DifficultyTier.Intermediate => 0,   // any quality still
            DifficultyTier.Advanced => 1,       // silver+
            DifficultyTier.Expert => 2,         // gold+
            _ => 0
        };
    }

    private static int GetRelevantSkillLevel(QuestCategory category)
    {
        var player = Game1.player;
        return category switch
        {
            QuestCategory.Farming => player.FarmingLevel,
            QuestCategory.Fishing => player.FishingLevel,
            QuestCategory.Mining => player.MiningLevel,
            QuestCategory.Combat => player.CombatLevel,
            QuestCategory.Foraging => player.ForagingLevel,
            QuestCategory.Cooking => Math.Max(player.FarmingLevel, player.ForagingLevel),
            QuestCategory.Social => 0, // no skill scaling for social
            QuestCategory.Animal => player.FarmingLevel,
            _ => 0
        };
    }
}

internal enum DifficultyTier
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}

internal enum QuestCategory
{
    Farming,
    Fishing,
    Mining,
    Combat,
    Foraging,
    Cooking,
    Social,
    Animal,
    Festival
}
