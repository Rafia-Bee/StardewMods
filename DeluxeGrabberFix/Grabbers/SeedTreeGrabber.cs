using System.Collections.Generic;
using DeluxeGrabberFix.Framework;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class SeedTreeGrabber : TerrainFeaturesMapGrabber
{
    public SeedTreeGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.seedTrees)
            return false;

        if (feature is not Tree tree || !IsHarvestableSeedTree(tree))
            return false;

        HarvestInterceptor.BeginIntercept();
        tree.shake(tile, doEvenIfStillShaking: true);
        List<Item> items = HarvestInterceptor.EndIntercept();

        if (items.Count == 0)
            return false;

        if (TryAddItems(items))
            return true;

        return false;
    }

    private bool IsHarvestableSeedTree(Tree tree)
    {
        if (tree.growthStage.Value < 5 || tree.stump.Value || !tree.hasSeed.Value)
            return false;

        return Game1.IsMultiplayer || Player.ForagingLevel >= 1;
    }
}
