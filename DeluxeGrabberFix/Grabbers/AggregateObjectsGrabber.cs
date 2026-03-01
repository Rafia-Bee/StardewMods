using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class AggregateObjectsGrabber : ObjectsMapGrabber
{
    private readonly List<ObjectsMapGrabber> grabbers;

    public AggregateObjectsGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        grabbers = new List<ObjectsMapGrabber>
        {
            new SlimeHutchGrabber(mod, location),
            new FarmCaveMushroomGrabber(mod, location),
            new IndoorPotGrabber(mod, location),
            new ArtifactSpotsGrabber(mod, location),
            new SeedSpotsGrabber(mod, location),
            new GenericObjectGrabber(mod, location)
        };
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        return grabbers.Select(g => g.GrabObject(tile, obj)).Any(x => x);
    }
}
