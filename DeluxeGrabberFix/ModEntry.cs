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

    private readonly HashSet<GameLocation> _dirtyLocations = new();
    private bool _isGrabbing;
    private bool _pendingDayStartGrab;
    private bool _pendingGlobalAutoFire;
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

        if (Helper.ModRegistry.GetApi<IVanillaPlusProfessionsApi>("KediDili.VanillaPlusProfessions") != null)
            LogDebug("Vanilla Plus Professions detected -- VPP compatibility enabled.");

        _automateApi = Helper.ModRegistry.GetApi<IAutomateAPI>("Pathoschild.Automate");
        if (_automateApi != null)
            LogDebug("Automate detected -- compatibility mode available.");

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
    }

    private void LogConfig()
    {
        LogDebug(
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
            $"grabFrequency={Config.grabFrequency}, skipFestivals={Config.skipFestivalLocations}");

        if (Config.excludedItems?.Count > 0)
            LogDebug($"Excluded items: {string.Join(", ", Config.excludedItems)}");
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

        // Deferred day-start grab: runs 1 tick after DayStarted so other mods
        // finish spawning forage, artifact spots, etc.
        if (_pendingDayStartGrab)
        {
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

            // Process auto-fire global grab immediately after the local day-start grab
            if (_pendingGlobalAutoFire)
            {
                _pendingGlobalAutoFire = false;
                _grabbers.FireGlobalGrab();
            }

            _grabbers.ShowGrabCycleResults(showSummary: true);
            return;
        }

        if (_dirtyLocations.Count == 0 || Config.grabFrequency != ModConfig.GrabFrequency.Instant)
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

                    LogDebug($"Ore pan at {location.Name}: {(result ? "collected items" : "nothing to collect")}");

                    if (beforeInventory != null && result)
                    {
                        var afterInventory = orePanGrabber.GetInventory();
                        var sb = new StringBuilder($"Ore panning yield at {location.Name}:\n");
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
                }

                if (Config.grabFrequency != ModConfig.GrabFrequency.Daily)
                    _grabbers.GrabForageAtLocation(location);

                if (Config.grabFrequency == ModConfig.GrabFrequency.Hourly && _dirtyLocations.Contains(location))
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

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        LogDebug("Autograbbing on day start (deferred to next tick)");
        _pendingDayStartGrab = true;

        // Auto-fire global grab at day start if configured
        if (Config.globalAutoFire && Config.globalGrabber == ModConfig.GlobalGrabberMode.All && _grabbers.HasDesignatedGrabber())
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
