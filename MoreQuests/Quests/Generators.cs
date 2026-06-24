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
        Reg(fw, "AlexProteinShakes", AlexProteinShakes);
        Reg(fw, "GuntherDinosaurStudy", GuntherDinosaurStudy);
        Reg(fw, "HaySupplyRun", HaySupplyRun);
        Reg(fw, "KrobusVoidNote", KrobusVoidNote);
        Reg(fw, "LeahFarmPainting", LeahFarmPainting);
        Reg(fw, "MarnieChickenOffer", MarnieChickenOffer);
        Reg(fw, "MarnieCowOffer", MarnieCowOffer);
        Reg(fw, "MarnieEggRequest", MarnieEggRequest);
        Reg(fw, "MarnieLivestockShow", MarnieLivestockShow);
        Reg(fw, "MarnieMilkRequest", MarnieMilkRequest);
        Reg(fw, "RobinSiloOffer", RobinSiloOffer);

        // Cooking
        Reg(fw, "CravingDish", CravingDishGenerator);
        Reg(fw, "DinnerParty", DinnerParty);
        Reg(fw, "GrandFeast", GrandFeast);
        Reg(fw, "WeeklySpecialCommon", WeeklySpecialCommon);
        Reg(fw, "WeeklySpecialComplex", WeeklySpecialComplex);

        // Farming
        Reg(fw, "BasicCropDelivery", BasicCropDelivery);
        Reg(fw, "CarolineTeaGarden", CarolineTeaGarden);
        Reg(fw, "CropCycleQuest", CropCycleQuest);
        Reg(fw, "DehydratorRequest", DehydratorRequest);
        Reg(fw, "KegRequest", KegRequest);
        Reg(fw, "MassiveHarvestRequest", MassiveHarvestRequest);
        Reg(fw, "PierresStockUp", PierresStockUp);
        Reg(fw, "PremiumCropOrder", PremiumCropOrder);
        Reg(fw, "PreservesJarRequest", PreservesJarRequest);
        Reg(fw, "QualityCropDelivery", QualityCropDelivery);

        // Festival
        Reg(fw, "DyeForEggs", DyeForEggs);
        Reg(fw, "EastScarpSpiritsEveDecor", EastScarpSpiritsEveDecor);
        Reg(fw, "EggFestivalDecor", EggFestivalDecor);
        Reg(fw, "EggHuntSabotage", EggHuntSabotage);
        Reg(fw, "FairFestivalDecor", FairFestivalDecor);
        Reg(fw, "GusFestivalFeastFall", GusFestivalFeastFall);
        Reg(fw, "GusFestivalFeastSpring", GusFestivalFeastSpring);
        Reg(fw, "GusFestivalFeastSummer", GusFestivalFeastSummer);
        Reg(fw, "GusFestivalFeastWinter", GusFestivalFeastWinter);
        Reg(fw, "JellyfishWatchPrep", JellyfishWatchPrep);
        Reg(fw, "LuauFestivalDecor", LuauFestivalDecor);
        Reg(fw, "MerchantUnpacking", MerchantUnpacking);
        Reg(fw, "MoonlightJelliesFestivalDecor", MoonlightJelliesFestivalDecor);
        Reg(fw, "RainbowPlatter", RainbowPlatter);
        Reg(fw, "RidgesideGatheringDecor", RidgesideGatheringDecor);
        Reg(fw, "SecretGiftHint", SecretGiftHint);
        Reg(fw, "SpiritsEveDecor", SpiritsEveDecor);
        Reg(fw, "SquidFestShowcase", SquidFestShowcase);
        Reg(fw, "WrappingPaper", WrappingPaper);

        // Fishing
        Reg(fw, "FishSmokerRequest", FishSmokerRequest);
        Reg(fw, "KnowYourWaters", KnowYourWaters);
        Reg(fw, "LegendaryFishQuest", LegendaryFishQuest);
        Reg(fw, "LocationFishOverpopulation", LocationFishOverpopulation);
        Reg(fw, "MediumFishingHaul", MediumFishingHaul);
        Reg(fw, "QualityFishDelivery", QualityFishDelivery);
        Reg(fw, "RainyDayCatch", RainyDayCatch);
        Reg(fw, "SeafoodNight", SeafoodNight);
        Reg(fw, "SimpleFishingRequest", SimpleFishingRequest);
        Reg(fw, "SizeFishOverpopulation", SizeFishOverpopulation);

        // Foraging
        Reg(fw, "ClearDebris", ClearDebris);
        Reg(fw, "FeedWildCritters", FeedWildCritters);
        Reg(fw, "ForageWithLinus", ForageWithLinus);
        Reg(fw, "PlantTrees", PlantTrees);
        Reg(fw, "RareForageHunt", RareForageHunt);
        Reg(fw, "SeasonalForaging", SeasonalForaging);

        // Mining
        Reg(fw, "ArchaeologyDig", ArchaeologyDig);
        Reg(fw, "BarDelivery", BarDelivery);
        Reg(fw, "BasicSlimeClearing", BasicSlimeClearing);
        Reg(fw, "ClearTheFloor", ClearTheFloor);
        Reg(fw, "MinesDeepDive", MinesDeepDive);
        Reg(fw, "MonsterHunt", MonsterHunt);
        Reg(fw, "MonsterParts", MonsterParts);
        Reg(fw, "RareMaterialRequest", RareMaterialRequest);
        Reg(fw, "SkullCavernDeepDive", SkullCavernDeepDive);
        Reg(fw, "UnseenOffering", UnseenOffering);

        // Seasonal
        Reg(fw, "BattenDownTheHatches", BattenDownTheHatches);
        Reg(fw, "BeachCleanup", BeachCleanup);
        Reg(fw, "FloralTea", FloralTea);
        Reg(fw, "HeatWaveRelief", HeatWaveRelief);
        Reg(fw, "HolidayCookies", HolidayCookies);
        Reg(fw, "SpringCleaning", SpringCleaning);
        Reg(fw, "SubmarineFuel", SubmarineFuel);
        Reg(fw, "WizardsRitualMaterials", WizardsRitualMaterials);

        // Social
        Reg(fw, "CheckOnFriends", CheckOnFriends);
        Reg(fw, "CheckOnGeorge", CheckOnGeorge);
        Reg(fw, "ElliottPoemInspiration", ElliottPoemInspiration);
        Reg(fw, "EmilyHousewarming", EmilyHousewarming);
        Reg(fw, "GiftDelivery", GiftDelivery);
        Reg(fw, "GuntherMuseumDonation", GuntherMuseumDonation);

        // Story arcs. Registered directly (not through Reg) so they're never silenced by a
        // category master-toggle, since they're hand-authored storylines, not board filler.
        fw.RegisterGenerator("MorrisManOfMeans", MorrisManOfMeans);
        fw.RegisterGenerator("MorrisQualityControl", MorrisQualityControl);
        fw.RegisterGenerator("PierreDontGetCaught", PierreDontGetCaught);

        // Report-back prompts (talk-to-giver question dialogues that branch the reward).
        RegisterKnowYourWatersReportBack(fw);
    }

    /// Wraps fw.RegisterGenerator with a category master-toggle check. Drops the posting
    /// when the matching ModConfig.&lt;Category&gt;QuestsEnabled is off.
    private static void Reg(IMoreQuestsModApi fw, string name, Func<QuestContext, QuestPosting?> gen)
    {
        fw.RegisterGenerator(name, ctx =>
        {
            var posting = gen(ctx);
            return posting != null && IsCategoryEnabled(posting.Category) ? posting : null;
        });
    }

    private static bool IsCategoryEnabled(string cat) => cat switch
    {
        QuestCategory.Animal => ModEntry.Config.AnimalQuestsEnabled,
        QuestCategory.Cooking => ModEntry.Config.CookingQuestsEnabled,
        QuestCategory.Farming => ModEntry.Config.FarmingQuestsEnabled,
        QuestCategory.Festival => ModEntry.Config.FestivalQuestsEnabled,
        QuestCategory.Fishing => ModEntry.Config.FishingQuestsEnabled,
        QuestCategory.Foraging => ModEntry.Config.ForagingQuestsEnabled,
        QuestCategory.Mining => ModEntry.Config.MiningQuestsEnabled,
        QuestCategory.Seasonal => ModEntry.Config.SeasonalQuestsEnabled,
        QuestCategory.Social => ModEntry.Config.SocialQuestsEnabled,
        _ => true
    };
}
