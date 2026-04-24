using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;

namespace DeluxeGrabberFix;

internal class ModConfig
{
    public enum HarvestCropsRangeMode
    {
        Square,
        Walk
    }

    public enum GlobalGrabberMode
    {
        Off,
        All,
        Hover
    }

    public enum GrabFrequency
    {
        Instant,
        Hourly,
        Daily
    }

    public enum FlowerHarvestMode
    {
        Off,
        All,
        Smart
    }

    public enum GrabberMode
    {
        Classic,
        Specialized
    }

    public GrabberMode grabberMode;
    public GlobalGrabberMode globalGrabber;
    public GrabFrequency grabFrequency;
    public HarvestCropsRangeMode harvestCropsRangeMode;
    public int harvestCropsRange;

    // Specialized grabber milestone thresholds
    public int cropsShippedThreshold = 200;
    public int itemsForagedThreshold = 200;
    public int stumpsChoppedThreshold = 50;
    public int museumDonationsThreshold = 15;
    public int totalMoneyEarnedThreshold = 250000;

    // Specialized grabber crafting costs
    // Crop Grabber: Wood, Gold Bar, Quality Sprinkler
    public int recipeCropWood = 1000;
    public int recipeCropGoldBar = 5;
    public int recipeCropQualitySprinkler = 4;
    // Forage Grabber: Wood, Gold Bar, Mixed Seeds, Fiber
    public int recipeForageWood = 2000;
    public int recipeForageGoldBar = 20;
    public int recipeForageMixedSeeds = 100;
    public int recipeForageFiber = 100;
    // Tree Grabber: Hardwood, Iridium Bar, Maple Syrup, Oak Resin, Pine Tar
    public int recipeTreeHardwood = 50;
    public int recipeTreeIridiumBar = 2;
    public int recipeTreeMapleSyrup = 10;
    public int recipeTreeOakResin = 10;
    public int recipeTreePineTar = 10;
    // Scavenger Grabber: Hardwood, Iridium Bar, Bone Fragment, Artifact Trove
    public int recipeScavengerHardwood = 200;
    public int recipeScavengerIridiumBar = 5;
    public int recipeScavengerBoneFragment = 50;
    public int recipeScavengerArtifactTrove = 5;
    // Machine Grabber: Iridium Bar, Battery Pack, Diamond
    public int recipeMachineIridiumBar = 20;
    public int recipeMachineBatteryPack = 10;
    public int recipeMachineDiamond = 10;

    public bool animalProducts;
    public bool slimeHutch;
    public bool farmCaveMushrooms;
    public bool harvestCrops;
    public bool harvestCropsIndoorPots;
    public bool artifactSpots;
    public bool buriedItems = true;
    public bool orePan;
    public bool forage;
    public bool bushes;
    public bool fruitTrees;
    public bool seedTrees;
    public FlowerHarvestMode flowers;
    public int beeHouseRange;
    public bool reportYield;
    public bool replantReminder;
    public int replantReminderTime;
    public bool debugLogging;
    public bool specializedGrabbersCountForPerfection;
    public bool gainExperience;
    public bool fellHardwoodStumps;
    public bool fellSecretWoodsStumps;
    public bool garbageCans;
    public bool seedSpots;
    public bool harvestMoss;
    public bool harvestGreenRainWeeds;
    public bool excludeQuestItems;
    public bool skipFestivalLocations;
    public bool selectVisitedOnly;
    public bool disableMachineCollection;
    public bool collectCrabPots;
    public bool collectBeeHouses;
    public bool collectTappers;
    public bool collectLeafBaskets;
    public bool collectMushroomLogs;
    public bool collectFishPonds;
    public bool collectKegs;
    public bool collectPreservesJars;
    public bool collectCheesePresses;
    public bool collectMayonnaiseMachines;
    public bool collectLooms;
    public bool collectOilMakers;
    public bool collectFurnaces;
    public bool collectCharcoalKilns;
    public bool collectRecyclingMachines;
    public bool collectSeedMakers;
    public bool collectBoneMills;
    public bool collectGeodeCrushers;
    public bool collectWoodChippers;
    public bool collectDeconstructors;
    public bool collectFishSmokers;
    public bool collectBaitMakers;
    public bool collectDehydrators;
    public bool collectCrystalariums;
    public bool collectLightningRods;
    public bool collectWormBins;
    public bool collectSolarPanels;
    public bool collectSlimeEggPresses;
    public bool collectCoffeeMakers;
    public bool collectSodaMachines;
    public bool collectStatues;
    public bool collectOtherMachines;
    public bool collectDebris;
    public bool automateCompatibility;
    public HashSet<string> SkippedLocations;
    public HashSet<string> excludedItems;
    public bool sunberryVillageExclusions;
    public bool visitMtVapiusExclusions;
    public bool baublesExclusions;
    public bool resourceChickensExclusions;
    public bool capeStardewExclusions;
    public bool collectWildflowers;
    public SButton globalFireButton;
    public SButton designateGrabberButton;
    public bool globalAutoFire;
    public int globalButtonOffsetX;
    public int globalButtonOffsetY;
    public int renameButtonOffsetX;
    public int renameButtonOffsetY;

