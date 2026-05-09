using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal abstract class ObjectsMapGrabber : MapGrabber
{
    protected List<KeyValuePair<Vector2, Object>> Objects { get; set; }

    public ObjectsMapGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        if (Mod.UseLocationCache && Mod.CachedObjectPairs != null)
        {
            Objects = Mod.CachedObjectPairs;
            return;
        }

        Objects = location.Objects.Pairs.ToList();

        if (Mod.UseLocationCache)
            Mod.CachedObjectPairs = Objects;
    }

    public abstract bool GrabObject(Vector2 tile, Object obj);

    // Audit §3.10: foreach replaces Where + Select + Aggregate (three iterator
    // allocations + delegate captures per call). Every non-grabbed-tile pair
    // still gets a GrabObject invocation; we don't short-circuit on `any`
    // because GrabObject has side effects (item collection, debris cleanup).
    public override bool GrabItems()
    {
        bool any = false;
        foreach (var pair in Objects)
        {
            if (Mod.GrabbedTiles?.Contains(pair.Key) == true)
                continue;
            if (GrabObject(pair.Key, pair.Value))
                any = true;
        }
        return any;
    }
}
