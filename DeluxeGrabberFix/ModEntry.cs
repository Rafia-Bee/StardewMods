using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeluxeGrabberFix.Framework;
using DeluxeGrabberFix.Grabbers;
using DeluxeGrabberFix.Interfaces;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace DeluxeGrabberFix;

public class ModEntry : Mod
{
    internal readonly ModApi Api;
    internal ModConfig Config { get; set; }

    public ModEntry()
    {
        Api = new ModApi(this);
    }

    public override void Entry(IModHelper helper)
    {
        try
        {
            Config = Helper.ReadConfig<ModConfig>();
        }
        catch (Exception)
        {
            Config = new ModConfig();
        }

        helper.Events.GameLoop.GameLaunched += OnLaunched;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTenMinuteUpdate;
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    public void LogDebug(string message)
    {
        Monitor.Log(message, LogLevel.Trace);
    }

    public override object GetApi()
    {
        return Api;
    }

    private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
    {
        if (Config.globalGrabber == ModConfig.GlobalGrabberMode.Off || Config.globalFireButton != e.Button)
            return;

        LogDebug("Autograbbing on button pressed");
        var allLocations = Game1.locations
            .Concat(Game1.getFarm().buildings.Select(b => b.indoors.Value))
            .Where(loc => loc != null);

        foreach (var location in allLocations)
        {
            GrabAtLocation(location);
        }
    }

    private void OnLaunched(object sender, GameLaunchedEventArgs e)
    {
        var api = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api == null)
            return;

        api.Register(ModManifest,
            () => Config = new ModConfig(),
            () => Helper.WriteConfig(Config));

        // Crop Harvesting section
        api.AddSectionTitle(ModManifest,
            () => "Crop Harvesting",
            () => "These options are only considered if 'Harvest Crops' is enabled");

        api.AddBoolOption(ModManifest,
            () => Config.harvestCrops,
            v => Config.harvestCrops = v,
            () => "Harvest Crops");

        api.AddBoolOption(ModManifest,
            () => Config.harvestCropsIndoorPots,
            v => Config.harvestCropsIndoorPots = v,
            () => "Harvest Crops Inside Pots",
            () => "This is ignored if 'Harvest Crops' is disabled");

        api.AddBoolOption(ModManifest,
            () => Config.flowers,
            v => Config.flowers = v,
            () => "Harvest Flowers",
            () => "This is ignored if 'Harvest crops' is disabled");

        api.AddNumberOption(ModManifest,
            () => Config.harvestCropsRange,
            v => Config.harvestCropsRange = Math.Max(-1, v),
            () => "Harvest Range",
            () => "This is ignored if 'Harvest Crops' is disabled. Set to -1 to use infinite range. This ONLY affects crop harvesting.");

        api.AddTextOption(ModManifest,
            () => ModConfig.HarvestCropsRangeDict[Config.harvestCropsRangeMode],
            v => Config.harvestCropsRangeMode = ModConfig.HarvestCropsRangeReverseDict[v],
            () => "Harvest Range Mode",
            () => "'Walk': the distance is the walking distance (in four directions) from the grabber, becoming a diamond shape. 'Square': the distance is a square like a sprinkler. Covers more area than 'Walk'.",
            ModConfig.HarvestCropsRangeModeStrings);

        // Other Harvesting section
        api.AddSectionTitle(ModManifest, () => "Other Harvesting", () => "");

        api.AddBoolOption(ModManifest,
            () => Config.fruitTrees,
            v => Config.fruitTrees = v,
            () => "Harvest Fruit Trees");

        api.AddBoolOption(ModManifest,
            () => Config.bushes,
            v => Config.bushes = v,
            () => "Harvest Berry Bushes");

        api.AddBoolOption(ModManifest,
            () => Config.seedTrees,
            v => Config.seedTrees = v,
            () => "Shake Seed Trees");

        api.AddBoolOption(ModManifest,
            () => Config.slimeHutch,
            v => Config.slimeHutch = v,
            () => "Grab Slime Balls");

        api.AddBoolOption(ModManifest,
            () => Config.farmCaveMushrooms,
            v => Config.farmCaveMushrooms = v,
            () => "Grab Farm Cave Mushrooms",
            () => "This will also work for mushroom boxes placed outside the farm cave");

        api.AddBoolOption(ModManifest,
            () => Config.artifactSpots,
            v => Config.artifactSpots = v,
            () => "Dig Up Artifact Spots");

        api.AddBoolOption(ModManifest,
            () => Config.orePan,
            v => Config.orePan = v,
            () => "Collect Ore From Panning Sites");

        api.AddBoolOption(ModManifest,
            () => Config.fellSecretWoodsStumps,
            v => Config.fellSecretWoodsStumps = v,
            () => "Fell Stumps in Secret Woods");

        api.AddBoolOption(ModManifest,
            () => Config.garbageCans,
            v => Config.garbageCans = v,
            () => "Search Garbage Cans");

        api.AddBoolOption(ModManifest,
            () => Config.seedSpots,
            v => Config.seedSpots = v,
            () => "Dig up Seed Spots");

        api.AddBoolOption(ModManifest,
            () => Config.harvestMoss,
            v => Config.harvestMoss = v,
            () => "Harvest Moss from Trees");

        // Miscellaneous section
        api.AddSectionTitle(ModManifest, () => "Miscellaneous");

        api.AddBoolOption(ModManifest,
            () => Config.reportYield,
            v => Config.reportYield = v,
            () => "Report Yield",
            () => "Logs to the SMAPI console the yield of each auto grabber");

        api.AddBoolOption(ModManifest,
            () => Config.gainExperience,
            v => Config.gainExperience = v,
            () => "Gain Experience",
            () => "Gain appropriate experience as if you foraged or harvested yourself");

        api.AddTextOption(ModManifest,
            () => ModConfig.GlobalGrabberDict[Config.globalGrabber],
            v => Config.globalGrabber = ModConfig.GlobalGrabberReverseDict[v],
            () => "Global Grabber Mode",
            () => "'When on Hover you have to hover over a grabber with your cursor and press the Fire Global Grabber button to attempt to collect everything. On All Grabbers grab globally on day start and on button press, it should fill one inventory then move to the next (Warning, may cause performance issues)'.",
            ModConfig.GlobalGrabberModeStrings);

        api.AddKeybind(ModManifest,
            () => Config.globalFireButton,
            v => Config.globalFireButton = v,
            () => "Fire Global Grabber Event",
            () => "Grabbers grab globally on button press, it should fill one inventory then move to the next, only works if you have the global grabber setting on (Warning, will cause lag when called that will be worst the bigger your map is)");
    }

