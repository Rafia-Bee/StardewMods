using StardewModdingAPI;

namespace MoreQuests.Framework;

internal static class GmcmRegistration
{
    public static void Register(IModHelper helper, IManifest manifest)
    {
        var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(ModCompat.GenericModConfigMenu);
        if (api == null)
            return;

        var t = helper.Translation;

        api.Register(
            mod: manifest,
            reset: () => ModEntry.Config = new ModConfig(),
            save: () => helper.WriteConfig(ModEntry.Config)
        );

        api.AddSectionTitle(manifest, () => t.Get("config.section.questBoard"));
        api.AddNumberOption(manifest,
            () => ModEntry.Config.QuestsPerDay,
            v => ModEntry.Config.QuestsPerDay = v,
            () => t.Get("config.questsPerDay"),
            () => t.Get("config.questsPerDay.tooltip"),
            min: 1, max: 8);

        api.AddSectionTitle(manifest, () => t.Get("config.section.toggles"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.DifficultyScaling,
            v => ModEntry.Config.DifficultyScaling = v,
            () => t.Get("config.difficultyScaling"),
            () => t.Get("config.difficultyScaling.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.ConsequencesEnabled,
            v => ModEntry.Config.ConsequencesEnabled = v,
            () => t.Get("config.consequences"),
            () => t.Get("config.consequences.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.IncludeModdedItems,
            v => ModEntry.Config.IncludeModdedItems = v,
            () => t.Get("config.moddedItems"),
            () => t.Get("config.moddedItems.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.IncludeModdedNPCs,
            v => ModEntry.Config.IncludeModdedNPCs = v,
            () => t.Get("config.moddedNPCs"),
            () => t.Get("config.moddedNPCs.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.FestivalQuestsEnabled,
            v => ModEntry.Config.FestivalQuestsEnabled = v,
            () => t.Get("config.festivalQuests"),
            () => t.Get("config.festivalQuests.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.AnimalQuestsEnabled,
            v => ModEntry.Config.AnimalQuestsEnabled = v,
            () => t.Get("config.animalQuests"),
            () => t.Get("config.animalQuests.tooltip"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.SecretGiftHintEnabled,
            v => ModEntry.Config.SecretGiftHintEnabled = v,
            () => t.Get("config.secretGiftHint"),
            () => t.Get("config.secretGiftHint.tooltip"));

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

        api.AddSectionTitle(manifest, () => t.Get("config.section.discounts"));
        AddInt(api, manifest, t, "ShopDiscountPercent", () => ModEntry.Config.ShopDiscountPercent, v => ModEntry.Config.ShopDiscountPercent = v, 0, 100);
        AddInt(api, manifest, t, "ShopDiscountDurationDays", () => ModEntry.Config.ShopDiscountDurationDays, v => ModEntry.Config.ShopDiscountDurationDays = v, 1, 14);
        AddInt(api, manifest, t, "SeedShopDiscountPercent", () => ModEntry.Config.SeedShopDiscountPercent, v => ModEntry.Config.SeedShopDiscountPercent = v, 0, 100);
        AddInt(api, manifest, t, "SeedShopDiscountDurationDays", () => ModEntry.Config.SeedShopDiscountDurationDays, v => ModEntry.Config.SeedShopDiscountDurationDays = v, 1, 14);

        api.AddSectionTitle(manifest, () => t.Get("config.section.deadlines"));
        AddInt(api, manifest, t, "DeadlineShort", () => ModEntry.Config.DeadlineShort, v => ModEntry.Config.DeadlineShort = v, 1, 28);
        AddInt(api, manifest, t, "DeadlineMedium", () => ModEntry.Config.DeadlineMedium, v => ModEntry.Config.DeadlineMedium = v, 1, 28);
        AddInt(api, manifest, t, "DeadlineLong", () => ModEntry.Config.DeadlineLong, v => ModEntry.Config.DeadlineLong = v, 1, 28);
        AddInt(api, manifest, t, "DeadlineExtended", () => ModEntry.Config.DeadlineExtended, v => ModEntry.Config.DeadlineExtended = v, 1, 56);

        api.AddSectionTitle(manifest, () => t.Get("config.section.quantities"));
        AddInt(api, manifest, t, "FishHaulMediumQty", () => ModEntry.Config.FishHaulMediumQty, v => ModEntry.Config.FishHaulMediumQty = v, 1, 200);
        AddInt(api, manifest, t, "FishHaulLargeQty", () => ModEntry.Config.FishHaulLargeQty, v => ModEntry.Config.FishHaulLargeQty = v, 1, 500);
        AddInt(api, manifest, t, "FestivalFishQty", () => ModEntry.Config.FestivalFishQty, v => ModEntry.Config.FestivalFishQty = v, 1, 100);
        AddInt(api, manifest, t, "CropMassiveQty", () => ModEntry.Config.CropMassiveQty, v => ModEntry.Config.CropMassiveQty = v, 1, 500);
        AddInt(api, manifest, t, "HaySupplyBaseQty", () => ModEntry.Config.HaySupplyBaseQty, v => ModEntry.Config.HaySupplyBaseQty = v, 1, 100);
        AddInt(api, manifest, t, "SkullCavernMaxLevel", () => ModEntry.Config.SkullCavernMaxLevel, v => ModEntry.Config.SkullCavernMaxLevel = v, 5, 500);
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
