using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
using StardewValley.GameData.Machines;
using StardewValley.Internal;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix;

public class ModEntry : Mod
{
    internal readonly ModApi Api;
    internal ModConfig Config { get; set; }
    internal bool IsGlobalGrabActive { get; set; }
    internal bool IsForageGrabEnabled { get; set; }
    internal List<KeyValuePair<Vector2, Object>> CachedDesignatedGrabbers { get; set; }
    internal bool UseLocationCache { get; set; }
    internal List<KeyValuePair<Vector2, Object>> CachedGrabberPairs { get; set; }
    internal List<KeyValuePair<Vector2, Object>> CachedObjectPairs { get; set; }
    internal List<KeyValuePair<Vector2, TerrainFeature>> CachedFeaturePairs { get; set; }
    internal HashSet<Vector2> GrabbedTiles { get; set; }
    internal const string GlobalGrabberModDataKey = "Rafia.DeluxeGrabberFix/IsGlobalGrabber";
    internal const string GrabberNameModDataKey = "Rafia.DeluxeGrabberFix/CustomName";
    private const string ChestsAnywhereNameKey = "Pathoschild.ChestsAnywhere/Name";

    private readonly LocationManager _locations;
    private readonly GrabberManager _grabbers;
    private GmcmRegistration _gmcm;
    private IAutomateAPI _automateApi;
    private ICustomBushApi _customBushApi;

    private readonly HashSet<GameLocation> _dirtyLocations = new();
    private readonly HashSet<GameLocation> _machineReadyLocations = new();
    private bool _isGrabbing;
    private bool _pendingDayStartGrab;
    private int _dayStartGrabDelay;
    private bool _pendingGlobalAutoFire;
    private int _globalAutoFireDelay;
    private GlobalGrabberButton _globalGrabberButton;
    private RenameGrabberButton _renameGrabberButton;
    private static ModEntry _instance;

    public ModEntry()
    {
        _instance = this;
        Api = new ModApi(this);
        _locations = new LocationManager(this);
        _grabbers = new GrabberManager(this, _locations);
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

        if (Config.fellSecretWoodsStumps)
        {
            Config.fellHardwoodStumps = true;
            Config.fellSecretWoodsStumps = false;
            Helper.WriteConfig(Config);
        }

        helper.Events.GameLoop.GameLaunched += OnLaunched;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnHourlyUpdate;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Display.MenuChanged += OnMenuChanged;
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Player.Warped += OnPlayerWarped;
    }

    public void LogDebug(string message)
    {
        if (Config.debugLogging)
            Monitor.Log(message, LogLevel.Trace);
    }

    internal void ReportChestFull(Object grabber) => _grabbers.ReportChestFull(grabber);
    internal void ReportCropsHarvested(GameLocation location) => _grabbers.ReportCropsHarvested(location);
    internal void ResetDayTracking() => _grabbers.ResetDayTracking();
    internal void ShowEveningReplantReminder() => _grabbers.ShowEveningReplantReminder();

    internal static void FlagMachineReadyLocation(GameLocation location)
    {
        if (_instance == null || location == null)
            return;
        if (_instance._isGrabbing)
            return;
        if (_instance.Config.grabFrequency != ModConfig.GrabFrequency.Instant || !_instance.Config.collectMachines)
            return;

        _instance._machineReadyLocations.Add(location);
    }

    internal ICustomBushApi CustomBushApi => _customBushApi;

    internal IDictionary<Vector2, int> GetAutomatedMachineStates(GameLocation location)
    {
        if (_automateApi == null)
            return null;

        var map = location.Map;
        if (map == null)
            return null;

        var area = new Rectangle(0, 0, map.Layers[0].LayerWidth, map.Layers[0].LayerHeight);
        return _automateApi.GetMachineStates(location, area);
    }

    private string _automateConfigPath;
    private bool _automateConfigSearched;

    internal HashSet<string> GetAutomateDisabledMachineTypes()
    {
        if (_automateApi == null)
            return null;

        try
        {
            if (!_automateConfigSearched)
            {
                _automateConfigSearched = true;
                _automateConfigPath = FindAutomateConfigPath();
            }

            if (_automateConfigPath == null || !File.Exists(_automateConfigPath))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(_automateConfigPath));
            if (!doc.RootElement.TryGetProperty("MachineOverrides", out var overrides))
                return null;

            var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in overrides.EnumerateObject())
            {
                if (prop.Value.TryGetProperty("Enabled", out var enabled) && !enabled.GetBoolean())
                    disabled.Add(prop.Name);
            }

