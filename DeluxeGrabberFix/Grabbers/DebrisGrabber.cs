using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class DebrisGrabber : MapGrabber
{
    public DebrisGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabItems()
    {
        if (!Config.collectDebris)
            return false;

        var debrisList = Location.debris.ToList();
        bool grabbed = false;

        foreach (var debris in debrisList)
        {
            Item collectible = null;

            if (debris.item is Object obj)
            {
                collectible = obj;
            }
            else if (debris.item == null && !string.IsNullOrEmpty(debris.itemId?.Value))
            {
                int chunkCount = Math.Max(1, debris.Chunks?.Count ?? 1);
                collectible = ItemRegistry.Create(debris.itemId.Value, chunkCount);
            }

            if (collectible == null)
                continue;

            if (TryAddItem(collectible))
            {
                Location.debris.Remove(debris);
                grabbed = true;
            }
        }

        return grabbed;
    }
}
