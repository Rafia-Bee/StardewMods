using System.Collections.Generic;
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

    // Audit §3.10: foreach + locals replaces a `LINQ Aggregate` over a delegate
    // capture. Every sub-grabber still runs (no short-circuit on `any`); CanGrab
    // still gates GrabItems via && short-circuit, matching the prior semantics.
    public override bool GrabItems()
    {
        bool any = false;
        foreach (var grabber in grabbers)
        {
            if (grabber.CanGrab() && grabber.GrabItems())
                any = true;
        }
        return any;
    }
}
