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

/// Central registration point for all C# quest generators referenced by `assets/quests.json`.
/// Each method below is the `Build()` body that previously lived in its own
/// `IQuestDefinition` class. Phase 4 migration: metadata (Id/Category/Kind/Weight/MaxPerDay/
/// CooldownDays/Available) now lives in JSON; the C# side owns only runtime randomization.
internal static partial class Generators
{
    public static void RegisterAll(IMoreQuestsModApi fw)
    {
        fw.RegisterGenerator("BasicCropDelivery", BasicCropDelivery);
        fw.RegisterGenerator("SimpleFishingRequest", SimpleFishingRequest);
        fw.RegisterGenerator("BasicSlimeClearing", BasicSlimeClearing);
        fw.RegisterGenerator("BarDelivery", BarDelivery);
        fw.RegisterGenerator("SeasonalForaging", SeasonalForaging);
        fw.RegisterGenerator("ElliottPoemInspiration", ElliottPoemInspiration);
        fw.RegisterGenerator("CheckOnGeorge", CheckOnGeorge);
        fw.RegisterGenerator("HaySupplyRun", HaySupplyRun);
        fw.RegisterGenerator("BeachCleanup", BeachCleanup);
        fw.RegisterGenerator("SpringTea", SpringTea);
        fw.RegisterGenerator("CravingDish", CravingDishGenerator);
        fw.RegisterGenerator("SubmarineFuel", SubmarineFuel);
        fw.RegisterGenerator("WizardsRitualMaterials", WizardsRitualMaterials);
        fw.RegisterGenerator("HolidayCookies", HolidayCookies);
        fw.RegisterGenerator("CheckOnFriends", CheckOnFriends);
        fw.RegisterGenerator("GusFestivalFeastSpring", GusFestivalFeastSpring);
        fw.RegisterGenerator("GusFestivalFeastWinter", GusFestivalFeastWinter);
        fw.RegisterGenerator("PreservesSeason", PreservesSeason);
        fw.RegisterGenerator("SkullCavernDeepDive", SkullCavernDeepDive);
        fw.RegisterGenerator("MinesDeepDive", MinesDeepDive);
        fw.RegisterGenerator("PierresStockUp", PierresStockUp);
        fw.RegisterGenerator("MassiveHarvestRequest", MassiveHarvestRequest);
        fw.RegisterGenerator("WeeklySpecialCommon", WeeklySpecialCommon);
        fw.RegisterGenerator("WeeklySpecialComplex", WeeklySpecialComplex);
        fw.RegisterGenerator("GrandFeast", GrandFeast);
        fw.RegisterGenerator("MediumFishingHaul", MediumFishingHaul);
        fw.RegisterGenerator("SeafoodNight", SeafoodNight);
        fw.RegisterGenerator("MonsterParts", MonsterParts);
        fw.RegisterGenerator("ForageWithLinus", ForageWithLinus);
        fw.RegisterGenerator("GusFestivalFeastFall", GusFestivalFeastFall);
        fw.RegisterGenerator("GusFestivalFeastSummer", GusFestivalFeastSummer);
        fw.RegisterGenerator("GiftDelivery", GiftDelivery);
        fw.RegisterGenerator("HeatWaveRelief", HeatWaveRelief);
        fw.RegisterGenerator("JellyfishWatchPrep", JellyfishWatchPrep);
        fw.RegisterGenerator("MerchantUnpacking", MerchantUnpacking);
        fw.RegisterGenerator("MonsterHunt", MonsterHunt);
        fw.RegisterGenerator("RareForageHunt", RareForageHunt);
        fw.RegisterGenerator("RareMaterialRequest", RareMaterialRequest);
        fw.RegisterGenerator("SecretGiftHint", SecretGiftHint);
        fw.RegisterGenerator("WrappingPaper", WrappingPaper);
        fw.RegisterGenerator("PremiumCropOrder", PremiumCropOrder);
        fw.RegisterGenerator("QualityCropDelivery", QualityCropDelivery);
        fw.RegisterGenerator("QualityFishDelivery", QualityFishDelivery);
        fw.RegisterGenerator("MoonlightJelliesFestivalDecor", MoonlightJelliesFestivalDecor);
        fw.RegisterGenerator("EggFestivalDecor", EggFestivalDecor);
        fw.RegisterGenerator("FairFestivalDecor", FairFestivalDecor);
        fw.RegisterGenerator("LuauFestivalDecor", LuauFestivalDecor);
        fw.RegisterGenerator("SpiritsEveDecor", SpiritsEveDecor);
        fw.RegisterGenerator("EastScarpSpiritsEveDecor", EastScarpSpiritsEveDecor);
        fw.RegisterGenerator("RidgesideGatheringDecor", RidgesideGatheringDecor);
        fw.RegisterGenerator("LocationFishOverpopulation", LocationFishOverpopulation);
        fw.RegisterGenerator("RainyDayCatch", RainyDayCatch);
        fw.RegisterGenerator("SizeFishOverpopulation", SizeFishOverpopulation);
        fw.RegisterGenerator("RainbowPlatter", RainbowPlatter);
        fw.RegisterGenerator("SquidFestShowcase", SquidFestShowcase);
        fw.RegisterGenerator("AlexProteinShakes", AlexProteinShakes);
        fw.RegisterGenerator("GuntherDinosaurStudy", GuntherDinosaurStudy);
        fw.RegisterGenerator("MarnieChickenOffer", MarnieChickenOffer);
        fw.RegisterGenerator("MarnieCowOffer", MarnieCowOffer);
        fw.RegisterGenerator("MarnieEggRequest", MarnieEggRequest);
        fw.RegisterGenerator("MarnieMilkRequest", MarnieMilkRequest);
        fw.RegisterGenerator("RobinSiloOffer", RobinSiloOffer);
        fw.RegisterGenerator("CarolineTeaGarden", CarolineTeaGarden);
        fw.RegisterGenerator("ClearDebris", ClearDebris);
        fw.RegisterGenerator("DinnerParty", DinnerParty);
        fw.RegisterGenerator("PlantTrees", PlantTrees);
        fw.RegisterGenerator("SpringCleaning", SpringCleaning);
    }
}
