using System.Collections.Generic;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;

namespace MoreQuestsFramework.Config;

internal static class GmcmRegistration
{
    // Stable display order; categories not listed are appended alphabetically.
    private static readonly string[] CategoryOrder = new[]
    {
        "Vanilla", "Farming", "Fishing", "Mining", "Foraging",
        "Social", "Seasonal", "Cooking", "Animal", "Festival",
    };

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

        // Group by the ID prefix before the first '.'.
        var byCategory = new Dictionary<string, List<IQuestDefinition>>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var def in registry.All)
        {
            if (def.Kind != PostingKind.DailyBoard)
                continue;
            string id = def.Id ?? string.Empty;
            int dot = id.IndexOf('.');
            string category = dot > 0 ? id.Substring(0, dot) : "Other";
            if (!byCategory.TryGetValue(category, out var list))
            {
                list = new List<IQuestDefinition>();
                byCategory[category] = list;
            }
            list.Add(def);
        }

        api.AddSectionTitle(manifest, () => t.Get("config.section.questBoard"));
        api.AddNumberOption(manifest,
            () => ModEntry.Config.QuestsPerDay,
            v => ModEntry.Config.QuestsPerDay = v,
            () => t.Get("config.questsPerDay"),
            () => t.Get("config.questsPerDay.tooltip"),
            min: 1, max: 20);
        api.AddNumberOption(manifest,
            () => System.Math.Clamp(ModEntry.Config.SpecialOrdersBoardPages, 1, 5),
            v => ModEntry.Config.SpecialOrdersBoardPages = System.Math.Clamp(v, 1, 5),
            () => t.Get("config.specialOrdersBoardPages"),
            () => t.Get("config.specialOrdersBoardPages.tooltip"),
            min: 1, max: 5);

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

        api.AddSectionTitle(manifest, () => t.Get("config.section.exclusions"),
            () => t.Get("config.section.exclusions.tooltip"));
        api.AddTextOption(manifest,
            () => JoinIneligibleGivers(ModEntry.Config.IneligibleGivers),
            v => ModEntry.Config.IneligibleGivers = ParseIneligibleGivers(v),
            () => t.Get("config.ineligibleGivers"),
            () => t.Get("config.ineligibleGivers.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.MailFallbackForExcludedGivers,
            v => ModEntry.Config.MailFallbackForExcludedGivers = v,
            () => t.Get("config.mailFallbackForExcludedGivers"),
            () => t.Get("config.mailFallbackForExcludedGivers.tooltip"));

        // BuildingBuilt and MailReceived defs are skipped: their triggers are diff-based,
        // so a failed chance roll loses the quest forever. Matched by the same skip in
        // QuestPipeline.GenerateTriggered.
        var mailDefs = new List<IQuestDefinition>();
        foreach (var def in registry.All)
        {
            if (def.Kind != PostingKind.Mail)
                continue;
            var src = registry.EffectiveSource(def);
            if (src == TriggerSource.BuildingBuilt || src == TriggerSource.MailReceived)
                continue;
            mailDefs.Add(def);
        }

        api.AddSectionTitle(manifest, () => t.Get("config.section.weights"),
            () => t.Get("config.section.weights.tooltip"));
        foreach (var category in OrderedCategories(byCategory.Keys))
        {
            string pageId = "weights." + category.ToLowerInvariant();
            api.AddPageLink(manifest, pageId,
                () => t.Get($"config.page.weights.{category.ToLowerInvariant()}", new { category }).Default($"{category} quest weights"),
                () => t.Get("config.page.weights.tooltip", new { category }));
        }
        if (mailDefs.Count > 0)
        {
            api.AddPageLink(manifest, "mailChances",
                () => t.Get("config.page.mailChances"),
                () => t.Get("config.page.mailChances.tooltip"));
        }

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

        api.AddSectionTitle(manifest, () => t.Get("config.section.advanced"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.DebugLogging,
            v => ModEntry.Config.DebugLogging = v,
            () => t.Get("config.debugLogging"),
            () => t.Get("config.debugLogging.tooltip"));

        foreach (var category in OrderedCategories(byCategory.Keys))
        {
            string pageId = "weights." + category.ToLowerInvariant();
            string capturedCategory = category;
            api.AddPage(manifest, pageId,
                () => t.Get($"config.page.weights.{capturedCategory.ToLowerInvariant()}", new { category = capturedCategory })
                    .Default($"{capturedCategory} quest weights"));

            foreach (var def in byCategory[category])
            {
                string id = def.Id;
                int defaultWeight = def.DefaultWeight;
                api.AddNumberOption(manifest,
                    () => ModEntry.Config.QuestWeights.TryGetValue(id, out int w) ? w : defaultWeight,
                    v => ModEntry.Config.QuestWeights[id] = v,
                    () => t.Get($"config.weight.{id}", new { fallback = id }),
                    () => BuildWeightTooltip(t, id),
                    min: 0, max: 100);
            }
        }

        if (mailDefs.Count > 0)
        {
            api.AddPage(manifest, "mailChances", () => t.Get("config.page.mailChances"));
            foreach (var def in mailDefs)
            {
                string id = def.Id;
                api.AddNumberOption(manifest,
                    () => ModEntry.Config.MailQuestChancePercent.TryGetValue(id, out int p) ? p : 100,
                    v => ModEntry.Config.MailQuestChancePercent[id] = v,
                    () => t.Get($"config.mailChance.{id}", new { fallback = id })
                        .Default($"{id} mail chance %"),
                    () => t.Get($"config.mailChance.{id}.tooltip", new { id })
                        .Default($"Chance (0 to 100) that {id} mail fires when its trigger condition matches. 100 = always, 0 disables the quest."),
                    min: 0, max: 100);
            }
        }
    }

    private static IEnumerable<string> OrderedCategories(IEnumerable<string> categories)
    {
        var set = new HashSet<string>(categories, System.StringComparer.OrdinalIgnoreCase);
        foreach (var known in CategoryOrder)
        {
            if (set.Remove(known))
                yield return known;
        }
        var rest = new List<string>(set);
        rest.Sort(System.StringComparer.OrdinalIgnoreCase);
        foreach (var c in rest)
            yield return c;
    }

    private static string BuildWeightTooltip(ITranslationHelper t, string id)
    {
        string baseLine = t.Get("config.weight.tooltip", new { id }).ToString();
        var constraint = t.Get($"config.weight.{id}.constraints");
        if (!constraint.HasValue())
            return baseLine;
        return baseLine + "\n\n" + t.Get("config.weight.constraintsLabel").Default("Requirements:") + " " + constraint;
    }

    private static string JoinIneligibleGivers(List<string>? list)
    {
        if (list == null || list.Count == 0)
            return string.Empty;
        return string.Join(", ", list);
    }

    private static List<string> ParseIneligibleGivers(string? raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(new[] { ',', ';', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            if (seen.Add(trimmed))
                result.Add(trimmed);
        }
        return result;
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
