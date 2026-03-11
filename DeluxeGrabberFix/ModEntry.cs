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
    internal bool IsForageGrabEnabled { get; set; }
    internal List<KeyValuePair<Vector2, Object>> CachedDesignatedGrabbers { get; set; }
    internal const string GlobalGrabberModDataKey = "Rafia.DeluxeGrabberFix/IsGlobalGrabber";
    private readonly HashSet<GameLocation> _dirtyLocations = new();
    private bool _isGrabbing;
    private bool _pendingDayStartGrab;
    private IGenericModConfigMenuApi _gmcmApi;
    private IVanillaPlusProfessionsApi _vppApi;
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

    public void LogInfo(string message)
    {
        Monitor.Log(message, LogLevel.Info);
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
            Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("hud.designate-first"), HUDMessage.error_type));
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
        IsForageGrabEnabled = true;
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
            IsForageGrabEnabled = false;
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
        if (grabMenu.context is not Object obj || obj.QualifiedItemId != BigCraftableIds.AutoGrabber
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

        if (obj == null || obj.QualifiedItemId != BigCraftableIds.AutoGrabber
            || obj.heldObject.Value is not StardewValley.Objects.Chest)
        {
            Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("hud.hover-over-grabber"), HUDMessage.error_type));
            return;
        }

        if (obj.modData.ContainsKey(GlobalGrabberModDataKey))
        {
            obj.modData.Remove(GlobalGrabberModDataKey);
            Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("hud.no-longer-global")));
            return;
        }

        ClearAllDesignations();
        obj.modData[GlobalGrabberModDataKey] = "true";
        Game1.addHUDMessage(new HUDMessage(Helper.Translation.Get("hud.now-global")));
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

        _vppApi = Helper.ModRegistry.GetApi<IVanillaPlusProfessionsApi>("KediDili.VanillaPlusProfessions");
        if (_vppApi != null)
            Monitor.Log("Vanilla Plus Professions detected — VPP compatibility enabled.", LogLevel.Info);

        _gmcmApi = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (_gmcmApi == null)
            return;

        RegisterConfigMenu();
    }

    private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
    {
        _saveData = Helper.Data.ReadSaveData<SaveData>(SaveDataKey) ?? new SaveData();
        TownGarbageCanGrabber.ClearCache();
        DiscoverLocations();
        ApplyVisitAutoSkip();
        RebuildConfigMenu();
    }

    private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
    {
        _discoveredLocations = null;
        _saveData = null;
        TownGarbageCanGrabber.ClearCache();
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

        // Main page — category links
        api.AddPageLink(ModManifest, "crop-harvesting",
            () => Helper.Translation.Get("page.crop-harvesting.link"),
            () => Helper.Translation.Get("section.crop-harvesting.tooltip"));

        api.AddPageLink(ModManifest, "other-harvesting",
            () => Helper.Translation.Get("page.other-harvesting.link"));

        api.AddPageLink(ModManifest, "machine-collection",
            () => Helper.Translation.Get("page.machine-collection.link"),
            () => Helper.Translation.Get("section.machine-collection.tooltip"));

        api.AddPageLink(ModManifest, "miscellaneous",
            () => Helper.Translation.Get("page.miscellaneous.link"));

        api.AddPageLink(ModManifest, "compatibility",
            () => Helper.Translation.Get("page.compatibility.link"),
            () => Helper.Translation.Get("page.compatibility.tooltip"));

        // Skipped Locations page link
        api.AddPageLink(ModManifest, "skipped-locations",
            () => Helper.Translation.Get("config.skipped-locations-link"),
            () => Helper.Translation.Get("config.skipped-locations-link.tooltip"));

        // Crop Harvesting page
        api.AddPage(ModManifest, "crop-harvesting", () => Helper.Translation.Get("section.crop-harvesting"));

        api.AddBoolOption(ModManifest,
            () => Config.harvestCrops,
            v => Config.harvestCrops = v,
            () => Helper.Translation.Get("config.harvest-crops"));

        api.AddBoolOption(ModManifest,
            () => Config.harvestCropsIndoorPots,
            v => Config.harvestCropsIndoorPots = v,
            () => Helper.Translation.Get("config.harvest-crops-indoor-pots"),
            () => Helper.Translation.Get("config.harvest-crops-indoor-pots.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.flowers,
            v => Config.flowers = v,
            () => Helper.Translation.Get("config.harvest-flowers"),
            () => Helper.Translation.Get("config.harvest-flowers.tooltip"));

        api.AddNumberOption(ModManifest,
            () => Config.harvestCropsRange,
            v => Config.harvestCropsRange = Math.Max(-1, v),
            () => Helper.Translation.Get("config.harvest-range"),
            () => Helper.Translation.Get("config.harvest-range.tooltip"));

        api.AddTextOption(ModManifest,
            () => ModConfig.HarvestCropsRangeDict[Config.harvestCropsRangeMode],
            v => Config.harvestCropsRangeMode = ModConfig.HarvestCropsRangeReverseDict[v],
            () => Helper.Translation.Get("config.harvest-range-mode"),
            () => Helper.Translation.Get("config.harvest-range-mode.tooltip"),
            ModConfig.HarvestCropsRangeModeStrings,
            v => Helper.Translation.Get($"dropdown.{v.ToLower()}"));

        // Other Harvesting page
        api.AddPage(ModManifest, "other-harvesting", () => Helper.Translation.Get("section.other-harvesting"));

        api.AddBoolOption(ModManifest,
            () => Config.forage,
            v => Config.forage = v,
            () => Helper.Translation.Get("config.collect-forage"),
            () => Helper.Translation.Get("config.collect-forage.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.fruitTrees,
            v => Config.fruitTrees = v,
            () => Helper.Translation.Get("config.harvest-fruit-trees"));

        api.AddBoolOption(ModManifest,
            () => Config.bushes,
            v => Config.bushes = v,
            () => Helper.Translation.Get("config.harvest-berry-bushes"));

        api.AddBoolOption(ModManifest,
            () => Config.seedTrees,
            v => Config.seedTrees = v,
            () => Helper.Translation.Get("config.shake-seed-trees"));

        api.AddBoolOption(ModManifest,
            () => Config.animalProducts,
            v => Config.animalProducts = v,
            () => Helper.Translation.Get("config.collect-animal-products"),
            () => Helper.Translation.Get("config.collect-animal-products.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.slimeHutch,
            v => Config.slimeHutch = v,
            () => Helper.Translation.Get("config.grab-slime-balls"));

        api.AddBoolOption(ModManifest,
            () => Config.farmCaveMushrooms,
            v => Config.farmCaveMushrooms = v,
            () => Helper.Translation.Get("config.grab-farm-cave-mushrooms"),
            () => Helper.Translation.Get("config.grab-farm-cave-mushrooms.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.artifactSpots,
            v => Config.artifactSpots = v,
            () => Helper.Translation.Get("config.dig-up-artifact-spots"));

        api.AddBoolOption(ModManifest,
            () => Config.orePan,
            v => Config.orePan = v,
            () => Helper.Translation.Get("config.collect-ore-from-panning"));

        api.AddBoolOption(ModManifest,
            () => Config.fellSecretWoodsStumps,
            v => Config.fellSecretWoodsStumps = v,
            () => Helper.Translation.Get("config.fell-stumps-secret-woods"));

        api.AddBoolOption(ModManifest,
            () => Config.garbageCans,
            v => Config.garbageCans = v,
            () => Helper.Translation.Get("config.search-garbage-cans"));

        api.AddBoolOption(ModManifest,
            () => Config.seedSpots,
            v => Config.seedSpots = v,
            () => Helper.Translation.Get("config.dig-up-seed-spots"));

        api.AddBoolOption(ModManifest,
            () => Config.harvestMoss,
            v => Config.harvestMoss = v,
            () => Helper.Translation.Get("config.harvest-moss"));

        api.AddBoolOption(ModManifest,
            () => Config.collectDebris,
            v => Config.collectDebris = v,
            () => Helper.Translation.Get("config.collect-debris"),
            () => Helper.Translation.Get("config.collect-debris.tooltip"));

        // Machine Collection page
        api.AddPage(ModManifest, "machine-collection", () => Helper.Translation.Get("section.machine-collection"));

        api.AddBoolOption(ModManifest,
            () => Config.collectMachines,
            v => Config.collectMachines = v,
            () => Helper.Translation.Get("config.collect-machine-outputs"),
            () => Helper.Translation.Get("config.collect-machine-outputs.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.collectCrabPots,
            v => Config.collectCrabPots = v,
            () => Helper.Translation.Get("config.collect-crab-pots"),
            () => Helper.Translation.Get("config.collect-crab-pots.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.collectBeeHouses,
            v => Config.collectBeeHouses = v,
            () => Helper.Translation.Get("config.collect-bee-houses"),
            () => Helper.Translation.Get("config.collect-bee-houses.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.collectTappers,
            v => Config.collectTappers = v,
            () => Helper.Translation.Get("config.collect-tappers"),
            () => Helper.Translation.Get("config.collect-tappers.tooltip"));

        // Miscellaneous page
        api.AddPage(ModManifest, "miscellaneous", () => Helper.Translation.Get("section.miscellaneous"));

        api.AddBoolOption(ModManifest,
            () => Config.reportYield,
            v => Config.reportYield = v,
            () => Helper.Translation.Get("config.report-yield"),
            () => Helper.Translation.Get("config.report-yield.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.debugLogging,
            v => Config.debugLogging = v,
            () => Helper.Translation.Get("config.debug-logging"),
            () => Helper.Translation.Get("config.debug-logging.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.gainExperience,
            v => Config.gainExperience = v,
            () => Helper.Translation.Get("config.gain-experience"),
            () => Helper.Translation.Get("config.gain-experience.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.skipFestivalLocations,
            v => Config.skipFestivalLocations = v,
            () => Helper.Translation.Get("config.skip-festival-locations"),
            () => Helper.Translation.Get("config.skip-festival-locations.tooltip"));

        api.AddTextOption(ModManifest,
            () => string.Join(", ", Config.excludedItems ?? new HashSet<string>()),
            v => Config.excludedItems = new HashSet<string>(
                v.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)),
            () => Helper.Translation.Get("config.excluded-items"),
            () => Helper.Translation.Get("config.excluded-items.tooltip"));

        api.AddTextOption(ModManifest,
            () => ModConfig.GlobalGrabberDict[Config.globalGrabber],
            v => Config.globalGrabber = ModConfig.GlobalGrabberReverseDict[v],
            () => Helper.Translation.Get("config.global-grabber-mode"),
            () => Helper.Translation.Get("config.global-grabber-mode.tooltip"),
            ModConfig.GlobalGrabberModeStrings,
            v => Helper.Translation.Get($"dropdown.{v.ToLower()}"));

        api.AddBoolOption(ModManifest,
            () => Config.globalAutoFire,
            v => Config.globalAutoFire = v,
            () => Helper.Translation.Get("config.auto-fire-global-grabber"),
            () => Helper.Translation.Get("config.auto-fire-global-grabber.tooltip"));

        api.AddKeybind(ModManifest,
            () => Config.globalFireButton,
            v => Config.globalFireButton = v,
            () => Helper.Translation.Get("config.fire-global-grabber"),
            () => Helper.Translation.Get("config.fire-global-grabber.tooltip"));

        api.AddKeybind(ModManifest,
            () => Config.designateGrabberButton,
            v => Config.designateGrabberButton = v,
            () => Helper.Translation.Get("config.designate-global-grabber"),
            () => Helper.Translation.Get("config.designate-global-grabber.tooltip"));

        api.AddNumberOption(ModManifest,
            () => Config.globalButtonOffsetX,
            v => Config.globalButtonOffsetX = Math.Clamp(v, -500, 500),
            () => Helper.Translation.Get("config.global-button-x-offset"),
            () => Helper.Translation.Get("config.global-button-x-offset.tooltip"));

        api.AddNumberOption(ModManifest,
            () => Config.globalButtonOffsetY,
            v => Config.globalButtonOffsetY = Math.Clamp(v, -500, 500),
            () => Helper.Translation.Get("config.global-button-y-offset"),
            () => Helper.Translation.Get("config.global-button-y-offset.tooltip"));

        // Compatibility page
        api.AddPage(ModManifest, "compatibility", () => Helper.Translation.Get("page.compatibility"));

        api.AddParagraph(ModManifest,
            () => Helper.Translation.Get("page.compatibility.paragraph"));

        api.AddBoolOption(ModManifest,
            () => Config.sunberryVillageExclusions,
            v => Config.sunberryVillageExclusions = v,
            () => Helper.Translation.Get("config.sunberry-village-exclusions"),
            () => Helper.Translation.Get("config.sunberry-village-exclusions.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.visitMtVapiusExclusions,
            v => Config.visitMtVapiusExclusions = v,
            () => Helper.Translation.Get("config.visit-mt-vapius-exclusions"),
            () => Helper.Translation.Get("config.visit-mt-vapius-exclusions.tooltip"));

        api.AddBoolOption(ModManifest,
            () => Config.buriedItems,
            v => Config.buriedItems = v,
            () => Helper.Translation.Get("config.collect-buried-items"),
            () => Helper.Translation.Get("config.collect-buried-items.tooltip"));

        // Skipped Locations page
        api.AddPage(ModManifest, "skipped-locations", () => Helper.Translation.Get("config.skipped-locations-page"));

        if (_discoveredLocations != null && _discoveredLocations.Count > 0)
        {
            api.AddParagraph(ModManifest,
                () => Helper.Translation.Get("config.skipped-locations-paragraph"));

            api.AddBoolOption(ModManifest,
                getValue: () => _discoveredLocations.All(loc => Config.SkippedLocations?.Contains(loc.Name) != true),
                setValue: v => { },
                name: () => Helper.Translation.Get("config.enable-all"),
                tooltip: () => Helper.Translation.Get("config.enable-all.tooltip"),
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
                name: () => Helper.Translation.Get("config.select-visited-only"),
                tooltip: () => Helper.Translation.Get("config.select-visited-only.tooltip"),
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
                    api.AddSectionTitle(ModManifest, () => Helper.Translation.Get("section.visited-locations"));
                    foreach (var (locName, displayName) in visitedLocs)
                        AddLocationToggle(api, locName, displayName);
                }

                if (unvisitedLocs.Count > 0)
                {
                    api.AddSectionTitle(ModManifest, () => Helper.Translation.Get("section.not-yet-visited"));
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
                () => Helper.Translation.Get("config.no-save-loaded"));
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
            IsForageGrabEnabled = true;
            try
            {
                foreach (var location in GetAllLocations())
                {
                    GrabAtLocation(location);
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

    private void OnHourlyUpdate(object sender, TimeChangedEventArgs e)
    {
        if (e.NewTime % 100 != 0)
            return;

        LogDebug("Autograbbing on time change");

        bool useGlobal = Config.globalGrabber == ModConfig.GlobalGrabberMode.All && HasDesignatedGrabber();
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

        try
        {
            foreach (var location in Game1.locations)
            {
                if (!ShouldProcessLocation(location))
                    continue;

                var orePanGrabber = new OrePanGrabber(this, location);
                if (!orePanGrabber.CanGrab())
                    continue;

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
        }
        finally
        {
            if (useGlobal)
            {
                IsGlobalGrabActive = false;
                CachedDesignatedGrabbers = null;
            }
        }
    }

    private void OnDayEnding(object sender, DayEndingEventArgs e)
    {
        if (!Config.forage)
            return;

        LogDebug("Autograbbing forage before sleep");
        _isGrabbing = true;
        IsForageGrabEnabled = true;
        try
        {
            foreach (var location in GetAllLocations())
            {
                GrabAtLocation(location);
            }
        }
        finally
        {
            IsForageGrabEnabled = false;
            _isGrabbing = false;
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
