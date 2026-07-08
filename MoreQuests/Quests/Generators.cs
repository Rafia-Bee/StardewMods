using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Quests;

/// Registration point for all C# quest generators referenced by quests.json. Metadata
/// (Id/Category/Kind/Weight/MaxPerDay/CooldownDays/Available) lives in JSON; the C# side
/// owns only runtime randomization.
internal static partial class Generators
{
    public static void RegisterAll(IMoreQuestsModApi fw)
    {
        // Grouped by QuestCategory, alphabetical within section. Keep new generators
        // in their category and in order, otherwise this file just grows by accretion.

        // Animal
        fw.RegisterGenerator("AlexProteinShakes", AlexProteinShakes);
        fw.RegisterGenerator("GuntherDinosaurStudy", GuntherDinosaurStudy);
        fw.RegisterGenerator("HaySupplyRun", HaySupplyRun);
        fw.RegisterGenerator("KrobusVoidNote", KrobusVoidNote);
        fw.RegisterGenerator("LeahFarmPainting", LeahFarmPainting);
        fw.RegisterGenerator("MarnieChickenOffer", MarnieChickenOffer);
        fw.RegisterGenerator("MarnieCowOffer", MarnieCowOffer);
        fw.RegisterGenerator("MarnieEggRequest", MarnieEggRequest);
        fw.RegisterGenerator("MarnieLivestockShow", MarnieLivestockShow);
        fw.RegisterGenerator("MarnieMilkRequest", MarnieMilkRequest);
        fw.RegisterGenerator("RobinSiloOffer", RobinSiloOffer);

        // Cooking
        fw.RegisterGenerator("CravingDish", CravingDishGenerator);
        fw.RegisterGenerator("DinnerParty", DinnerParty);
        fw.RegisterGenerator("GrandFeast", GrandFeast);
        fw.RegisterGenerator("WeeklySpecialCommon", WeeklySpecialCommon);
        fw.RegisterGenerator("WeeklySpecialComplex", WeeklySpecialComplex);

        // Farming
        fw.RegisterGenerator("BasicCropDelivery", BasicCropDelivery);
        fw.RegisterGenerator("CarolineTeaGarden", CarolineTeaGarden);
        fw.RegisterGenerator("CropCycleQuest", CropCycleQuest);
        fw.RegisterGenerator("DehydratorRequest", DehydratorRequest);
        fw.RegisterGenerator("KegRequest", KegRequest);
        fw.RegisterGenerator("MassiveHarvestRequest", MassiveHarvestRequest);
        fw.RegisterGenerator("PierresStockUp", PierresStockUp);
        fw.RegisterGenerator("PremiumCropOrder", PremiumCropOrder);
        fw.RegisterGenerator("PreservesJarRequest", PreservesJarRequest);
        fw.RegisterGenerator("QualityCropDelivery", QualityCropDelivery);

        // Festival
        fw.RegisterGenerator("DyeForEggs", DyeForEggs);
        fw.RegisterGenerator("EastScarpSpiritsEveDecor", EastScarpSpiritsEveDecor);
        fw.RegisterGenerator("EggFestivalDecor", EggFestivalDecor);
        fw.RegisterGenerator("EggHuntSabotage", EggHuntSabotage);
        fw.RegisterGenerator("FairFestivalDecor", FairFestivalDecor);
        fw.RegisterGenerator("GusFestivalFeastFall", GusFestivalFeastFall);
        fw.RegisterGenerator("GusFestivalFeastSpring", GusFestivalFeastSpring);
        fw.RegisterGenerator("GusFestivalFeastSummer", GusFestivalFeastSummer);
        fw.RegisterGenerator("GusFestivalFeastWinter", GusFestivalFeastWinter);
        fw.RegisterGenerator("JellyfishWatchPrep", JellyfishWatchPrep);
        fw.RegisterGenerator("LuauFestivalDecor", LuauFestivalDecor);
        fw.RegisterGenerator("MerchantUnpacking", MerchantUnpacking);
        fw.RegisterGenerator("MoonlightJelliesFestivalDecor", MoonlightJelliesFestivalDecor);
        fw.RegisterGenerator("RainbowPlatter", RainbowPlatter);
        fw.RegisterGenerator("RidgesideGatheringDecor", RidgesideGatheringDecor);
        fw.RegisterGenerator("SecretGiftHint", SecretGiftHint);
        fw.RegisterGenerator("SpiritsEveDecor", SpiritsEveDecor);
        fw.RegisterGenerator("SquidFestShowcase", SquidFestShowcase);
        fw.RegisterGenerator("WrappingPaper", WrappingPaper);

        // Fishing
        fw.RegisterGenerator("FishSmokerRequest", FishSmokerRequest);
        fw.RegisterGenerator("KnowYourWaters", KnowYourWaters);
        fw.RegisterGenerator("LegendaryFishQuest", LegendaryFishQuest);
        fw.RegisterGenerator("LocationFishOverpopulation", LocationFishOverpopulation);
        fw.RegisterGenerator("MediumFishingHaul", MediumFishingHaul);
        fw.RegisterGenerator("QualityFishDelivery", QualityFishDelivery);
        fw.RegisterGenerator("RainyDayCatch", RainyDayCatch);
        fw.RegisterGenerator("SeafoodNight", SeafoodNight);
        fw.RegisterGenerator("SimpleFishingRequest", SimpleFishingRequest);
        fw.RegisterGenerator("SizeFishOverpopulation", SizeFishOverpopulation);

        // Foraging
        fw.RegisterGenerator("ClearDebris", ClearDebris);
        fw.RegisterGenerator("FeedWildCritters", FeedWildCritters);
        fw.RegisterGenerator("ForageWithLinus", ForageWithLinus);
        fw.RegisterGenerator("PlantTrees", PlantTrees);
        fw.RegisterGenerator("RareForageHunt", RareForageHunt);
        fw.RegisterGenerator("SeasonalForaging", SeasonalForaging);

        // Mining
        fw.RegisterGenerator("ArchaeologyDig", ArchaeologyDig);
        fw.RegisterGenerator("BarDelivery", BarDelivery);
        fw.RegisterGenerator("BasicSlimeClearing", BasicSlimeClearing);
        fw.RegisterGenerator("ClearTheFloor", ClearTheFloor);
        fw.RegisterGenerator("MinesDeepDive", MinesDeepDive);
        fw.RegisterGenerator("MonsterHunt", MonsterHunt);
        fw.RegisterGenerator("MonsterParts", MonsterParts);
        fw.RegisterGenerator("RareMaterialRequest", RareMaterialRequest);
        fw.RegisterGenerator("SkullCavernDeepDive", SkullCavernDeepDive);
        fw.RegisterGenerator("UnseenOffering", UnseenOffering);

        // Seasonal
        fw.RegisterGenerator("BattenDownTheHatches", BattenDownTheHatches);
        fw.RegisterGenerator("BeachCleanup", BeachCleanup);
        fw.RegisterGenerator("FloralTea", FloralTea);
        fw.RegisterGenerator("HeatWaveRelief", HeatWaveRelief);
        fw.RegisterGenerator("HolidayCookies", HolidayCookies);
        fw.RegisterGenerator("SpringCleaning", SpringCleaning);
        fw.RegisterGenerator("SubmarineFuel", SubmarineFuel);
        fw.RegisterGenerator("WizardsRitualMaterials", WizardsRitualMaterials);

        // Social
        fw.RegisterGenerator("CheckOnFriends", CheckOnFriends);
        fw.RegisterGenerator("CheckOnGeorge", CheckOnGeorge);
        fw.RegisterGenerator("ElliottPoemInspiration", ElliottPoemInspiration);
        fw.RegisterGenerator("EmilyHousewarming", EmilyHousewarming);
        fw.RegisterGenerator("GiftDelivery", GiftDelivery);
        fw.RegisterGenerator("GuntherMuseumDonation", GuntherMuseumDonation);
        fw.RegisterGenerator("TimedPackageDelivery", TimedPackageDelivery);

        // Story arcs. The Social category toggle gates these like any other quest now, so
        // turning Social off stops the Morris and Pierre lines from starting.
        fw.RegisterGenerator("MorrisManOfMeans", MorrisManOfMeans);
        fw.RegisterGenerator("MorrisQualityControl", MorrisQualityControl);
        fw.RegisterGenerator("PierreDontGetCaught", PierreDontGetCaught);

        // Report-back prompts (talk-to-giver question dialogues that branch the reward).
        RegisterKnowYourWatersReportBack(fw);
    }
}
