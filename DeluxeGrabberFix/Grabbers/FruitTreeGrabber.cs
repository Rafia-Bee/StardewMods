using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class FruitTreeGrabber : TerrainFeaturesMapGrabber
{
    public FruitTreeGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.fruitTrees)
            return false;

        if (feature is not FruitTree tree || !IsHarvestableFruitTree(tree))
            return false;

        int daysUntilMature = tree.daysUntilMature.Value;
        bool struckByLightning = tree.struckByLightningCountdown.Value > 0;

        int quality = 0;
        if (struckByLightning)
            quality = 0;
        else if (daysUntilMature <= -336)
            quality = 4;
        else if (daysUntilMature <= -224)
            quality = 2;
        else if (daysUntilMature <= -112)
            quality = 1;

        if (struckByLightning)
        {
            var coal = ItemRegistry.Create<Object>(382.ToString(), tree.fruit.Count(), quality);
            if (TryAddItem(coal))
            {
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
            if (TryAddItem(fruit))
            {
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
