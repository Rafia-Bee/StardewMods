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
    public bool skipFestivalLocations;
    public bool selectVisitedOnly;
    public bool collectMachines;
    public bool collectCrabPots;
    public bool collectBeeHouses;
    public bool collectTappers;
    public HashSet<string> SkippedLocations;
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
        skipFestivalLocations = true;
        selectVisitedOnly = false;
        collectMachines = false;
        collectCrabPots = true;
        collectBeeHouses = true;
        collectTappers = true;
        SkippedLocations = new HashSet<string>();
    }
}
