using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.Objects;

namespace DeluxeGrabberFix.Grabbers;

internal class MachineGrabber : ObjectsMapGrabber
{
    private readonly IDictionary<Vector2, int> _automatedTiles;

    public MachineGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        _automatedTiles = Config.automateCompatibility
            ? mod.GetAutomatedMachineStates(location)
            : null;
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.collectMachines)
            return false;

        if (!obj.readyForHarvest.Value || obj.heldObject.Value == null)
            return false;

        if (_automatedTiles != null && _automatedTiles.ContainsKey(tile))
            return false;

        if (IsCrabPot(obj))
            return GrabCrabPot(tile, obj);

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
            return true;
        }
        return false;
    }

    private static bool IsCrabPot(Object obj)
        => obj is CrabPot || IsMps(obj, "CrabPot");

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
