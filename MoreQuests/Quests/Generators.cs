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
        Reg(fw, "BasicCropDelivery", BasicCropDelivery);
        Reg(fw, "SimpleFishingRequest", SimpleFishingRequest);
        Reg(fw, "BasicSlimeClearing", BasicSlimeClearing);
        Reg(fw, "BarDelivery", BarDelivery);
        Reg(fw, "SeasonalForaging", SeasonalForaging);
        Reg(fw, "ElliottPoemInspiration", ElliottPoemInspiration);
        Reg(fw, "CheckOnGeorge", CheckOnGeorge);
        Reg(fw, "HaySupplyRun", HaySupplyRun);
        Reg(fw, "BeachCleanup", BeachCleanup);
        Reg(fw, "FloralTea", FloralTea);
        Reg(fw, "CravingDish", CravingDishGenerator);
        Reg(fw, "SubmarineFuel", SubmarineFuel);
        Reg(fw, "WizardsRitualMaterials", WizardsRitualMaterials);
        Reg(fw, "HolidayCookies", HolidayCookies);
        Reg(fw, "CheckOnFriends", CheckOnFriends);
        Reg(fw, "GusFestivalFeastSpring", GusFestivalFeastSpring);
        Reg(fw, "GusFestivalFeastWinter", GusFestivalFeastWinter);
        Reg(fw, "PreservesJarRequest", PreservesJarRequest);
        Reg(fw, "KegRequest", KegRequest);
        Reg(fw, "DehydratorRequest", DehydratorRequest);
        Reg(fw, "FishSmokerRequest", FishSmokerRequest);
        Reg(fw, "SkullCavernDeepDive", SkullCavernDeepDive);
        Reg(fw, "MinesDeepDive", MinesDeepDive);
        Reg(fw, "PierresStockUp", PierresStockUp);
        Reg(fw, "MassiveHarvestRequest", MassiveHarvestRequest);
        Reg(fw, "WeeklySpecialCommon", WeeklySpecialCommon);
        Reg(fw, "WeeklySpecialComplex", WeeklySpecialComplex);
        Reg(fw, "GrandFeast", GrandFeast);
        Reg(fw, "MediumFishingHaul", MediumFishingHaul);
        Reg(fw, "SeafoodNight", SeafoodNight);
        Reg(fw, "MonsterParts", MonsterParts);
        Reg(fw, "ForageWithLinus", ForageWithLinus);
        Reg(fw, "GusFestivalFeastFall", GusFestivalFeastFall);
        Reg(fw, "GusFestivalFeastSummer", GusFestivalFeastSummer);
        Reg(fw, "GiftDelivery", GiftDelivery);
        Reg(fw, "HeatWaveRelief", HeatWaveRelief);
        Reg(fw, "JellyfishWatchPrep", JellyfishWatchPrep);
        Reg(fw, "MerchantUnpacking", MerchantUnpacking);
        Reg(fw, "MonsterHunt", MonsterHunt);
        Reg(fw, "RareForageHunt", RareForageHunt);
        Reg(fw, "RareMaterialRequest", RareMaterialRequest);
        Reg(fw, "SecretGiftHint", SecretGiftHint);
        Reg(fw, "WrappingPaper", WrappingPaper);
        Reg(fw, "PremiumCropOrder", PremiumCropOrder);
        Reg(fw, "QualityCropDelivery", QualityCropDelivery);
        Reg(fw, "QualityFishDelivery", QualityFishDelivery);
        Reg(fw, "MoonlightJelliesFestivalDecor", MoonlightJelliesFestivalDecor);
        Reg(fw, "EggFestivalDecor", EggFestivalDecor);
        Reg(fw, "DyeForEggs", DyeForEggs);
        Reg(fw, "FairFestivalDecor", FairFestivalDecor);
        Reg(fw, "LuauFestivalDecor", LuauFestivalDecor);
        Reg(fw, "SpiritsEveDecor", SpiritsEveDecor);
        Reg(fw, "EastScarpSpiritsEveDecor", EastScarpSpiritsEveDecor);
        Reg(fw, "RidgesideGatheringDecor", RidgesideGatheringDecor);
        Reg(fw, "LocationFishOverpopulation", LocationFishOverpopulation);
        Reg(fw, "RainyDayCatch", RainyDayCatch);
        Reg(fw, "SizeFishOverpopulation", SizeFishOverpopulation);
        Reg(fw, "LegendaryFishQuest", LegendaryFishQuest);
        Reg(fw, "RainbowPlatter", RainbowPlatter);
        Reg(fw, "SquidFestShowcase", SquidFestShowcase);
        Reg(fw, "AlexProteinShakes", AlexProteinShakes);
        Reg(fw, "GuntherDinosaurStudy", GuntherDinosaurStudy);
        Reg(fw, "KrobusVoidNote", KrobusVoidNote);
        Reg(fw, "LeahFarmPainting", LeahFarmPainting);
        Reg(fw, "MarnieLivestockShow", MarnieLivestockShow);
        Reg(fw, "MarnieChickenOffer", MarnieChickenOffer);
        Reg(fw, "MarnieCowOffer", MarnieCowOffer);
        Reg(fw, "MarnieEggRequest", MarnieEggRequest);
        Reg(fw, "MarnieMilkRequest", MarnieMilkRequest);
        Reg(fw, "RobinSiloOffer", RobinSiloOffer);
        Reg(fw, "CarolineTeaGarden", CarolineTeaGarden);
        Reg(fw, "ClearDebris", ClearDebris);
        Reg(fw, "DinnerParty", DinnerParty);
        Reg(fw, "PlantTrees", PlantTrees);
        Reg(fw, "SpringCleaning", SpringCleaning);
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

    private static bool IsCategoryEnabled(QuestCategory cat) => cat switch
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
