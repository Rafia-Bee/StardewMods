using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace DeluxeGrabberFix.Grabbers;

internal class OrePanGrabber : MapGrabber
{
    public OrePanGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabItems()
    {
        if (!Config.orePan)
            return false;

        Point panPoint = Location.orePanPoint.Value;
        if (panPoint.Equals(Point.Zero))
            return false;

        var items = new List<Item>();
        int oreType = 378;
        int secondaryItem = -1;

        Random random = new(Location.orePanPoint.X + Location.orePanPoint.Y * 1000 + (int)Game1.stats.DaysPlayed);
        double roll = random.NextDouble() - Player.team.AverageLuckLevel() * 0.001 - Player.team.AverageDailyLuck();

        if (roll < 0.01)
            oreType = 386;
        else if (roll < 0.241)
            oreType = 384;
        else if (roll < 0.6)
            oreType = 380;

        int oreCount = random.Next(5) + 1 + (int)((random.NextDouble() + 0.1 + Player.team.AverageLuckLevel() / 10f + Player.team.AverageDailyLuck()) * 2.0);
        int secondaryCount = random.Next(5) + 1 + (int)((random.NextDouble() + 0.1 + Player.team.AverageLuckLevel() / 10f) * 2.0);

        roll = random.NextDouble() - Player.team.AverageDailyLuck();
        if (roll < 0.4 + Player.team.AverageLuckLevel() * 0.04)
        {
            roll = random.NextDouble() - Player.team.AverageDailyLuck();
            secondaryItem = 382;

            if (roll < 0.02 + Player.team.AverageLuckLevel() * 0.002)
            {
                secondaryItem = 72;
                secondaryCount = 1;
            }
            else if (roll < 0.1)
            {
                secondaryItem = 60 + random.Next(5) * 2;
                secondaryCount = 1;
            }
            else if (roll < 0.36)
            {
                secondaryItem = 749;
                secondaryCount = Math.Max(1, secondaryCount / 2);
            }
            else if (roll < 0.5)
            {
                secondaryItem = random.NextDouble() < 0.3 ? 82 : (random.NextDouble() < 0.5 ? 84 : 86);
                secondaryCount = 1;
            }

            if (roll < Player.team.AverageLuckLevel() * 0.002)
            {
                items.Add(new Ring(859.ToString()));
            }
        }

        items.Add(ItemRegistry.Create<Object>(oreType.ToString(), oreCount));
        if (secondaryItem != -1)
            items.Add(ItemRegistry.Create<Object>(secondaryItem.ToString(), secondaryCount));

        if (Location is IslandNorth islandNorth && islandNorth.bridgeFixed.Value && random.NextDouble() < 0.2)
        {
            items.Add(ItemRegistry.Create<Object>(822.ToString()));
        }
        else if (Location is IslandLocation && random.NextDouble() < 0.2)
        {
            items.Add(ItemRegistry.Create<Object>(831.ToString(), random.Next(2, 6)));
        }

        if (TryAddItems(items))
        {
            Location.orePanPoint.Value = Point.Zero;
            return true;
        }
        return false;
    }
}
