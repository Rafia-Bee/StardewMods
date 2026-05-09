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
        _lastRebuildSnapshot = ComputeOptionSetSnapshot();
        _lastInvalidatedGrabberMode = _mod.Config.grabberMode;
        // Config-swap paths (per-save load, return-to-title, GMCM reset) invalidate
        // the recipe asset via the ActiveConfigChanged subscriber wired in ModEntry.
        // Mirror that here so the GMCM Save callback's compare stays current.
        _mod.ActiveConfigChanged += () => _lastInvalidatedGrabberMode = _mod.Config.grabberMode;
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

        _mod.Config.Locations.SkippedLocations ??= new HashSet<string>();

        switch (action)
        {
            case LocationBatchAction.EnableAll:
                // "Enable All" and "Select Visited Only" are contradictory intents:
                // SVO auto-skips unvisited maps, EnableAll wants every map enabled.
                // If SVO stays on, the next ApplyVisitAutoSkip (e.g. on the next GMCM
                // Save while SVO is on, or on save load) re-skips unvisited maps and
                // silently undoes EnableAll. Force SVO off so the user-visible result
                // matches the user-visible intent.
                _mod.Config.Locations.selectVisitedOnly = false;
                _mod.Config.Locations.SkippedLocations.Clear();
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
                        _mod.Config.Locations.SkippedLocations.Add(loc.Name);
                break;

            case LocationBatchAction.SelectVisitedOnly:
                _mod.Config.Locations.selectVisitedOnly = true;
                if (_locations.DiscoveredLocations != null)
                {
                    foreach (var (locName, _) in _locations.DiscoveredLocations)
                    {
                        bool visited = Game1.MasterPlayer.locationsVisited.Contains(locName);
                        if (visited)
                            _mod.Config.Locations.SkippedLocations.Remove(locName);
                        else
                            _mod.Config.Locations.SkippedLocations.Add(locName);
                    }

                    if (_locations.SaveData != null)
                    {
                        _locations.SaveData.AutoSkippedLocations.Clear();
                        foreach (var name in _mod.Config.Locations.SkippedLocations)
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
                if (_mod.Config.grabberMode != _lastInvalidatedGrabberMode)
                {
                    _mod.Helper.GameContent.InvalidateCache("Data/CraftingRecipes");
                    _lastInvalidatedGrabberMode = _mod.Config.grabberMode;
                }
                _mod.Monitor.Log($"GMCM saved. selectVisitedOnly={_mod.Config.Locations.selectVisitedOnly}, IsWorldReady={Context.IsWorldReady}, saveData={((_locations.SaveData != null) ? "loaded" : "null")}", LogLevel.Info);
                if (_mod.Config.Locations.selectVisitedOnly && Context.IsWorldReady && _locations.SaveData != null)
                {
                    _locations.DiscoverLocations();
                    _locations.ApplyVisitAutoSkip();
                }
                _mod.RefreshRenderedWorldHook();
            });

        // Lambdas below MUST read `_mod.Config` directly each invocation -- never cache
        // it into a local. Reset-to-defaults swaps the `_mod.Config` reference for a
        // fresh ModConfig, and PerSaveConfigManager swaps it on save load / return to
        // title; a captured local would leave the lambdas reading/writing the orphaned
        // old instance (audit §4.12). Same reason the original pre-refactor code didn't
        // cache it either.
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
            () => _mod.Config.grabberMode, v => _mod.Config.grabberMode = v,
            ModConfig.GrabberModeDict, ModConfig.GrabberModeReverseDict, ModConfig.GrabberModeStrings);

        // Specialized Grabbers page
        b.Page("specialized-grabbers", "page.specialized-grabbers")
         .Paragraph("page.specialized-grabbers.paragraph")
         .Bool("config.specialized-grabbers-count-for-perfection",
             () => _mod.Config.Specialized.specializedGrabbersCountForPerfection,
             v => _mod.Config.Specialized.specializedGrabbersCountForPerfection = v)
         .Number("config.crops-shipped-threshold",
             () => _mod.Config.Specialized.cropsShippedThreshold,
             v => _mod.Config.Specialized.cropsShippedThreshold = v,
             min: 1, max: 10000, interval: 10)
         .Number("config.items-foraged-threshold",
             () => _mod.Config.Specialized.itemsForagedThreshold,
             v => _mod.Config.Specialized.itemsForagedThreshold = v,
             min: 1, max: 10000, interval: 10)
         .Number("config.stumps-chopped-threshold",
             () => _mod.Config.Specialized.stumpsChoppedThreshold,
             v => _mod.Config.Specialized.stumpsChoppedThreshold = v,
             min: 1, max: 1000, interval: 5)
         .Number("config.museum-donations-threshold",
             () => _mod.Config.Specialized.museumDonationsThreshold,
             v => _mod.Config.Specialized.museumDonationsThreshold = v,
             min: 1, max: 100, interval: 1)
         .Number("config.total-money-earned-threshold",
             () => _mod.Config.Specialized.totalMoneyEarnedThreshold,
             v => _mod.Config.Specialized.totalMoneyEarnedThreshold = v,
             min: 10000, max: 10000000, interval: 10000);

        // Recipe costs
        b.Section("section.recipe-costs.crop")
         .Number("config.recipe-crop-wood",
             () => _mod.Config.Specialized.recipeCropWood, v => _mod.Config.Specialized.recipeCropWood = v,
             min: 1, max: 9999, interval: 1)
         .Number("config.recipe-crop-gold-bar",
             () => _mod.Config.Specialized.recipeCropGoldBar, v => _mod.Config.Specialized.recipeCropGoldBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-crop-quality-sprinkler",
             () => _mod.Config.Specialized.recipeCropQualitySprinkler, v => _mod.Config.Specialized.recipeCropQualitySprinkler = v,
             min: 1, max: 999, interval: 1);

        b.Section("section.recipe-costs.forage")
         .Number("config.recipe-forage-wood",
             () => _mod.Config.Specialized.recipeForageWood, v => _mod.Config.Specialized.recipeForageWood = v,
             min: 1, max: 9999, interval: 1)
         .Number("config.recipe-forage-gold-bar",
             () => _mod.Config.Specialized.recipeForageGoldBar, v => _mod.Config.Specialized.recipeForageGoldBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-forage-mixed-seeds",
             () => _mod.Config.Specialized.recipeForageMixedSeeds, v => _mod.Config.Specialized.recipeForageMixedSeeds = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-forage-fiber",
             () => _mod.Config.Specialized.recipeForageFiber, v => _mod.Config.Specialized.recipeForageFiber = v,
             min: 1, max: 999, interval: 1);

        b.Section("section.recipe-costs.tree")
         .Number("config.recipe-tree-hardwood",
             () => _mod.Config.Specialized.recipeTreeHardwood, v => _mod.Config.Specialized.recipeTreeHardwood = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-tree-iridium-bar",
             () => _mod.Config.Specialized.recipeTreeIridiumBar, v => _mod.Config.Specialized.recipeTreeIridiumBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-tree-maple-syrup",
             () => _mod.Config.Specialized.recipeTreeMapleSyrup, v => _mod.Config.Specialized.recipeTreeMapleSyrup = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-tree-oak-resin",
             () => _mod.Config.Specialized.recipeTreeOakResin, v => _mod.Config.Specialized.recipeTreeOakResin = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-tree-pine-tar",
             () => _mod.Config.Specialized.recipeTreePineTar, v => _mod.Config.Specialized.recipeTreePineTar = v,
             min: 1, max: 999, interval: 1);

        b.Section("section.recipe-costs.scavenger")
         .Number("config.recipe-scavenger-hardwood",
             () => _mod.Config.Specialized.recipeScavengerHardwood, v => _mod.Config.Specialized.recipeScavengerHardwood = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-scavenger-iridium-bar",
             () => _mod.Config.Specialized.recipeScavengerIridiumBar, v => _mod.Config.Specialized.recipeScavengerIridiumBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-scavenger-bone-fragment",
             () => _mod.Config.Specialized.recipeScavengerBoneFragment, v => _mod.Config.Specialized.recipeScavengerBoneFragment = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-scavenger-artifact-trove",
             () => _mod.Config.Specialized.recipeScavengerArtifactTrove, v => _mod.Config.Specialized.recipeScavengerArtifactTrove = v,
             min: 1, max: 999, interval: 1);

        b.Section("section.recipe-costs.machine")
         .Number("config.recipe-machine-iridium-bar",
             () => _mod.Config.Specialized.recipeMachineIridiumBar, v => _mod.Config.Specialized.recipeMachineIridiumBar = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-machine-battery-pack",
             () => _mod.Config.Specialized.recipeMachineBatteryPack, v => _mod.Config.Specialized.recipeMachineBatteryPack = v,
             min: 1, max: 999, interval: 1)
         .Number("config.recipe-machine-diamond",
             () => _mod.Config.Specialized.recipeMachineDiamond, v => _mod.Config.Specialized.recipeMachineDiamond = v,
             min: 1, max: 999, interval: 1);

        // Crop Harvesting page
        b.Page("crop-harvesting", "section.crop-harvesting")
         .Bool("config.harvest-crops",
             () => _mod.Config.Features.harvestCrops, v => _mod.Config.Features.harvestCrops = v)
         .Bool("config.harvest-crops-indoor-pots",
             () => _mod.Config.Features.harvestCropsIndoorPots, v => _mod.Config.Features.harvestCropsIndoorPots = v)
         .Dropdown("config.harvest-flowers",
             () => _mod.Config.Features.flowers, v => _mod.Config.Features.flowers = v,
             ModConfig.FlowerHarvestDict, ModConfig.FlowerHarvestReverseDict, ModConfig.FlowerHarvestStrings,
             dropdownLabelPrefix: "flower-")
         .Number("config.bee-house-range",
             () => _mod.Config.Features.beeHouseRange, v => _mod.Config.Features.beeHouseRange = Math.Max(1, v))
         .Number("config.harvest-range",
             () => _mod.Config.Features.harvestCropsRange, v => _mod.Config.Features.harvestCropsRange = Math.Max(-1, v))
         .Dropdown("config.harvest-range-mode",
             () => _mod.Config.Features.harvestCropsRangeMode, v => _mod.Config.Features.harvestCropsRangeMode = v,
             ModConfig.HarvestCropsRangeDict, ModConfig.HarvestCropsRangeReverseDict, ModConfig.HarvestCropsRangeModeStrings,
             dropdownLabelPrefix: "")
         .Bool("config.replant-reminder",
             () => _mod.Config.replantReminder, v => _mod.Config.replantReminder = v)
         .Number("config.replant-reminder-time",
             () => _mod.Config.replantReminderTime, v => _mod.Config.replantReminderTime = v,
             min: 600, max: 2500, interval: 100);

        // Other Harvesting page
        b.Page("other-harvesting", "section.other-harvesting")
         .Bool("config.collect-forage", () => _mod.Config.Features.forage, v => _mod.Config.Features.forage = v)
         .Bool("config.harvest-fruit-trees", () => _mod.Config.Features.fruitTrees, v => _mod.Config.Features.fruitTrees = v)
         .Bool("config.harvest-berry-bushes", () => _mod.Config.Features.bushes, v => _mod.Config.Features.bushes = v)
         .Bool("config.shake-seed-trees", () => _mod.Config.Features.seedTrees, v => _mod.Config.Features.seedTrees = v)
         .Bool("config.collect-animal-products", () => _mod.Config.Features.animalProducts, v => _mod.Config.Features.animalProducts = v)
         .Bool("config.grab-slime-balls", () => _mod.Config.Features.slimeHutch, v => _mod.Config.Features.slimeHutch = v)
         .Bool("config.dig-up-artifact-spots", () => _mod.Config.Features.artifactSpots, v => _mod.Config.Features.artifactSpots = v)
         .Bool("config.collect-ore-from-panning", () => _mod.Config.Features.orePan, v => _mod.Config.Features.orePan = v)
         .Bool("config.fell-stumps", () => _mod.Config.Features.fellHardwoodStumps, v => _mod.Config.Features.fellHardwoodStumps = v)
         .Bool("config.search-garbage-cans", () => _mod.Config.Features.garbageCans, v => _mod.Config.Features.garbageCans = v)
         .Bool("config.dig-up-seed-spots", () => _mod.Config.Features.seedSpots, v => _mod.Config.Features.seedSpots = v)
         .Bool("config.harvest-moss", () => _mod.Config.Features.harvestMoss, v => _mod.Config.Features.harvestMoss = v)
         .Bool("config.harvest-green-rain-weeds", () => _mod.Config.Features.harvestGreenRainWeeds, v => _mod.Config.Features.harvestGreenRainWeeds = v)
         .Bool("config.collect-debris", () => _mod.Config.Features.collectDebris, v => _mod.Config.Features.collectDebris = v);

        // Machine Collection page
        b.Page("machine-collection", "section.machine-collection")
         .Paragraph("page.machine-collection.paragraph")
         .Bool("config.disable-machine-collection",
             () => _mod.Config.Machines.disableMachineCollection, v => _mod.Config.Machines.disableMachineCollection = v);

        b.Section("section.fishing-machines")
         .Bool("config.collect-crab-pots", () => _mod.Config.Machines.collectCrabPots, v => _mod.Config.Machines.collectCrabPots = v)
         .Bool("config.collect-fish-ponds", () => _mod.Config.Machines.collectFishPonds, v => _mod.Config.Machines.collectFishPonds = v);

        b.Section("section.outdoor-producers")
         .Bool("config.collect-bee-houses", () => _mod.Config.Machines.collectBeeHouses, v => _mod.Config.Machines.collectBeeHouses = v)
         .Bool("config.collect-tappers", () => _mod.Config.Machines.collectTappers, v => _mod.Config.Machines.collectTappers = v)
         .Bool("config.collect-leaf-baskets", () => _mod.Config.Machines.collectLeafBaskets, v => _mod.Config.Machines.collectLeafBaskets = v);

        b.Section("section.farm-cave")
         .Bool("config.collect-mushroom-boxes", () => _mod.Config.Features.farmCaveMushrooms, v => _mod.Config.Features.farmCaveMushrooms = v);

        b.Section("section.artisan-machines")
         .Bool("config.collect-kegs", () => _mod.Config.Machines.collectKegs, v => _mod.Config.Machines.collectKegs = v)
         .Bool("config.collect-preserves-jars", () => _mod.Config.Machines.collectPreservesJars, v => _mod.Config.Machines.collectPreservesJars = v)
         .Bool("config.collect-cheese-presses", () => _mod.Config.Machines.collectCheesePresses, v => _mod.Config.Machines.collectCheesePresses = v)
         .Bool("config.collect-mayonnaise-machines", () => _mod.Config.Machines.collectMayonnaiseMachines, v => _mod.Config.Machines.collectMayonnaiseMachines = v)
         .Bool("config.collect-looms", () => _mod.Config.Machines.collectLooms, v => _mod.Config.Machines.collectLooms = v)
         .Bool("config.collect-oil-makers", () => _mod.Config.Machines.collectOilMakers, v => _mod.Config.Machines.collectOilMakers = v);

        b.Section("section.processing-machines")
         .Bool("config.collect-furnaces", () => _mod.Config.Machines.collectFurnaces, v => _mod.Config.Machines.collectFurnaces = v)
         .Bool("config.collect-charcoal-kilns", () => _mod.Config.Machines.collectCharcoalKilns, v => _mod.Config.Machines.collectCharcoalKilns = v)
         .Bool("config.collect-recycling-machines", () => _mod.Config.Machines.collectRecyclingMachines, v => _mod.Config.Machines.collectRecyclingMachines = v)
         .Bool("config.collect-seed-makers", () => _mod.Config.Machines.collectSeedMakers, v => _mod.Config.Machines.collectSeedMakers = v)
         .Bool("config.collect-bone-mills", () => _mod.Config.Machines.collectBoneMills, v => _mod.Config.Machines.collectBoneMills = v)
         .Bool("config.collect-geode-crushers", () => _mod.Config.Machines.collectGeodeCrushers, v => _mod.Config.Machines.collectGeodeCrushers = v)
         .Bool("config.collect-wood-chippers", () => _mod.Config.Machines.collectWoodChippers, v => _mod.Config.Machines.collectWoodChippers = v)
         .Bool("config.collect-deconstructors", () => _mod.Config.Machines.collectDeconstructors, v => _mod.Config.Machines.collectDeconstructors = v);

        b.Section("section.16-machines")
         .Bool("config.collect-mushroom-logs", () => _mod.Config.Machines.collectMushroomLogs, v => _mod.Config.Machines.collectMushroomLogs = v)
         .Bool("config.collect-fish-smokers", () => _mod.Config.Machines.collectFishSmokers, v => _mod.Config.Machines.collectFishSmokers = v)
         .Bool("config.collect-bait-makers", () => _mod.Config.Machines.collectBaitMakers, v => _mod.Config.Machines.collectBaitMakers = v)
         .Bool("config.collect-dehydrators", () => _mod.Config.Machines.collectDehydrators, v => _mod.Config.Machines.collectDehydrators = v);

        b.Section("section.passive-producers")
         .Bool("config.collect-crystalariums", () => _mod.Config.Machines.collectCrystalariums, v => _mod.Config.Machines.collectCrystalariums = v)
         .Bool("config.collect-lightning-rods", () => _mod.Config.Machines.collectLightningRods, v => _mod.Config.Machines.collectLightningRods = v)
         .Bool("config.collect-worm-bins", () => _mod.Config.Machines.collectWormBins, v => _mod.Config.Machines.collectWormBins = v)
         .Bool("config.collect-solar-panels", () => _mod.Config.Machines.collectSolarPanels, v => _mod.Config.Machines.collectSolarPanels = v)
         .Bool("config.collect-slime-egg-presses", () => _mod.Config.Machines.collectSlimeEggPresses, v => _mod.Config.Machines.collectSlimeEggPresses = v)
         .Bool("config.collect-coffee-makers", () => _mod.Config.Machines.collectCoffeeMakers, v => _mod.Config.Machines.collectCoffeeMakers = v)
         .Bool("config.collect-soda-machines", () => _mod.Config.Machines.collectSodaMachines, v => _mod.Config.Machines.collectSodaMachines = v);

        b.Section("section.statues")
         .Bool("config.collect-statues", () => _mod.Config.Machines.collectStatues, v => _mod.Config.Machines.collectStatues = v);

        b.Section("section.other-machines")
         .Bool("config.collect-other-machines", () => _mod.Config.Machines.collectOtherMachines, v => _mod.Config.Machines.collectOtherMachines = v);

        // Miscellaneous page
        b.Page("miscellaneous", "section.miscellaneous")
         .Bool("config.report-yield", () => _mod.Config.reportYield, v => _mod.Config.reportYield = v)
         .Bool("config.debug-logging", () => _mod.Config.debugLogging, v => _mod.Config.debugLogging = v)
         .Bool("config.gain-experience", () => _mod.Config.gainExperience, v => _mod.Config.gainExperience = v)
         .Dropdown("config.grab-frequency",
             () => _mod.Config.grabFrequency,
             v =>
             {
                 // Clear the Instant-mode queues when frequency changes so locations
                 // queued under the old mode don't sit in memory (audit §1.2, §3.8).
                 if (_mod.Config.grabFrequency != v)
                     _mod.ClearLocationQueues();
                 _mod.Config.grabFrequency = v;
             },
             ModConfig.GrabFrequencyDict, ModConfig.GrabFrequencyReverseDict, ModConfig.GrabFrequencyStrings,
             dropdownLabelPrefix: "")
         .Bool("config.exclude-quest-items",
             () => _mod.Config.Compatibility.excludeQuestItems, v => _mod.Config.Compatibility.excludeQuestItems = v)
         .Bool("config.skip-festival-locations",
             () => _mod.Config.Locations.skipFestivalLocations, v => _mod.Config.Locations.skipFestivalLocations = v)
         .TextOption("config.excluded-items",
             () => string.Join(", ", _mod.Config.Compatibility.excludedItems ?? new HashSet<string>()),
             v => _mod.Config.Compatibility.excludedItems = new HashSet<string>(
                 v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
         .Dropdown("config.global-grabber-mode",
             () => _mod.Config.globalGrabber, v => _mod.Config.globalGrabber = v,
             ModConfig.GlobalGrabberDict, ModConfig.GlobalGrabberReverseDict, ModConfig.GlobalGrabberModeStrings,
             dropdownLabelPrefix: "")
         .Bool("config.auto-fire-global-grabber",
             () => _mod.Config.GlobalGrab.globalAutoFire, v => _mod.Config.GlobalGrab.globalAutoFire = v)
         .Keybind("config.fire-global-grabber",
             () => _mod.Config.GlobalGrab.globalFireButton, v => _mod.Config.GlobalGrab.globalFireButton = v)
         .Keybind("config.designate-global-grabber",
             () => _mod.Config.GlobalGrab.designateGrabberButton, v => _mod.Config.GlobalGrab.designateGrabberButton = v)
         .Number("config.global-button-x-offset",
             () => _mod.Config.GlobalGrab.globalButtonOffsetX, v => _mod.Config.GlobalGrab.globalButtonOffsetX = Math.Clamp(v, -500, 500))
         .Number("config.global-button-y-offset",
             () => _mod.Config.GlobalGrab.globalButtonOffsetY, v => _mod.Config.GlobalGrab.globalButtonOffsetY = Math.Clamp(v, -500, 500))
         .Number("config.rename-button-x-offset",
             () => _mod.Config.GlobalGrab.renameButtonOffsetX, v => _mod.Config.GlobalGrab.renameButtonOffsetX = Math.Clamp(v, -500, 500))
         .Number("config.rename-button-y-offset",
             () => _mod.Config.GlobalGrab.renameButtonOffsetY, v => _mod.Config.GlobalGrab.renameButtonOffsetY = Math.Clamp(v, -500, 500));

        // Compatibility page
        b.Page("compatibility", "page.compatibility")
         .Paragraph("page.compatibility.paragraph")
         .Bool("config.sunberry-village-exclusions",
             () => _mod.Config.Compatibility.sunberryVillageExclusions, v => _mod.Config.Compatibility.sunberryVillageExclusions = v)
         .Bool("config.visit-mt-vapius-exclusions",
             () => _mod.Config.Compatibility.visitMtVapiusExclusions, v => _mod.Config.Compatibility.visitMtVapiusExclusions = v)
         .Bool("config.baubles-exclusions",
             () => _mod.Config.Compatibility.baublesExclusions, v => _mod.Config.Compatibility.baublesExclusions = v)
         .Bool("config.resource-chickens-exclusions",
             () => _mod.Config.Compatibility.resourceChickensExclusions, v => _mod.Config.Compatibility.resourceChickensExclusions = v)
         .Bool("config.cape-stardew-exclusions",
             () => _mod.Config.Compatibility.capeStardewExclusions, v => _mod.Config.Compatibility.capeStardewExclusions = v)
         .Bool("config.collect-wildflowers",
             () => _mod.Config.Features.collectWildflowers, v => _mod.Config.Features.collectWildflowers = v)
         .Bool("config.collect-buried-items",
             () => _mod.Config.Features.buriedItems, v => _mod.Config.Features.buriedItems = v)
         .Bool("config.automate-compatibility",
             () => _mod.Config.Machines.automateCompatibility, v => _mod.Config.Machines.automateCompatibility = v);

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
                 () => _locations.DiscoveredLocations.All(loc => _mod.Config.Locations.SkippedLocations?.Contains(loc.Name) != true),
                 v => { },
                 fieldId: "enable-all")
             .Bool("config.select-visited-only",
                 () => _mod.Config.Locations.selectVisitedOnly,
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
                         _mod.Config.Locations.selectVisitedOnly = false;
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
            getValue: () => _mod.Config.Locations.SkippedLocations?.Contains(capturedName) != true,
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
        _mod.Config.Locations.SkippedLocations ??= new HashSet<string>();
        bool currentlyEnabled = !_mod.Config.Locations.SkippedLocations.Contains(locName);
        if (enabled == currentlyEnabled)
            return;

        if (!enabled)
        {
            _mod.Config.Locations.SkippedLocations.Add(locName);
            if (_locations.SaveData != null)
            {
                _locations.SaveData.BlacklistedLocations.Add(locName);
                _locations.SaveData.AutoSkippedLocations.Remove(locName);
                _locations.WriteSaveData();
            }
        }
        else
        {
            _mod.Config.Locations.SkippedLocations.Remove(locName);
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
