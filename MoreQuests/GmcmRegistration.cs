using MoreQuestsFramework;
using StardewModdingAPI;

namespace MoreQuests;

/// Registers per-quest content settings with GMCM. Engine-wide tunables live on the
/// framework's own GMCM page. Quest-specific configs are grouped onto category subpages
/// (Farming, Fishing, Mining, etc.) so the root page stays scannable as more quests land.
internal static class GmcmRegistration
{
    private const string PageFarming = "farming";
    private const string PageFishing = "fishing";
    private const string PageMining = "mining";
    private const string PageForaging = "foraging";
    private const string PageCooking = "cooking";
    private const string PageSocial = "social";
    private const string PageAnimal = "animal";
    private const string PageFestival = "festival";
    private const string PageAdventureBoard = "adventureBoard";

    public static void Register(IModHelper helper, IManifest manifest)
    {
        var api = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(ModCompat.GenericModConfigMenu);
        if (api == null)
            return;

        var t = helper.Translation;

        api.Register(
            mod: manifest,
            reset: () =>
            {
                ModEntry.Config = new ModConfig();
                ModEntry.ApplyAdventureBoardConfig();
            },
            save: () =>
            {
                helper.WriteConfig(ModEntry.Config);
                ModEntry.ApplyAdventureBoardConfig();
            }
        );

        // ----- Root page -----
        // Category master toggles and the consequence toggle live on the framework's GMCM
        // page now. These page links open the per-category quest tuning subpages.
        api.AddSectionTitle(manifest, () => t.Get("config.section.categories"));
        api.AddPageLink(manifest, PageFarming, () => t.Get("config.page.farming"), () => t.Get("config.page.farming.tooltip"));
        api.AddPageLink(manifest, PageFishing, () => t.Get("config.page.fishing"), () => t.Get("config.page.fishing.tooltip"));
        api.AddPageLink(manifest, PageMining, () => t.Get("config.page.mining"), () => t.Get("config.page.mining.tooltip"));
        api.AddPageLink(manifest, PageForaging, () => t.Get("config.page.foraging"), () => t.Get("config.page.foraging.tooltip"));
        api.AddPageLink(manifest, PageCooking, () => t.Get("config.page.cooking"), () => t.Get("config.page.cooking.tooltip"));
        api.AddPageLink(manifest, PageSocial, () => t.Get("config.page.social"), () => t.Get("config.page.social.tooltip"));
        api.AddPageLink(manifest, PageAnimal, () => t.Get("config.page.animal"), () => t.Get("config.page.animal.tooltip"));
        api.AddPageLink(manifest, PageFestival, () => t.Get("config.page.festival"), () => t.Get("config.page.festival.tooltip"));
        api.AddPageLink(manifest, PageAdventureBoard, () => t.Get("config.page.adventureBoard"), () => t.Get("config.page.adventureBoard.tooltip"));

        api.AddSectionTitle(manifest, () => t.Get("config.section.itemExclusions"));
        AddText(api, manifest, t, "RewardExclusionItemIds",
            () => ModEntry.Config.RewardExclusionItemIds,
            v => ModEntry.Config.RewardExclusionItemIds = v);
        api.AddBoolOption(manifest,
            () => ModEntry.Config.ExcludeListAppliesToRequests,
            v => ModEntry.Config.ExcludeListAppliesToRequests = v,
            () => t.Get("config.ExcludeListAppliesToRequests"),
            () => t.Get("config.ExcludeListAppliesToRequests.tooltip"));

        api.AddSectionTitle(manifest, () => t.Get("config.section.advanced"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.DebugLogging,
            v => ModEntry.Config.DebugLogging = v,
            () => t.Get("config.debugLogging"),
            () => t.Get("config.debugLogging.tooltip"));

        // ----- Farming -----
        api.AddPage(manifest, PageFarming, () => t.Get("config.page.farming"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.massiveHarvest"));
        AddInt(api, manifest, t, "CropMassiveQty", () => ModEntry.Config.CropMassiveQty, v => ModEntry.Config.CropMassiveQty = v, 1, 500);
        api.AddSectionTitle(manifest, () => t.Get("config.section.pierresStockUp"));
        AddInt(api, manifest, t, "RequestVariationCount", () => ModEntry.Config.RequestVariationCount, v => ModEntry.Config.RequestVariationCount = v, 2, 5);
        AddInt(api, manifest, t, "SeedShopDiscountPercent", () => ModEntry.Config.SeedShopDiscountPercent, v => ModEntry.Config.SeedShopDiscountPercent = v, 0, 100);
        AddInt(api, manifest, t, "SeedShopDiscountDurationDays", () => ModEntry.Config.SeedShopDiscountDurationDays, v => ModEntry.Config.SeedShopDiscountDurationDays = v, 1, 14);

        // ----- Fishing -----
        api.AddPage(manifest, PageFishing, () => t.Get("config.page.fishing"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.fishHauls"));
        AddInt(api, manifest, t, "FishHaulMediumQty", () => ModEntry.Config.FishHaulMediumQty, v => ModEntry.Config.FishHaulMediumQty = v, 1, 200);
        AddInt(api, manifest, t, "FishHaulLargeQty", () => ModEntry.Config.FishHaulLargeQty, v => ModEntry.Config.FishHaulLargeQty = v, 1, 500);
        api.AddSectionTitle(manifest, () => t.Get("config.section.knowYourWaters"));
        AddInt(api, manifest, t, "KnowYourWatersMaxFish", () => ModEntry.Config.KnowYourWatersMaxFish, v => ModEntry.Config.KnowYourWatersMaxFish = v, 0, 30);

        // ----- Mining -----
        api.AddPage(manifest, PageMining, () => t.Get("config.page.mining"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.skullCavernDeepDive"));
        AddInt(api, manifest, t, "SkullCavernMaxLevel", () => ModEntry.Config.SkullCavernMaxLevel, v => ModEntry.Config.SkullCavernMaxLevel = v, 5, 500);
        AddInt(api, manifest, t, "MagicianDropRadius", () => ModEntry.Config.MagicianDropRadius, v => ModEntry.Config.MagicianDropRadius = v, 2, 6);

        // ----- Foraging -----
        api.AddPage(manifest, PageForaging, () => t.Get("config.page.foraging"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.feedWildCritters"));
        AddInt(api, manifest, t, "FeedWildCrittersPerPetCredit", () => ModEntry.Config.FeedWildCrittersPerPetCredit, v => ModEntry.Config.FeedWildCrittersPerPetCredit = v, 1, 20);
        AddInt(api, manifest, t, "MarniePetDiscountPercent", () => ModEntry.Config.MarniePetDiscountPercent, v => ModEntry.Config.MarniePetDiscountPercent = v, 1, 100);
        AddInt(api, manifest, t, "MarniePetCreditExpiryDays", () => ModEntry.Config.MarniePetCreditExpiryDays, v => ModEntry.Config.MarniePetCreditExpiryDays = v, 1, 56);

        // ----- Cooking -----
        api.AddPage(manifest, PageCooking, () => t.Get("config.page.cooking"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.weeklySpecial"));
        AddInt(api, manifest, t, "WeeklySpecialComplexMinIngredients", () => ModEntry.Config.WeeklySpecialComplexMinIngredients, v => ModEntry.Config.WeeklySpecialComplexMinIngredients = v, 1, 10);

        // ----- Social -----
        api.AddPage(manifest, PageSocial, () => t.Get("config.page.social"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.checkOnFriends"));
        AddInt(api, manifest, t, "CheckOnFriendsCount", () => ModEntry.Config.CheckOnFriendsCount, v => ModEntry.Config.CheckOnFriendsCount = v, 1, 10);
        api.AddSectionTitle(manifest, () => t.Get("config.section.emilyHousewarming"));
        AddInt(api, manifest, t, "EmilyHousewarmingCount", () => ModEntry.Config.EmilyHousewarmingCount, v => ModEntry.Config.EmilyHousewarmingCount = v, 3, 20);
        api.AddSectionTitle(manifest, () => t.Get("config.section.morrisQualityControl"));
        AddInt(api, manifest, t, "MorrisQualityControlMaxCropPrice", () => ModEntry.Config.MorrisQualityControlMaxCropPrice, v => ModEntry.Config.MorrisQualityControlMaxCropPrice = v, 1, 1000);

        // Seasonal has no per-quest settings of its own yet (its only knob was the category
        // toggle, now on the framework page), so there's no Seasonal subpage for now. It
        // comes back once it has real settings to show, like the spring cleaning count.

        // ----- Animal -----
        api.AddPage(manifest, PageAnimal, () => t.Get("config.page.animal"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.leahPainting"));
        api.AddTextOption(manifest,
            getValue: () => ModEntry.Config.LeahPaintingFrame,
            setValue: v => ModEntry.Config.LeahPaintingFrame = v,
            name: () => t.Get("config.LeahPaintingFrame"),
            tooltip: () => t.Get("config.LeahPaintingFrame.tooltip"),
            allowedValues: ModEntry.LeahPaintingFrameOptions);

        // ----- Festival -----
        api.AddPage(manifest, PageFestival, () => t.Get("config.page.festival"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.SecretGiftHintEnabled,
            v => ModEntry.Config.SecretGiftHintEnabled = v,
            () => t.Get("config.secretGiftHint"),
            () => t.Get("config.secretGiftHint.tooltip"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.festivalShopDiscount"));
        AddInt(api, manifest, t, "ShopDiscountPercent", () => ModEntry.Config.ShopDiscountPercent, v => ModEntry.Config.ShopDiscountPercent = v, 0, 100);
        AddInt(api, manifest, t, "ShopDiscountDurationDays", () => ModEntry.Config.ShopDiscountDurationDays, v => ModEntry.Config.ShopDiscountDurationDays = v, 1, 14);
        api.AddSectionTitle(manifest, () => t.Get("config.section.gusFeasts"));
        AddInt(api, manifest, t, "GusFestivalFeastIngredientCount", () => ModEntry.Config.GusFestivalFeastIngredientCount, v => ModEntry.Config.GusFestivalFeastIngredientCount = v, 1, 6);
        api.AddSectionTitle(manifest, () => t.Get("config.section.luauFairBias"));
        AddInt(api, manifest, t, "FestivalBiasLuauMagnitude", () => ModEntry.Config.FestivalBiasLuauMagnitude, v => ModEntry.Config.FestivalBiasLuauMagnitude = v, 0, 4);
        AddInt(api, manifest, t, "FestivalBiasFairMagnitude", () => ModEntry.Config.FestivalBiasFairMagnitude, v => ModEntry.Config.FestivalBiasFairMagnitude = v, 0, 100);
        api.AddTextOption(manifest,
            getValue: () => ModEntry.Config.FairFestivalRewardKind,
            setValue: v => ModEntry.Config.FairFestivalRewardKind = v,
            name: () => t.Get("config.FairFestivalRewardKind"),
            tooltip: () => t.Get("config.FairFestivalRewardKind.tooltip"),
            allowedValues: new[] { "GrangeScoreBonus", "StarTokens" });
        AddInt(api, manifest, t, "FairStarTokensAmount", () => ModEntry.Config.FairStarTokensAmount, v => ModEntry.Config.FairStarTokensAmount = v, 0, 1000);

        // ----- Adventurer's Guild board -----
        api.AddPage(manifest, PageAdventureBoard, () => t.Get("config.page.adventureBoard"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.EnableAdventurersGuildBoard,
            v => ModEntry.Config.EnableAdventurersGuildBoard = v,
            () => t.Get("config.enableAdventurersGuildBoard"),
            () => t.Get("config.enableAdventurersGuildBoard.tooltip"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.adventureBoardPlacement"));
        AddInt(api, manifest, t, "AdventureBoardTileX", () => ModEntry.Config.AdventureBoardTileX, v => ModEntry.Config.AdventureBoardTileX = v, 0, 200);
        AddInt(api, manifest, t, "AdventureBoardTileY", () => ModEntry.Config.AdventureBoardTileY, v => ModEntry.Config.AdventureBoardTileY = v, 0, 200);
        AddInt(api, manifest, t, "AdventureBoardOffsetX", () => ModEntry.Config.AdventureBoardOffsetX, v => ModEntry.Config.AdventureBoardOffsetX = v, -64, 64);
        AddInt(api, manifest, t, "AdventureBoardOffsetY", () => ModEntry.Config.AdventureBoardOffsetY, v => ModEntry.Config.AdventureBoardOffsetY = v, -64, 64);

        // Per-quest weight overrides live on the framework's GMCM page (it owns the registry view).
    }

    private static void AddText(IGenericModConfigMenuApi api, IManifest manifest, ITranslationHelper t,
        string key, System.Func<string> get, System.Action<string> set)
    {
        api.AddTextOption(manifest, get, set,
            () => t.Get($"config.{key}"),
            () => t.Get($"config.{key}.tooltip"));
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
