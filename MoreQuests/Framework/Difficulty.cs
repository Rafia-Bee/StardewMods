using System;
using StardewValley;

namespace MoreQuests.Framework;

internal enum DifficultyTier
{
    Beginner,
    Intermediate,
    Advanced,
    Expert,
    Special
}

internal enum DeadlineKind
{
    Short,
    Medium,
    Long,
    Extended,
    None
}

internal enum QuestCategory
{
    Animal,
    Cooking,
    Farming,
    Festival,
    Fishing,
    Foraging,
    Mining,
    Seasonal,
    Social
}

internal static class Difficulty
{
    public static DifficultyTier TierForSkill(int skillLevel) =>
        skillLevel switch
        {
            >= 10 => DifficultyTier.Expert,
            >= 7 => DifficultyTier.Advanced,
            >= 4 => DifficultyTier.Intermediate,
            _ => DifficultyTier.Beginner
        };

    public static int GetSkillLevel(QuestCategory category)
    {
        var p = Game1.player;
        return category switch
        {
            QuestCategory.Farming => p.FarmingLevel,
            QuestCategory.Fishing => p.FishingLevel,
            QuestCategory.Mining => p.MiningLevel,
            QuestCategory.Foraging => p.ForagingLevel,
            QuestCategory.Cooking => Math.Max(p.FarmingLevel, p.ForagingLevel),
            QuestCategory.Animal => p.FarmingLevel,
            _ => 0
        };
    }

    public static int GoldBase(DifficultyTier tier, ModConfig cfg) =>
        tier switch
        {
            DifficultyTier.Beginner => cfg.GoldBeginnerBase,
            DifficultyTier.Intermediate => cfg.GoldIntermediateBase,
            DifficultyTier.Advanced => cfg.GoldAdvancedBase,
            DifficultyTier.Expert => cfg.GoldExpertBase,
            _ => cfg.GoldBasicBase
        };

    public static int Deadline(DeadlineKind kind, ModConfig cfg) =>
        kind switch
        {
            DeadlineKind.Short => cfg.DeadlineShort,
            DeadlineKind.Medium => cfg.DeadlineMedium,
            DeadlineKind.Long => cfg.DeadlineLong,
            DeadlineKind.Extended => cfg.DeadlineExtended,
            DeadlineKind.None => cfg.DeadlineNone,
            _ => cfg.DeadlineMedium
        };
}
