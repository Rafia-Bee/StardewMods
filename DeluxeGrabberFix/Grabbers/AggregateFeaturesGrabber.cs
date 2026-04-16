using System.Collections.Generic;
using System.Linq;
using DeluxeGrabberFix.Framework;
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
            new ForageHoeDirtGrabber(mod, location) { BelongsToType = GrabberType.Forage },
            new HarvestableCropHoeDirtGrabber(mod, location) { BelongsToType = GrabberType.Crop },
            new FruitTreeGrabber(mod, location) { BelongsToType = GrabberType.Tree },
            new SeedTreeGrabber(mod, location) { BelongsToType = GrabberType.Tree },
            new BerryBushGrabber(mod, location) { BelongsToType = GrabberType.Forage },
            new TreeMossGrabber(mod, location) { BelongsToType = GrabberType.Forage },
            new WildflowersGrabber(mod, location) { BelongsToType = GrabberType.Forage }
        };
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        return grabbers.Any(g => g.GrabFeature(tile, feature));
    }
}
