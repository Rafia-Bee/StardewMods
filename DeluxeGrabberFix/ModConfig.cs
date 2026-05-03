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

    // ---- Top-level: modes + cross-cutting UX flags ----

    public GrabberMode grabberMode = GrabberMode.Classic;
    public GlobalGrabberMode globalGrabber = GlobalGrabberMode.Off;
    public GrabFrequency grabFrequency = GrabFrequency.Instant;

    public bool reportYield = true;
    public bool debugLogging;
    public bool gainExperience = true;
    public bool replantReminder = true;
    public int replantReminderTime = 2000;

    // ---- Nested groups ----

    public FeatureSettings Features { get; set; } = new();
    public MachineSettings Machines { get; set; } = new();
    public SpecializedSettings Specialized { get; set; } = new();
    public LocationSettings Locations { get; set; } = new();
    public GlobalGrabSettings GlobalGrab { get; set; } = new();
    public CompatibilitySettings Compatibility { get; set; } = new();

    internal class FeatureSettings
    {
        public bool animalProducts = true;
        public bool slimeHutch = true;
        public bool farmCaveMushrooms = true;
        public bool harvestCrops;
        public bool harvestCropsIndoorPots = true;
        public int harvestCropsRange = -1;
        public HarvestCropsRangeMode harvestCropsRangeMode = HarvestCropsRangeMode.Walk;
        public bool artifactSpots;
        public bool buriedItems = true;
        public bool orePan;
        public bool forage = true;
        public bool bushes = true;
        public bool fruitTrees;
        public bool seedTrees;
        public FlowerHarvestMode flowers = FlowerHarvestMode.Smart;
        public int beeHouseRange = 5;
        public bool garbageCans;
        public bool seedSpots;
        public bool harvestMoss;
        public bool harvestGreenRainWeeds;
        public bool fellHardwoodStumps;
        public bool collectDebris;
        public bool collectWildflowers = true;
    }

    internal class MachineSettings
    {
        public bool disableMachineCollection;
        public bool collectCrabPots = true;
        public bool collectBeeHouses = true;
        public bool collectTappers = true;
        public bool collectLeafBaskets = true;
        public bool collectMushroomLogs = true;
        public bool collectFishPonds = true;
        public bool collectKegs = true;
        public bool collectPreservesJars = true;
        public bool collectCheesePresses = true;
        public bool collectMayonnaiseMachines = true;
        public bool collectLooms = true;
        public bool collectOilMakers = true;
        public bool collectFurnaces = true;
        public bool collectCharcoalKilns = true;
        public bool collectRecyclingMachines = true;
        public bool collectSeedMakers = true;
        public bool collectBoneMills = true;
        public bool collectGeodeCrushers = true;
        public bool collectWoodChippers = true;
        public bool collectDeconstructors = true;
        public bool collectFishSmokers = true;
        public bool collectBaitMakers = true;
        public bool collectDehydrators = true;
        public bool collectCrystalariums = true;
        public bool collectLightningRods = true;
        public bool collectWormBins = true;
        public bool collectSolarPanels = true;
        public bool collectSlimeEggPresses = true;
        public bool collectCoffeeMakers = true;
        public bool collectSodaMachines = true;
        public bool collectStatues = true;
        public bool collectOtherMachines = true;
        public bool automateCompatibility = true;
    }

    internal class SpecializedSettings
    {
        // Milestone thresholds
        public int cropsShippedThreshold = 200;
        public int itemsForagedThreshold = 200;
        public int stumpsChoppedThreshold = 50;
        public int museumDonationsThreshold = 15;
        public int totalMoneyEarnedThreshold = 250000;

        public bool specializedGrabbersCountForPerfection = true;

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
    }

    internal class LocationSettings
    {
        public HashSet<string> SkippedLocations = new();
        public bool skipFestivalLocations = true;
        public bool selectVisitedOnly;
    }

    internal class GlobalGrabSettings
    {
        public SButton globalFireButton = SButton.B;
        public SButton designateGrabberButton = SButton.G;
        public bool globalAutoFire;
        public int globalButtonOffsetX;
        public int globalButtonOffsetY;
        public int renameButtonOffsetX;
        public int renameButtonOffsetY;
    }

    internal class CompatibilitySettings
    {
        public HashSet<string> excludedItems = new();
        public bool excludeQuestItems = true;
        public bool sunberryVillageExclusions = true;
        public bool visitMtVapiusExclusions = true;
        public bool baublesExclusions = true;
        public bool resourceChickensExclusions = true;
        public bool capeStardewExclusions = true;
    }

    // ---- Static enum lookups (unchanged) ----

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
        var c = Compatibility;
        if (c.excludedItems != null && c.excludedItems.Contains(qualifiedItemId))
            return true;
        if (c.sunberryVillageExclusions && SunberryVillageExcludedItems.Contains(qualifiedItemId))
            return true;
        if (c.visitMtVapiusExclusions && qualifiedItemId.Contains("_Node_"))
            return true;
        if (c.baublesExclusions && BaublesExcludedItems.Contains(qualifiedItemId))
            return true;
        if (c.resourceChickensExclusions && ResourceChickensExcludedItems.Contains(qualifiedItemId))
            return true;
        if (c.capeStardewExclusions && CapeStardewExcludedItems.Contains(qualifiedItemId))
            return true;
        return false;
    }

    public ModConfig() { }

    internal ModConfig Clone()
    {
        var clone = (ModConfig)MemberwiseClone();

        clone.Features = new FeatureSettings();
        CopyAll(Features, clone.Features);

        clone.Machines = new MachineSettings();
        CopyAll(Machines, clone.Machines);

        clone.Specialized = new SpecializedSettings();
        CopyAll(Specialized, clone.Specialized);

        clone.Locations = new LocationSettings();
        CopyAll(Locations, clone.Locations);
        clone.Locations.SkippedLocations = Locations.SkippedLocations != null
            ? new HashSet<string>(Locations.SkippedLocations)
            : new HashSet<string>();

        clone.GlobalGrab = new GlobalGrabSettings();
        CopyAll(GlobalGrab, clone.GlobalGrab);

        clone.Compatibility = new CompatibilitySettings();
        CopyAll(Compatibility, clone.Compatibility);
        clone.Compatibility.excludedItems = Compatibility.excludedItems != null
            ? new HashSet<string>(Compatibility.excludedItems)
            : new HashSet<string>();

        return clone;
    }

    private static void CopyAll<T>(T source, T target) where T : class
    {
        foreach (var field in typeof(T).GetFields(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance))
        {
            field.SetValue(target, field.GetValue(source));
        }
    }
}
