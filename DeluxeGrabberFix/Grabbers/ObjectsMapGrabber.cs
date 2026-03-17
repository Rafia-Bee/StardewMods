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

    public override bool GrabItems()
    {
        return Objects
            .Select(pair => GrabObject(pair.Key, pair.Value))
            .Aggregate(false, (grabbed, next) => grabbed || next);
    }
}
