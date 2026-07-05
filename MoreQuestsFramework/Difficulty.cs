using System;
using MoreQuestsFramework.Config;
using StardewValley;

namespace MoreQuestsFramework;

public enum DifficultyTier
{
    Beginner,
    Intermediate,
    Advanced,
    Expert,
    Special
}

public enum DeadlineKind
{
    Short,
    Medium,
    Long,
    Extended,
    None
}

// The nine built-in category ids. Categories are strings now (authors can register
// their own via the Categories asset / RegisterCategory), so these are just the
// well-known names the framework seeds. Kept as a class of const strings rather than
// an enum so existing C# call sites like QuestCategory.Farming keep compiling.
public static class QuestCategory
{
    public const string Animal = "Animal";
    public const string Cooking = "Cooking";
    public const string Farming = "Farming";
    public const string Festival = "Festival";
    public const string Fishing = "Fishing";
    public const string Foraging = "Foraging";
    public const string Mining = "Mining";
    public const string Seasonal = "Seasonal";
    public const string Social = "Social";
}

public static class Difficulty
{
    // 1 heart of friendship = 250 points in the vanilla ladder. Shared so heart-gate checks
    // and max-heart math don't each inline the magic number.
    public const int FriendshipPointsPerHeart = 250;

    // Not called yet. Reserved for the difficulty-scaling rework, which will use this to turn
    // a skill level into a tier instead of the inline per-quest math. Kept until then (pairs
    // with GoldBase below).
    public static DifficultyTier TierForSkill(int skillLevel) =>
        skillLevel switch
        {
            >= 10 => DifficultyTier.Expert,
            >= 7 => DifficultyTier.Advanced,
            >= 4 => DifficultyTier.Intermediate,
            _ => DifficultyTier.Beginner
        };

    // Reads the skill a category scales against from its registered definition, then
    // resolves that to a level. Unknown category or unresolved skill scales 0.
    public static int GetSkillLevel(string category)
        => ResolveSkillLevel(ModEntry.Categories?.SkillFor(category));

    // Skill resolution: a vanilla skill name reads Farmer.*Level; "Cooking" routes to
    // whichever cooking-skill mod is installed (0 if none); "None"/empty scales 0;
    // anything else is treated as a SpaceCore custom skill id.
    private static int ResolveSkillLevel(string? skill)
    {
        var p = Game1.player;
        if (p == null || string.IsNullOrWhiteSpace(skill))
            return 0;
        switch (skill.ToLowerInvariant())
        {
            case "farming": return p.FarmingLevel;
            case "fishing": return p.FishingLevel;
            case "mining": return p.MiningLevel;
            case "foraging": return p.ForagingLevel;
            case "combat": return p.CombatLevel;
            case "cooking": return ModCompat.GetCookingLevel(ModEntry.Instance.Helper.ModRegistry);
            case "none": return 0;
            default: return ModCompat.GetCustomSkillLevel(skill);
        }
    }

    // Not called yet. Pairs with TierForSkill for the difficulty-scaling rework: maps a tier
    // to its gold base from config. Kept until that lands.
    public static int GoldBase(DifficultyTier tier, MoreQuestsFrameworkConfig cfg) =>
        tier switch
        {
            DifficultyTier.Beginner => cfg.GoldBeginnerBase,
            DifficultyTier.Intermediate => cfg.GoldIntermediateBase,
            DifficultyTier.Advanced => cfg.GoldAdvancedBase,
            DifficultyTier.Expert => cfg.GoldExpertBase,
            _ => cfg.GoldBasicBase
        };

    public static int Deadline(DeadlineKind kind, MoreQuestsFrameworkConfig cfg) =>
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
