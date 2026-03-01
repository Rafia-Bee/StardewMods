using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class AggregateFeaturesGrabber : TerrainFeaturesMapGrabber
{
    private readonly List<TerrainFeaturesMapGrabber> grabbers;

    public AggregateFeaturesGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        grabbers = new List<TerrainFeaturesMapGrabber>
        {
            new ForageHoeDirtGrabber(mod, location),
            new HarvestableCropHoeDirtGrabber(mod, location),
            new FruitTreeGrabber(mod, location),
            new SeedTreeGrabber(mod, location),
            new BerryBushGrabber(mod, location),
            new TreeMossGrabber(mod, location)
        };
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        return grabbers.Select(g => g.GrabFeature(tile, feature)).Any(x => x);
    }
}
