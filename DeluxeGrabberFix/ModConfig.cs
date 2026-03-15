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

    public GlobalGrabberMode globalGrabber;
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
    public bool flowers;
    public bool reportYield;
    public bool debugLogging;
    public bool gainExperience;
    public bool fellSecretWoodsStumps;
    public bool garbageCans;
    public bool seedSpots;
    public bool harvestMoss;
    public bool hourlyCollection;
    public bool skipFestivalLocations;
    public bool selectVisitedOnly;
    public bool collectMachines;
    public bool collectCrabPots;
    public bool collectBeeHouses;
    public bool collectTappers;
    public bool collectDebris;
    public HashSet<string> SkippedLocations;
    public HashSet<string> excludedItems;
    public bool sunberryVillageExclusions;
    public bool visitMtVapiusExclusions;
    public SButton globalFireButton;
    public SButton designateGrabberButton;
    public bool globalAutoFire;
    public int globalButtonOffsetX;
    public int globalButtonOffsetY;

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

    internal static Dictionary<string, HarvestCropsRangeMode> HarvestCropsRangeReverseDict =
        HarvestCropsRangeDict.ToDictionary(p => p.Value, p => p.Key);

    internal static Dictionary<string, GlobalGrabberMode> GlobalGrabberReverseDict =
        GlobalGrabberDict.ToDictionary(p => p.Value, p => p.Key);

    internal static string[] HarvestCropsRangeModeStrings = { "Square", "Walk" };
    internal static string[] GlobalGrabberModeStrings = { "Off", "All", "Hover" };

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

    public bool IsItemExcluded(string qualifiedItemId)
    {
        if (excludedItems != null && excludedItems.Contains(qualifiedItemId))
            return true;
        if (sunberryVillageExclusions && SunberryVillageExcludedItems.Contains(qualifiedItemId))
            return true;
        if (visitMtVapiusExclusions && qualifiedItemId.Contains("_Node_"))
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
        flowers = false;
        reportYield = true;
        debugLogging = false;
        gainExperience = true;
        fellSecretWoodsStumps = false;
        garbageCans = false;
        globalGrabber = GlobalGrabberMode.Off;
        globalFireButton = SButton.B;
        designateGrabberButton = SButton.G;
        globalAutoFire = false;
        globalButtonOffsetX = 0;
        globalButtonOffsetY = 0;
        seedSpots = false;
        harvestMoss = false;
        collectDebris = false;
        hourlyCollection = true;
        skipFestivalLocations = true;
        selectVisitedOnly = false;
        collectMachines = false;
        collectCrabPots = true;
        collectBeeHouses = true;
        collectTappers = true;
        SkippedLocations = new HashSet<string>();
        excludedItems = new HashSet<string>();
        sunberryVillageExclusions = true;
        visitMtVapiusExclusions = true;
    }
}
