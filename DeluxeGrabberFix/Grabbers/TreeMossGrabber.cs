using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class TreeMossGrabber : TerrainFeaturesMapGrabber
{
    public TreeMossGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.harvestMoss)
            return false;

        if (feature is not Tree tree || !tree.hasMoss.Value)
            return false;

        Item moss = Tree.CreateMossItem();
        if (TryAddItem(moss))
        {
            tree.hasMoss.Value = false;
            return true;
        }
        return false;
    }
}
