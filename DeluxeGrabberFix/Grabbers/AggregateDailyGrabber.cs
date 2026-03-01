using System.Collections.Generic;
using System.Linq;
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
            new AggregateObjectsGrabber(mod, location),
            new AggregateFeaturesGrabber(mod, location),
            new WoodsHardwoodGrabber(mod, location),
            new TownGarbageCanGrabber(mod, location)
        };
    }

    public AggregateDailyGrabber(ModEntry mod, GameLocation location, bool DayStarted)
        : base(mod, location, DayStarted)
    {
        grabbers = new List<MapGrabber>
        {
            new AggregateObjectsGrabber(mod, location),
            new AggregateFeaturesGrabber(mod, location),
            new WoodsHardwoodGrabber(mod, location),
            new TownGarbageCanGrabber(mod, location)
        };
    }

    public override bool GrabItems()
    {
        return grabbers.Aggregate(false, (grabbed, grabber) => (grabber.CanGrab() && grabber.GrabItems()) || grabbed);
    }
}
