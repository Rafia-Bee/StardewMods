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

    // Snapshot of the GMCM-relevant inputs that determine the SET of registered options.
    // The static option set never changes, so the only signal is the Enabled Locations
    // page contents -- driven by `LocationManager.DiscoveredLocations` (null vs populated,
    // count, and the names themselves). When this snapshot is unchanged from the last
    // rebuild, RebuildConfigMenu can skip the ~150 API calls without observable difference
    // in the menu (option values are read live via getValue lambdas every time the menu
    // opens). Audit §3.5.
    private OptionSetSnapshot _lastRebuildSnapshot;

    // Tracks the grabberMode at the time the recipe asset was last invalidated, so the
    // GMCM Save callback can skip InvalidateCache("Data/CraftingRecipes") when the user
    // saved without changing modes. Refreshed on ActiveConfigChanged (per-save swap, GMCM
    // reset, return-to-title) since those paths invalidate the cache through ModEntry's
    // own subscriber. Audit §4.14.
    private ModConfig.GrabberMode _lastInvalidatedGrabberMode;

    internal enum LocationBatchAction { EnableAll, DisableAll, SelectVisitedOnly }

    private readonly record struct OptionSetSnapshot(bool HasLocations, int Count, int NamesHash);

    // Audit §4.12: registration lambdas read the active config via this thunk rather
    // than `_mod.Config` directly. The thunk re-resolves on every invocation, so it
    // tolerates `PerSaveConfigManager.SwapActiveConfig` and the GMCM reset path. The
    // contract is: never assign `_getConfig()` into a local before passing the local
    // to a lambda -- the local would close over the orphaned pre-swap instance and
    // every later read/write would target the wrong config. Visible-as-a-getter spelling
    // makes the contract explicit at every call site.
    private readonly Func<ModConfig> _getConfig;

    public GmcmRegistration(ModEntry mod, LocationManager locations)
    {
        _mod = mod;
        _locations = locations;
        _getConfig = () => _mod.Config;
    }

    internal bool Initialize()
    {
        _api = _mod.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (_api == null)
            return false;

        RegisterConfigMenu();
        _lastRebuildSnapshot = ComputeOptionSetSnapshot();
        _lastInvalidatedGrabberMode = _getConfig().grabberMode;
        // Config-swap paths (per-save load, return-to-title, GMCM reset) invalidate
        // the recipe asset via the ActiveConfigChanged subscriber wired in ModEntry.
        // Mirror that here so the GMCM Save callback's compare stays current.
        _mod.ActiveConfigChanged += () => _lastInvalidatedGrabberMode = _getConfig().grabberMode;
        return true;
    }

    internal bool RebuildConfigMenu()
    {
        if (_api == null)
            return false;

        var current = ComputeOptionSetSnapshot();
        if (current == _lastRebuildSnapshot)
        {
            _mod.LogDebug("GMCM rebuild skipped: option set unchanged since last register");
            return false;
        }

        _api.Unregister(_mod.ModManifest);
        RegisterConfigMenu();
        _lastRebuildSnapshot = current;
        return true;
    }

    private OptionSetSnapshot ComputeOptionSetSnapshot()
    {
        var locs = _locations.DiscoveredLocations;
        if (locs == null)
            return new OptionSetSnapshot(false, 0, 0);

        int hash = 0;
        foreach (var (name, _) in locs)
            hash = HashCode.Combine(hash, name);
        return new OptionSetSnapshot(true, locs.Count, hash);
    }

    // Audit §3.9: hot-path gate so `OnUpdateTicked` can early-out at the call site
    // without dispatching into GMCM. The pending action is set by `OnFieldChanged`
    // (GMCM's own synchronous event), but we can't process it inline -- doing so
    // would re-enter GMCM via `OpenModMenu` while it's still iterating field changes.
    // Polling is the only safe pattern; this property keeps the polled check to one
    // field read per tick when nothing is pending (the common case, every tick that
    // isn't right after a batch button click).
    internal bool HasPendingBatchAction => _pendingBatchAction.HasValue;

    internal bool ProcessPendingBatchAction()
    {
        if (!_pendingBatchAction.HasValue)
            return false;

        var action = _pendingBatchAction.Value;
        _pendingBatchAction = null;

        _getConfig().Locations.SkippedLocations ??= new HashSet<string>();

        switch (action)
        {
            case LocationBatchAction.EnableAll:
                // "Enable All" and "Select Visited Only" are contradictory intents:
                // SVO auto-skips unvisited maps, EnableAll wants every map enabled.
                // If SVO stays on, the next ApplyVisitAutoSkip (e.g. on the next GMCM
                // Save while SVO is on, or on save load) re-skips unvisited maps and
                // silently undoes EnableAll. Force SVO off so the user-visible result
                // matches the user-visible intent.
                _getConfig().Locations.selectVisitedOnly = false;
                _getConfig().Locations.SkippedLocations.Clear();
                if (_locations.SaveData != null)
                {
                    _locations.SaveData.ManuallyManagedLocations.Clear();
                    _locations.SaveData.AutoSkippedLocations.Clear();
                    _locations.SaveData.BlacklistedLocations.Clear();
                    _locations.WriteSaveData();
                }
                break;

            case LocationBatchAction.DisableAll:
                if (_locations.DiscoveredLocations != null)
                    foreach (var loc in _locations.DiscoveredLocations)
                        _getConfig().Locations.SkippedLocations.Add(loc.Name);
                break;

            case LocationBatchAction.SelectVisitedOnly:
                _getConfig().Locations.selectVisitedOnly = true;
                if (_locations.DiscoveredLocations != null)
                {
                    foreach (var (locName, _) in _locations.DiscoveredLocations)
                    {
                        bool visited = Game1.MasterPlayer.locationsVisited.Contains(locName);
                        if (visited)
                            _getConfig().Locations.SkippedLocations.Remove(locName);
                        else
                            _getConfig().Locations.SkippedLocations.Add(locName);
                    }

                    if (_locations.SaveData != null)
                    {
                        _locations.SaveData.AutoSkippedLocations.Clear();
                        foreach (var name in _getConfig().Locations.SkippedLocations)
                            _locations.SaveData.AutoSkippedLocations.Add(name);
                        _locations.SaveData.ManuallyManagedLocations.Clear();
                        _locations.SaveData.BlacklistedLocations.Clear();
                        _locations.WriteSaveData();
                    }
                }
                break;
        }

        _mod.ConfigManager.SaveActiveConfig();
        // Only re-open the menu when an actual unregister+register happened.
        // When the option set is unchanged (the common case for batch actions
        // that only mutate values), the user's current subpage stays valid and
        // we don't need to surprise them by jumping back to the root page.
        if (RebuildConfigMenu())
            _api.OpenModMenu(_mod.ModManifest);
        return true;
    }

    private void RegisterConfigMenu()
    {
        _api.Register(_mod.ModManifest,
            reset: () =>
            {
                // Audit §2.9: SwapActiveConfig fires ActiveConfigChanged so
                // RefreshRenderedWorldHook + Data/CraftingRecipes invalidation run
                // automatically. The latter is new for the reset path -- previously
                // a Specialized -> Classic reset would leave the recipe asset stale
                // until the next manual save or save-load.
                _mod.SwapActiveConfig(new ModConfig());
                if (_locations.SaveData != null)
                {
                    _locations.SaveData.ManuallyManagedLocations.Clear();
                    _locations.SaveData.AutoSkippedLocations.Clear();
                    _locations.SaveData.BlacklistedLocations.Clear();
                    _locations.WriteSaveData();
                }
            },
            save: () =>
            {
                _mod.ConfigManager.SaveActiveConfig();
                // Audit §4.14: invalidate Data/CraftingRecipes only when grabberMode
                // actually changed since the last invalidation. The recipe asset's
                // contents are mode-driven (Classic hides specialized recipes, Specialized
                // hides the vanilla one), so a Save that didn't toggle the mode produces
                // an identical asset and the invalidate is wasted work. ActiveConfigChanged
                // refreshes the tracker on every config swap.
                if (_getConfig().grabberMode != _lastInvalidatedGrabberMode)
                {
                    _mod.Helper.GameContent.InvalidateCache("Data/CraftingRecipes");
                    _lastInvalidatedGrabberMode = _getConfig().grabberMode;
                }
                _mod.Monitor.Log($"GMCM saved. selectVisitedOnly={_getConfig().Locations.selectVisitedOnly}, IsWorldReady={Context.IsWorldReady}, saveData={((_locations.SaveData != null) ? "loaded" : "null")}", LogLevel.Info);
                if (_getConfig().Locations.selectVisitedOnly && Context.IsWorldReady && _locations.SaveData != null)
                {
                    _locations.DiscoverLocations();
                    _locations.ApplyVisitAutoSkip();
                }
                _mod.RefreshRenderedWorldHook();
            });

        // Lambdas below resolve the active config via _getConfig() rather than _getConfig().
        // Both spellings re-resolve on every call (Config is a property), but _getConfig is
        // explicitly a thunk: the calling shape `_getConfig().X` makes the re-resolution
        // contract visible at the call site, so a future reader is less likely to write
        // `var c = _getConfig(); ... () => c.X` (which would close over the orphaned pre-swap
        // instance and silently misroute reads/writes after PerSaveConfigManager or the GMCM
        // reset path swap the active config). Audit §4.12.
        var b = new GmcmBuilder(_api, _mod.ModManifest, _mod.Helper.Translation);

        // Main page -- category links. A few links use a tooltip key that doesn't follow
        // the `{linkKey}.tooltip` convention (crop-harvesting and machine-collection
        // inherit their section's tooltip; compatibility and specialized-grabbers use
        // the page key's tooltip), so those pass an explicit tooltipKey override.
        b.PageLink("crop-harvesting", "page.crop-harvesting.link", tooltipKey: "section.crop-harvesting.tooltip")
         .PageLink("other-harvesting", "page.other-harvesting.link")
         .PageLink("machine-collection", "page.machine-collection.link", tooltipKey: "section.machine-collection.tooltip")
         .PageLink("miscellaneous", "page.miscellaneous.link")
         .PageLink("compatibility", "page.compatibility.link", tooltipKey: "page.compatibility.tooltip")
         .PageLink("enabled-locations", "config.enabled-locations-link")
         .PageLink("specialized-grabbers", "page.specialized-grabbers.link", tooltipKey: "page.specialized-grabbers.tooltip");

        b.Dropdown("config.grabber-mode",
            () => _getConfig().grabberMode, v => _getConfig().grabberMode = v,
            ModConfig.GrabberModeDict, ModConfig.GrabberModeReverseDict, ModConfig.GrabberModeStrings);

        // Specialized Grabbers page
        b.Page("specialized-grabbers", "page.specialized-grabbers")
         .Paragraph("page.specialized-grabbers.paragraph")
         .Bool("config.specialized-grabbers-count-for-perfection",
             () => _getConfig().Specialized.specializedGrabbersCountForPerfection,
             v => _getConfig().Specialized.specializedGrabbersCountForPerfection = v)
         .Number("config.crops-shipped-threshold",
             () => _getConfig().Specialized.cropsShippedThreshold,
             v => _getConfig().Specialized.cropsShippedThreshold = v,
             min: 1, max: 10000, interval: 10)
         .Number("config.items-foraged-threshold",
             () => _getConfig().Specialized.itemsForagedThreshold,
             v => _getConfig().Specialized.itemsForagedThreshold = v,
             min: 1, max: 10000, interval: 10)
         .Number("config.stumps-chopped-threshold",
             () => _getConfig().Specialized.stumpsChoppedThreshold,
             v => _getConfig().Specialized.stumpsChoppedThreshold = v,
             min: 1, max: 1000, interval: 5)
         .Number("config.museum-donations-threshold",
             () => _getConfig().Specialized.museumDonationsThreshold,
             v => _getConfig().Specialized.museumDonationsThreshold = v,
             min: 1, max: 100, interval: 1)
         .Number("config.total-money-earned-threshold",
             () => _getConfig().Specialized.totalMoneyEarnedThreshold,
             v => _getConfig().Specialized.totalMoneyEarnedThreshold = v,
             min: 10000, max: 10000000, interval: 10000);

        // Recipe costs
        b.Section("section.recipe-costs.crop")
         .Number("config.recipe-crop-wood",
             () => _getConfig().Specialized.recipeCropWood, v => _getConfig().Specialized.recipeCropWood = v,
             min: 1, max: 9999, interval: 1)
         .Number("config.recipe-crop-gold-bar",
             () => _getConfig().Specialized.recipeCropGoldBar, v => _getConfig().Specialized.recipeCropGoldBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-crop-quality-sprinkler",
             () => _getConfig().Specialized.recipeCropQualitySprinkler, v => _getConfig().Specialized.recipeCropQualitySprinkler = v,
             min: 1, max: 999, interval: 1);

        b.Section("section.recipe-costs.forage")
         .Number("config.recipe-forage-wood",
             () => _getConfig().Specialized.recipeForageWood, v => _getConfig().Specialized.recipeForageWood = v,
             min: 1, max: 9999, interval: 1)
         .Number("config.recipe-forage-gold-bar",
             () => _getConfig().Specialized.recipeForageGoldBar, v => _getConfig().Specialized.recipeForageGoldBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-forage-mixed-seeds",
             () => _getConfig().Specialized.recipeForageMixedSeeds, v => _getConfig().Specialized.recipeForageMixedSeeds = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-forage-fiber",
             () => _getConfig().Specialized.recipeForageFiber, v => _getConfig().Specialized.recipeForageFiber = v,
             min: 1, max: 999, interval: 1);

        b.Section("section.recipe-costs.tree")
         .Number("config.recipe-tree-hardwood",
             () => _getConfig().Specialized.recipeTreeHardwood, v => _getConfig().Specialized.recipeTreeHardwood = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-tree-iridium-bar",
             () => _getConfig().Specialized.recipeTreeIridiumBar, v => _getConfig().Specialized.recipeTreeIridiumBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-tree-maple-syrup",
             () => _getConfig().Specialized.recipeTreeMapleSyrup, v => _getConfig().Specialized.recipeTreeMapleSyrup = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-tree-oak-resin",
             () => _getConfig().Specialized.recipeTreeOakResin, v => _getConfig().Specialized.recipeTreeOakResin = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-tree-pine-tar",
             () => _getConfig().Specialized.recipeTreePineTar, v => _getConfig().Specialized.recipeTreePineTar = v,
             min: 1, max: 999, interval: 1);

        b.Section("section.recipe-costs.scavenger")
         .Number("config.recipe-scavenger-hardwood",
             () => _getConfig().Specialized.recipeScavengerHardwood, v => _getConfig().Specialized.recipeScavengerHardwood = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-scavenger-iridium-bar",
             () => _getConfig().Specialized.recipeScavengerIridiumBar, v => _getConfig().Specialized.recipeScavengerIridiumBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-scavenger-bone-fragment",
             () => _getConfig().Specialized.recipeScavengerBoneFragment, v => _getConfig().Specialized.recipeScavengerBoneFragment = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-scavenger-artifact-trove",
             () => _getConfig().Specialized.recipeScavengerArtifactTrove, v => _getConfig().Specialized.recipeScavengerArtifactTrove = v,
             min: 1, max: 999, interval: 1);

        b.Section("section.recipe-costs.machine")
         .Number("config.recipe-machine-iridium-bar",
             () => _getConfig().Specialized.recipeMachineIridiumBar, v => _getConfig().Specialized.recipeMachineIridiumBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-machine-battery-pack",
             () => _getConfig().Specialized.recipeMachineBatteryPack, v => _getConfig().Specialized.recipeMachineBatteryPack = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-machine-diamond",
             () => _getConfig().Specialized.recipeMachineDiamond, v => _getConfig().Specialized.recipeMachineDiamond = v,
             min: 1, max: 999, interval: 1);

        // Crop Harvesting page
        b.Page("crop-harvesting", "section.crop-harvesting")
         .Bool("config.harvest-crops",
             () => _getConfig().Features.harvestCrops, v => _getConfig().Features.harvestCrops = v)
         .Bool("config.harvest-crops-indoor-pots",
             () => _getConfig().Features.harvestCropsIndoorPots, v => _getConfig().Features.harvestCropsIndoorPots = v)
         .Dropdown("config.harvest-flowers",
             () => _getConfig().Features.flowers, v => _getConfig().Features.flowers = v,
             ModConfig.FlowerHarvestDict, ModConfig.FlowerHarvestReverseDict, ModConfig.FlowerHarvestStrings,
             dropdownLabelPrefix: "flower-")
         .Number("config.bee-house-range",
             () => _getConfig().Features.beeHouseRange, v => _getConfig().Features.beeHouseRange = Math.Max(1, v))
         .Number("config.harvest-range",
             () => _getConfig().Features.harvestCropsRange, v => _getConfig().Features.harvestCropsRange = Math.Max(-1, v))
         .Dropdown("config.harvest-range-mode",
             () => _getConfig().Features.harvestCropsRangeMode, v => _getConfig().Features.harvestCropsRangeMode = v,
             ModConfig.HarvestCropsRangeDict, ModConfig.HarvestCropsRangeReverseDict, ModConfig.HarvestCropsRangeModeStrings,
             dropdownLabelPrefix: "")
         .Bool("config.replant-reminder",
             () => _getConfig().replantReminder, v => _getConfig().replantReminder = v)
         .Number("config.replant-reminder-time",
             () => _getConfig().replantReminderTime, v => _getConfig().replantReminderTime = v,
             min: 600, max: 2500, interval: 100);

        // Other Harvesting page
        b.Page("other-harvesting", "section.other-harvesting")
         .Bool("config.collect-forage", () => _getConfig().Features.forage, v => _getConfig().Features.forage = v)
         .Bool("config.harvest-fruit-trees", () => _getConfig().Features.fruitTrees, v => _getConfig().Features.fruitTrees = v)
         .Bool("config.harvest-berry-bushes", () => _getConfig().Features.bushes, v => _getConfig().Features.bushes = v)
         .Bool("config.shake-seed-trees", () => _getConfig().Features.seedTrees, v => _getConfig().Features.seedTrees = v)
         .Bool("config.collect-animal-products", () => _getConfig().Features.animalProducts, v => _getConfig().Features.animalProducts = v)
         .Bool("config.grab-slime-balls", () => _getConfig().Features.slimeHutch, v => _getConfig().Features.slimeHutch = v)
         .Bool("config.dig-up-artifact-spots", () => _getConfig().Features.artifactSpots, v => _getConfig().Features.artifactSpots = v)
         .Bool("config.collect-ore-from-panning", () => _getConfig().Features.orePan, v => _getConfig().Features.orePan = v)
         .Bool("config.fell-stumps", () => _getConfig().Features.fellHardwoodStumps, v => _getConfig().Features.fellHardwoodStumps = v)
         .Bool("config.search-garbage-cans", () => _getConfig().Features.garbageCans, v => _getConfig().Features.garbageCans = v)
         .Bool("config.dig-up-seed-spots", () => _getConfig().Features.seedSpots, v => _getConfig().Features.seedSpots = v)
         .Bool("config.harvest-moss", () => _getConfig().Features.harvestMoss, v => _getConfig().Features.harvestMoss = v)
         .Bool("config.harvest-green-rain-weeds", () => _getConfig().Features.harvestGreenRainWeeds, v => _getConfig().Features.harvestGreenRainWeeds = v)
         .Bool("config.collect-debris", () => _getConfig().Features.collectDebris, v => _getConfig().Features.collectDebris = v);

        // Machine Collection page
        b.Page("machine-collection", "section.machine-collection")
         .Paragraph("page.machine-collection.paragraph")
         .Bool("config.disable-machine-collection",
             () => _getConfig().Machines.disableMachineCollection, v => _getConfig().Machines.disableMachineCollection = v);

        b.Section("section.fishing-machines")
         .Bool("config.collect-crab-pots", () => _getConfig().Machines.collectCrabPots, v => _getConfig().Machines.collectCrabPots = v)
         .Bool("config.collect-fish-ponds", () => _getConfig().Machines.collectFishPonds, v => _getConfig().Machines.collectFishPonds = v);

        b.Section("section.outdoor-producers")
         .Bool("config.collect-bee-houses", () => _getConfig().Machines.collectBeeHouses, v => _getConfig().Machines.collectBeeHouses = v)
         .Bool("config.collect-tappers", () => _getConfig().Machines.collectTappers, v => _getConfig().Machines.collectTappers = v)
         .Bool("config.collect-leaf-baskets", () => _getConfig().Machines.collectLeafBaskets, v => _getConfig().Machines.collectLeafBaskets = v);

        b.Section("section.farm-cave")
         .Bool("config.collect-mushroom-boxes", () => _getConfig().Features.farmCaveMushrooms, v => _getConfig().Features.farmCaveMushrooms = v);

        b.Section("section.artisan-machines")
         .Bool("config.collect-kegs", () => _getConfig().Machines.collectKegs, v => _getConfig().Machines.collectKegs = v)
         .Bool("config.collect-preserves-jars", () => _getConfig().Machines.collectPreservesJars, v => _getConfig().Machines.collectPreservesJars = v)
         .Bool("config.collect-cheese-presses", () => _getConfig().Machines.collectCheesePresses, v => _getConfig().Machines.collectCheesePresses = v)
         .Bool("config.collect-mayonnaise-machines", () => _getConfig().Machines.collectMayonnaiseMachines, v => _getConfig().Machines.collectMayonnaiseMachines = v)
         .Bool("config.collect-looms", () => _getConfig().Machines.collectLooms, v => _getConfig().Machines.collectLooms = v)
         .Bool("config.collect-oil-makers", () => _getConfig().Machines.collectOilMakers, v => _getConfig().Machines.collectOilMakers = v);

        b.Section("section.processing-machines")
         .Bool("config.collect-furnaces", () => _getConfig().Machines.collectFurnaces, v => _getConfig().Machines.collectFurnaces = v)
         .Bool("config.collect-charcoal-kilns", () => _getConfig().Machines.collectCharcoalKilns, v => _getConfig().Machines.collectCharcoalKilns = v)
         .Bool("config.collect-recycling-machines", () => _getConfig().Machines.collectRecyclingMachines, v => _getConfig().Machines.collectRecyclingMachines = v)
         .Bool("config.collect-seed-makers", () => _getConfig().Machines.collectSeedMakers, v => _getConfig().Machines.collectSeedMakers = v)
         .Bool("config.collect-bone-mills", () => _getConfig().Machines.collectBoneMills, v => _getConfig().Machines.collectBoneMills = v)
         .Bool("config.collect-geode-crushers", () => _getConfig().Machines.collectGeodeCrushers, v => _getConfig().Machines.collectGeodeCrushers = v)
         .Bool("config.collect-wood-chippers", () => _getConfig().Machines.collectWoodChippers, v => _getConfig().Machines.collectWoodChippers = v)
         .Bool("config.collect-deconstructors", () => _getConfig().Machines.collectDeconstructors, v => _getConfig().Machines.collectDeconstructors = v);

        b.Section("section.16-machines")
         .Bool("config.collect-mushroom-logs", () => _getConfig().Machines.collectMushroomLogs, v => _getConfig().Machines.collectMushroomLogs = v)
         .Bool("config.collect-fish-smokers", () => _getConfig().Machines.collectFishSmokers, v => _getConfig().Machines.collectFishSmokers = v)
         .Bool("config.collect-bait-makers", () => _getConfig().Machines.collectBaitMakers, v => _getConfig().Machines.collectBaitMakers = v)
         .Bool("config.collect-dehydrators", () => _getConfig().Machines.collectDehydrators, v => _getConfig().Machines.collectDehydrators = v);

        b.Section("section.passive-producers")
         .Bool("config.collect-crystalariums", () => _getConfig().Machines.collectCrystalariums, v => _getConfig().Machines.collectCrystalariums = v)
         .Bool("config.collect-lightning-rods", () => _getConfig().Machines.collectLightningRods, v => _getConfig().Machines.collectLightningRods = v)
         .Bool("config.collect-worm-bins", () => _getConfig().Machines.collectWormBins, v => _getConfig().Machines.collectWormBins = v)
         .Bool("config.collect-solar-panels", () => _getConfig().Machines.collectSolarPanels, v => _getConfig().Machines.collectSolarPanels = v)
         .Bool("config.collect-slime-egg-presses", () => _getConfig().Machines.collectSlimeEggPresses, v => _getConfig().Machines.collectSlimeEggPresses = v)
         .Bool("config.collect-coffee-makers", () => _getConfig().Machines.collectCoffeeMakers, v => _getConfig().Machines.collectCoffeeMakers = v)
         .Bool("config.collect-soda-machines", () => _getConfig().Machines.collectSodaMachines, v => _getConfig().Machines.collectSodaMachines = v);

        b.Section("section.statues")
         .Bool("config.collect-statues", () => _getConfig().Machines.collectStatues, v => _getConfig().Machines.collectStatues = v);

        b.Section("section.other-machines")
         .Bool("config.collect-other-machines", () => _getConfig().Machines.collectOtherMachines, v => _getConfig().Machines.collectOtherMachines = v);

        // Miscellaneous page
        b.Page("miscellaneous", "section.miscellaneous")
         .Bool("config.report-yield", () => _getConfig().reportYield, v => _getConfig().reportYield = v)
         .Bool("config.debug-logging", () => _getConfig().debugLogging, v => _getConfig().debugLogging = v)
         .Bool("config.gain-experience", () => _getConfig().gainExperience, v => _getConfig().gainExperience = v)
         .Dropdown("config.grab-frequency",
             () => _getConfig().grabFrequency,
             v =>
             {
                 // Clear the Instant-mode queues when frequency changes so locations
                 // queued under the old mode don't sit in memory (audit §1.2, §3.8).
                 if (_getConfig().grabFrequency != v)
                     _mod.ClearLocationQueues();
                 _getConfig().grabFrequency = v;
             },
             ModConfig.GrabFrequencyDict, ModConfig.GrabFrequencyReverseDict, ModConfig.GrabFrequencyStrings,
             dropdownLabelPrefix: "")
         .Bool("config.exclude-quest-items",
             () => _getConfig().Compatibility.excludeQuestItems, v => _getConfig().Compatibility.excludeQuestItems = v)
         .Bool("config.skip-festival-locations",
             () => _getConfig().Locations.skipFestivalLocations, v => _getConfig().Locations.skipFestivalLocations = v)
         .TextOption("config.excluded-items",
             () => string.Join(", ", _getConfig().Compatibility.excludedItems ?? new HashSet<string>()),
             v => _getConfig().Compatibility.excludedItems = new HashSet<string>(
                 v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
         .Dropdown("config.global-grabber-mode",
             () => _getConfig().globalGrabber, v => _getConfig().globalGrabber = v,
             ModConfig.GlobalGrabberDict, ModConfig.GlobalGrabberReverseDict, ModConfig.GlobalGrabberModeStrings,
             dropdownLabelPrefix: "")
         .Bool("config.auto-fire-global-grabber",
             () => _getConfig().GlobalGrab.globalAutoFire, v => _getConfig().GlobalGrab.globalAutoFire = v)
         .Keybind("config.fire-global-grabber",
             () => _getConfig().GlobalGrab.globalFireButton, v => _getConfig().GlobalGrab.globalFireButton = v)
         .Keybind("config.designate-global-grabber",
             () => _getConfig().GlobalGrab.designateGrabberButton, v => _getConfig().GlobalGrab.designateGrabberButton = v)
         .Number("config.global-button-x-offset",
             () => _getConfig().GlobalGrab.globalButtonOffsetX, v => _getConfig().GlobalGrab.globalButtonOffsetX = Math.Clamp(v, -500, 500))
         .Number("config.global-button-y-offset",
             () => _getConfig().GlobalGrab.globalButtonOffsetY, v => _getConfig().GlobalGrab.globalButtonOffsetY = Math.Clamp(v, -500, 500))
         .Number("config.rename-button-x-offset",
             () => _getConfig().GlobalGrab.renameButtonOffsetX, v => _getConfig().GlobalGrab.renameButtonOffsetX = Math.Clamp(v, -500, 500))
         .Number("config.rename-button-y-offset",
             () => _getConfig().GlobalGrab.renameButtonOffsetY, v => _getConfig().GlobalGrab.renameButtonOffsetY = Math.Clamp(v, -500, 500));

        // Compatibility page
        b.Page("compatibility", "page.compatibility")
         .Paragraph("page.compatibility.paragraph")
         .Bool("config.sunberry-village-exclusions",
             () => _getConfig().Compatibility.sunberryVillageExclusions, v => _getConfig().Compatibility.sunberryVillageExclusions = v)
         .Bool("config.visit-mt-vapius-exclusions",
             () => _getConfig().Compatibility.visitMtVapiusExclusions, v => _getConfig().Compatibility.visitMtVapiusExclusions = v)
         .Bool("config.baubles-exclusions",
             () => _getConfig().Compatibility.baublesExclusions, v => _getConfig().Compatibility.baublesExclusions = v)
         .Bool("config.resource-chickens-exclusions",
             () => _getConfig().Compatibility.resourceChickensExclusions, v => _getConfig().Compatibility.resourceChickensExclusions = v)
         .Bool("config.cape-stardew-exclusions",
             () => _getConfig().Compatibility.capeStardewExclusions, v => _getConfig().Compatibility.capeStardewExclusions = v)
         .Bool("config.collect-wildflowers",
             () => _getConfig().Features.collectWildflowers, v => _getConfig().Features.collectWildflowers = v)
         .Bool("config.collect-buried-items",
             () => _getConfig().Features.buriedItems, v => _getConfig().Features.buriedItems = v)
         .Bool("config.automate-compatibility",
             () => _getConfig().Machines.automateCompatibility, v => _getConfig().Machines.automateCompatibility = v);

        b.Section("section.modded-skill-xp")
         .Bool("config.binning-skill-stat-tracking",
             () => _getConfig().Compatibility.binningSkillStatTracking, v => _getConfig().Compatibility.binningSkillStatTracking = v)
         .Bool("config.archaeology-skill-integration",
             () => _getConfig().Compatibility.archaeologySkillIntegration, v => _getConfig().Compatibility.archaeologySkillIntegration = v)
         .Number("config.archaeology-xp-from-artifact-spot",
             () => _getConfig().Compatibility.archaeologyXpFromArtifactSpot,
             v => _getConfig().Compatibility.archaeologyXpFromArtifactSpot = Math.Clamp(v, 0, 100),
             min: 0, max: 100, interval: 1)
         .Number("config.archaeology-xp-from-ore-pan",
             () => _getConfig().Compatibility.archaeologyXpFromOrePan,
             v => _getConfig().Compatibility.archaeologyXpFromOrePan = Math.Clamp(v, 0, 100),
             min: 0, max: 100, interval: 1);

        // Enabled Locations page
        b.Page("enabled-locations", "config.enabled-locations-page");

        if (_locations.DiscoveredLocations != null && _locations.DiscoveredLocations.Count > 0)
        {
            // setValue lambdas are intentionally empty: GMCM commits every option's
            // setValue on Save with its cached display value, including stale ones.
            // Mutation lives in OnFieldChanged below, which only fires on user-initiated
            // toggle, so a Save commit can never re-apply a stale buffered toggle state
            // (which is how EnableAll's clear got silently undone before this fix).
            b.Paragraph("config.enabled-locations-paragraph")
             .Bool("config.enable-all",
                 () => _locations.DiscoveredLocations.All(loc => _getConfig().Locations.SkippedLocations?.Contains(loc.Name) != true),
                 v => { },
                 fieldId: "enable-all")
             .Bool("config.select-visited-only",
                 () => _getConfig().Locations.selectVisitedOnly,
                 v => { },
                 fieldId: "select-visited-only")
             .OnFieldChanged((fieldId, value) =>
             {
                 if (fieldId == "enable-all")
                     _pendingBatchAction = (bool)value
                         ? LocationBatchAction.EnableAll
                         : LocationBatchAction.DisableAll;
                 else if (fieldId == "select-visited-only")
                 {
                     if ((bool)value)
                         _pendingBatchAction = LocationBatchAction.SelectVisitedOnly;
                     else
                         _getConfig().Locations.selectVisitedOnly = false;
                 }
                 else if (fieldId != null && fieldId.StartsWith("loc:"))
                 {
                     // Per-location toggle. Mutation lives here (not in setValue) so
                     // GMCM-Save commit's stale-value pass can't undo batch actions.
                     HandleLocationToggleChanged(fieldId.Substring(4), (bool)value);
                 }
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
                    b.Section("section.visited-locations");
                    foreach (var (locName, displayName) in visitedLocs)
                        AddLocationToggle(b, locName, displayName);
                }

                if (unvisitedLocs.Count > 0)
                {
                    b.Section("section.not-yet-visited");
                    foreach (var (locName, displayName) in unvisitedLocs)
                        AddLocationToggle(b, locName, displayName);
                }
            }
            else
            {
                foreach (var (locName, displayName) in _locations.DiscoveredLocations)
                    AddLocationToggle(b, locName, displayName);
            }
        }
        else
        {
            b.Paragraph("config.no-save-loaded");
        }
    }

    // Per-location toggle uses the location's own display name (not an i18n key)
    // and a fieldId of "loc:<name>" so OnFieldChanged can route the user-initiated
    // mutation. setValue is intentionally empty (no-op): GMCM commits ALL options'
    // setValues on Save with their cached display values, including stale ones --
    // if we mutated SkippedLocations here, every Save would re-add unvisited maps
    // that were just cleared by an EnableAll batch action. OnFieldChanged only
    // fires on user-initiated change, so it's the safe place to mutate.
    private void AddLocationToggle(GmcmBuilder b, string locName, string displayName)
    {
        string capturedName = locName;
        string capturedDisplay = displayName;

        b.Api.AddBoolOption(b.Manifest,
            getValue: () => _getConfig().Locations.SkippedLocations?.Contains(capturedName) != true,
            setValue: v => { },
            name: () => capturedDisplay,
            tooltip: () => capturedName != capturedDisplay ? capturedName : null,
            fieldId: $"loc:{capturedName}");
    }

    // Routed from OnFieldChanged when a per-location toggle is user-initiated.
    // Mirrors the original per-location setValue logic but only fires on real
    // toggles, never on the GMCM-Save commit pass that previously caused stale
    // cached values to re-populate SkippedLocations after EnableAll.
    private void HandleLocationToggleChanged(string locName, bool enabled)
    {
        _getConfig().Locations.SkippedLocations ??= new HashSet<string>();
        bool currentlyEnabled = !_getConfig().Locations.SkippedLocations.Contains(locName);
        if (enabled == currentlyEnabled)
            return;

        if (!enabled)
        {
            _getConfig().Locations.SkippedLocations.Add(locName);
            if (_locations.SaveData != null)
            {
                _locations.SaveData.BlacklistedLocations.Add(locName);
                _locations.SaveData.AutoSkippedLocations.Remove(locName);
                _locations.WriteSaveData();
            }
        }
        else
        {
            _getConfig().Locations.SkippedLocations.Remove(locName);
            if (_locations.SaveData != null)
            {
                // Pin this location as user-managed so ApplyVisitAutoSkip won't
                // re-skip it on next save load / GMCM save while SVO is on.
                _locations.SaveData.ManuallyManagedLocations.Add(locName);
                _locations.SaveData.BlacklistedLocations.Remove(locName);
                _locations.SaveData.AutoSkippedLocations.Remove(locName);
                _locations.WriteSaveData();
            }
        }
    }
}
