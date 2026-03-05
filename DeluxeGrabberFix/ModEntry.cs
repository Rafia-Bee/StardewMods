using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeluxeGrabberFix.Framework;
using DeluxeGrabberFix.Grabbers;
using DeluxeGrabberFix.Interfaces;
using HarmonyLib;
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
    internal bool IsGlobalGrabActive { get; set; }
    internal List<KeyValuePair<Vector2, Object>> CachedDesignatedGrabbers { get; set; }
    internal const string GlobalGrabberModDataKey = "Rafia.DeluxeGrabberFix/IsGlobalGrabber";
    private readonly HashSet<GameLocation> _dirtyLocations = new();
    private bool _isGrabbing;
    private bool _pendingDayStartGrab;
    private IGenericModConfigMenuApi _gmcmApi;
    private List<(string Name, string DisplayName)> _discoveredLocations;
    private LocationBatchAction? _pendingLocationBatchAction;
    private SaveData _saveData;
    private GlobalGrabberButton _globalGrabberButton;
    private static GlobalGrabberButton _staticGlobalGrabberButton;
    private bool _pendingGlobalAutoFire;

    private enum LocationBatchAction { EnableAll, DisableAll, SelectVisitedOnly }
    private const string SaveDataKey = "visit-tracking";

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
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Display.MenuChanged += OnMenuChanged;
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Player.Warped += OnPlayerWarped;
    }

    public void LogDebug(string message)
    {
        if (Config.debugLogging)
            Monitor.Log(message, LogLevel.Trace);
    }

    public override object GetApi()
    {
        return Api;
    }

    internal static IEnumerable<GameLocation> GetAllLocations()
    {
        foreach (var location in Game1.locations)
        {
            yield return location;
            foreach (var building in location.buildings)
            {
                if (building.indoors.Value != null)
                    yield return building.indoors.Value;
            }
        }
    }

    private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsPlayerFree)
            return;

        if (e.Button == Config.designateGrabberButton
            && Config.globalGrabber == ModConfig.GlobalGrabberMode.All)
        {
            HandleDesignateGrabber();
            return;
        }

        if (Config.globalGrabber == ModConfig.GlobalGrabberMode.Off || Config.globalFireButton != e.Button)
            return;

        if (Config.globalGrabber == ModConfig.GlobalGrabberMode.All && !HasDesignatedGrabber())
        {
            Game1.addHUDMessage(new HUDMessage("Designate a global grabber first by opening an auto-grabber and pressing the star button, or by hovering over it and pressing the designate key.", HUDMessage.error_type));
            return;
        }

        FireGlobalGrab();
    }

    private void FireGlobalGrab()
    {
        DiscoverLocations();
        if (Config.selectVisitedOnly)
            ApplyVisitAutoSkip();

        LogDebug("Firing global grab");
        IsGlobalGrabActive = true;
        try
        {
            var allLocations = GetAllLocations().ToList();

            if (Config.globalGrabber == ModConfig.GlobalGrabberMode.All)
            {
                CachedDesignatedGrabbers = new List<KeyValuePair<Vector2, Object>>();
                foreach (var loc in allLocations)
                {
                    CachedDesignatedGrabbers.AddRange(
                        loc.Objects.Pairs
                            .Where(pair => pair.Value != null
                                && pair.Value.modData.ContainsKey(GlobalGrabberModDataKey))
                            .ToList());
                }
            }

            foreach (var location in allLocations)
            {
                GrabAtLocation(location);
            }
        }
        finally
        {
            IsGlobalGrabActive = false;
            CachedDesignatedGrabbers = null;
        }
    }

    private void OnMenuChanged(object sender, MenuChangedEventArgs e)
    {
        _globalGrabberButton = null;
        _staticGlobalGrabberButton = null;

        if (e.NewMenu is not StardewValley.Menus.ItemGrabMenu grabMenu)
            return;

        // The auto-grabber passes itself as 'context', not 'sourceItem'
        if (grabMenu.context is not Object obj || obj.ParentSheetIndex != 165
            || obj.heldObject.Value is not StardewValley.Objects.Chest)
            return;

        _globalGrabberButton = new GlobalGrabberButton(this, obj, grabMenu);
        _staticGlobalGrabberButton = _globalGrabberButton;
    }

    private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e)
    {
        if (_globalGrabberButton == null || Game1.activeClickableMenu is not StardewValley.Menus.ItemGrabMenu)
            return;

        _globalGrabberButton.Draw(e.SpriteBatch);
    }

    private void HandleDesignateGrabber()
    {
        var cursorTile = Game1.lastCursorTile;
        var obj = Game1.player.currentLocation.getObjectAtTile((int)cursorTile.X, (int)cursorTile.Y);

        if (obj == null || obj.ParentSheetIndex != 165
            || obj.heldObject.Value is not StardewValley.Objects.Chest)
        {
            Game1.addHUDMessage(new HUDMessage("You need to hover your cursor over an auto-grabber to designate it.", HUDMessage.error_type));
            return;
        }

        if (obj.modData.ContainsKey(GlobalGrabberModDataKey))
        {
            obj.modData.Remove(GlobalGrabberModDataKey);
            Game1.addHUDMessage(new HUDMessage("This auto-grabber is no longer the Global Grabber."));
            return;
        }

        ClearAllDesignations();
        obj.modData[GlobalGrabberModDataKey] = "true";
        Game1.addHUDMessage(new HUDMessage("This auto-grabber is now the Global Grabber!"));
    }

    private void ClearAllDesignations()
    {
        foreach (var location in GetAllLocations())
        {
            foreach (var pair in location.Objects.Pairs)
            {
                if (pair.Value.modData.ContainsKey(GlobalGrabberModDataKey))
                    pair.Value.modData.Remove(GlobalGrabberModDataKey);
            }
        }
    }

    private bool HasDesignatedGrabber()
    {
        foreach (var location in GetAllLocations())
        {
            foreach (var pair in location.Objects.Pairs)
            {
                if (pair.Value.modData.ContainsKey(GlobalGrabberModDataKey))
                    return true;
            }
        }
        return false;
    }

    internal static void ItemGrabMenu_ReceiveLeftClick_Postfix(int x, int y)
    {
        _staticGlobalGrabberButton?.TryClick(x, y);
    }

    private void OnLaunched(object sender, GameLaunchedEventArgs e)
    {
        var harmony = new Harmony(ModManifest.UniqueID);
        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.createItemDebris)),
            prefix: new HarmonyMethod(typeof(HarvestInterceptor), nameof(HarvestInterceptor.CreateItemDebris_Prefix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Menus.ItemGrabMenu), nameof(StardewValley.Menus.ItemGrabMenu.receiveLeftClick)),
            postfix: new HarmonyMethod(typeof(ModEntry), nameof(ItemGrabMenu_ReceiveLeftClick_Postfix))
        );

        _gmcmApi = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (_gmcmApi == null)
            return;

        RegisterConfigMenu();
    }

    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
        _saveData = Helper.Data.ReadSaveData<SaveData>(SaveDataKey) ?? new SaveData();
        DiscoverLocations();
        ApplyVisitAutoSkip();
        RebuildConfigMenu();
    }

    private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
    {
        _discoveredLocations = null;
        _saveData = null;
        RebuildConfigMenu();
    }

    private void OnPlayerWarped(object sender, WarpedEventArgs e)
    {
        if (!Config.selectVisitedOnly || _saveData == null)
            return;

        string name = e.NewLocation?.Name;
        if (string.IsNullOrEmpty(name))
            return;

        if (_saveData.AutoSkippedLocations.Remove(name))
        {
            Config.SkippedLocations?.Remove(name);
            Helper.WriteConfig(Config);
            Helper.Data.WriteSaveData(SaveDataKey, _saveData);
            LogDebug($"Auto-enabled location after visit: {name}");

            if (_gmcmApi != null)
                RebuildConfigMenu();
        }
    }

    private void ApplyVisitAutoSkip()
    {
        if (!Config.selectVisitedOnly || _discoveredLocations == null)
        {
            Monitor.Log($"ApplyVisitAutoSkip skipped: selectVisitedOnly={Config.selectVisitedOnly}, discoveredLocations={_discoveredLocations?.Count ?? -1}", LogLevel.Info);
            return;
        }

        if (_saveData == null)
        {
            Monitor.Log("ApplyVisitAutoSkip skipped: _saveData is null", LogLevel.Info);
            return;
        }

        Config.SkippedLocations ??= new HashSet<string>();
        int skipped = 0;
        int enabled = 0;

        foreach (var (locName, _) in _discoveredLocations)
        {
            bool visited = Game1.MasterPlayer.locationsVisited.Contains(locName);

            if (!visited
                && !Config.SkippedLocations.Contains(locName)
                && !_saveData.AutoSkippedLocations.Contains(locName)
                && !_saveData.ManuallyManagedLocations.Contains(locName))
            {
                Config.SkippedLocations.Add(locName);
                _saveData.AutoSkippedLocations.Add(locName);
                skipped++;
            }
            else if (visited && _saveData.AutoSkippedLocations.Contains(locName))
            {
                Config.SkippedLocations.Remove(locName);
                _saveData.AutoSkippedLocations.Remove(locName);
                enabled++;
            }
        }

        Monitor.Log($"ApplyVisitAutoSkip: {_discoveredLocations.Count} locations checked, {skipped} auto-skipped, {enabled} auto-enabled", LogLevel.Info);

        if (skipped > 0 || enabled > 0)
        {
            Helper.WriteConfig(Config);
            Helper.Data.WriteSaveData(SaveDataKey, _saveData);
        }
    }

    private void DiscoverLocations()
    {
        _discoveredLocations = GetAllLocations()
            .Where(loc => !string.IsNullOrEmpty(loc.Name))
            .GroupBy(loc => loc.Name)
            .Select(g => (Name: g.Key, DisplayName: GetLocationDisplayName(g.First())))
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    private static string GetLocationDisplayName(GameLocation location)
    {
        string display = location.DisplayName;

        if (string.IsNullOrEmpty(display)
            || display.StartsWith("(no translation", StringComparison.OrdinalIgnoreCase))
        {
            display = location.Name;

            if (display.StartsWith("Custom_"))
                display = display.Substring(7);

            display = display.Replace('_', ' ');
        }

        return display;
    }

    private void RebuildConfigMenu()
    {
        if (_gmcmApi == null)
            return;

        _gmcmApi.Unregister(ModManifest);
        RegisterConfigMenu();
    }

    private void RegisterConfigMenu()
    {
        var api = _gmcmApi;

        api.Register(ModManifest,
            () => Config = new ModConfig(),
            () =>
            {
                Helper.WriteConfig(Config);
                Monitor.Log($"GMCM saved. selectVisitedOnly={Config.selectVisitedOnly}, IsWorldReady={Context.IsWorldReady}, saveData={((_saveData != null) ? "loaded" : "null")}", LogLevel.Info);
                if (Config.selectVisitedOnly && Context.IsWorldReady && _saveData != null)
                {
                    DiscoverLocations();
                    ApplyVisitAutoSkip();
                }
            });

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
            () => Config.animalProducts,
            v => Config.animalProducts = v,
            () => "Collect Animal Products",
            () => "Collects eggs, milk, wool, and other animal products from barns and coops. Also picks up dropped items like eggs from the floor.");

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

        // Machine Collection section
        api.AddSectionTitle(ModManifest,
            () => "Machine Collection",
            () => "Automatically collect finished outputs from supported machines");

        api.AddBoolOption(ModManifest,
            () => Config.collectMachines,
            v => Config.collectMachines = v,
            () => "Collect Machine Outputs",
            () => "Automatically collects finished outputs from supported machines and deposits them into nearby auto-grabbers");

        api.AddBoolOption(ModManifest,
            () => Config.collectCrabPots,
            v => Config.collectCrabPots = v,
            () => "Collect from Crab Pots",
            () => "This is ignored if 'Collect Machine Outputs' is disabled");

        api.AddBoolOption(ModManifest,
            () => Config.collectBeeHouses,
            v => Config.collectBeeHouses = v,
            () => "Collect from Bee Houses",
            () => "This is ignored if 'Collect Machine Outputs' is disabled");

        api.AddBoolOption(ModManifest,
            () => Config.collectTappers,
            v => Config.collectTappers = v,
            () => "Collect from Tappers",
            () => "Collects from both regular and heavy tappers. This is ignored if 'Collect Machine Outputs' is disabled");

        // Miscellaneous section
        api.AddSectionTitle(ModManifest, () => "Miscellaneous");

        api.AddBoolOption(ModManifest,
            () => Config.reportYield,
            v => Config.reportYield = v,
            () => "Report Yield",
            () => "Logs to the SMAPI console the yield of each auto grabber");

        api.AddBoolOption(ModManifest,
            () => Config.debugLogging,
            v => Config.debugLogging = v,
            () => "Debug Logging",
            () => "Logs detailed trace info to the SMAPI log file for troubleshooting");

        api.AddBoolOption(ModManifest,
            () => Config.gainExperience,
            v => Config.gainExperience = v,
            () => "Gain Experience",
            () => "Gain appropriate experience as if you foraged or harvested yourself");

        api.AddBoolOption(ModManifest,
            () => Config.skipFestivalLocations,
            v => Config.skipFestivalLocations = v,
            () => "Skip Festival Locations",
            () => "Prevents auto-grabbers from collecting items in festival and temporary event locations, including during active festivals");

        api.AddTextOption(ModManifest,
            () => ModConfig.GlobalGrabberDict[Config.globalGrabber],
            v => Config.globalGrabber = ModConfig.GlobalGrabberReverseDict[v],
            () => "Global Grabber Mode",
            () => "'Hover': hover over a grabber and press the fire key to make it collect from all locations. 'All': designate a grabber (open it and tap the star button, or hover + press designate key), then fire it to collect globally.",
            ModConfig.GlobalGrabberModeStrings);

        api.AddBoolOption(ModManifest,
            () => Config.globalAutoFire,
            v => Config.globalAutoFire = v,
            () => "Auto-Fire Global Grabber",
            () => "When enabled, the designated global grabber automatically collects from all locations at the start of each day. Only used in All mode. Great for mobile users.");

        api.AddKeybind(ModManifest,
            () => Config.globalFireButton,
            v => Config.globalFireButton = v,
            () => "Fire Global Grabber",
            () => "Press to trigger the designated or hovered grabber to collect items from all locations. Only works when Global Grabber Mode is set to All or Hover.");

        api.AddKeybind(ModManifest,
            () => Config.designateGrabberButton,
            v => Config.designateGrabberButton = v,
            () => "Designate Global Grabber",
            () => "Hover over an auto-grabber and press this key to designate it as the global grabber. Only used in All mode. On mobile, use the star button in the auto-grabber menu instead.");

        api.AddNumberOption(ModManifest,
            () => Config.globalButtonOffsetX,
            v => Config.globalButtonOffsetX = v,
            () => "Global Button X Offset",
            () => "Adjust the horizontal position of the Global Grabber star button in the auto-grabber menu. Use this if it overlaps with buttons from other mods.",
            -500, 500);

        api.AddNumberOption(ModManifest,
            () => Config.globalButtonOffsetY,
            v => Config.globalButtonOffsetY = v,
            () => "Global Button Y Offset",
            () => "Adjust the vertical position of the Global Grabber star button in the auto-grabber menu. Use this if it overlaps with buttons from other mods.",
            -500, 500);

        // Skipped Locations page link
        api.AddPageLink(ModManifest, "skipped-locations",
            () => "Skipped Locations >",
            () => "Choose which game locations to skip when auto-grabbing");

        // Skipped Locations page
        api.AddPage(ModManifest, "skipped-locations", () => "Skipped Locations");

        if (_discoveredLocations != null && _discoveredLocations.Count > 0)
        {
            api.AddParagraph(ModManifest,
                () => "Toggle locations on or off. Disabled locations will be skipped by all auto-grabbers.");

            api.AddBoolOption(ModManifest,
                getValue: () => _discoveredLocations.All(loc => Config.SkippedLocations?.Contains(loc.Name) != true),
                setValue: v => { },
                name: () => "Enable All",
                tooltip: () => "Toggle all locations on or off at once",
                fieldId: "enable-all");

            api.AddBoolOption(ModManifest,
                getValue: () => Context.IsWorldReady && _discoveredLocations != null &&
                    _discoveredLocations.All(loc =>
                    {
                        bool visited = Game1.MasterPlayer.locationsVisited.Contains(loc.Name);
                        bool enabled = Config.SkippedLocations?.Contains(loc.Name) != true;
                        return visited == enabled;
                    }),
                setValue: v => { },
                name: () => "Select Visited Only",
                tooltip: () => "Enables only locations you've visited and skips the rest. If you discover new locations, toggle this again to update.",
                fieldId: "select-visited-only");

            api.OnFieldChanged(ModManifest, (fieldId, value) =>
            {
                if (fieldId == "enable-all")
                    _pendingLocationBatchAction = (bool)value
                        ? LocationBatchAction.EnableAll
                        : LocationBatchAction.DisableAll;
                else if (fieldId == "select-visited-only" && (bool)value)
                    _pendingLocationBatchAction = LocationBatchAction.SelectVisitedOnly;
            });

            if (Context.IsWorldReady)
            {
                var visitedLocs = _discoveredLocations
                    .Where(loc => Game1.MasterPlayer.locationsVisited.Contains(loc.Name))
                    .ToList();
                var unvisitedLocs = _discoveredLocations
                    .Where(loc => !Game1.MasterPlayer.locationsVisited.Contains(loc.Name))
                    .ToList();

                if (visitedLocs.Count > 0)
                {
                    api.AddSectionTitle(ModManifest, () => "Visited Locations");
                    foreach (var (locName, displayName) in visitedLocs)
                        AddLocationToggle(api, locName, displayName);
                }

                if (unvisitedLocs.Count > 0)
                {
                    api.AddSectionTitle(ModManifest, () => "Not Yet Visited");
                    foreach (var (locName, displayName) in unvisitedLocs)
                        AddLocationToggle(api, locName, displayName);
                }
            }
            else
            {
                foreach (var (locName, displayName) in _discoveredLocations)
                    AddLocationToggle(api, locName, displayName);
            }
        }
        else
        {
            api.AddParagraph(ModManifest,
                () => "Load a save file to see available locations.");
        }
    }

    private void AddLocationToggle(IGenericModConfigMenuApi api, string locName, string displayName)
    {
        string capturedName = locName;
        string capturedDisplay = displayName;

        api.AddBoolOption(ModManifest,
            getValue: () => Config.SkippedLocations?.Contains(capturedName) != true,
            setValue: v =>
            {
                Config.SkippedLocations ??= new HashSet<string>();
                if (!v)
                    Config.SkippedLocations.Add(capturedName);
                else
                    Config.SkippedLocations.Remove(capturedName);

                if (_saveData != null)
                {
                    _saveData.ManuallyManagedLocations.Add(capturedName);
                    _saveData.AutoSkippedLocations.Remove(capturedName);
                    Helper.Data.WriteSaveData(SaveDataKey, _saveData);
                }
            },
            name: () => capturedDisplay,
            tooltip: () => capturedName != capturedDisplay ? capturedName : null);
    }

    private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
    {
        if (_isGrabbing)
            return;

        _dirtyLocations.Add(e.Location);
    }

    private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
    {
        if (_pendingLocationBatchAction.HasValue)
        {
            var action = _pendingLocationBatchAction.Value;
            _pendingLocationBatchAction = null;

            Config.SkippedLocations ??= new HashSet<string>();

            switch (action)
            {
                case LocationBatchAction.EnableAll:
                    Config.SkippedLocations.Clear();
                    break;

                case LocationBatchAction.DisableAll:
                    if (_discoveredLocations != null)
                        foreach (var loc in _discoveredLocations)
                            Config.SkippedLocations.Add(loc.Name);
                    break;

                case LocationBatchAction.SelectVisitedOnly:
                    Config.selectVisitedOnly = true;
                    if (_discoveredLocations != null)
                    {
                        foreach (var (locName, _) in _discoveredLocations)
                        {
                            bool visited = Game1.MasterPlayer.locationsVisited.Contains(locName);
                            if (visited)
                                Config.SkippedLocations.Remove(locName);
                            else
                                Config.SkippedLocations.Add(locName);
                        }

                        if (_saveData != null)
                        {
                            _saveData.AutoSkippedLocations.Clear();
                            foreach (var name in Config.SkippedLocations)
                                _saveData.AutoSkippedLocations.Add(name);
                            _saveData.ManuallyManagedLocations.Clear();
                            Helper.Data.WriteSaveData(SaveDataKey, _saveData);
                        }
                    }
                    break;
            }

            Helper.WriteConfig(Config);
            RebuildConfigMenu();
            _gmcmApi.OpenModMenu(ModManifest);
        }

        // Deferred day-start grab: runs 1 tick after DayStarted so other mods
        // finish spawning forage, artifact spots, etc.
        if (_pendingDayStartGrab)
        {
            _pendingDayStartGrab = false;
            _dirtyLocations.Clear();

            LogDebug("Executing deferred day-start grab");
            _isGrabbing = true;
            try
            {
                foreach (var location in GetAllLocations())
                {
                    GrabAtLocation(location);
                }
            }
            finally
            {
                _isGrabbing = false;
            }

            // Process auto-fire global grab immediately after the local day-start grab
            if (_pendingGlobalAutoFire)
            {
                _pendingGlobalAutoFire = false;
                FireGlobalGrab();
            }

            return;
        }

        if (_dirtyLocations.Count == 0)
            return;

        var locations = _dirtyLocations.ToList();
        _dirtyLocations.Clear();

        _isGrabbing = true;
        try
        {
            foreach (var location in locations)
            {
                LogDebug("Object list changed at " + location.Name);
                GrabAtLocation(location);
            }
        }
        finally
        {
            _isGrabbing = false;
        }
    }

    private void OnTenMinuteUpdate(object sender, TimeChangedEventArgs e)
    {
        if (e.NewTime % 100 != 0)
            return;

        LogDebug("Autograbbing on time change");
        foreach (var location in Game1.locations)
        {
            if (!ShouldProcessLocation(location))
                continue;

            var orePanGrabber = new OrePanGrabber(this, location);
            if (orePanGrabber.CanGrab())
                orePanGrabber.GrabItems();
        }
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        LogDebug("Autograbbing on day start (deferred to next tick)");
        _pendingDayStartGrab = true;

        // Auto-fire global grab at day start if configured
        if (Config.globalAutoFire && Config.globalGrabber == ModConfig.GlobalGrabberMode.All && HasDesignatedGrabber())
        {
            _pendingGlobalAutoFire = true;
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

    private bool ShouldProcessLocation(GameLocation location)
    {
        if (location == null)
            return false;

        string name = location.Name;
        if (string.IsNullOrEmpty(name))
            return false;

        // User-configured location skip list
        if (Config.SkippedLocations?.Contains(name) == true)
        {
            LogDebug($"Skipping {name}: disabled in config");
            return false;
        }

        // Festival/event location filtering
        if (Config.skipFestivalLocations)
        {
            if (name.Contains("Festival", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Temp", StringComparison.OrdinalIgnoreCase))
            {
                LogDebug($"Skipping {name}: festival/event location");
                return false;
            }

            if (Game1.isFestival())
            {
                LogDebug($"Skipping {name}: festival currently active");
                return false;
            }
        }

        return true;
    }

    private bool GrabAtLocation(GameLocation location)
    {
        if (!ShouldProcessLocation(location))
            return false;

        var aggregateGrabber = new AggregateDailyGrabber(this, location);

        if (!aggregateGrabber.CanGrab())
        {
            LogDebug($"No valid auto-grabbers at {location.Name}, skipping");
            return false;
        }

        var beforeInventory = Config.reportYield ? aggregateGrabber.GetInventory() : null;
        bool result = aggregateGrabber.GrabItems();

        LogDebug($"Grab at {location.Name}: {(result ? "collected items" : "nothing to collect")}");

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
