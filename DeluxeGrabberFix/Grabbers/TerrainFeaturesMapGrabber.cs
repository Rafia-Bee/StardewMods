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
        if (Mod.UseLocationCache && Mod.CachedFeaturePairs != null)
        {
            Features = Mod.CachedFeaturePairs;
            return;
        }

        Features = location.terrainFeatures.Pairs
            .Concat(location.largeTerrainFeatures
                .Select(ft => new KeyValuePair<Vector2, TerrainFeature>(ft.Tile, ft)))
            .ToList();

        if (Mod.UseLocationCache)
            Mod.CachedFeaturePairs = Features;
    }

    public abstract bool GrabFeature(Vector2 tile, TerrainFeature feature);

    // Audit §3.10: foreach replaces Select + Aggregate. Same iteration shape
    // (every feature processed; GrabFeature has side effects), no delegate
    // capture or LINQ iterator allocation per call.
    public override bool GrabItems()
    {
        bool any = false;
        foreach (var pair in Features)
        {
            if (GrabFeature(pair.Key, pair.Value))
                any = true;
        }
        return any;
    }
}
