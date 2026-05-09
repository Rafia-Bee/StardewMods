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

    // Audit §2.9: the active ModConfig reference is swapped at runtime by
    // PerSaveConfigManager (per-save load + return-to-title) and the GMCM "reset to
    // defaults" lambda. Direct-access readers (`_mod.Config.foo`) and lazy lambdas
    // pick up the new instance automatically because Config is a property, but
    // anyone who caches a local (`var c = _mod.Config; ...`) ends up reading the
    // orphaned old instance. Setter is private so every swap funnels through
    // SwapActiveConfig, which fires ActiveConfigChanged for cross-cutting consumers
    // (RefreshRenderedWorldHook, Data/CraftingRecipes invalidation) instead of each
    // call site re-implementing the side-effect list.
    internal ModConfig Config { get; private set; }

    // Fires when the active ModConfig reference has been swapped (per-save load,
    // return to title, GMCM reset). Does NOT fire on in-place GMCM saves; those keep
    // their imperative side-effects in the GMCM save lambda because they're scoped
    // to the user clicking Save and don't change the Config reference.
    internal event Action ActiveConfigChanged;

    internal void SwapActiveConfig(ModConfig next)
    {
        if (next == null)
            throw new ArgumentNullException(nameof(next));
        Config = next;
        ActiveConfigChanged?.Invoke();
    }

    internal bool IsGlobalGrabActive { get; set; }
    internal bool IsForageGrabEnabled { get; set; }
    internal List<KeyValuePair<Vector2, Object>> CachedDesignatedGrabbers { get; set; }
    // Captured at GrabSession entry for Specialized + Hover manual-fire so the per-iteration
    // grab path doesn't re-read Game1.lastCursorTile mid-cycle. Null outside a Hover session.
    internal KeyValuePair<Vector2, Object>? CachedHoverGrabber { get; set; }
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
    private SpecializedGrabberAssets _specializedAssets;
    private ProgressionTracker _progression;
    internal PerSaveConfigManager ConfigManager { get; private set; }

    private readonly HashSet<GameLocation> _dirtyLocations = new();
    private readonly HashSet<GameLocation> _machineReadyLocations = new();
    private bool _isGrabbingField;
    internal bool IsGrabbing
    {
        get => _isGrabbingField;
        set
        {
            _isGrabbingField = value;
            SpecializedGrabberPatches.IsGrabbingActive = value;
        }
    }
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
        ConfigManager = new PerSaveConfigManager(this);
    }

    public override void Entry(IModHelper helper)
    {
        try
        {
            Config = Helper.ReadConfig<ModConfig>();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to load config.json, using defaults: {ex.Message}", LogLevel.Warn);
            Config = new ModConfig();
        }

        ConfigManager.SetGlobalConfig(Config);

        _specializedAssets = new SpecializedGrabberAssets(helper, () => Config);
        _specializedAssets.Register();
        _progression = new ProgressionTracker(this);

        helper.Events.GameLoop.GameLaunched += OnLaunched;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnHourlyUpdate;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Display.RenderedActiveMenu += OnRenderedActiveMenu;
        helper.Events.Display.MenuChanged += OnMenuChanged;
        helper.Events.World.ObjectListChanged += OnObjectListChanged;
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayEnding += OnDayEnding;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Player.Warped += OnPlayerWarped;

        // Audit §2.9: side-effects coupled to the active Config reference go here.
        // Any future feature that reacts to a config swap should subscribe rather
        // than chase the swap call sites (PerSaveConfigManager, GMCM reset, etc).
        ActiveConfigChanged += RefreshRenderedWorldHook;
        ActiveConfigChanged += () => Helper.GameContent.InvalidateCache("Data/CraftingRecipes");

        RefreshRenderedWorldHook();
    }

    private bool _renderedWorldHooked;

    // Hooks Display.RenderedWorld only when crop-harvest range preview is enabled, so
    // most users don't pay a per-frame guard check for a feature they never turned on.
    // Call after any path that may have mutated Config (initial load, GMCM save/reset,
    // per-save config swap on save load and return to title).
    internal void RefreshRenderedWorldHook()
    {
        bool shouldHook = Config?.Features?.harvestCrops == true;
        if (shouldHook == _renderedWorldHooked)
            return;

        if (shouldHook)
            Helper.Events.Display.RenderedWorld += OnRenderedWorld;
        else
            Helper.Events.Display.RenderedWorld -= OnRenderedWorld;

        _renderedWorldHooked = shouldHook;
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
    internal bool HasDesignatedGrabber() => _grabbers.HasDesignatedGrabber();
    internal void ClearAllDesignations() => _grabbers.ClearAllDesignations();
    internal bool ToggleGrabberDesignation(Object grabber) => _grabbers.ToggleGrabberDesignation(grabber);

    // Drains the per-location queues used by Instant-mode draining (audit §1.2, §3.8).
    // Called on title return (state should not survive a save unload) and when
    // grabFrequency changes via GMCM (queued locations from a previous mode would
    // otherwise sit in memory holding GameLocation refs until the user switched back).
    internal void ClearLocationQueues()
    {
        _dirtyLocations.Clear();
        _machineReadyLocations.Clear();
    }

    internal static void FlagMachineReadyLocation(GameLocation location)
    {
        if (_instance == null || location == null)
            return;
        if (_instance.IsGrabbing)
            return;
        if (_instance.Config.grabFrequency != ModConfig.GrabFrequency.Instant || _instance.Config.Machines.disableMachineCollection)
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
            catch (Exception ex)
            {
                LogDebug($"Failed to read manifest at {manifest}: {ex.GetType().Name}: {ex.Message}");
            }
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

    private static List<GameLocation> _cachedAllLocations;
    private static int _cachedAllLocationsTick = -1;

    internal static IReadOnlyList<GameLocation> GetAllLocations()
    {
        if (_cachedAllLocationsTick == Game1.ticks && _cachedAllLocations != null)
            return _cachedAllLocations;

        var locations = new List<GameLocation>();
        foreach (var location in Game1.locations)
        {
            locations.Add(location);
            foreach (var building in location.buildings)
            {
                if (building.indoors.Value != null)
                    locations.Add(building.indoors.Value);
            }
        }

        _cachedAllLocations = locations;
        _cachedAllLocationsTick = Game1.ticks;
        return locations;
    }

    private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsPlayerFree)
            return;

        // In Specialized mode, the fire keybind respects the global grabber setting
        if (Config.grabberMode == ModConfig.GrabberMode.Specialized)
        {
            if (Config.GlobalGrab.globalFireButton == SButton.None || e.Button != Config.GlobalGrab.globalFireButton)
                return;

            // Off mode: fire keybind disabled (grabbers work locally via automatic grabs)
            if (Config.globalGrabber == ModConfig.GlobalGrabberMode.Off)
                return;

            // Hover mode: verify cursor is over a grabber before firing
            if (Config.globalGrabber == ModConfig.GlobalGrabberMode.Hover)
            {
                var hoveredObj = Game1.player.currentLocation?.getObjectAtTile(
                    (int)Game1.lastCursorTile.X, (int)Game1.lastCursorTile.Y);
                if (hoveredObj == null || !GrabberTypeHelper.IsGrabber(hoveredObj.QualifiedItemId))
                {
                    Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("hud.hover-over-grabber"), HUDMessage.error_type));
                    return;
                }
            }

            _grabbers.ResetGrabCycleTracking();
            using (var session = new GrabSession(this, GrabSessionKind.ManualGlobalFire))
            {
                foreach (var location in GetAllLocations())
                    _grabbers.GrabAtLocation(location);
            }
            _grabbers.ShowGrabCycleResults(showSummary: true);
            return;
        }

        if (e.Button == Config.GlobalGrab.designateGrabberButton
            && Config.globalGrabber == ModConfig.GlobalGrabberMode.All)
        {
            _grabbers.HandleDesignateGrabber();
            return;
        }

        if (Config.globalGrabber == ModConfig.GlobalGrabberMode.Off || Config.GlobalGrab.globalFireButton != e.Button)
            return;

        if (Config.globalGrabber == ModConfig.GlobalGrabberMode.All && !_grabbers.HasDesignatedGrabber())
        {
            Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("hud.designate-first"), HUDMessage.error_type));
            return;
        }

        _grabbers.ResetGrabCycleTracking();
        using (var session = new GrabSession(this, GrabSessionKind.ManualGlobalFire))
        {
            _grabbers.FireGlobalGrab();
        }
        _grabbers.ShowGrabCycleResults(showSummary: true);
    }

    private void OnMenuChanged(object sender, MenuChangedEventArgs e)
    {
        _globalGrabberButton = null;
        _renameGrabberButton = null;

        if (e.NewMenu is not StardewValley.Menus.ItemGrabMenu grabMenu)
            return;

        // The auto-grabber passes itself as 'context', not 'sourceItem'
        if (grabMenu.context is not Object obj || !GrabberTypeHelper.IsGrabber(obj.QualifiedItemId)
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
        harmony.Patch(
            original: AccessTools.Method(typeof(Object), nameof(Object.minutesElapsed)),
            prefix: new HarmonyMethod(typeof(SpecializedGrabberPatches), nameof(SpecializedGrabberPatches.MinutesElapsed_Prefix))
                { priority = HarmonyLib.Priority.First }
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(Object), nameof(Object.draw),
                new[] { typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch), typeof(int), typeof(int), typeof(float) }),
            prefix: new HarmonyMethod(typeof(SpecializedGrabberPatches), nameof(SpecializedGrabberPatches.Draw_Prefix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(Object), nameof(Object.performToolAction)),
            prefix: new HarmonyMethod(typeof(SpecializedGrabberPatches), nameof(SpecializedGrabberPatches.PerformToolAction_Prefix))
        );

        // Block external mods from adding items to wrong specialized grabber type
        harmony.Patch(
            original: AccessTools.Method(typeof(StardewValley.Objects.Chest), nameof(StardewValley.Objects.Chest.addItem)),
            prefix: new HarmonyMethod(typeof(SpecializedGrabberPatches), nameof(SpecializedGrabberPatches.Chest_addItem_Prefix))
        );

        PerfectionPatch.GetConfig = () => Config;
        harmony.Patch(
            original: AccessTools.Method(typeof(Utility), nameof(Utility.getCraftedRecipesPercent)),
            postfix: new HarmonyMethod(typeof(PerfectionPatch), nameof(PerfectionPatch.GetCraftedRecipesPercent_Postfix))
        );
        harmony.Patch(
            original: AccessTools.Method(typeof(Stats), nameof(Stats.checkForCraftingAchievements)),
            postfix: new HarmonyMethod(typeof(PerfectionPatch), nameof(PerfectionPatch.CheckForCraftingAchievements_Postfix))
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
        // ConfigManager.OnSaveLoaded swaps to the per-save Config and SwapActiveConfig
        // fires ActiveConfigChanged (audit §2.9). The subscription wired in Entry()
        // covers RefreshRenderedWorldHook + Data/CraftingRecipes invalidation -- the
        // latter handles the 2026-04-30 "Specialized recipes missing from crafting
        // menu" case where the asset loaded before the per-save config took effect.
        ConfigManager.OnSaveLoaded();

        _locations.LoadSaveData();
        TownGarbageCanGrabber.ClearCache();
        _locations.DiscoverLocations();
        _locations.ApplyVisitAutoSkip();
        _gmcm.RebuildConfigMenu();
        RunPerSaveMigrations();
        RecountSpecializedGrabbers();
        _progression.RetroactiveCheck();
        LogConfig();
    }

    // One-shot per-save migrations gated by SaveData.SchemaVersion. New saves and
    // already-migrated ones skip the heavy world scans entirely; legacy saves run
    // the migrations once and then get bumped to current.
    private void RunPerSaveMigrations()
    {
        if (_locations.SaveData == null)
            return;
        if (_locations.SaveData.SchemaVersion >= SaveData.CurrentSchemaVersion)
            return;

        int from = _locations.SaveData.SchemaVersion;
        LogDebug($"Running per-save migrations (schema {from} -> {SaveData.CurrentSchemaVersion})");

        MigrateSpecializedGrabbers();
        RepairStuckMachines();

        _locations.SaveData.SchemaVersion = SaveData.CurrentSchemaVersion;
        _locations.WriteSaveData();
    }

    // Converts pre-(BC)165 custom-id grabbers, normalizes inner Chests on existing
    // specialized grabbers (init missing chest, replace legacy playerChest:true).
    // Idempotent: re-running on an already-migrated save is a no-op walk.
    private void MigrateSpecializedGrabbers()
    {
        int converted = 0;
        int initialized = 0;
        foreach (var location in GetAllLocations())
        {
            var toConvert = new List<KeyValuePair<Vector2, Object>>();

            foreach (var pair in location.Objects.Pairs)
            {
                if (GrabberTypeHelper.IsSpecializedGrabberItem(pair.Value.QualifiedItemId))
                {
                    toConvert.Add(pair);
                    continue;
                }

                if (pair.Value.QualifiedItemId == BigCraftableIds.AutoGrabber
                    && pair.Value.modData.TryGetValue(SpecializedGrabberPatches.ModDataGrabberType, out string existingType))
                {
                    if (pair.Value.heldObject.Value is not StardewValley.Objects.Chest)
                    {
                        pair.Value.heldObject.Value = new StardewValley.Objects.Chest();
                        pair.Value.showNextIndex.Value = false;
                        initialized++;
                    }
                    else if (pair.Value.heldObject.Value is StardewValley.Objects.Chest oldChest && oldChest.playerChest.Value)
                    {
                        var replacement = new StardewValley.Objects.Chest();
                        foreach (var item in oldChest.Items)
                        {
                            if (item != null)
                                replacement.Items.Add(item);
                        }
                        pair.Value.heldObject.Value = replacement;
                        pair.Value.showNextIndex.Value = replacement.Items.Count > 0;
                        initialized++;
                    }
                    if (pair.Value.heldObject.Value is StardewValley.Objects.Chest existingHeldChest)
                        existingHeldChest.modData[SpecializedGrabberPatches.ModDataGrabberType] = existingType;
                }
            }

            foreach (var pair in toConvert)
            {
                var tile = pair.Key;
                var obj = pair.Value;
                var grabberType = GrabberTypeHelper.GetGrabberType(obj.QualifiedItemId);
                string originalId = obj.QualifiedItemId;

                // Preserve existing chest contents if any
                StardewValley.Objects.Chest existingChest = obj.heldObject.Value as StardewValley.Objects.Chest;

                location.Objects.Remove(tile);

                var autoGrabber = new Object(tile, "165");
                autoGrabber.heldObject.Value = existingChest ?? new StardewValley.Objects.Chest();
                autoGrabber.showNextIndex.Value = false;
                autoGrabber.modData[SpecializedGrabberPatches.ModDataGrabberType] = grabberType.ToString();
                autoGrabber.modData[SpecializedGrabberPatches.ModDataOriginalId] = originalId;
                if (autoGrabber.heldObject.Value is StardewValley.Objects.Chest migChest)
                    migChest.modData[SpecializedGrabberPatches.ModDataGrabberType] = grabberType.ToString();
                location.Objects.Add(tile, autoGrabber);
                converted++;
            }
        }
        if (converted > 0)
            LogDebug($"Migrated {converted} old-format specialized grabber(s) to (BC)165 format");
        if (initialized > 0)
            LogDebug($"Initialized Chests for {initialized} specialized grabber(s)");
    }

    // Authoritative recount of placed specialized grabbers across the world. Sets
    // SpecializedGrabberPatches.SpecializedGrabberCount so Chest_addItem_Prefix's
    // early-out (audit step 3) stays accurate. Cheap: a modData ContainsKey per
    // (BC)165 object, no GetMachineData / OutputRules walks.
    private void RecountSpecializedGrabbers()
    {
        int total = 0;
        foreach (var location in GetAllLocations())
        {
            if (location?.Objects == null)
                continue;

            foreach (var pair in location.Objects.Pairs)
            {
                if (pair.Value?.QualifiedItemId == BigCraftableIds.AutoGrabber
                    && pair.Value.modData.ContainsKey(SpecializedGrabberPatches.ModDataGrabberType))
                {
                    total++;
                }
            }
        }
        SpecializedGrabberPatches.SpecializedGrabberCount = total;
    }

    private void LogConfig()
    {
        // Reflection-based dump: any new field added to ModConfig (top-level or nested
        // group) is automatically picked up. No hand-maintained list to drift out of sync.
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
            {
                IgnoreSerializableInterface = true
            }
        };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(Config, settings);
        Monitor.Log("Config:\n" + json, LogLevel.Trace);
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
        // ConfigManager.OnReturnedToTitle restores the global Config; SwapActiveConfig
        // fires ActiveConfigChanged so RefreshRenderedWorldHook + Data/CraftingRecipes
        // invalidation run automatically (audit §2.9).
        ConfigManager.OnReturnedToTitle();
        SpecializedGrabberPatches.SpecializedGrabberCount = 0;
        ClearLocationQueues();
        _gmcm.RebuildConfigMenu();
    }

    private void OnPlayerWarped(object sender, WarpedEventArgs e)
    {
        if (_locations.HandleLocationVisit(e.NewLocation?.Name))
            _gmcm.RebuildConfigMenu();
    }

    private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
    {
        // Convert newly placed custom grabbers to (BC)165 + modData
        foreach (var pair in e.Added)
        {
            if (pair.Value == null)
                continue;

            // Skip (BC)165 objects (vanilla or already-converted specialized)
            if (pair.Value.QualifiedItemId == BigCraftableIds.AutoGrabber)
                continue;

            // Convert custom specialized grabber → (BC)165 + modData + Chest
            if (GrabberTypeHelper.IsSpecializedGrabberItem(pair.Value.QualifiedItemId))
            {
                var tile = pair.Key;
                var grabberType = GrabberTypeHelper.GetGrabberType(pair.Value.QualifiedItemId);
                string originalId = pair.Value.QualifiedItemId;

                e.Location.Objects.Remove(tile);

                var autoGrabber = new Object(tile, "165");
                var newChest = new StardewValley.Objects.Chest();
                newChest.modData[SpecializedGrabberPatches.ModDataGrabberType] = grabberType.ToString();
                autoGrabber.heldObject.Value = newChest;
                autoGrabber.showNextIndex.Value = false;
                autoGrabber.modData[SpecializedGrabberPatches.ModDataGrabberType] = grabberType.ToString();
                autoGrabber.modData[SpecializedGrabberPatches.ModDataOriginalId] = originalId;
                e.Location.Objects.Add(tile, autoGrabber);
                SpecializedGrabberPatches.SpecializedGrabberCount++;
                LogDebug($"Placed {grabberType} grabber at {e.Location.Name} ({tile})");
            }
        }

        // Track pickups so the Chest_addItem_Prefix early-out flag stays accurate.
        // Counter is conservative: any drift only loses the optimization, never breaks
        // protection (it fully resyncs on save load and title return).
        foreach (var pair in e.Removed)
        {
            if (pair.Value?.modData.ContainsKey(SpecializedGrabberPatches.ModDataGrabberType) == true
                && SpecializedGrabberPatches.SpecializedGrabberCount > 0)
            {
                SpecializedGrabberPatches.SpecializedGrabberCount--;
            }
        }

        if (IsGrabbing)
            return;

        // _dirtyLocations is purely an Instant-mode signal for OnUpdateTicked. Hourly and
        // Daily run their full sweeps from OnHourlyUpdate / day-start instead, so queueing
        // in those modes used to silently grow the set across the whole session (audit
        // §2.6) and pin GameLocation refs (audit §3.8).
        if (Config.grabFrequency != ModConfig.GrabFrequency.Instant)
            return;

        _dirtyLocations.Add(e.Location);
    }

    private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
    {
        // Audit §3.9: gate the per-tick GMCM batch-action poll behind an inlinable
        // field-read property. When nothing is pending (the overwhelming common case
        // every tick that isn't right after a batch button click), this short-circuits
        // before dispatching into ProcessPendingBatchAction.
        if (_gmcm.HasPendingBatchAction && _gmcm.ProcessPendingBatchAction())
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
            using (var session = new GrabSession(this, GrabSessionKind.AutoSweep))
            {
                foreach (var location in GetAllLocations())
                    _grabbers.GrabAtLocation(location);
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

            // Re-check current state: GMCM stays open across ticks, so the player can
            // toggle globalGrabber to Off or change grabberMode in the delay window
            // (audit §1.1). Skip rather than fire against a config that no longer
            // matches the queue intent.
            if (Config.globalGrabber != ModConfig.GlobalGrabberMode.All
                || (Config.grabberMode == ModConfig.GrabberMode.Classic && !_grabbers.HasDesignatedGrabber()))
            {
                LogDebug("Skipping deferred auto-fire: config changed during delay window");
                return;
            }

            LogDebug("Executing deferred auto-fire global grab");
            _grabbers.ResetGrabCycleTracking();
            using (var session = new GrabSession(this, GrabSessionKind.ManualGlobalFire))
            {
                _grabbers.FireGlobalGrab();
            }
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

            using (var session = new GrabSession(this, GrabSessionKind.MachineSweep))
            {
                foreach (var location in machineLocations)
                {
                    LogDebug("Machine output ready at " + location.Name);
                    _grabbers.GrabMachinesAtLocation(location);
                }
            }
            _grabbers.ShowGrabCycleResults(showSummary: false);
        }

        // Process dirty locations (forage, debris, artifacts)
        if (_dirtyLocations.Count == 0)
            return;

        var locations = _dirtyLocations.ToList();
        _dirtyLocations.Clear();

        using (var session = new GrabSession(this, GrabSessionKind.AutoSweep))
        {
            foreach (var location in locations)
            {
                LogDebug("Object list changed at " + location.Name);
                _grabbers.GrabAtLocation(location);
            }
        }
        _grabbers.ShowGrabCycleResults(showSummary: false);
    }

    private void OnHourlyUpdate(object sender, TimeChangedEventArgs e)
    {
        if (e.NewTime % 100 != 0)
            return;

        if (e.NewTime == Config.replantReminderTime)
            ShowEveningReplantReminder();

        LogDebug("Autograbbing on time change");
        _grabbers.ResetGrabCycleTracking();

        using (var session = new GrabSession(this, GrabSessionKind.Hourly))
        {
            foreach (var location in GetAllLocations())
            {
                if (!_locations.ShouldProcessLocation(location))
                    continue;

                // Daily mode opts out of hourly polling for ore pan, forage, and machines.
                // Pre-fix the ore pan was running unconditionally above the Daily gate
                // (audit §1.3); now all three live under one gate so the rule is obvious.
                if (Config.grabFrequency != ModConfig.GrabFrequency.Daily)
                {
                    var orePanGrabber = new OrePanGrabber(this, location) { BelongsToType = GrabberType.Scavenger };
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

                    _grabbers.GrabForageAtLocation(location);

                    // Machine outputs don't fire ObjectListChanged when they finish processing,
                    // so poll machines every hour regardless of dirty state for non-Daily modes.
                    _grabbers.GrabMachinesAtLocation(location);

                    // Full sweep in both Instant and Hourly. Pre-fix Instant mode gated this on
                    // _dirtyLocations.Contains(location), so terrain-feature drops (chopped tree
                    // wood/sap, fruit-tree fruit, bush forage) only got picked up if something
                    // else mutated location.Objects in the same hour to dirty the location.
                    // Cutting a tree and walking away in pure Instant mode meant the debris sat
                    // until the next day-start sweep. Running the full grab unconditionally in
                    // non-Daily modes mirrors Hourly's behavior and is mostly idempotent.
                    _grabbers.GrabAtLocation(location);
                }
            }
        }
        _grabbers.ShowGrabCycleResults(showSummary: false);
    }

    private void OnDayEnding(object sender, DayEndingEventArgs e)
    {
        if (!Config.Features.forage)
            return;

        LogDebug("Autograbbing forage before sleep");
        _grabbers.ResetGrabCycleTracking();
        using (var session = new GrabSession(this, GrabSessionKind.AutoSweep))
        {
            foreach (var location in GetAllLocations())
                _grabbers.GrabForageAtLocation(location);
        }
        _grabbers.ShowGrabCycleResults(showSummary: false);
    }

    private void OnDayStarted(object sender, DayStartedEventArgs e)
    {
        ResetDayTracking();
        _progression.CheckProgression();

        // Auto-fire global grab at day start. Only meaningful when globalGrabber == All
        // (Hover requires a cursor target the player isn't supplying at day start;
        // Off means no global grab). Classic-mode additionally requires a designated
        // grabber to exist; Specialized routes by grabber type instead, so any placed
        // specialized grabber is sufficient. Intentionally queued before the Hourly
        // early-return below: Hourly users still get the day-start auto-fire (the
        // hourly poll only starts at 0700+, so without this they'd miss the morning
        // sweep), while skipping the redundant Instant/Daily-style day-start sweep.
        if (Config.GlobalGrab.globalAutoFire
            && Config.globalGrabber == ModConfig.GlobalGrabberMode.All
            && (Config.grabberMode == ModConfig.GrabberMode.Specialized || _grabbers.HasDesignatedGrabber()))
        {
            _pendingGlobalAutoFire = true;
            _globalAutoFireDelay = _automateApi != null ? 5 : 1;
        }

        // Run day-start sweep for Daily and Instant modes.
        // Instant mode needs this because terrain feature changes (fruit trees, bushes, etc.)
        // don't trigger ObjectListChanged, so fruit added overnight would never be collected.
        // Hourly mode already polls all locations every hour, so it doesn't need a day-start sweep.
        if (Config.grabFrequency == ModConfig.GrabFrequency.Hourly)
            return;

        _dayStartGrabDelay = _automateApi != null ? 5 : 1;
        LogDebug($"Autograbbing on day start (deferred {_dayStartGrabDelay} ticks)");
        _pendingDayStartGrab = true;
    }

    private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsPlayerFree || Game1.eventUp || Game1.farmEvent != null
            || Config.Features.harvestCropsRange <= 0 || !Config.Features.harvestCrops
            || Game1.player.ActiveObject == null
            || !Game1.player.ActiveObject.bigCraftable.Value
            || !GrabberTypeHelper.IsGrabber(Game1.player.ActiveObject.QualifiedItemId))
        {
            return;
        }

        if (!Game1.IsPerformingMousePlacement())
            return;

        Vector2 grabTile = Game1.GetPlacementGrabTile();
        int centerX = (int)grabTile.X;
        int centerY = (int)grabTile.Y;

        int range = Config.Features.harvestCropsRange;
        for (int x = centerX - range; x <= centerX + range; x++)
        {
            for (int y = centerY - range; y <= centerY + range; y++)
            {
                if (Config.Features.harvestCropsRangeMode != ModConfig.HarvestCropsRangeMode.Walk
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
