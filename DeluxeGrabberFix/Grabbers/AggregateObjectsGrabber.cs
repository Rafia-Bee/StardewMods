using System.Collections.Generic;
using System.Linq;
using DeluxeGrabberFix.Framework;
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
            new SlimeHutchGrabber(mod, location) { BelongsToType = GrabberType.Animal },
            new FarmCaveMushroomGrabber(mod, location) { BelongsToType = GrabberType.Forage },
            new IndoorPotGrabber(mod, location) { BelongsToType = GrabberType.Crop },
            new ArtifactSpotsGrabber(mod, location) { BelongsToType = GrabberType.Scavenger },
            new SeedSpotsGrabber(mod, location) { BelongsToType = GrabberType.Crop },
            new MachineGrabber(mod, location) { BelongsToType = GrabberType.Machine },
            new GenericObjectGrabber(mod, location) { BelongsToType = GrabberType.Forage }
        };
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        return grabbers.Select(g => g.GrabObject(tile, obj)).Any(x => x);
    }
}
