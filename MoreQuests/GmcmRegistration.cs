using MoreQuestsFramework;
using StardewModdingAPI;

namespace MoreQuests;

/// Registers the content mod's per-quest content settings with Generic Mod Config Menu.
/// Engine-wide tunables (deadlines, friendship/gold sizes, vanilla quest weights) live
/// on the framework's own GMCM page.
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

        api.AddSectionTitle(manifest, () => t.Get("config.section.toggles"));
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
        api.AddBoolOption(manifest,
            () => ModEntry.Config.EnableAdventurersGuildBoard,
            v => ModEntry.Config.EnableAdventurersGuildBoard = v,
            () => t.Get("config.enableAdventurersGuildBoard"),
            () => t.Get("config.enableAdventurersGuildBoard.tooltip"));

        api.AddSectionTitle(manifest, () => t.Get("config.section.discounts"));
        AddInt(api, manifest, t, "ShopDiscountPercent", () => ModEntry.Config.ShopDiscountPercent, v => ModEntry.Config.ShopDiscountPercent = v, 0, 100);
        AddInt(api, manifest, t, "ShopDiscountDurationDays", () => ModEntry.Config.ShopDiscountDurationDays, v => ModEntry.Config.ShopDiscountDurationDays = v, 1, 14);
        AddInt(api, manifest, t, "SeedShopDiscountPercent", () => ModEntry.Config.SeedShopDiscountPercent, v => ModEntry.Config.SeedShopDiscountPercent = v, 0, 100);
        AddInt(api, manifest, t, "SeedShopDiscountDurationDays", () => ModEntry.Config.SeedShopDiscountDurationDays, v => ModEntry.Config.SeedShopDiscountDurationDays = v, 1, 14);

        api.AddSectionTitle(manifest, () => t.Get("config.section.quantities"));
        AddInt(api, manifest, t, "FishHaulMediumQty", () => ModEntry.Config.FishHaulMediumQty, v => ModEntry.Config.FishHaulMediumQty = v, 1, 200);
        AddInt(api, manifest, t, "FishHaulLargeQty", () => ModEntry.Config.FishHaulLargeQty, v => ModEntry.Config.FishHaulLargeQty = v, 1, 500);
        AddInt(api, manifest, t, "FestivalFishQty", () => ModEntry.Config.FestivalFishQty, v => ModEntry.Config.FestivalFishQty = v, 1, 100);
        AddInt(api, manifest, t, "CropMassiveQty", () => ModEntry.Config.CropMassiveQty, v => ModEntry.Config.CropMassiveQty = v, 1, 500);
        AddInt(api, manifest, t, "HaySupplyBaseQty", () => ModEntry.Config.HaySupplyBaseQty, v => ModEntry.Config.HaySupplyBaseQty = v, 1, 100);
        AddInt(api, manifest, t, "SkullCavernMaxLevel", () => ModEntry.Config.SkullCavernMaxLevel, v => ModEntry.Config.SkullCavernMaxLevel = v, 5, 500);
        AddInt(api, manifest, t, "WeeklySpecialCommonMaxIngredients", () => ModEntry.Config.WeeklySpecialCommonMaxIngredients, v => ModEntry.Config.WeeklySpecialCommonMaxIngredients = v, 1, 10);
        AddInt(api, manifest, t, "WeeklySpecialComplexMinIngredients", () => ModEntry.Config.WeeklySpecialComplexMinIngredients, v => ModEntry.Config.WeeklySpecialComplexMinIngredients = v, 1, 10);
        AddInt(api, manifest, t, "GrandFeastRecipeCount", () => ModEntry.Config.GrandFeastRecipeCount, v => ModEntry.Config.GrandFeastRecipeCount = v, 1, 6);
        AddInt(api, manifest, t, "GusFestivalFeastIngredientCount", () => ModEntry.Config.GusFestivalFeastIngredientCount, v => ModEntry.Config.GusFestivalFeastIngredientCount = v, 1, 6);
        AddInt(api, manifest, t, "FestivalBiasLuauMagnitude", () => ModEntry.Config.FestivalBiasLuauMagnitude, v => ModEntry.Config.FestivalBiasLuauMagnitude = v, 0, 5);
        AddInt(api, manifest, t, "FestivalBiasFairMagnitude", () => ModEntry.Config.FestivalBiasFairMagnitude, v => ModEntry.Config.FestivalBiasFairMagnitude = v, 0, 100);

        // Per-quest weight overrides (for content-mod quests) live in the framework's GMCM page
        // alongside the vanilla quest weights, since the framework owns the registry view.
    }

    private static void AddInt(IGenericModConfigMenuApi api, IManifest manifest, ITranslationHelper t,
        string key, System.Func<int> get, System.Action<int> set, int min, int max)
    {
        api.AddNumberOption(manifest, get, set,
            () => t.Get($"config.{key}"),
            () => t.Get($"config.{key}.tooltip"),
            min: min, max: max);
    }
}
