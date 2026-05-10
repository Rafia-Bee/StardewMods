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
        if (!Config.Features.seedTrees)
            return false;

        if (feature is not Tree tree || !IsHarvestableSeedTree(tree))
            return false;

        // try/finally so a thrown shake (third-party Harmony patch on Tree.shake, etc.)
        // doesn't leave HarvestInterceptor._intercepting=true and trip audit 4.6's
        // reentry throw on every subsequent grab.
        List<Item> items;
        HarvestInterceptor.BeginIntercept();
        try
        {
            tree.shake(tile, doEvenIfStillShaking: true);
        }
        finally
        {
            items = HarvestInterceptor.EndIntercept();
        }

        if (items.Count == 0)
            return false;

        // tree.shake() has already cleared hasSeed.Value, so the seed is gone
        // from the tree no matter what. If the chest is full or rejects the
        // item, drop it on the ground rather than silently losing it.
        foreach (var item in items)
        {
            if (!TryAddItem(item))
                Game1.createItemDebris(item, new Vector2(tile.X * 64 + 32, tile.Y * 64 + 32), -1, Location);
        }

        return true;
    }

    private bool IsHarvestableSeedTree(Tree tree)
    {
        if (tree.growthStage.Value < 5 || tree.stump.Value || !tree.hasSeed.Value)
            return false;

        return Game1.IsMultiplayer || Player.ForagingLevel >= 1;
    }
}
