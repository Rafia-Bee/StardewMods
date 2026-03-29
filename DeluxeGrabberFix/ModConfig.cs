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

    public GlobalGrabberMode globalGrabber;
    public GrabFrequency grabFrequency;
    public HarvestCropsRangeMode harvestCropsRangeMode;
    public int harvestCropsRange;
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
    public bool debugLogging;
    public bool gainExperience;
    public bool fellHardwoodStumps;
    public bool fellSecretWoodsStumps;
    public bool garbageCans;
    public bool seedSpots;
    public bool harvestMoss;
    public bool harvestGreenRainWeeds;
    public bool skipFestivalLocations;
    public bool selectVisitedOnly;
    public bool collectMachines;
    public bool collectCrabPots;
    public bool collectBeeHouses;
    public bool collectTappers;
    public bool collectLeafBaskets;
    public bool collectMushroomLogs;
    public bool collectFishPonds;
    public bool collectDebris;
    public HashSet<string> SkippedLocations;
    public HashSet<string> excludedItems;
    public bool sunberryVillageExclusions;
    public bool visitMtVapiusExclusions;
    public bool baublesExclusions;
    public bool resourceChickensExclusions;
    public bool capeStardewExclusions;
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

    internal static Dictionary<string, HarvestCropsRangeMode> HarvestCropsRangeReverseDict =
        HarvestCropsRangeDict.ToDictionary(p => p.Value, p => p.Key);

    internal static Dictionary<string, GlobalGrabberMode> GlobalGrabberReverseDict =
        GlobalGrabberDict.ToDictionary(p => p.Value, p => p.Key);

    internal static Dictionary<string, GrabFrequency> GrabFrequencyReverseDict =
        GrabFrequencyDict.ToDictionary(p => p.Value, p => p.Key);

    internal static Dictionary<string, FlowerHarvestMode> FlowerHarvestReverseDict =
        FlowerHarvestDict.ToDictionary(p => p.Value, p => p.Key);

    internal static string[] HarvestCropsRangeModeStrings = { "Square", "Walk" };
    internal static string[] GlobalGrabberModeStrings = { "Off", "All", "Hover" };
    internal static string[] GrabFrequencyStrings = { "Instant", "Hourly", "Daily" };
    internal static string[] FlowerHarvestStrings = { "Off", "All", "Smart" };

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
        debugLogging = false;
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
        skipFestivalLocations = true;
        selectVisitedOnly = false;
        collectMachines = false;
        collectCrabPots = true;
        collectBeeHouses = true;
        collectTappers = true;
        collectLeafBaskets = true;
        collectMushroomLogs = true;
        collectFishPonds = true;
        SkippedLocations = new HashSet<string>();
        excludedItems = new HashSet<string>();
        sunberryVillageExclusions = true;
        visitMtVapiusExclusions = true;
        baublesExclusions = true;
        resourceChickensExclusions = true;
        capeStardewExclusions = true;
    }
}
