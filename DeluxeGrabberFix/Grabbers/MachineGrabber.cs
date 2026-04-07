using System.Collections.Generic;
using System.Linq;
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
        _automateSkipTiles = Config.automateCompatibility
            ? BuildAutomateSkipTiles(mod, location)
            : null;
    }

    /// <summary>
    /// Determines which automated machines should be skipped by checking whether
    /// they belong to an Automate group that actually has a connected chest that accepts output.
    /// Machines in groups without output-capable chests are NOT skipped so DGF can collect from them.
    /// </summary>
    private static HashSet<Vector2> BuildAutomateSkipTiles(ModEntry mod, GameLocation location)
    {
        const string storeItemsKey = "Pathoschild.Automate/StoreItems";

        var allMachineTiles = mod.GetAutomatedMachineStates(location);
        if (allMachineTiles == null || allMachineTiles.Count == 0)
            return null;

        var skipTiles = new HashSet<Vector2>();
        var visited = new HashSet<Vector2>();

        foreach (var startTile in allMachineTiles.Keys)
        {
            if (visited.Contains(startTile))
                continue;

            // BFS to find the connected component (machines + connectors + chests)
            var component = new List<Vector2>();
            var hasOutputChest = false;
            var queue = new Queue<Vector2>();
            queue.Enqueue(startTile);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!visited.Add(current))
                    continue;

                if (allMachineTiles.ContainsKey(current))
                    component.Add(current);

                if (!hasOutputChest && location.Objects.TryGetValue(current, out var currentObj) && currentObj is Chest chest
                    && !chest.modData.ContainsKey("spacechase0.SuperHopper"))
                {
                    // Only count this chest if Automate can store items in it
                    // "Disable" means "Never put items in this chest"
                    if (!chest.modData.TryGetValue(storeItemsKey, out var storeValue) || storeValue != "Disable")
                        hasOutputChest = true;
                }

                // Check cardinal neighbors for machines, chests, or connectors (flooring/paths)
                Vector2[] neighbors =
                {
                    new(current.X, current.Y - 1),
                    new(current.X, current.Y + 1),
                    new(current.X - 1, current.Y),
                    new(current.X + 1, current.Y)
                };

                foreach (var neighbor in neighbors)
                {
                    if (visited.Contains(neighbor))
                        continue;

                    if (allMachineTiles.ContainsKey(neighbor))
                        queue.Enqueue(neighbor);
                    else if (location.Objects.TryGetValue(neighbor, out var nObj) && nObj is Chest nChest
                             && !nChest.modData.ContainsKey("spacechase0.SuperHopper"))
                        queue.Enqueue(neighbor);
                    else if (location.terrainFeatures.TryGetValue(neighbor, out var feature) && feature is Flooring)
                        queue.Enqueue(neighbor);
                }
            }

            if (hasOutputChest)
            {
                foreach (var tile in component)
                    skipTiles.Add(tile);
            }
        }

        return skipTiles.Count > 0 ? skipTiles : null;
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.collectMachines)
            return false;

        if (!obj.readyForHarvest.Value || obj.heldObject.Value == null)
            return false;

        if (_automateSkipTiles != null && _automateSkipTiles.Contains(tile))
            return false;

        if (obj.GetMachineData()?.IsIncubator == true)
            return false;

        if (IsCrabPot(obj))
            return GrabCrabPot(tile, obj);

        if (IsFishNet(obj))
            return GrabFishNet(tile, obj);

        if (IsBeeHouse(obj))
            return GrabStandardMachine(tile, obj, Config.collectBeeHouses, "bee house");

        if (IsTapper(obj))
            return GrabStandardMachine(tile, obj, Config.collectTappers, "tapper");

        if (IsMushroomLog(obj))
            return GrabStandardMachine(tile, obj, Config.collectMushroomLogs, "mushroom log");

        if (IsLeafBasket(obj))
            return GrabStandardMachine(tile, obj, Config.collectLeafBaskets, "leaf basket");

        // Artisan equipment
        if (IsAnyOf(obj, BigCraftableIds.Keg, "Keg"))
            return GrabStandardMachine(tile, obj, Config.collectKegs, "keg");
        if (IsAnyOf(obj, BigCraftableIds.PreservesJar, "PreservesJar"))
            return GrabStandardMachine(tile, obj, Config.collectPreservesJars, "preserves jar");
        if (IsAnyOf(obj, BigCraftableIds.CheesePress, "CheesePress"))
            return GrabStandardMachine(tile, obj, Config.collectCheesePresses, "cheese press");
        if (IsAnyOf(obj, BigCraftableIds.MayonnaiseMachine, "MayonnaiseMachine"))
            return GrabStandardMachine(tile, obj, Config.collectMayonnaiseMachines, "mayonnaise machine");
        if (IsAnyOf(obj, BigCraftableIds.Loom, "Loom"))
            return GrabStandardMachine(tile, obj, Config.collectLooms, "loom");
        if (IsAnyOf(obj, BigCraftableIds.OilMaker, "OilMaker"))
            return GrabStandardMachine(tile, obj, Config.collectOilMakers, "oil maker");

        // Processing machines
        if (obj.QualifiedItemId == BigCraftableIds.Furnace || obj.QualifiedItemId == BigCraftableIds.HeavyFurnace)
            return GrabStandardMachine(tile, obj, Config.collectFurnaces, "furnace");
        if (obj.QualifiedItemId == BigCraftableIds.CharcoalKiln)
            return GrabStandardMachine(tile, obj, Config.collectCharcoalKilns, "charcoal kiln");
        if (IsAnyOf(obj, BigCraftableIds.RecyclingMachine, "RecyclingMachine"))
            return GrabStandardMachine(tile, obj, Config.collectRecyclingMachines, "recycling machine");
        if (obj.QualifiedItemId == BigCraftableIds.SeedMaker)
            return GrabStandardMachine(tile, obj, Config.collectSeedMakers, "seed maker");
        if (IsAnyOf(obj, BigCraftableIds.BoneMill, "BoneMill"))
            return GrabStandardMachine(tile, obj, Config.collectBoneMills, "bone mill");
        if (obj.QualifiedItemId == BigCraftableIds.GeodeCrusher)
            return GrabStandardMachine(tile, obj, Config.collectGeodeCrushers, "geode crusher");
        if (obj.QualifiedItemId == BigCraftableIds.WoodChipper)
            return GrabStandardMachine(tile, obj, Config.collectWoodChippers, "wood chipper");
        if (obj.QualifiedItemId == BigCraftableIds.Deconstructor)
            return GrabStandardMachine(tile, obj, Config.collectDeconstructors, "deconstructor");

        // 1.6 machines
        if (IsAnyOf(obj, BigCraftableIds.FishSmoker, "FishSmoker"))
            return GrabStandardMachine(tile, obj, Config.collectFishSmokers, "fish smoker");
        if (IsAnyOf(obj, BigCraftableIds.BaitMaker, "BaitMaker"))
            return GrabStandardMachine(tile, obj, Config.collectBaitMakers, "bait maker");
        if (obj.QualifiedItemId == BigCraftableIds.Dehydrator)
            return GrabStandardMachine(tile, obj, Config.collectDehydrators, "dehydrator");

        // Passive producers
        if (obj.QualifiedItemId == BigCraftableIds.Crystalarium)
            return GrabStandardMachine(tile, obj, Config.collectCrystalariums, "crystalarium");
        if (IsAnyOf(obj, BigCraftableIds.LightningRod, "LightningRod"))
            return GrabStandardMachine(tile, obj, Config.collectLightningRods, "lightning rod");
        if (obj.QualifiedItemId == BigCraftableIds.WormBin
            || obj.QualifiedItemId == BigCraftableIds.DeluxeWormBin
            || IsMps(obj, "WormBin") || IsMps(obj, "DeluxeWormBin"))
            return GrabStandardMachine(tile, obj, Config.collectWormBins, "worm bin");
        if (IsAnyOf(obj, BigCraftableIds.SolarPanel, "SolarPanel"))
            return GrabStandardMachine(tile, obj, Config.collectSolarPanels, "solar panel");
        if (obj.QualifiedItemId == BigCraftableIds.SlimeEggPress)
            return GrabStandardMachine(tile, obj, Config.collectSlimeEggPresses, "slime egg-press");
        if (obj.QualifiedItemId == BigCraftableIds.CoffeeMaker)
            return GrabStandardMachine(tile, obj, Config.collectCoffeeMakers, "coffee maker");
        if (obj.QualifiedItemId == BigCraftableIds.SodaMachine)
            return GrabStandardMachine(tile, obj, Config.collectSodaMachines, "soda machine");

        // Statues
        if (obj.QualifiedItemId == BigCraftableIds.StatueOfPerfection
            || obj.QualifiedItemId == BigCraftableIds.StatueOfTruePerfection
            || obj.QualifiedItemId == BigCraftableIds.StatueOfEndlessFortune)
            return GrabStandardMachine(tile, obj, Config.collectStatues, "statue");

        // Catch-all for any other machine (modded machines, etc.)
        if (obj.bigCraftable.Value)
            return GrabStandardMachine(tile, obj, Config.collectOtherMachines, obj.Name ?? "unknown machine");

        return false;
    }

    private bool GrabCrabPot(Vector2 tile, Object obj)
    {
        if (!Config.collectCrabPots)
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
        if (!Config.collectCrabPots)
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
        => obj is CrabPot || IsMps(obj, "CrabPot");

    private static bool IsFishNet(Object obj)
        => obj.QualifiedItemId == ItemIds.FishNet;

    private static bool IsBeeHouse(Object obj)
        => obj.QualifiedItemId == BigCraftableIds.BeeHouse || IsMps(obj, "BeeHouse");

    private static bool IsTapper(Object obj)
        => obj.QualifiedItemId == BigCraftableIds.Tapper
           || obj.QualifiedItemId == BigCraftableIds.HeavyTapper
           || IsMps(obj, "Tapper");

    private static bool IsLeafBasket(Object obj)
        => obj.QualifiedItemId == BigCraftableIds.LeafBasket;

    private static bool IsMushroomLog(Object obj)
        => obj.QualifiedItemId == BigCraftableIds.MushroomLog || IsMps(obj, "MushroomLog");

    private static bool IsMps(Object obj, string machineName)
        => obj.QualifiedItemId?.StartsWith(BigCraftableIds.MpsPrefix) == true
           && obj.QualifiedItemId.Contains(machineName);

    private static bool IsAnyOf(Object obj, string vanillaId, string mpsName)
        => obj.QualifiedItemId == vanillaId || IsMps(obj, mpsName);
}
