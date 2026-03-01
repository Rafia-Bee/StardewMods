using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal abstract class TerrainFeaturesMapGrabber : MapGrabber
{
    protected List<KeyValuePair<Vector2, TerrainFeature>> Features { get; set; }

    public TerrainFeaturesMapGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        Features = location.terrainFeatures.Pairs
            .Concat(location.largeTerrainFeatures
                .Select(ft => new KeyValuePair<Vector2, TerrainFeature>(ft.Tile, ft)))
            .ToList();
    }

    public abstract bool GrabFeature(Vector2 tile, TerrainFeature feature);

    public override bool GrabItems()
    {
        return Features
            .Select(pair => GrabFeature(pair.Key, pair.Value))
            .Aggregate(false, (grabbed, next) => grabbed || next);
    }
}
