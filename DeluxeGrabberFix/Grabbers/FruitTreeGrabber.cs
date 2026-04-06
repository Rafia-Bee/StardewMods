using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class FruitTreeGrabber : TerrainFeaturesMapGrabber
{
    private readonly Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetFruitTreeHarvest;

    public FruitTreeGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        GetFruitTreeHarvest = Mod.Api.GetFruitTreeHarvest ?? DefaultGetFruitTreeHarvest;
    }

    private KeyValuePair<Object, int> DefaultGetFruitTreeHarvest(Object fruit, Vector2 treeTile, GameLocation location)
    {
        return new KeyValuePair<Object, int>(fruit, 0);
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.fruitTrees)
            return false;

        if (feature is not FruitTree tree || !IsHarvestableFruitTree(tree))
            return false;

        bool struckByLightning = tree.struckByLightningCountdown.Value > 0;
        int quality = tree.GetQuality();

        if (struckByLightning)
        {
            var coal = ItemRegistry.Create<Object>(Object.coalQID, tree.fruit.Count(), quality);
            var harvest = GetFruitTreeHarvest(coal, tile, Location);
            if (TryAddItem(harvest.Key))
            {
                GainExperience(0, harvest.Value);
                tree.fruit.Clear();
                return true;
            }
            return false;
        }

        bool anyAdded = false;
        for (int i = tree.fruit.Count() - 1; i >= 0; i--)
        {
            Item fruitItem = tree.fruit[i];
            var fruit = ItemRegistry.Create<Object>(fruitItem.ItemId, 1, quality);
            var harvest = GetFruitTreeHarvest(fruit, tile, Location);
            if (TryAddItem(harvest.Key))
            {
                GainExperience(0, harvest.Value);
                tree.fruit.RemoveAt(i);
                anyAdded = true;
            }
        }

        return anyAdded;
    }

    private bool IsHarvestableFruitTree(FruitTree tree)
    {
        return !tree.stump.Value
            && tree.growthStage.Value >= 4
            && tree.fruit.Count() > 0;
    }
}
