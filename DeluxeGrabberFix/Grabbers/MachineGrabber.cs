using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.Internal;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class MachineGrabber : ObjectsMapGrabber
{
    private readonly HashSet<Vector2> _automateSkipTiles;

    public MachineGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        _automateSkipTiles = AutomateSkipTiles.Get(mod, location);
    }

    // Audit §4.9: dispatch table replaces the prior 100-line if/else chain. Adding
    // a new machine is a one-line entry here (or in MpsMachines if a Machine Progression
    // System variant exists). The Func<MachineCollection, bool> gate is evaluated against
    // the live config every dispatch, so per-save config swaps and GMCM toggles are honored
    // without rebuilding the table.
    private readonly record struct MachineEntry(Func<ModConfig.MachineSettings, bool> Gate, string Name);

    // Direct QualifiedItemId -> entry. Multiple IDs that share a config flag are listed
    // individually (Furnace + HeavyFurnace, the three statues, WormBin + DeluxeWormBin).
    private static readonly Dictionary<string, MachineEntry> StandardMachines = new()
    {
        [BigCraftableIds.BeeHouse] = new(c => c.collectBeeHouses, "bee house"),
        [BigCraftableIds.MushroomLog] = new(c => c.collectMushroomLogs, "mushroom log"),
        [BigCraftableIds.LeafBasket] = new(c => c.collectLeafBaskets, "leaf basket"),
        [BigCraftableIds.Keg] = new(c => c.collectKegs, "keg"),
        [BigCraftableIds.PreservesJar] = new(c => c.collectPreservesJars, "preserves jar"),
        [BigCraftableIds.CheesePress] = new(c => c.collectCheesePresses, "cheese press"),
        [BigCraftableIds.MayonnaiseMachine] = new(c => c.collectMayonnaiseMachines, "mayonnaise machine"),
        [BigCraftableIds.Loom] = new(c => c.collectLooms, "loom"),
        [BigCraftableIds.OilMaker] = new(c => c.collectOilMakers, "oil maker"),
        [BigCraftableIds.Furnace] = new(c => c.collectFurnaces, "furnace"),
        [BigCraftableIds.HeavyFurnace] = new(c => c.collectFurnaces, "furnace"),
        [BigCraftableIds.CharcoalKiln] = new(c => c.collectCharcoalKilns, "charcoal kiln"),
        [BigCraftableIds.RecyclingMachine] = new(c => c.collectRecyclingMachines, "recycling machine"),
        [BigCraftableIds.SeedMaker] = new(c => c.collectSeedMakers, "seed maker"),
        [BigCraftableIds.BoneMill] = new(c => c.collectBoneMills, "bone mill"),
        [BigCraftableIds.GeodeCrusher] = new(c => c.collectGeodeCrushers, "geode crusher"),
        [BigCraftableIds.WoodChipper] = new(c => c.collectWoodChippers, "wood chipper"),
        [BigCraftableIds.Deconstructor] = new(c => c.collectDeconstructors, "deconstructor"),
        [BigCraftableIds.FishSmoker] = new(c => c.collectFishSmokers, "fish smoker"),
        [BigCraftableIds.BaitMaker] = new(c => c.collectBaitMakers, "bait maker"),
        [BigCraftableIds.Dehydrator] = new(c => c.collectDehydrators, "dehydrator"),
        [BigCraftableIds.Crystalarium] = new(c => c.collectCrystalariums, "crystalarium"),
        [BigCraftableIds.LightningRod] = new(c => c.collectLightningRods, "lightning rod"),
        [BigCraftableIds.WormBin] = new(c => c.collectWormBins, "worm bin"),
        [BigCraftableIds.DeluxeWormBin] = new(c => c.collectWormBins, "worm bin"),
        [BigCraftableIds.SolarPanel] = new(c => c.collectSolarPanels, "solar panel"),
        [BigCraftableIds.SlimeEggPress] = new(c => c.collectSlimeEggPresses, "slime egg-press"),
        [BigCraftableIds.CoffeeMaker] = new(c => c.collectCoffeeMakers, "coffee maker"),
        [BigCraftableIds.SodaMachine] = new(c => c.collectSodaMachines, "soda machine"),
        [BigCraftableIds.StatueOfPerfection] = new(c => c.collectStatues, "statue"),
        [BigCraftableIds.StatueOfTruePerfection] = new(c => c.collectStatues, "statue"),
        [BigCraftableIds.StatueOfEndlessFortune] = new(c => c.collectStatues, "statue"),
    };

    // MPS-prefixed objects (Machine Progression System): match if the QualifiedItemId
    // contains the fragment. Mirrors the prior IsMps()/IsAnyOf() set: only machines that
    // had explicit MPS branches in the original chain are listed here. WormBin's
    // fragment also matches DeluxeWormBin since both share the same gate.
    private static readonly Dictionary<string, MachineEntry> MpsMachines = new()
    {
        ["BeeHouse"] = new(c => c.collectBeeHouses, "bee house"),
        ["MushroomLog"] = new(c => c.collectMushroomLogs, "mushroom log"),
        ["Keg"] = new(c => c.collectKegs, "keg"),
        ["PreservesJar"] = new(c => c.collectPreservesJars, "preserves jar"),
        ["CheesePress"] = new(c => c.collectCheesePresses, "cheese press"),
        ["MayonnaiseMachine"] = new(c => c.collectMayonnaiseMachines, "mayonnaise machine"),
        ["Loom"] = new(c => c.collectLooms, "loom"),
        ["OilMaker"] = new(c => c.collectOilMakers, "oil maker"),
        ["RecyclingMachine"] = new(c => c.collectRecyclingMachines, "recycling machine"),
        ["BoneMill"] = new(c => c.collectBoneMills, "bone mill"),
        ["FishSmoker"] = new(c => c.collectFishSmokers, "fish smoker"),
        ["BaitMaker"] = new(c => c.collectBaitMakers, "bait maker"),
        ["LightningRod"] = new(c => c.collectLightningRods, "lightning rod"),
        ["WormBin"] = new(c => c.collectWormBins, "worm bin"),
        ["SolarPanel"] = new(c => c.collectSolarPanels, "solar panel"),
    };

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (Config.Machines.disableMachineCollection)
            return false;

        if (!obj.readyForHarvest.Value || obj.heldObject.Value == null)
            return false;

        // Specialized grabbers use heldObject for storage, not as machine output
        if (GrabberTypeHelper.IsGrabber(obj.QualifiedItemId))
            return false;

        if (_automateSkipTiles != null && _automateSkipTiles.Contains(tile))
        {
            Mod.LogDebug($"Skipping {obj.Name} at {Location.Name} [{tile}] (managed by Automate)");
            return false;
        }

        if (obj.GetMachineData()?.IsIncubator == true)
            return false;

        // Special grab semantics (clear bait, clear fish-net modData, etc.) -- can't
        // share the standard GrabStandardMachine restart helper.
        if (IsCrabPot(obj))
            return GrabCrabPot(tile, obj);
        if (IsFishNet(obj))
            return GrabFishNet(tile, obj);

        // Tapper kept inline (vs the dispatch table) because obj.IsTapper() is the
        // SDV-side source of truth for "is this a tap-able machine" -- including
        // modded tappers registered through SDV's machine data, which a hard-coded
        // dictionary of QualifiedItemId would miss.
        if (obj.IsTapper())
            return GrabStandardMachine(tile, obj, Config.Machines.collectTappers, "tapper");

        var qid = obj.QualifiedItemId;
        if (qid != null)
        {
            if (StandardMachines.TryGetValue(qid, out var entry))
                return GrabStandardMachine(tile, obj, entry.Gate(Config.Machines), entry.Name);

            if (qid.StartsWith(BigCraftableIds.MpsPrefix))
            {
                foreach (var (fragment, mpsEntry) in MpsMachines)
                {
                    if (qid.Contains(fragment))
                        return GrabStandardMachine(tile, obj, mpsEntry.Gate(Config.Machines), mpsEntry.Name);
                }
            }
        }

        // Catch-all for any other big-craftable machine (modded machines that don't
        // declare an MPS prefix or one of the registered IDs above).
        if (obj.bigCraftable.Value)
            return GrabStandardMachine(tile, obj, Config.Machines.collectOtherMachines, obj.Name ?? "unknown machine");

        return false;
    }

    private bool GrabCrabPot(Vector2 tile, Object obj)
    {
        if (!Config.Machines.collectCrabPots)
            return false;

        var output = obj.heldObject.Value;
        if (TryAddItem(output))
        {
            Mod.LogDebug($"Collected {output.Name} x{output.Stack} from crab pot at {Location.Name} [{tile}]");
            obj.heldObject.Value = null;
            obj.readyForHarvest.Value = false;
            obj.showNextIndex.Value = false;

            if (obj is CrabPot crabPot)
                crabPot.bait.Value = null;

            GainExperience(1, 5);
            return true;
        }
        return false;
    }

    private bool GrabFishNet(Vector2 tile, Object obj)
    {
        if (!Config.Machines.collectCrabPots)
            return false;

        var output = obj.heldObject.Value;
        if (TryAddItem(output))
        {
            Mod.LogDebug($"Collected {output.Name} x{output.Stack} from fish net at {Location.Name} [{tile}]");
            obj.heldObject.Value = null;
            obj.readyForHarvest.Value = false;
            obj.showNextIndex.Value = false;

            obj.modData.Remove(ItemIds.FishNetBaitModDataKey);
            obj.modData.Remove(ItemIds.FishNetTileIndexModDataKey);

            GainExperience(1, 5);
            return true;
        }
        return false;
    }

    private bool GrabStandardMachine(Vector2 tile, Object obj, bool configEnabled, string machineName)
    {
        if (!configEnabled)
            return false;

        var output = obj.heldObject.Value;
        if (TryAddItem(output))
        {
            Mod.LogDebug($"Collected {output.Name} x{output.Stack} from {machineName} at {Location.Name} [{tile}]");
            obj.heldObject.Value = null;
            obj.readyForHarvest.Value = false;
            obj.showNextIndex.Value = false;
            obj.ResetParentSheetIndex();

            RestartMachineIfNeeded(tile, obj, output);

            return true;
        }
        return false;
    }

    private void RestartMachineIfNeeded(Vector2 tile, Object obj, Object collectedOutput)
    {
        var machineData = obj.GetMachineData();
        if (machineData == null)
            return;

        // Restart self-repeating machines (Crystalarium, Worm Bin, Bee House, etc.)
        if (MachineDataUtility.TryGetMachineOutputRule(obj, machineData, MachineOutputTrigger.OutputCollected, collectedOutput.getOne(), Player, Location, out var rule, out _, out _, out _))
        {
            obj.OutputMachine(machineData, rule, obj.lastInputItem.Value, Player, Location, probe: false);
        }

        // Tappers have a special case: update the tree's tapper product
        if (obj.IsTapper() && Location.terrainFeatures.TryGetValue(tile, out var feature) && feature is Tree tree)
        {
            tree.UpdateTapperProduct(obj, collectedOutput);
        }
    }

    private static bool IsCrabPot(Object obj)
        => obj is CrabPot
           || (obj.QualifiedItemId?.StartsWith(BigCraftableIds.MpsPrefix) == true && obj.QualifiedItemId.Contains("CrabPot"));

    private static bool IsFishNet(Object obj)
        => obj.QualifiedItemId == ItemIds.FishNet;
}
