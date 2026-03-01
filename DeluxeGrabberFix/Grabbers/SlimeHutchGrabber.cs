using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class SlimeHutchGrabber : ObjectsMapGrabber
{
    public SlimeHutchGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.slimeHutch || obj.Name != "Slime Ball")
            return false;

        var items = new List<Object>();
        Random random = new((int)Game1.stats.DaysPlayed + (int)Game1.uniqueIDForThisGame + (int)tile.X * 77 + (int)tile.Y * 777 + 2);

        int slimeCount = random.Next(10, 21);
        items.Add(ItemRegistry.Create<Object>(766.ToString(), slimeCount));

        int petrifiedCount = 0;
        while (random.NextDouble() < 0.33)
            petrifiedCount++;

        items.Add(ItemRegistry.Create<Object>(557.ToString(), petrifiedCount));

        if (TryAddItems((IEnumerable<Item>)items))
        {
            Location.Objects.Remove(tile);
            return true;
        }
        return false;
    }
}
