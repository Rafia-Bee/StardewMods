using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class BerryBushGrabber : TerrainFeaturesMapGrabber
{
    public BerryBushGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (!Config.Features.bushes || feature is not Bush bush)
            return false;

        if (!BushHarvest.TryGetHarvest(Mod, bush, tile, Location, out var items, out int exp))
            return false;

        if (TryAddItems(items))
        {
            bush.tileSheetOffset.Value = 0;
            bush.setUpSourceRect();
            GainExperience(2, exp);
            return true;
        }
        return false;
    }
}
