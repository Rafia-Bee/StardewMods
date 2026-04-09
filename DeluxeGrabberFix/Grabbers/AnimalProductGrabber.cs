using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.FarmAnimals;

namespace DeluxeGrabberFix.Grabbers;

internal class AnimalProductGrabber : MapGrabber
{
    public AnimalProductGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabItems()
    {
        if (!Config.animalProducts)
            return false;

        bool grabbed = false;

        // Collect tool-harvest products (milk, wool) from animals —
        // mirrors vanilla auto-grabber DayUpdate logic for (BC)165.
        // In SDV 1.6, all GameLocations have an animals dictionary,
        // so this works on Farm, AnimalHouse, and any modded location.
        foreach (var animal in Location.animals.Values)
        {
            if (animal.GetHarvestType() != FarmAnimalHarvestType.HarvestWithTool
                || animal.currentProduce.Value == null)
                continue;

            var item = ItemRegistry.Create<Object>("(O)" + animal.currentProduce.Value);
            item.CanBeSetDown = false;
            item.Quality = animal.produceQuality.Value;

            if (animal.hasEatenAnimalCracker.Value)
                item.Stack = 2;

            if (TryAddItem(item))
            {
                Mod.LogDebug($"Collected {item.Name} x{item.Stack} from {animal.displayName} at {Location.Name}");
                animal.HandleStatsOnProduceCollected(item, (uint)item.Stack);
                animal.currentProduce.Value = null;
                animal.ReloadTextureIfNeeded();
                GainExperience(0, 5);
                grabbed = true;
            }
        }

        // Collect dropped products (eggs, feathers, etc.) from the ground.
        // Animals with HarvestType.DropOvernight place their produce inside
        // the AnimalHouse floor, so only collect from barn/coop interiors.
        // Collects all spawned ground items (not just EggCategory) to catch
        // duck feathers, wool, rabbit's foot, and modded animal products.
        if (Location is AnimalHouse)
        {
            var tilesToRemove = new List<Vector2>();
            foreach (var pair in Location.Objects.Pairs)
            {
                var obj = pair.Value;
                if (obj.bigCraftable.Value || !obj.IsSpawnedObject)
                    continue;

                if (TryAddItem(obj))
                {
                    Mod.LogDebug($"Collected {obj.Name} x{obj.Stack} from ground at {Location.Name} [{pair.Key}]");
                    tilesToRemove.Add(pair.Key);
                    GainExperience(0, 5);
                    grabbed = true;
                }
            }

            foreach (var tile in tilesToRemove)
            {
                Location.Objects.Remove(tile);
                Mod.GrabbedTiles?.Add(tile);
            }
        }

        return grabbed;
    }
}
