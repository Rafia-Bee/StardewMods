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

        if (IsCrabPot(obj))
            return GrabCrabPot(tile, obj);

        if (IsFishNet(obj))
            return GrabFishNet(tile, obj);

        if (IsBeeHouse(obj))
            return GrabStandardMachine(tile, obj, Config.Machines.collectBeeHouses, "bee house");

        if (IsTapper(obj))
            return GrabStandardMachine(tile, obj, Config.Machines.collectTappers, "tapper");

        if (IsMushroomLog(obj))
            return GrabStandardMachine(tile, obj, Config.Machines.collectMushroomLogs, "mushroom log");

        if (IsLeafBasket(obj))
            return GrabStandardMachine(tile, obj, Config.Machines.collectLeafBaskets, "leaf basket");

        // Artisan equipment
        if (IsAnyOf(obj, BigCraftableIds.Keg, "Keg"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectKegs, "keg");
        if (IsAnyOf(obj, BigCraftableIds.PreservesJar, "PreservesJar"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectPreservesJars, "preserves jar");
        if (IsAnyOf(obj, BigCraftableIds.CheesePress, "CheesePress"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectCheesePresses, "cheese press");
        if (IsAnyOf(obj, BigCraftableIds.MayonnaiseMachine, "MayonnaiseMachine"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectMayonnaiseMachines, "mayonnaise machine");
        if (IsAnyOf(obj, BigCraftableIds.Loom, "Loom"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectLooms, "loom");
        if (IsAnyOf(obj, BigCraftableIds.OilMaker, "OilMaker"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectOilMakers, "oil maker");

        // Processing machines
        if (obj.QualifiedItemId == BigCraftableIds.Furnace || obj.QualifiedItemId == BigCraftableIds.HeavyFurnace)
            return GrabStandardMachine(tile, obj, Config.Machines.collectFurnaces, "furnace");
        if (obj.QualifiedItemId == BigCraftableIds.CharcoalKiln)
            return GrabStandardMachine(tile, obj, Config.Machines.collectCharcoalKilns, "charcoal kiln");
        if (IsAnyOf(obj, BigCraftableIds.RecyclingMachine, "RecyclingMachine"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectRecyclingMachines, "recycling machine");
        if (obj.QualifiedItemId == BigCraftableIds.SeedMaker)
            return GrabStandardMachine(tile, obj, Config.Machines.collectSeedMakers, "seed maker");
        if (IsAnyOf(obj, BigCraftableIds.BoneMill, "BoneMill"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectBoneMills, "bone mill");
        if (obj.QualifiedItemId == BigCraftableIds.GeodeCrusher)
            return GrabStandardMachine(tile, obj, Config.Machines.collectGeodeCrushers, "geode crusher");
        if (obj.QualifiedItemId == BigCraftableIds.WoodChipper)
            return GrabStandardMachine(tile, obj, Config.Machines.collectWoodChippers, "wood chipper");
        if (obj.QualifiedItemId == BigCraftableIds.Deconstructor)
            return GrabStandardMachine(tile, obj, Config.Machines.collectDeconstructors, "deconstructor");

        // 1.6 machines
        if (IsAnyOf(obj, BigCraftableIds.FishSmoker, "FishSmoker"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectFishSmokers, "fish smoker");
        if (IsAnyOf(obj, BigCraftableIds.BaitMaker, "BaitMaker"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectBaitMakers, "bait maker");
        if (obj.QualifiedItemId == BigCraftableIds.Dehydrator)
            return GrabStandardMachine(tile, obj, Config.Machines.collectDehydrators, "dehydrator");

        // Passive producers
        if (obj.QualifiedItemId == BigCraftableIds.Crystalarium)
            return GrabStandardMachine(tile, obj, Config.Machines.collectCrystalariums, "crystalarium");
        if (IsAnyOf(obj, BigCraftableIds.LightningRod, "LightningRod"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectLightningRods, "lightning rod");
        if (obj.QualifiedItemId == BigCraftableIds.WormBin
            || obj.QualifiedItemId == BigCraftableIds.DeluxeWormBin
            || IsMps(obj, "WormBin") || IsMps(obj, "DeluxeWormBin"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectWormBins, "worm bin");
        if (IsAnyOf(obj, BigCraftableIds.SolarPanel, "SolarPanel"))
            return GrabStandardMachine(tile, obj, Config.Machines.collectSolarPanels, "solar panel");
        if (obj.QualifiedItemId == BigCraftableIds.SlimeEggPress)
            return GrabStandardMachine(tile, obj, Config.Machines.collectSlimeEggPresses, "slime egg-press");
        if (obj.QualifiedItemId == BigCraftableIds.CoffeeMaker)
            return GrabStandardMachine(tile, obj, Config.Machines.collectCoffeeMakers, "coffee maker");
        if (obj.QualifiedItemId == BigCraftableIds.SodaMachine)
            return GrabStandardMachine(tile, obj, Config.Machines.collectSodaMachines, "soda machine");

        // Statues
        if (obj.QualifiedItemId == BigCraftableIds.StatueOfPerfection
            || obj.QualifiedItemId == BigCraftableIds.StatueOfTruePerfection
            || obj.QualifiedItemId == BigCraftableIds.StatueOfEndlessFortune)
            return GrabStandardMachine(tile, obj, Config.Machines.collectStatues, "statue");

        // Catch-all for any other machine (modded machines, etc.)
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
        => obj is CrabPot || IsMps(obj, "CrabPot");

    private static bool IsFishNet(Object obj)
        => obj.QualifiedItemId == ItemIds.FishNet;

    private static bool IsBeeHouse(Object obj)
        => obj.QualifiedItemId == BigCraftableIds.BeeHouse || IsMps(obj, "BeeHouse");

    private static bool IsTapper(Object obj)
        => obj.IsTapper();

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