    private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
    {
        LogDebug("Object list changed at " + e.Location.Name);
        GrabAtLocation(e.Location);
    }

    private void OnTenMinuteUpdate(object sender, TimeChangedEventArgs e)
    {
        if (e.NewTime % 100 != 0)
            return;

        LogDebug("Autograbbing on time change");
        foreach (var location in Game1.locations)
        {
            var orePanGrabber = new OrePanGrabber(this, location);
            if (orePanGrabber.CanGrab())
                orePanGrabber.GrabItems();
        }
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        LogDebug("Autograbbing on day start");
        var allLocations = Game1.locations
            .Concat(Game1.getFarm().buildings.Select(b => b.indoors.Value))
            .Where(loc => loc != null);

        foreach (var location in allLocations)
        {
            GrabAtLocation(location, DayStarted: true);
        }
    }

    private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsPlayerFree || Game1.eventUp || Game1.farmEvent != null
            || Config.harvestCropsRange <= 0 || !Config.harvestCrops
            || Game1.player.ActiveObject == null
            || !Game1.player.ActiveObject.bigCraftable.Value
            || Game1.player.ActiveObject.ParentSheetIndex != 165)
        {
            return;
        }

        Vector2 grabTile = Game1.GetPlacementGrabTile();
        int centerX = (int)grabTile.X;
        int centerY = (int)grabTile.Y;

        if (!Game1.IsPerformingMousePlacement())
            return;

        int range = Config.harvestCropsRange;
        for (int x = centerX - range; x <= centerX + range; x++)
        {
            for (int y = centerY - range; y <= centerY + range; y++)
            {
                if (Config.harvestCropsRangeMode != ModConfig.HarvestCropsRangeMode.Walk
                    || Math.Abs(centerX - x) + Math.Abs(centerY - y) <= range)
                {
                    Game1.spriteBatch.Draw(
                        Game1.mouseCursors,
                        Game1.GlobalToLocal(new Vector2(x, y) * 64f),
                        new Rectangle(194, 388, 16, 16),
                        Color.White, 0f, Vector2.Zero, 4f,
                        SpriteEffects.None, 0.01f);
                }
            }
        }
    }

    private bool GrabAtLocation(GameLocation location, bool DayStarted = false)
    {
        var aggregateGrabber = new AggregateDailyGrabber(this, location, DayStarted);
        var beforeInventory = Config.reportYield ? aggregateGrabber.GetInventory() : null;
        bool result = aggregateGrabber.GrabItems();

        if (beforeInventory != null)
        {
            var afterInventory = aggregateGrabber.GetInventory();
            var sb = new StringBuilder($"Yield of autograbber(s) at {location.Name}:\n");
            bool anyYield = false;

            foreach (var entry in afterInventory)
            {
                int newCount = entry.Value;
                if (beforeInventory.ContainsKey(entry.Key))
                    newCount -= beforeInventory[entry.Key];

                if (newCount > 0)
                {
                    sb.AppendLine($"    {entry.Key.Name} ({entry.Key.QualityName}) x{newCount}");
                    anyYield = true;
                }
            }

            if (anyYield)
                Monitor.Log(sb.ToString(), LogLevel.Info);
        }

        return result;
    }
}
