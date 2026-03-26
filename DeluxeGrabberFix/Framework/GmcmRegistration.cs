using System;
using System.Collections.Generic;
using System.Linq;
using DeluxeGrabberFix.Interfaces;
using StardewModdingAPI;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

internal class GmcmRegistration
{
    private readonly ModEntry _mod;
    private readonly LocationManager _locations;

    private IGenericModConfigMenuApi _api;
    private LocationBatchAction? _pendingBatchAction;

    internal enum LocationBatchAction { EnableAll, DisableAll, SelectVisitedOnly }

    public GmcmRegistration(ModEntry mod, LocationManager locations)
    {
        _mod = mod;
        _locations = locations;
    }

    internal bool Initialize()
    {
        _api = _mod.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (_api == null)
            return false;

        RegisterConfigMenu();
        return true;
    }

    internal void RebuildConfigMenu()
    {
        if (_api == null)
            return;

        _api.Unregister(_mod.ModManifest);
        RegisterConfigMenu();
    }

    internal bool ProcessPendingBatchAction()
    {
        if (!_pendingBatchAction.HasValue)
            return false;

        var action = _pendingBatchAction.Value;
        _pendingBatchAction = null;

        _mod.Config.SkippedLocations ??= new HashSet<string>();

        switch (action)
        {
            case LocationBatchAction.EnableAll:
                _mod.Config.SkippedLocations.Clear();
                break;

            case LocationBatchAction.DisableAll:
                if (_locations.DiscoveredLocations != null)
                    foreach (var loc in _locations.DiscoveredLocations)
                        _mod.Config.SkippedLocations.Add(loc.Name);
                break;

            case LocationBatchAction.SelectVisitedOnly:
                _mod.Config.selectVisitedOnly = true;
                if (_locations.DiscoveredLocations != null)
                {
                    foreach (var (locName, _) in _locations.DiscoveredLocations)
                    {
                        bool visited = Game1.MasterPlayer.locationsVisited.Contains(locName);
                        if (visited)
                            _mod.Config.SkippedLocations.Remove(locName);
                        else
                            _mod.Config.SkippedLocations.Add(locName);
                    }

                    if (_locations.SaveData != null)
                    {
                        _locations.SaveData.AutoSkippedLocations.Clear();
                        foreach (var name in _mod.Config.SkippedLocations)
                            _locations.SaveData.AutoSkippedLocations.Add(name);
                        _locations.SaveData.ManuallyManagedLocations.Clear();
                        _locations.WriteSaveData();
                    }
                }
                break;
        }

        _mod.Helper.WriteConfig(_mod.Config);
        RebuildConfigMenu();
        _api.OpenModMenu(_mod.ModManifest);
        return true;
    }

