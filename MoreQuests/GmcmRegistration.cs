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
    private const string PageSeasonal = "seasonal";
    private const string PageAnimal = "animal";
    private const string PageFestival = "festival";
    private const string PageAdventureBoard = "adventureBoard";
    private const string PageRedecorate = "redecorate";
    private const string PageCompat = "compat";

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
        api.AddPageLink(manifest, PageSeasonal, () => t.Get("config.page.seasonal"), () => t.Get("config.page.seasonal.tooltip"));
        api.AddPageLink(manifest, PageAnimal, () => t.Get("config.page.animal"), () => t.Get("config.page.animal.tooltip"));
        api.AddPageLink(manifest, PageFestival, () => t.Get("config.page.festival"), () => t.Get("config.page.festival.tooltip"));

        api.AddSectionTitle(manifest, () => t.Get("config.section.otherPages"));
        api.AddPageLink(manifest, PageAdventureBoard, () => t.Get("config.page.adventureBoard"), () => t.Get("config.page.adventureBoard.tooltip"));
        api.AddPageLink(manifest, PageRedecorate, () => t.Get("config.page.redecorate"), () => t.Get("config.page.redecorate.tooltip"));
        api.AddPageLink(manifest, PageCompat, () => t.Get("config.page.compat"), () => t.Get("config.page.compat.tooltip"));

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
        api.AddSectionTitle(manifest, () => t.Get("config.section.cropCycle"));
        AddInt(api, manifest, t, "CropCycleMinFarmingLevel", () => ModEntry.Config.CropCycleMinFarmingLevel, v => ModEntry.Config.CropCycleMinFarmingLevel = v, 0, 10);

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
        api.AddSectionTitle(manifest, () => t.Get("config.section.timedDelivery"));
        AddInt(api, manifest, t, "TimedDeliveryMinMinutes", () => ModEntry.Config.TimedDeliveryMinMinutes, v => ModEntry.Config.TimedDeliveryMinMinutes = v, 30, 600);
        AddInt(api, manifest, t, "TimedDeliveryMaxMinutes", () => ModEntry.Config.TimedDeliveryMaxMinutes, v => ModEntry.Config.TimedDeliveryMaxMinutes = v, 30, 600);
        AddInt(api, manifest, t, "TimedDeliveryLatestHandoffTime", () => ModEntry.Config.TimedDeliveryLatestHandoffTime, v => ModEntry.Config.TimedDeliveryLatestHandoffTime = v, 900, 2400);
        api.AddSectionTitle(manifest, () => t.Get("config.section.jojaArc"));
        AddInt(api, manifest, t, "MorrisManOfMeansGoldTarget", () => ModEntry.Config.MorrisManOfMeansGoldTarget, v => ModEntry.Config.MorrisManOfMeansGoldTarget = v, 1000, 50000);
        AddInt(api, manifest, t, "MorrisQualityControlMaxCropPrice", () => ModEntry.Config.MorrisQualityControlMaxCropPrice, v => ModEntry.Config.MorrisQualityControlMaxCropPrice = v, 1, 1000);
        AddInt(api, manifest, t, "MorrisQualityControlSellCount", () => ModEntry.Config.MorrisQualityControlSellCount, v => ModEntry.Config.MorrisQualityControlSellCount = v, 1, 100);
        AddInt(api, manifest, t, "PierreStockPickleCount", () => ModEntry.Config.PierreStockPickleCount, v => ModEntry.Config.PierreStockPickleCount = v, 1, 50);

        // ----- Seasonal -----
        api.AddPage(manifest, PageSeasonal, () => t.Get("config.page.seasonal"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.springCleaning"));
        AddInt(api, manifest, t, "SpringCleaningCount", () => ModEntry.Config.SpringCleaningCount, v => ModEntry.Config.SpringCleaningCount = v, 1, 30);

        // ----- Animal -----
        api.AddPage(manifest, PageAnimal, () => t.Get("config.page.animal"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.alexProtein"));
        AddInt(api, manifest, t, "AlexProteinShakesBaseQty", () => ModEntry.Config.AlexProteinShakesBaseQty, v => ModEntry.Config.AlexProteinShakesBaseQty = v, 1, 50);
        AddInt(api, manifest, t, "AlexProteinShakesPerChicken", () => ModEntry.Config.AlexProteinShakesPerChicken, v => ModEntry.Config.AlexProteinShakesPerChicken = v, 0, 10);
        AddInt(api, manifest, t, "AlexProteinShakesMaxQty", () => ModEntry.Config.AlexProteinShakesMaxQty, v => ModEntry.Config.AlexProteinShakesMaxQty = v, 1, 100);
        api.AddSectionTitle(manifest, () => t.Get("config.section.marnieOffers"));
        AddInt(api, manifest, t, "MarnieChickenOfferRebate", () => ModEntry.Config.MarnieChickenOfferRebate, v => ModEntry.Config.MarnieChickenOfferRebate = v, 0, 5000);
        AddInt(api, manifest, t, "MarnieChickenOfferSeedQty", () => ModEntry.Config.MarnieChickenOfferSeedQty, v => ModEntry.Config.MarnieChickenOfferSeedQty = v, 1, 100);
        AddInt(api, manifest, t, "MarnieCowOfferRebate", () => ModEntry.Config.MarnieCowOfferRebate, v => ModEntry.Config.MarnieCowOfferRebate = v, 0, 10000);
        AddInt(api, manifest, t, "MarnieCreditExpiryDays", () => ModEntry.Config.MarnieCreditExpiryDays, v => ModEntry.Config.MarnieCreditExpiryDays = v, 1, 56);
        AddInt(api, manifest, t, "MarnieEggRequestQty", () => ModEntry.Config.MarnieEggRequestQty, v => ModEntry.Config.MarnieEggRequestQty = v, 1, 50);
        AddInt(api, manifest, t, "MarnieMilkRequestQty", () => ModEntry.Config.MarnieMilkRequestQty, v => ModEntry.Config.MarnieMilkRequestQty = v, 1, 50);
        api.AddSectionTitle(manifest, () => t.Get("config.section.robinSilo"));
        AddInt(api, manifest, t, "RobinSiloOfferStoneQty", () => ModEntry.Config.RobinSiloOfferStoneQty, v => ModEntry.Config.RobinSiloOfferStoneQty = v, 1, 500);
        AddInt(api, manifest, t, "RobinSiloOfferClayQty", () => ModEntry.Config.RobinSiloOfferClayQty, v => ModEntry.Config.RobinSiloOfferClayQty = v, 1, 100);
        AddInt(api, manifest, t, "RobinSiloOfferCopperBarQty", () => ModEntry.Config.RobinSiloOfferCopperBarQty, v => ModEntry.Config.RobinSiloOfferCopperBarQty = v, 1, 50);
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
        api.AddSectionTitle(manifest, () => t.Get("config.section.esvSpiritsEve"));
        AddInt(api, manifest, t, "EastScarpDecorPurpleQty", () => ModEntry.Config.EastScarpDecorPurpleQty, v => ModEntry.Config.EastScarpDecorPurpleQty = v, 1, 50);
        AddInt(api, manifest, t, "EastScarpDecorSlimeQty", () => ModEntry.Config.EastScarpDecorSlimeQty, v => ModEntry.Config.EastScarpDecorSlimeQty = v, 1, 100);
        AddInt(api, manifest, t, "EastScarpDecorStoneQty", () => ModEntry.Config.EastScarpDecorStoneQty, v => ModEntry.Config.EastScarpDecorStoneQty = v, 1, 100);
        api.AddSectionTitle(manifest, () => t.Get("config.section.eggFestivalDecor"));
        AddInt(api, manifest, t, "EggFestivalHayBaleQty", () => ModEntry.Config.EggFestivalHayBaleQty, v => ModEntry.Config.EggFestivalHayBaleQty = v, 1, 50);

        // ----- Adventurer's Guild board -----
        api.AddPage(manifest, PageAdventureBoard, () => t.Get("config.page.adventureBoard"));
        api.AddBoolOption(manifest,
            () => ModEntry.Config.EnableAdventurersGuildBoard,
            v => ModEntry.Config.EnableAdventurersGuildBoard = v,
            () => t.Get("config.enableAdventurersGuildBoard"),
            () => t.Get("config.enableAdventurersGuildBoard.tooltip"));
        AddInt(api, manifest, t, "AdventureBoardPoolSize", () => ModEntry.Config.AdventureBoardPoolSize, v => ModEntry.Config.AdventureBoardPoolSize = v, 1, 5);
        api.AddSectionTitle(manifest, () => t.Get("config.section.adventureBoardPlacement"));
        AddInt(api, manifest, t, "AdventureBoardTileX", () => ModEntry.Config.AdventureBoardTileX, v => ModEntry.Config.AdventureBoardTileX = v, 0, 200);
        AddInt(api, manifest, t, "AdventureBoardTileY", () => ModEntry.Config.AdventureBoardTileY, v => ModEntry.Config.AdventureBoardTileY = v, 0, 200);
        AddInt(api, manifest, t, "AdventureBoardOffsetX", () => ModEntry.Config.AdventureBoardOffsetX, v => ModEntry.Config.AdventureBoardOffsetX = v, -64, 64);
        AddInt(api, manifest, t, "AdventureBoardOffsetY", () => ModEntry.Config.AdventureBoardOffsetY, v => ModEntry.Config.AdventureBoardOffsetY = v, -64, 64);

        // ----- Redecorate quest -----
        // How often the quest posts lives on the framework's Social weights page (it's a
        // Social daily-board quest, so the framework already draws a weight slider for it).
        // Adding a second weight slider here would be two knobs fighting over the same value,
        // so this page carries everything else about the quest instead.
        api.AddPage(manifest, PageRedecorate, () => t.Get("config.page.redecorate"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.redecoratePosting"));
        AddInt(api, manifest, t, "RedecorateDeadlineDays", () => ModEntry.Config.RedecorateDeadlineDays, v => ModEntry.Config.RedecorateDeadlineDays = v, 1, 28);
        AddInt(api, manifest, t, "RedecorateCooldownDays", () => ModEntry.Config.RedecorateCooldownDays, v => ModEntry.Config.RedecorateCooldownDays = v, 1, 56);
        api.AddSectionTitle(manifest, () => t.Get("config.section.redecorateObjectives"));
        AddInt(api, manifest, t, "RedecorateMinObjectives", () => ModEntry.Config.RedecorateMinObjectives, v => ModEntry.Config.RedecorateMinObjectives = v, 1, 5);
        AddInt(api, manifest, t, "RedecorateMaxObjectives", () => ModEntry.Config.RedecorateMaxObjectives, v => ModEntry.Config.RedecorateMaxObjectives = v, 1, 5);
        AddInt(api, manifest, t, "RedecorateMinPerObjective", () => ModEntry.Config.RedecorateMinPerObjective, v => ModEntry.Config.RedecorateMinPerObjective = v, 1, 5);
        AddInt(api, manifest, t, "RedecorateMaxPerObjective", () => ModEntry.Config.RedecorateMaxPerObjective, v => ModEntry.Config.RedecorateMaxPerObjective = v, 1, 5);
        api.AddSectionTitle(manifest, () => t.Get("config.section.redecorateBudget"));
        AddDouble(api, manifest, t, "RedecorateBudgetGenerosity", () => ModEntry.Config.RedecorateBudgetGenerosity, v => ModEntry.Config.RedecorateBudgetGenerosity = v, 0.5f, 3f);
        AddInt(api, manifest, t, "RedecorateReferenceLampPrice", () => ModEntry.Config.RedecorateReferenceLampPrice, v => ModEntry.Config.RedecorateReferenceLampPrice = v, 100, 5000);
        AddInt(api, manifest, t, "RedecorateReferenceRugPrice", () => ModEntry.Config.RedecorateReferenceRugPrice, v => ModEntry.Config.RedecorateReferenceRugPrice = v, 100, 5000);
        AddInt(api, manifest, t, "RedecorateReferenceChairPrice", () => ModEntry.Config.RedecorateReferenceChairPrice, v => ModEntry.Config.RedecorateReferenceChairPrice = v, 100, 5000);
        AddInt(api, manifest, t, "RedecorateReferenceTablePrice", () => ModEntry.Config.RedecorateReferenceTablePrice, v => ModEntry.Config.RedecorateReferenceTablePrice = v, 100, 5000);
        api.AddSectionTitle(manifest, () => t.Get("config.section.redecorateReward"));
        AddInt(api, manifest, t, "RedecorateRewardItemCount", () => ModEntry.Config.RedecorateRewardItemCount, v => ModEntry.Config.RedecorateRewardItemCount = v, 1, 10);

        // ----- Mod compatibility -----
        // Text ids and NPC lists for the optional content mods this can work with. Only matter
        // when the matching mod is installed. Kept off the Festival / Fishing pages so those
        // stay short.
        api.AddPage(manifest, PageCompat, () => t.Get("config.page.compat"));
        api.AddSectionTitle(manifest, () => t.Get("config.section.troutDerby"));
        AddText(api, manifest, t, "TroutDerbyRecipeGus", () => ModEntry.Config.TroutDerbyRecipeGus, v => ModEntry.Config.TroutDerbyRecipeGus = v);
        AddText(api, manifest, t, "TroutDerbyRecipePika", () => ModEntry.Config.TroutDerbyRecipePika, v => ModEntry.Config.TroutDerbyRecipePika = v);
        AddText(api, manifest, t, "TroutDerbyRecipeCelestine", () => ModEntry.Config.TroutDerbyRecipeCelestine, v => ModEntry.Config.TroutDerbyRecipeCelestine = v);
        AddText(api, manifest, t, "TroutDerbyRecipeRosa", () => ModEntry.Config.TroutDerbyRecipeRosa, v => ModEntry.Config.TroutDerbyRecipeRosa = v);
        AddText(api, manifest, t, "TroutDerbyShopGus", () => ModEntry.Config.TroutDerbyShopGus, v => ModEntry.Config.TroutDerbyShopGus = v);
        AddText(api, manifest, t, "TroutDerbyShopPika", () => ModEntry.Config.TroutDerbyShopPika, v => ModEntry.Config.TroutDerbyShopPika = v);
        AddText(api, manifest, t, "TroutDerbyShopRosa", () => ModEntry.Config.TroutDerbyShopRosa, v => ModEntry.Config.TroutDerbyShopRosa = v);
        AddText(api, manifest, t, "TroutDerbyShopCelestine", () => ModEntry.Config.TroutDerbyShopCelestine, v => ModEntry.Config.TroutDerbyShopCelestine = v);
        AddText(api, manifest, t, "TroutDerbyDishGus", () => ModEntry.Config.TroutDerbyDishGus, v => ModEntry.Config.TroutDerbyDishGus = v);
        AddText(api, manifest, t, "TroutDerbyDishPika", () => ModEntry.Config.TroutDerbyDishPika, v => ModEntry.Config.TroutDerbyDishPika = v);
        AddText(api, manifest, t, "TroutDerbyDishRosa", () => ModEntry.Config.TroutDerbyDishRosa, v => ModEntry.Config.TroutDerbyDishRosa = v);
        AddText(api, manifest, t, "TroutDerbyDishCelestine", () => ModEntry.Config.TroutDerbyDishCelestine, v => ModEntry.Config.TroutDerbyDishCelestine = v);
        api.AddSectionTitle(manifest, () => t.Get("config.section.squidFest"));
        AddText(api, manifest, t, "SquidFestRecipeGus", () => ModEntry.Config.SquidFestRecipeGus, v => ModEntry.Config.SquidFestRecipeGus = v);
        AddText(api, manifest, t, "SquidFestRecipePika", () => ModEntry.Config.SquidFestRecipePika, v => ModEntry.Config.SquidFestRecipePika = v);
        AddText(api, manifest, t, "SquidFestRecipeCelestine", () => ModEntry.Config.SquidFestRecipeCelestine, v => ModEntry.Config.SquidFestRecipeCelestine = v);
        AddText(api, manifest, t, "SquidFestRecipeRosa", () => ModEntry.Config.SquidFestRecipeRosa, v => ModEntry.Config.SquidFestRecipeRosa = v);
        AddText(api, manifest, t, "SquidFestShopGus", () => ModEntry.Config.SquidFestShopGus, v => ModEntry.Config.SquidFestShopGus = v);
        AddText(api, manifest, t, "SquidFestShopPika", () => ModEntry.Config.SquidFestShopPika, v => ModEntry.Config.SquidFestShopPika = v);
        AddText(api, manifest, t, "SquidFestShopRosa", () => ModEntry.Config.SquidFestShopRosa, v => ModEntry.Config.SquidFestShopRosa = v);
        AddText(api, manifest, t, "SquidFestShopCelestine", () => ModEntry.Config.SquidFestShopCelestine, v => ModEntry.Config.SquidFestShopCelestine = v);
        AddText(api, manifest, t, "SquidFestDishGus", () => ModEntry.Config.SquidFestDishGus, v => ModEntry.Config.SquidFestDishGus = v);
        AddText(api, manifest, t, "SquidFestDishPika", () => ModEntry.Config.SquidFestDishPika, v => ModEntry.Config.SquidFestDishPika = v);
        AddText(api, manifest, t, "SquidFestDishRosa", () => ModEntry.Config.SquidFestDishRosa, v => ModEntry.Config.SquidFestDishRosa = v);
        AddText(api, manifest, t, "SquidFestDishCelestine", () => ModEntry.Config.SquidFestDishCelestine, v => ModEntry.Config.SquidFestDishCelestine = v);
        api.AddSectionTitle(manifest, () => t.Get("config.section.festivalNpcLists"));
        AddText(api, manifest, t, "EastScarpFestivalNpcs", () => ModEntry.Config.EastScarpFestivalNpcs, v => ModEntry.Config.EastScarpFestivalNpcs = v);
        AddText(api, manifest, t, "RidgesideFestivalNpcs", () => ModEntry.Config.RidgesideFestivalNpcs, v => ModEntry.Config.RidgesideFestivalNpcs = v);
        api.AddSectionTitle(manifest, () => t.Get("config.section.rsvTubOFlowers"));
        AddText(api, manifest, t, "RsvTubOFlowersId", () => ModEntry.Config.RsvTubOFlowersId, v => ModEntry.Config.RsvTubOFlowersId = v);
        AddText(api, manifest, t, "RsvTubOFlowersRecipeName", () => ModEntry.Config.RsvTubOFlowersRecipeName, v => ModEntry.Config.RsvTubOFlowersRecipeName = v);

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

    // GMCM's number option only takes int or float, so this runs a double config value
    // through a float slider.
    private static void AddDouble(IGenericModConfigMenuApi api, IManifest manifest, ITranslationHelper t,
        string key, System.Func<double> get, System.Action<double> set, float min, float max, float interval = 0.05f)
    {
        api.AddNumberOption(manifest, () => (float)get(), v => set(v),
            () => t.Get($"config.{key}"),
            () => t.Get($"config.{key}.tooltip"),
            min: min, max: max, interval: interval);
    }
}