    internal static Dictionary<HarvestCropsRangeMode, string> HarvestCropsRangeDict = new()
    {
        { HarvestCropsRangeMode.Square, "Square" },
        { HarvestCropsRangeMode.Walk, "Walk" }
    };

    internal static Dictionary<GlobalGrabberMode, string> GlobalGrabberDict = new()
    {
        { GlobalGrabberMode.Off, "Off" },
        { GlobalGrabberMode.All, "All" },
        { GlobalGrabberMode.Hover, "Hover" }
    };

    internal static Dictionary<GrabFrequency, string> GrabFrequencyDict = new()
    {
        { GrabFrequency.Instant, "Instant" },
        { GrabFrequency.Hourly, "Hourly" },
        { GrabFrequency.Daily, "Daily" }
    };

    internal static Dictionary<FlowerHarvestMode, string> FlowerHarvestDict = new()
    {
        { FlowerHarvestMode.Off, "Off" },
        { FlowerHarvestMode.All, "All" },
        { FlowerHarvestMode.Smart, "Smart" }
    };

    internal static Dictionary<GrabberMode, string> GrabberModeDict = new()
    {
        { GrabberMode.Classic, "Classic" },
        { GrabberMode.Specialized, "Specialized" }
    };

    internal static Dictionary<string, HarvestCropsRangeMode> HarvestCropsRangeReverseDict =
        HarvestCropsRangeDict.ToDictionary(p => p.Value, p => p.Key);

    internal static Dictionary<string, GlobalGrabberMode> GlobalGrabberReverseDict =
        GlobalGrabberDict.ToDictionary(p => p.Value, p => p.Key);

    internal static Dictionary<string, GrabFrequency> GrabFrequencyReverseDict =
        GrabFrequencyDict.ToDictionary(p => p.Value, p => p.Key);

    internal static Dictionary<string, FlowerHarvestMode> FlowerHarvestReverseDict =
        FlowerHarvestDict.ToDictionary(p => p.Value, p => p.Key);

    internal static Dictionary<string, GrabberMode> GrabberModeReverseDict =
        GrabberModeDict.ToDictionary(p => p.Value, p => p.Key);

    internal static string[] HarvestCropsRangeModeStrings = { "Square", "Walk" };
    internal static string[] GlobalGrabberModeStrings = { "Off", "All", "Hover" };
    internal static string[] GrabFrequencyStrings = { "Instant", "Hourly", "Daily" };
    internal static string[] FlowerHarvestStrings = { "Off", "All", "Smart" };
    internal static string[] GrabberModeStrings = { "Classic", "Specialized" };

    internal static readonly HashSet<string> SunberryVillageExcludedItems = new()
    {
        "(O)skellady.SBVCP_AnnabergiteNode",
        "(O)skellady.SBVCP_BlueAuraQuartzNode",
        "(O)skellady.SBVCP_CitrineNode",
        "(O)skellady.SBVCP_EclipseOrbNode",
        "(O)skellady.SBVCP_MidnightOrbNode",
        "(O)skellady.SBVCP_PurpuriteNode",
        "(O)skellady.SBVCP_SerpentineNode",
        "(O)skellady.SBVCP_SunberryGeodeNode",
        "(O)skellady.SBVCP_SunriseOrbNode",
        "(O)skellady.SBVCP_SunsetOrbNode",
        "(O)skellady.SBVCP_SupplyCrate1",
        "(O)skellady.SBVCP_SupplyCrate2"
    };

    internal static readonly HashSet<string> BaublesExcludedItems = new()
    {
        "(O)appleseed.BCP.CattailNodeOne",
        "(O)appleseed.BCP.CattailNodeTwo",
        "(O)appleseed.BCP.PetuntseNode"
    };

