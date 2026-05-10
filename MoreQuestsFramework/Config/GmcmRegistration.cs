using MoreQuestsFramework.Registry;
using StardewModdingAPI;

namespace MoreQuestsFramework.Config;

/// Registers the framework's engine tunables with Generic Mod Config Menu. Per-quest
/// content settings live in the consuming content mod's own GMCM page.
internal static class GmcmRegistration
{
    public static void Register(IModHelper helper, IManifest manifest, MoreQuestsFrameworkConfig config, QuestRegistry registry, System.Action onReset)
    {
        var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(ModCompat.GenericModConfigMenu);
        if (api == null)
            return;

        var t = helper.Translation;

        api.Register(
            mod: manifest,
            reset: () => { onReset(); },
            save: () => helper.WriteConfig(ModEntry.Config)
        );

        api.AddSectionTitle(manifest, () => t.Get("config.section.questBoard"));
        api.AddNumberOption(manifest,
            () => ModEntry.Config.QuestsPerDay,
            v => ModEntry.Config.QuestsPerDay = v,
            () => t.Get("config.questsPerDay"),
            () => t.Get("config.questsPerDay.tooltip"),
            min: 1, max: 20);
        api.AddNumberOption(manifest,
            () => System.Math.Clamp(ModEntry.Config.SpecialOrdersBoardPages, 1, 3),
            v => ModEntry.Config.SpecialOrdersBoardPages = System.Math.Clamp(v, 1, 3),
            () => t.Get("config.specialOrdersBoardPages"),
            () => t.Get("config.specialOrdersBoardPages.tooltip"),
            min: 1, max: 3);

        api.AddSectionTitle(manifest, () => t.Get("config.section.weights"),
            () => t.Get("config.section.weights.tooltip"));
        foreach (var def in registry.All)
        {
            if (def.Kind != PostingKind.DailyBoard)
                continue;
            string id = def.Id;
            int defaultWeight = def.DefaultWeight;
            api.AddNumberOption(manifest,
                () => ModEntry.Config.QuestWeights.TryGetValue(id, out int w) ? w : defaultWeight,
                v => ModEntry.Config.QuestWeights[id] = v,
                () => t.Get($"config.weight.{id}", new { fallback = id }),
                () => BuildWeightTooltip(t, id),
                min: 0, max: 100);
        }

        api.AddSectionTitle(manifest, () => t.Get("config.section.toggles"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.DifficultyScaling,
            v => ModEntry.Config.DifficultyScaling = v,
            () => t.Get("config.difficultyScaling"),
            () => t.Get("config.difficultyScaling.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.FishingIgnoresVisitedLocations,
            v => ModEntry.Config.FishingIgnoresVisitedLocations = v,
            () => t.Get("config.fishingIgnoresVisitedLocations"),
            () => t.Get("config.fishingIgnoresVisitedLocations.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.ForagingIgnoresVisitedLocations,
            v => ModEntry.Config.ForagingIgnoresVisitedLocations = v,
            () => t.Get("config.foragingIgnoresVisitedLocations"),
            () => t.Get("config.foragingIgnoresVisitedLocations.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.AllowDuplicateGiverPerDay,
            v => ModEntry.Config.AllowDuplicateGiverPerDay = v,
            () => t.Get("config.allowDuplicateGiverPerDay"),
            () => t.Get("config.allowDuplicateGiverPerDay.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.SkipFriendshipQuestsAtMaxHeart,
            v => ModEntry.Config.SkipFriendshipQuestsAtMaxHeart = v,
            () => t.Get("config.skipFriendshipQuestsAtMaxHeart"),
            () => t.Get("config.skipFriendshipQuestsAtMaxHeart.tooltip"));

        api.AddSectionTitle(manifest, () => t.Get("config.section.friendship"));
        AddInt(api, manifest, t, "FriendshipBasic", () => ModEntry.Config.FriendshipBasic, v => ModEntry.Config.FriendshipBasic = v, 0, 500);
        AddInt(api, manifest, t, "FriendshipMid", () => ModEntry.Config.FriendshipMid, v => ModEntry.Config.FriendshipMid = v, 0, 500);
        AddInt(api, manifest, t, "FriendshipIntermediate", () => ModEntry.Config.FriendshipIntermediate, v => ModEntry.Config.FriendshipIntermediate = v, 0, 500);
        AddInt(api, manifest, t, "FriendshipLarge", () => ModEntry.Config.FriendshipLarge, v => ModEntry.Config.FriendshipLarge = v, 0, 1000);
        AddInt(api, manifest, t, "FriendshipMultiSmall", () => ModEntry.Config.FriendshipMultiSmall, v => ModEntry.Config.FriendshipMultiSmall = v, 0, 500);
        AddInt(api, manifest, t, "FriendshipMultiHeart", () => ModEntry.Config.FriendshipMultiHeart, v => ModEntry.Config.FriendshipMultiHeart = v, 0, 1000);

        api.AddSectionTitle(manifest, () => t.Get("config.section.gold"));
        AddInt(api, manifest, t, "GoldBeginnerBase", () => ModEntry.Config.GoldBeginnerBase, v => ModEntry.Config.GoldBeginnerBase = v, 0, 5000);
        AddInt(api, manifest, t, "GoldBasicBase", () => ModEntry.Config.GoldBasicBase, v => ModEntry.Config.GoldBasicBase = v, 0, 5000);
        AddInt(api, manifest, t, "GoldIntermediateBase", () => ModEntry.Config.GoldIntermediateBase, v => ModEntry.Config.GoldIntermediateBase = v, 0, 10000);
        AddInt(api, manifest, t, "GoldAdvancedBase", () => ModEntry.Config.GoldAdvancedBase, v => ModEntry.Config.GoldAdvancedBase = v, 0, 20000);
        AddInt(api, manifest, t, "GoldExpertBase", () => ModEntry.Config.GoldExpertBase, v => ModEntry.Config.GoldExpertBase = v, 0, 50000);

        api.AddSectionTitle(manifest, () => t.Get("config.section.multipliers"));
        AddFloat(api, manifest, t, "RewardMultiplierBelowSell", () => ModEntry.Config.RewardMultiplierBelowSell, v => ModEntry.Config.RewardMultiplierBelowSell = v, 0.1f, 2f);
        AddFloat(api, manifest, t, "RewardMultiplierAboveSell", () => ModEntry.Config.RewardMultiplierAboveSell, v => ModEntry.Config.RewardMultiplierAboveSell = v, 0.1f, 5f);
        AddFloat(api, manifest, t, "RewardMultiplierFishPremium", () => ModEntry.Config.RewardMultiplierFishPremium, v => ModEntry.Config.RewardMultiplierFishPremium = v, 0.1f, 5f);

        api.AddSectionTitle(manifest, () => t.Get("config.section.deadlines"));
        AddInt(api, manifest, t, "DeadlineShort", () => ModEntry.Config.DeadlineShort, v => ModEntry.Config.DeadlineShort = v, 1, 28);
        AddInt(api, manifest, t, "DeadlineMedium", () => ModEntry.Config.DeadlineMedium, v => ModEntry.Config.DeadlineMedium = v, 1, 28);
        AddInt(api, manifest, t, "DeadlineLong", () => ModEntry.Config.DeadlineLong, v => ModEntry.Config.DeadlineLong = v, 1, 28);
        AddInt(api, manifest, t, "DeadlineExtended", () => ModEntry.Config.DeadlineExtended, v => ModEntry.Config.DeadlineExtended = v, 1, 56);

        api.AddSectionTitle(manifest, () => t.Get("config.section.consequences"));
        AddInt(api, manifest, t, "ConsequenceGraceDays", () => ModEntry.Config.ConsequenceGraceDays, v => ModEntry.Config.ConsequenceGraceDays = v, 1, 60);
    }

    /// Combines the generic weight tooltip with a per-quest constraint hint, if one is
    /// defined. Definitions whose `IsAvailable` is unconditional don't get a hint.
    private static string BuildWeightTooltip(ITranslationHelper t, string id)
    {
        string baseLine = t.Get("config.weight.tooltip", new { id }).ToString();
        var constraint = t.Get($"config.weight.{id}.constraints");
        if (!constraint.HasValue())
            return baseLine;
        return baseLine + "\n\n" + t.Get("config.weight.constraintsLabel").Default("Requirements:") + " " + constraint;
    }

    private static void AddInt(IGenericModConfigMenuApi api, IManifest manifest, ITranslationHelper t,
        string key, System.Func<int> get, System.Action<int> set, int min, int max)
    {
        api.AddNumberOption(manifest, get, set,
            () => t.Get($"config.{key}"),
            () => t.Get($"config.{key}.tooltip"),
            min: min, max: max);
    }

    private static void AddFloat(IGenericModConfigMenuApi api, IManifest manifest, ITranslationHelper t,
        string key, System.Func<float> get, System.Action<float> set, float min, float max)
    {
        api.AddNumberOption(manifest, get, set,
            () => t.Get($"config.{key}"),
            () => t.Get($"config.{key}.tooltip"),
            min: min, max: max, interval: 0.05f);
    }
}