            return disabled.Count > 0 ? disabled : null;
        }
        catch (Exception ex)
        {
            LogDebug($"Failed to read Automate config: {ex.Message}");
            return null;
        }
    }

    private string FindAutomateConfigPath()
    {
        var modsDir = new DirectoryInfo(Helper.DirectoryPath).Parent;
        if (modsDir == null)
            return null;

        // Try common folder name first
        var direct = Path.Combine(modsDir.FullName, "Automate", "config.json");
        if (File.Exists(direct))
            return direct;

        // Scan mod folders for Automate's manifest
        foreach (var dir in modsDir.GetDirectories())
        {
            var manifest = Path.Combine(dir.FullName, "manifest.json");
            if (!File.Exists(manifest))
                continue;

            try
            {
                using var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (manifestDoc.RootElement.TryGetProperty("UniqueID", out var uid)
                    && string.Equals(uid.GetString(), "Pathoschild.Automate", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(dir.FullName, "config.json");
                }
            }
            catch { }
        }

        return null;
    }

    internal static string GetGrabberCustomName(Object grabber)
    {
        if (grabber.modData.TryGetValue(GrabberNameModDataKey, out string name) && !string.IsNullOrWhiteSpace(name))
            return name;

        if (grabber.heldObject.Value is StardewValley.Objects.Chest chest
            && chest.modData.TryGetValue(ChestsAnywhereNameKey, out string caName)
            && !string.IsNullOrWhiteSpace(caName))
            return caName;

        return null;
    }

    internal static string GetGrabberDisplayName(Object grabber)
    {
        string custom = GetGrabberCustomName(grabber);
        if (custom != null)
            return custom;

        var loc = grabber.Location;
        if (loc != null)
            return !string.IsNullOrEmpty(loc.DisplayName) ? loc.DisplayName : loc.Name;

        return "Auto-Grabber";
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
            _grabbers.HandleDesignateGrabber();
            return;
        }

        if (Config.globalGrabber == ModConfig.GlobalGrabberMode.Off || Config.globalFireButton != e.Button)
            return;

        if (Config.globalGrabber == ModConfig.GlobalGrabberMode.All && !_grabbers.HasDesignatedGrabber())
        {
            Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("hud.designate-first"), HUDMessage.error_type));
            return;
        }

        _grabbers.ResetGrabCycleTracking();
        _grabbers.FireGlobalGrab();
        _grabbers.ShowGrabCycleResults(showSummary: true);
    }

    private void OnMenuChanged(object sender, MenuChangedEventArgs e)
    {
        _globalGrabberButton = null;
        _renameGrabberButton = null;

        if (e.NewMenu is not StardewValley.Menus.ItemGrabMenu grabMenu)
            return;

        // The auto-grabber passes itself as 'context', not 'sourceItem'
        if (grabMenu.context is not Object obj || obj.QualifiedItemId != BigCraftableIds.AutoGrabber
            || obj.heldObject.Value is not StardewValley.Objects.Chest)
            return;

        _globalGrabberButton = new GlobalGrabberButton(this, obj, grabMenu);
        _renameGrabberButton = new RenameGrabberButton(this, obj, grabMenu);
    }

    private void OnRenderedActiveMenu(object sender, RenderedActiveMenuEventArgs e)
    {
        if (Game1.activeClickableMenu is not StardewValley.Menus.ItemGrabMenu)
            return;

        _globalGrabberButton?.Draw(e.SpriteBatch);
        _renameGrabberButton?.Draw(e.SpriteBatch);
    }

    internal static void ItemGrabMenu_ReceiveLeftClick_Postfix(int x, int y)
    {
        _instance?._globalGrabberButton?.TryClick(x, y);
        _instance?._renameGrabberButton?.TryClick(x, y);
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
        harmony.Patch(
            original: AccessTools.Method(typeof(Object), nameof(Object.minutesElapsed)),
            prefix: new HarmonyMethod(typeof(MachineOutputPatch), nameof(MachineOutputPatch.MinutesElapsed_Prefix)),
            postfix: new HarmonyMethod(typeof(MachineOutputPatch), nameof(MachineOutputPatch.MinutesElapsed_Postfix))
        );

        if (Helper.ModRegistry.GetApi<IVanillaPlusProfessionsApi>("KediDili.VanillaPlusProfessions") != null)
            LogDebug("Vanilla Plus Professions detected -- VPP compatibility enabled.");

        _automateApi = Helper.ModRegistry.GetApi<IAutomateAPI>("Pathoschild.Automate");
        if (_automateApi != null)
            LogDebug("Automate detected -- compatibility mode available.");

        _customBushApi = Helper.ModRegistry.GetApi<ICustomBushApi>("furyx639.CustomBush");
        if (_customBushApi != null)
            LogDebug("Custom Bush detected -- compatibility mode enabled.");

        _gmcm = new GmcmRegistration(this, _locations);
        _gmcm.Initialize();
    }

    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
        _locations.LoadSaveData();
        TownGarbageCanGrabber.ClearCache();
        _locations.DiscoverLocations();
        _locations.ApplyVisitAutoSkip();
        _gmcm.RebuildConfigMenu();
        LogConfig();
        RepairStuckMachines();
    }

    private void LogConfig()
    {
        Monitor.Log(
            $"Config: forage={Config.forage}, animalProducts={Config.animalProducts}, " +
            $"slimeHutch={Config.slimeHutch}, farmCaveMushrooms={Config.farmCaveMushrooms}, " +
            $"harvestCrops={Config.harvestCrops}, indoorPots={Config.harvestCropsIndoorPots}, " +
            $"flowers={Config.flowers}, cropRange={Config.harvestCropsRange}, " +
            $"fruitTrees={Config.fruitTrees}, bushes={Config.bushes}, seedTrees={Config.seedTrees}, " +
            $"artifactSpots={Config.artifactSpots}, buriedItems={Config.buriedItems}, " +
            $"seedSpots={Config.seedSpots}, orePan={Config.orePan}, " +
            $"garbageCans={Config.garbageCans}, fellStumps={Config.fellHardwoodStumps}, " +
            $"moss={Config.harvestMoss}, debris={Config.collectDebris}, " +
            $"machines={Config.collectMachines}, crabPots={Config.collectCrabPots}, " +
            $"beeHouses={Config.collectBeeHouses}, tappers={Config.collectTappers}, " +
            $"globalMode={Config.globalGrabber}, globalAutoFire={Config.globalAutoFire}, " +
            $"reportYield={Config.reportYield}, gainXP={Config.gainExperience}, " +
            $"grabFrequency={Config.grabFrequency}, skipFestivals={Config.skipFestivalLocations}",
            LogLevel.Trace);

        if (Config.excludedItems?.Count > 0)
            Monitor.Log($"Excluded items: {string.Join(", ", Config.excludedItems)}", LogLevel.Trace);
    }

    private void RepairStuckMachines()
    {
        int repaired = 0;

        foreach (var location in GetAllLocations())
        {
            if (location?.Objects == null)
                continue;

            foreach (var pair in location.Objects.Pairs.ToList())
            {
                try
                {
                    var obj = pair.Value;
                    if (obj == null || !obj.bigCraftable.Value)
                        continue;
                    if (obj.heldObject.Value != null)
                        continue;
                    if (obj.MinutesUntilReady > 0)
                        continue;

                    var machineData = obj.GetMachineData();
                    if (machineData?.OutputRules == null)
                        continue;

                    bool hasOutputCollectedRule = machineData.OutputRules.Any(r =>
                        r.Triggers?.Any(t => t.Trigger.HasFlag(MachineOutputTrigger.OutputCollected)) == true);
                    if (!hasOutputCollectedRule)
                        continue;

                    // Stale readyForHarvest: heldObject is null but readyForHarvest still true
                    // Clear the flags and let the game's normal cycle restart the machine
                    if (obj.readyForHarvest.Value)
                    {
                        obj.readyForHarvest.Value = false;
                        obj.showNextIndex.Value = false;
                        repaired++;
                        Monitor.Log($"Repaired stuck {obj.Name} at {location.Name} [{pair.Key}] (cleared stale readyForHarvest)", LogLevel.Trace);
                    }
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Error repairing {pair.Value?.Name ?? "unknown"} at {location.Name} [{pair.Key}]: {ex.Message}", LogLevel.Trace);
                }
            }
        }

        if (repaired > 0)
            Monitor.Log($"Repaired {repaired} stuck machine(s) from a previous bug.", LogLevel.Info);
    }

    private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
    {
        _locations.ClearState();
        TownGarbageCanGrabber.ClearCache();
        _gmcm.RebuildConfigMenu();
    }

    private void OnPlayerWarped(object sender, WarpedEventArgs e)
    {
        if (_locations.HandleLocationVisit(e.NewLocation?.Name))
            _gmcm.RebuildConfigMenu();
    }

    private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
    {
        if (_isGrabbing)
            return;

        _dirtyLocations.Add(e.Location);
    }

    private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
    {
        if (_gmcm.ProcessPendingBatchAction())
            return;
        if (_gmcm.ProcessPendingMachineToggle())
            return;

        // Deferred day-start grab: waits a few ticks after DayStarted so other mods
        // finish spawning forage, artifact spots, etc. Extra ticks when Automate is
        // installed so it can rebuild its machine groups before we query them.
        if (_pendingDayStartGrab)
        {
            if (--_dayStartGrabDelay > 0)
                return;
            _pendingDayStartGrab = false;
            _dirtyLocations.Clear();

            LogDebug("Executing deferred day-start grab");
            _grabbers.ResetGrabCycleTracking();
            _isGrabbing = true;
            IsForageGrabEnabled = true;
            try
            {
                foreach (var location in GetAllLocations())
                {
                    _grabbers.GrabAtLocation(location);
                }
            }
            finally
            {
                IsForageGrabEnabled = false;
                _isGrabbing = false;
            }

            _grabbers.ShowGrabCycleResults(showSummary: true);
            return;
        }

        // Deferred auto-fire global grab (independent of grab frequency)
        if (_pendingGlobalAutoFire)
        {
            if (--_globalAutoFireDelay > 0)
                return;
            _pendingGlobalAutoFire = false;

            LogDebug("Executing deferred auto-fire global grab");
            _grabbers.ResetGrabCycleTracking();
            _grabbers.FireGlobalGrab();
            _grabbers.ShowGrabCycleResults(showSummary: true);
            return;
        }

        if (Config.grabFrequency != ModConfig.GrabFrequency.Instant)
            return;

        // Process machine-ready locations (flagged by Harmony patch on Object.minutesElapsed)
        if (_machineReadyLocations.Count > 0)
        {
            var machineLocations = _machineReadyLocations.ToList();
            _machineReadyLocations.Clear();

            bool useGlobal = Config.globalGrabber == ModConfig.GlobalGrabberMode.All && _grabbers.HasDesignatedGrabber();
            if (useGlobal)
            {
                IsGlobalGrabActive = true;
                CachedDesignatedGrabbers = new List<KeyValuePair<Vector2, Object>>();
                foreach (var loc in GetAllLocations())
                {
                    CachedDesignatedGrabbers.AddRange(
                        loc.Objects.Pairs
                            .Where(pair => pair.Value != null
                                && pair.Value.modData.ContainsKey(GlobalGrabberModDataKey))
                            .ToList());
                }
            }

            _isGrabbing = true;
            try
            {
                foreach (var location in machineLocations)
                {
                    LogDebug("Machine output ready at " + location.Name);
                    _grabbers.GrabMachinesAtLocation(location);
                }
            }
            finally
            {
                _isGrabbing = false;
                if (useGlobal)
                {
                    IsGlobalGrabActive = false;
                    CachedDesignatedGrabbers = null;
                }
                _grabbers.ShowGrabCycleResults(showSummary: false);
            }
        }

        // Process dirty locations (forage, debris, artifacts)
        if (_dirtyLocations.Count == 0)
            return;

        var locations = _dirtyLocations.ToList();
        _dirtyLocations.Clear();

        _isGrabbing = true;
        IsForageGrabEnabled = true;
        try
        {
            foreach (var location in locations)
            {
                LogDebug("Object list changed at " + location.Name);
                _grabbers.GrabAtLocation(location);
            }
        }
        finally
        {
            IsForageGrabEnabled = false;
            _isGrabbing = false;
            _grabbers.ShowGrabCycleResults(showSummary: false);
        }
    }

    private void OnHourlyUpdate(object sender, TimeChangedEventArgs e)
    {
        if (e.NewTime % 100 != 0)
            return;

        if (e.NewTime == Config.replantReminderTime)
            ShowEveningReplantReminder();

        LogDebug("Autograbbing on time change");
        _grabbers.ResetGrabCycleTracking();

        bool useGlobal = Config.globalGrabber == ModConfig.GlobalGrabberMode.All && _grabbers.HasDesignatedGrabber();
        if (useGlobal)
        {
            IsGlobalGrabActive = true;
            CachedDesignatedGrabbers = new List<KeyValuePair<Vector2, Object>>();
            foreach (var loc in GetAllLocations())
            {
                CachedDesignatedGrabbers.AddRange(
                    loc.Objects.Pairs
                        .Where(pair => pair.Value != null
                            && pair.Value.modData.ContainsKey(GlobalGrabberModDataKey))
                        .ToList());
            }
        }

        _isGrabbing = true;
        IsForageGrabEnabled = Config.grabFrequency != ModConfig.GrabFrequency.Daily;
        try
        {
            foreach (var location in GetAllLocations())
            {
                if (!_locations.ShouldProcessLocation(location))
                    continue;

                var orePanGrabber = new OrePanGrabber(this, location);
                if (orePanGrabber.CanGrab())
                {
                    var beforeInventory = Config.reportYield ? orePanGrabber.GetInventory() : null;
                    bool result = orePanGrabber.GrabItems();

                    if (result)
                        LogDebug($"Ore pan at {location.Name}: collected items");

                    if (beforeInventory != null && result)
                    {
                        var afterInventory = orePanGrabber.GetInventory();
                        var sb = new StringBuilder(Helper.Translation.Get("log.ore-panning-yield-header", new { location = location.Name }) + "\n");
                        bool anyYield = false;

                        foreach (var entry in afterInventory)
                        {
                            int newCount = entry.Value;
                            if (beforeInventory.ContainsKey(entry.Key))
                                newCount -= beforeInventory[entry.Key];

                            if (newCount > 0)
                            {
                                sb.AppendLine(Helper.Translation.Get("log.yield-item", new
                                {
                                    name = entry.Key.DisplayName,
                                    quality = Helper.Translation.Get(entry.Key.QualityKey),
                                    count = newCount
                                }));
                                anyYield = true;
                            }
                        }

                        if (anyYield)
                            Monitor.Log(sb.ToString(), LogLevel.Info);
                    }
                }

                if (Config.grabFrequency != ModConfig.GrabFrequency.Daily)
                    _grabbers.GrabForageAtLocation(location);

                // Machine outputs don't fire ObjectListChanged when they finish processing,
                // so poll machines every hour regardless of dirty state for non-Daily modes
                if (Config.grabFrequency != ModConfig.GrabFrequency.Daily)
                    _grabbers.GrabMachinesAtLocation(location);

                // In Hourly mode, run the full grab for all locations every tick so crops,
                // fruit trees, bushes, etc. (terrain features that don't trigger ObjectListChanged)
                // are collected. In Instant mode, only run full grab for dirty locations.
                if (Config.grabFrequency == ModConfig.GrabFrequency.Hourly)
                {
                    IsForageGrabEnabled = true;
                    _grabbers.GrabAtLocation(location);
                    IsForageGrabEnabled = Config.grabFrequency != ModConfig.GrabFrequency.Daily;
                }
                else if (Config.grabFrequency == ModConfig.GrabFrequency.Instant && _dirtyLocations.Contains(location))
                {
                    IsForageGrabEnabled = true;
                    _grabbers.GrabAtLocation(location);
                    IsForageGrabEnabled = Config.grabFrequency != ModConfig.GrabFrequency.Daily;
                }
            }

            if (Config.grabFrequency == ModConfig.GrabFrequency.Hourly)
                _dirtyLocations.Clear();
        }
        finally
        {
            IsForageGrabEnabled = false;
            _isGrabbing = false;
            if (useGlobal)
            {
                IsGlobalGrabActive = false;
                CachedDesignatedGrabbers = null;
            }
            _grabbers.ShowGrabCycleResults(showSummary: false);
        }
    }

    private void OnDayEnding(object sender, DayEndingEventArgs e)
    {
        if (!Config.forage)
            return;

        LogDebug("Autograbbing forage before sleep");
        _grabbers.ResetGrabCycleTracking();
        _isGrabbing = true;
        IsForageGrabEnabled = true;
        try
        {
            foreach (var location in GetAllLocations())
            {
                _grabbers.GrabForageAtLocation(location);
            }
        }
        finally
        {
            IsForageGrabEnabled = false;
            _isGrabbing = false;
            _grabbers.ShowGrabCycleResults(showSummary: false);
        }
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        ResetDayTracking();

        // Auto-fire global grab at day start if configured (works in all frequency modes)
        if (Config.globalAutoFire && Config.globalGrabber == ModConfig.GlobalGrabberMode.All && _grabbers.HasDesignatedGrabber())
        {
            _pendingGlobalAutoFire = true;
            _globalAutoFireDelay = _automateApi != null ? 5 : 1;
        }

        // Only run the day-start sweep in Daily mode; Hourly/Instant handle collection on their own schedule
        if (Config.grabFrequency != ModConfig.GrabFrequency.Daily)
            return;

        _dayStartGrabDelay = _automateApi != null ? 5 : 1;
        LogDebug($"Autograbbing on day start (deferred {_dayStartGrabDelay} ticks)");
        _pendingDayStartGrab = true;
    }

    private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsPlayerFree || Game1.eventUp || Game1.farmEvent != null
            || Config.harvestCropsRange <= 0 || !Config.harvestCrops
            || Game1.player.ActiveObject == null
            || !Game1.player.ActiveObject.bigCraftable.Value
            || Game1.player.ActiveObject.QualifiedItemId != BigCraftableIds.AutoGrabber)
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
}