    private void RegisterConfigMenu()
    {
        var api = _api;
        var config = _mod.Config;

        api.Register(_mod.ModManifest,
            () => _mod.Config = new ModConfig(),
            () =>
            {
                _mod.Helper.WriteConfig(_mod.Config);
                _mod.Monitor.Log($"GMCM saved. selectVisitedOnly={_mod.Config.selectVisitedOnly}, IsWorldReady={Context.IsWorldReady}, saveData={((_locations.SaveData != null) ? "loaded" : "null")}", LogLevel.Info);
                if (_mod.Config.selectVisitedOnly && Context.IsWorldReady && _locations.SaveData != null)
                {
                    _locations.DiscoverLocations();
                    _locations.ApplyVisitAutoSkip();
                }
            });

        // Main page -- category links
        api.AddPageLink(_mod.ModManifest, "crop-harvesting",
            () => _mod.Helper.Translation.Get("page.crop-harvesting.link"),
            () => _mod.Helper.Translation.Get("section.crop-harvesting.tooltip"));

        api.AddPageLink(_mod.ModManifest, "other-harvesting",
            () => _mod.Helper.Translation.Get("page.other-harvesting.link"));

        api.AddPageLink(_mod.ModManifest, "machine-collection",
            () => _mod.Helper.Translation.Get("page.machine-collection.link"),
            () => _mod.Helper.Translation.Get("section.machine-collection.tooltip"));

        api.AddPageLink(_mod.ModManifest, "miscellaneous",
            () => _mod.Helper.Translation.Get("page.miscellaneous.link"));

        api.AddPageLink(_mod.ModManifest, "compatibility",
            () => _mod.Helper.Translation.Get("page.compatibility.link"),
            () => _mod.Helper.Translation.Get("page.compatibility.tooltip"));

        api.AddPageLink(_mod.ModManifest, "skipped-locations",
            () => _mod.Helper.Translation.Get("config.skipped-locations-link"),
            () => _mod.Helper.Translation.Get("config.skipped-locations-link.tooltip"));

        // Crop Harvesting page
        api.AddPage(_mod.ModManifest, "crop-harvesting", () => _mod.Helper.Translation.Get("section.crop-harvesting"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.harvestCrops,
            v => _mod.Config.harvestCrops = v,
            () => _mod.Helper.Translation.Get("config.harvest-crops"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.harvestCropsIndoorPots,
            v => _mod.Config.harvestCropsIndoorPots = v,
            () => _mod.Helper.Translation.Get("config.harvest-crops-indoor-pots"),
            () => _mod.Helper.Translation.Get("config.harvest-crops-indoor-pots.tooltip"));

        api.AddTextOption(_mod.ModManifest,
            () => ModConfig.FlowerHarvestDict[_mod.Config.flowers],
            v => _mod.Config.flowers = ModConfig.FlowerHarvestReverseDict[v],
            () => _mod.Helper.Translation.Get("config.harvest-flowers"),
            () => _mod.Helper.Translation.Get("config.harvest-flowers.tooltip"),
            ModConfig.FlowerHarvestStrings,
            v => _mod.Helper.Translation.Get($"dropdown.flower-{v.ToLower()}"));

        api.AddNumberOption(_mod.ModManifest,
            () => _mod.Config.beeHouseRange,
            v => _mod.Config.beeHouseRange = Math.Max(1, v),
            () => _mod.Helper.Translation.Get("config.bee-house-range"),
            () => _mod.Helper.Translation.Get("config.bee-house-range.tooltip"));

        api.AddNumberOption(_mod.ModManifest,
            () => _mod.Config.harvestCropsRange,
            v => _mod.Config.harvestCropsRange = Math.Max(-1, v),
            () => _mod.Helper.Translation.Get("config.harvest-range"),
            () => _mod.Helper.Translation.Get("config.harvest-range.tooltip"));

        api.AddTextOption(_mod.ModManifest,
            () => ModConfig.HarvestCropsRangeDict[_mod.Config.harvestCropsRangeMode],
            v => _mod.Config.harvestCropsRangeMode = ModConfig.HarvestCropsRangeReverseDict[v],
            () => _mod.Helper.Translation.Get("config.harvest-range-mode"),
            () => _mod.Helper.Translation.Get("config.harvest-range-mode.tooltip"),
            ModConfig.HarvestCropsRangeModeStrings,
            v => _mod.Helper.Translation.Get($"dropdown.{v.ToLower()}"));

        // Other Harvesting page
        api.AddPage(_mod.ModManifest, "other-harvesting", () => _mod.Helper.Translation.Get("section.other-harvesting"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.forage,
            v => _mod.Config.forage = v,
            () => _mod.Helper.Translation.Get("config.collect-forage"),
            () => _mod.Helper.Translation.Get("config.collect-forage.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.fruitTrees,
            v => _mod.Config.fruitTrees = v,
            () => _mod.Helper.Translation.Get("config.harvest-fruit-trees"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.bushes,
            v => _mod.Config.bushes = v,
            () => _mod.Helper.Translation.Get("config.harvest-berry-bushes"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.seedTrees,
            v => _mod.Config.seedTrees = v,
            () => _mod.Helper.Translation.Get("config.shake-seed-trees"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.animalProducts,
            v => _mod.Config.animalProducts = v,
            () => _mod.Helper.Translation.Get("config.collect-animal-products"),
            () => _mod.Helper.Translation.Get("config.collect-animal-products.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.slimeHutch,
            v => _mod.Config.slimeHutch = v,
            () => _mod.Helper.Translation.Get("config.grab-slime-balls"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.farmCaveMushrooms,
            v => _mod.Config.farmCaveMushrooms = v,
            () => _mod.Helper.Translation.Get("config.grab-farm-cave-mushrooms"),
            () => _mod.Helper.Translation.Get("config.grab-farm-cave-mushrooms.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.artifactSpots,
            v => _mod.Config.artifactSpots = v,
            () => _mod.Helper.Translation.Get("config.dig-up-artifact-spots"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.orePan,
            v => _mod.Config.orePan = v,
            () => _mod.Helper.Translation.Get("config.collect-ore-from-panning"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.fellHardwoodStumps,
            v => _mod.Config.fellHardwoodStumps = v,
            () => _mod.Helper.Translation.Get("config.fell-stumps"),
            () => _mod.Helper.Translation.Get("config.fell-stumps.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.garbageCans,
            v => _mod.Config.garbageCans = v,
            () => _mod.Helper.Translation.Get("config.search-garbage-cans"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.seedSpots,
            v => _mod.Config.seedSpots = v,
            () => _mod.Helper.Translation.Get("config.dig-up-seed-spots"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.harvestMoss,
            v => _mod.Config.harvestMoss = v,
            () => _mod.Helper.Translation.Get("config.harvest-moss"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.harvestGreenRainWeeds,
            v => _mod.Config.harvestGreenRainWeeds = v,
            () => _mod.Helper.Translation.Get("config.harvest-green-rain-weeds"),
            () => _mod.Helper.Translation.Get("config.harvest-green-rain-weeds.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.collectDebris,
            v => _mod.Config.collectDebris = v,
            () => _mod.Helper.Translation.Get("config.collect-debris"),
            () => _mod.Helper.Translation.Get("config.collect-debris.tooltip"));

        // Machine Collection page
        api.AddPage(_mod.ModManifest, "machine-collection", () => _mod.Helper.Translation.Get("section.machine-collection"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.collectMachines,
            v => _mod.Config.collectMachines = v,
            () => _mod.Helper.Translation.Get("config.collect-machine-outputs"),
            () => _mod.Helper.Translation.Get("config.collect-machine-outputs.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.collectCrabPots,
            v => _mod.Config.collectCrabPots = v,
            () => _mod.Helper.Translation.Get("config.collect-crab-pots"),
            () => _mod.Helper.Translation.Get("config.collect-crab-pots.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.collectBeeHouses,
            v => _mod.Config.collectBeeHouses = v,
            () => _mod.Helper.Translation.Get("config.collect-bee-houses"),
            () => _mod.Helper.Translation.Get("config.collect-bee-houses.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.collectTappers,
            v => _mod.Config.collectTappers = v,
            () => _mod.Helper.Translation.Get("config.collect-tappers"),
            () => _mod.Helper.Translation.Get("config.collect-tappers.tooltip"));

        // Miscellaneous page
        api.AddPage(_mod.ModManifest, "miscellaneous", () => _mod.Helper.Translation.Get("section.miscellaneous"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.reportYield,
            v => _mod.Config.reportYield = v,
            () => _mod.Helper.Translation.Get("config.report-yield"),
            () => _mod.Helper.Translation.Get("config.report-yield.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.debugLogging,
            v => _mod.Config.debugLogging = v,
            () => _mod.Helper.Translation.Get("config.debug-logging"),
            () => _mod.Helper.Translation.Get("config.debug-logging.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.gainExperience,
            v => _mod.Config.gainExperience = v,
            () => _mod.Helper.Translation.Get("config.gain-experience"),
            () => _mod.Helper.Translation.Get("config.gain-experience.tooltip"));

        api.AddTextOption(_mod.ModManifest,
            () => ModConfig.GrabFrequencyDict[_mod.Config.grabFrequency],
            v => _mod.Config.grabFrequency = ModConfig.GrabFrequencyReverseDict[v],
            () => _mod.Helper.Translation.Get("config.grab-frequency"),
            () => _mod.Helper.Translation.Get("config.grab-frequency.tooltip"),
            ModConfig.GrabFrequencyStrings,
            v => _mod.Helper.Translation.Get($"dropdown.{v.ToLower()}"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.skipFestivalLocations,
            v => _mod.Config.skipFestivalLocations = v,
            () => _mod.Helper.Translation.Get("config.skip-festival-locations"),
            () => _mod.Helper.Translation.Get("config.skip-festival-locations.tooltip"));

        api.AddTextOption(_mod.ModManifest,
            () => string.Join(", ", _mod.Config.excludedItems ?? new HashSet<string>()),
            v => _mod.Config.excludedItems = new HashSet<string>(
                v.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)),
            () => _mod.Helper.Translation.Get("config.excluded-items"),
            () => _mod.Helper.Translation.Get("config.excluded-items.tooltip"));

        api.AddTextOption(_mod.ModManifest,
            () => ModConfig.GlobalGrabberDict[_mod.Config.globalGrabber],
            v => _mod.Config.globalGrabber = ModConfig.GlobalGrabberReverseDict[v],
            () => _mod.Helper.Translation.Get("config.global-grabber-mode"),
            () => _mod.Helper.Translation.Get("config.global-grabber-mode.tooltip"),
            ModConfig.GlobalGrabberModeStrings,
            v => _mod.Helper.Translation.Get($"dropdown.{v.ToLower()}"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.globalAutoFire,
            v => _mod.Config.globalAutoFire = v,
            () => _mod.Helper.Translation.Get("config.auto-fire-global-grabber"),
            () => _mod.Helper.Translation.Get("config.auto-fire-global-grabber.tooltip"));

        api.AddKeybind(_mod.ModManifest,
            () => _mod.Config.globalFireButton,
            v => _mod.Config.globalFireButton = v,
            () => _mod.Helper.Translation.Get("config.fire-global-grabber"),
            () => _mod.Helper.Translation.Get("config.fire-global-grabber.tooltip"));

        api.AddKeybind(_mod.ModManifest,
            () => _mod.Config.designateGrabberButton,
            v => _mod.Config.designateGrabberButton = v,
            () => _mod.Helper.Translation.Get("config.designate-global-grabber"),
            () => _mod.Helper.Translation.Get("config.designate-global-grabber.tooltip"));

        api.AddNumberOption(_mod.ModManifest,
            () => _mod.Config.globalButtonOffsetX,
            v => _mod.Config.globalButtonOffsetX = Math.Clamp(v, -500, 500),
            () => _mod.Helper.Translation.Get("config.global-button-x-offset"),
            () => _mod.Helper.Translation.Get("config.global-button-x-offset.tooltip"));

        api.AddNumberOption(_mod.ModManifest,
            () => _mod.Config.globalButtonOffsetY,
            v => _mod.Config.globalButtonOffsetY = Math.Clamp(v, -500, 500),
            () => _mod.Helper.Translation.Get("config.global-button-y-offset"),
            () => _mod.Helper.Translation.Get("config.global-button-y-offset.tooltip"));

        api.AddNumberOption(_mod.ModManifest,
            () => _mod.Config.renameButtonOffsetX,
            v => _mod.Config.renameButtonOffsetX = Math.Clamp(v, -500, 500),
            () => _mod.Helper.Translation.Get("config.rename-button-x-offset"),
            () => _mod.Helper.Translation.Get("config.rename-button-x-offset.tooltip"));

        api.AddNumberOption(_mod.ModManifest,
            () => _mod.Config.renameButtonOffsetY,
            v => _mod.Config.renameButtonOffsetY = Math.Clamp(v, -500, 500),
            () => _mod.Helper.Translation.Get("config.rename-button-y-offset"),
            () => _mod.Helper.Translation.Get("config.rename-button-y-offset.tooltip"));

        // Compatibility page
        api.AddPage(_mod.ModManifest, "compatibility", () => _mod.Helper.Translation.Get("page.compatibility"));

        api.AddParagraph(_mod.ModManifest,
            () => _mod.Helper.Translation.Get("page.compatibility.paragraph"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.sunberryVillageExclusions,
            v => _mod.Config.sunberryVillageExclusions = v,
            () => _mod.Helper.Translation.Get("config.sunberry-village-exclusions"),
            () => _mod.Helper.Translation.Get("config.sunberry-village-exclusions.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.visitMtVapiusExclusions,
            v => _mod.Config.visitMtVapiusExclusions = v,
            () => _mod.Helper.Translation.Get("config.visit-mt-vapius-exclusions"),
            () => _mod.Helper.Translation.Get("config.visit-mt-vapius-exclusions.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.baublesExclusions,
            v => _mod.Config.baublesExclusions = v,
            () => _mod.Helper.Translation.Get("config.baubles-exclusions"),
            () => _mod.Helper.Translation.Get("config.baubles-exclusions.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.resourceChickensExclusions,
            v => _mod.Config.resourceChickensExclusions = v,
            () => _mod.Helper.Translation.Get("config.resource-chickens-exclusions"),
            () => _mod.Helper.Translation.Get("config.resource-chickens-exclusions.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.capeStardewExclusions,
            v => _mod.Config.capeStardewExclusions = v,
            () => _mod.Helper.Translation.Get("config.cape-stardew-exclusions"),
            () => _mod.Helper.Translation.Get("config.cape-stardew-exclusions.tooltip"));

        api.AddBoolOption(_mod.ModManifest,
            () => _mod.Config.buriedItems,
            v => _mod.Config.buriedItems = v,
            () => _mod.Helper.Translation.Get("config.collect-buried-items"),
            () => _mod.Helper.Translation.Get("config.collect-buried-items.tooltip"));

        // Skipped Locations page
        api.AddPage(_mod.ModManifest, "skipped-locations", () => _mod.Helper.Translation.Get("config.skipped-locations-page"));

        if (_locations.DiscoveredLocations != null && _locations.DiscoveredLocations.Count > 0)
        {
            api.AddParagraph(_mod.ModManifest,
                () => _mod.Helper.Translation.Get("config.skipped-locations-paragraph"));

            api.AddBoolOption(_mod.ModManifest,
                getValue: () => _locations.DiscoveredLocations.All(loc => _mod.Config.SkippedLocations?.Contains(loc.Name) != true),
                setValue: v => { },
                name: () => _mod.Helper.Translation.Get("config.enable-all"),
                tooltip: () => _mod.Helper.Translation.Get("config.enable-all.tooltip"),
                fieldId: "enable-all");

            api.AddBoolOption(_mod.ModManifest,
                getValue: () => Context.IsWorldReady && _locations.DiscoveredLocations != null &&
                    _locations.DiscoveredLocations.All(loc =>
                    {
                        bool visited = Game1.MasterPlayer.locationsVisited.Contains(loc.Name);
                        bool enabled = _mod.Config.SkippedLocations?.Contains(loc.Name) != true;
                        return visited == enabled;
                    }),
                setValue: v => { },
                name: () => _mod.Helper.Translation.Get("config.select-visited-only"),
                tooltip: () => _mod.Helper.Translation.Get("config.select-visited-only.tooltip"),
                fieldId: "select-visited-only");

            api.OnFieldChanged(_mod.ModManifest, (fieldId, value) =>
            {
                if (fieldId == "enable-all")
                    _pendingBatchAction = (bool)value
                        ? LocationBatchAction.EnableAll
                        : LocationBatchAction.DisableAll;
                else if (fieldId == "select-visited-only" && (bool)value)
                    _pendingBatchAction = LocationBatchAction.SelectVisitedOnly;
            });

            if (Context.IsWorldReady)
            {
                var visitedLocs = _locations.DiscoveredLocations
                    .Where(loc => Game1.MasterPlayer.locationsVisited.Contains(loc.Name))
                    .ToList();
                var unvisitedLocs = _locations.DiscoveredLocations
                    .Where(loc => !Game1.MasterPlayer.locationsVisited.Contains(loc.Name))
                    .ToList();

                if (visitedLocs.Count > 0)
                {
                    api.AddSectionTitle(_mod.ModManifest, () => _mod.Helper.Translation.Get("section.visited-locations"));
                    foreach (var (locName, displayName) in visitedLocs)
                        AddLocationToggle(api, locName, displayName);
                }

                if (unvisitedLocs.Count > 0)
                {
                    api.AddSectionTitle(_mod.ModManifest, () => _mod.Helper.Translation.Get("section.not-yet-visited"));
                    foreach (var (locName, displayName) in unvisitedLocs)
                        AddLocationToggle(api, locName, displayName);
                }
            }
            else
            {
                foreach (var (locName, displayName) in _locations.DiscoveredLocations)
                    AddLocationToggle(api, locName, displayName);
            }
        }
        else
        {
            api.AddParagraph(_mod.ModManifest,
                () => _mod.Helper.Translation.Get("config.no-save-loaded"));
        }
    }

    private void AddLocationToggle(IGenericModConfigMenuApi api, string locName, string displayName)
    {
        string capturedName = locName;
        string capturedDisplay = displayName;

        api.AddBoolOption(_mod.ModManifest,
            getValue: () => _mod.Config.SkippedLocations?.Contains(capturedName) != true,
            setValue: v =>
            {
                _mod.Config.SkippedLocations ??= new HashSet<string>();
                if (!v)
                    _mod.Config.SkippedLocations.Add(capturedName);
                else
                    _mod.Config.SkippedLocations.Remove(capturedName);

                if (_locations.SaveData != null)
                {
                    _locations.SaveData.ManuallyManagedLocations.Add(capturedName);
                    _locations.SaveData.AutoSkippedLocations.Remove(capturedName);
                    _locations.WriteSaveData();
                }
            },
            name: () => capturedDisplay,
            tooltip: () => capturedName != capturedDisplay ? capturedName : null);
    }
}