    internal static readonly HashSet<string> ResourceChickensExcludedItems = new()
    {
        "(O)UncleArya.ResourceChickens.WeedFiberEgg",
        "(O)UncleArya.ResourceChickens.WeedMossEgg",
        "(O)UncleArya.ResourceChickens.GeodeStoneEgg",
        "(O)UncleArya.ResourceChickens.FossilStoneBoneEgg",
        "(O)UncleArya.ResourceChickens.MineBarrelBoneEgg",
        "(O)UncleArya.ResourceChickens.QuarryMineMonsterEgg",
        "(O)UncleArya.ResourceChickens.SkullCavernBarrelBombEgg",
        "(O)UncleArya.ResourceChickens.VolcanoMineVolcanoEgg",
        "(O)UncleArya.ResourceChickens.DangerousMineRadioactiveEgg"
    };

    internal static readonly HashSet<string> CapeStardewExcludedItems = new()
    {
        "(O)Cape.kimberliteheart",
        "(O)Cape.kimberlitefire",
        "(O)Cape.kimberliteblue",
        "(O)Cape.kimberlitecelestial"
    };

    public bool IsItemExcluded(string qualifiedItemId)
    {
        if (excludedItems != null && excludedItems.Contains(qualifiedItemId))
            return true;
        if (sunberryVillageExclusions && SunberryVillageExcludedItems.Contains(qualifiedItemId))
            return true;
        if (visitMtVapiusExclusions && qualifiedItemId.Contains("_Node_"))
            return true;
        if (baublesExclusions && BaublesExcludedItems.Contains(qualifiedItemId))
            return true;
        if (resourceChickensExclusions && ResourceChickensExcludedItems.Contains(qualifiedItemId))
            return true;
        if (capeStardewExclusions && CapeStardewExcludedItems.Contains(qualifiedItemId))
            return true;
        return false;
    }

    public ModConfig()
    {
        grabberMode = GrabberMode.Classic;
        animalProducts = true;
        slimeHutch = true;
        farmCaveMushrooms = true;
        harvestCrops = false;
        harvestCropsIndoorPots = true;
        harvestCropsRange = -1;
        harvestCropsRangeMode = HarvestCropsRangeMode.Walk;
        artifactSpots = false;
        orePan = false;
        forage = true;
        bushes = true;
        fruitTrees = false;
        seedTrees = false;
        flowers = FlowerHarvestMode.Smart;
        beeHouseRange = 5;
        reportYield = true;
        replantReminder = true;
        replantReminderTime = 2000;
        debugLogging = false;
        specializedGrabbersCountForPerfection = true;
        gainExperience = true;
        fellHardwoodStumps = false;
        garbageCans = false;
        globalGrabber = GlobalGrabberMode.Off;
        globalFireButton = SButton.B;
        designateGrabberButton = SButton.G;
        globalAutoFire = false;
        globalButtonOffsetX = 0;
        globalButtonOffsetY = 0;
        renameButtonOffsetX = 0;
        renameButtonOffsetY = 0;
        seedSpots = false;
        harvestMoss = false;
        harvestGreenRainWeeds = false;
        collectDebris = false;
        grabFrequency = GrabFrequency.Instant;
        excludeQuestItems = true;
        skipFestivalLocations = true;
        selectVisitedOnly = false;
        disableMachineCollection = false;
        collectCrabPots = true;
        collectBeeHouses = true;
        collectTappers = true;
        collectLeafBaskets = true;
        collectMushroomLogs = true;
        collectFishPonds = true;
        collectKegs = true;
        collectPreservesJars = true;
        collectCheesePresses = true;
        collectMayonnaiseMachines = true;
        collectLooms = true;
        collectOilMakers = true;
        collectFurnaces = true;
        collectCharcoalKilns = true;
        collectRecyclingMachines = true;
        collectSeedMakers = true;
        collectBoneMills = true;
        collectGeodeCrushers = true;
        collectWoodChippers = true;
        collectDeconstructors = true;
        collectFishSmokers = true;
        collectBaitMakers = true;
        collectDehydrators = true;
        collectCrystalariums = true;
        collectLightningRods = true;
        collectWormBins = true;
        collectSolarPanels = true;
        collectSlimeEggPresses = true;
        collectCoffeeMakers = true;
        collectSodaMachines = true;
        collectStatues = true;
        collectOtherMachines = true;
        automateCompatibility = true;
        SkippedLocations = new HashSet<string>();
        excludedItems = new HashSet<string>();
        sunberryVillageExclusions = true;
        visitMtVapiusExclusions = true;
        baublesExclusions = true;
        resourceChickensExclusions = true;
        capeStardewExclusions = true;
        collectWildflowers = true;
    }

    internal ModConfig Clone()
    {
        var clone = (ModConfig)MemberwiseClone();
        clone.SkippedLocations = SkippedLocations != null ? new HashSet<string>(SkippedLocations) : new HashSet<string>();
        clone.excludedItems = excludedItems != null ? new HashSet<string>(excludedItems) : new HashSet<string>();
        return clone;
    }
}
