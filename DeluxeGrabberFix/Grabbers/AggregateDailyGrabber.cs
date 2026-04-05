using System.Collections.Generic;
using System.Linq;
using DeluxeGrabberFix.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class AggregateDailyGrabber : MapGrabber
{
    private readonly List<MapGrabber> grabbers;

    public AggregateDailyGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        grabbers = new List<MapGrabber>
        {
            new AnimalProductGrabber(mod, location) { BelongsToType = GrabberType.Animal },
            new AggregateObjectsGrabber(mod, location),
            new AggregateFeaturesGrabber(mod, location),
            new FishPondGrabber(mod, location) { BelongsToType = GrabberType.Machine },
            new WoodsHardwoodGrabber(mod, location) { BelongsToType = GrabberType.Tree },
            new GreenRainWeedGrabber(mod, location) { BelongsToType = GrabberType.Scavenger },
            new TownGarbageCanGrabber(mod, location) { BelongsToType = GrabberType.Scavenger },
            new DebrisGrabber(mod, location) { BelongsToType = GrabberType.Scavenger }
        };
    }

    public override bool GrabItems()
    {
        return grabbers.Aggregate(false, (grabbed, grabber) => (grabber.CanGrab() && grabber.GrabItems()) || grabbed);
    }
}
