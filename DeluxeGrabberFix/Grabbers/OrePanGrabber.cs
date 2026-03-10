using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace DeluxeGrabberFix.Grabbers;

internal class OrePanGrabber : MapGrabber
{
    private static readonly string[] GemIds =
    {
        Object.emeraldQID, Object.aquamarineQID, Object.rubyQID,
        Object.amethystClusterQID, Object.topazQID
    };

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
        string oreType = Object.copperQID;
        string secondaryItem = null;

        Random random = new(Location.orePanPoint.X + Location.orePanPoint.Y * 1000 + (int)Game1.stats.DaysPlayed);
        double roll = random.NextDouble() - Player.team.AverageLuckLevel() * 0.001 - Player.team.AverageDailyLuck();

        if (roll < 0.01)
            oreType = Object.iridiumQID;
        else if (roll < 0.241)
            oreType = Object.goldQID;
        else if (roll < 0.6)
            oreType = Object.ironQID;

        int oreCount = random.Next(5) + 1 + (int)((random.NextDouble() + 0.1 + Player.team.AverageLuckLevel() / 10f + Player.team.AverageDailyLuck()) * 2.0);
        int secondaryCount = random.Next(5) + 1 + (int)((random.NextDouble() + 0.1 + Player.team.AverageLuckLevel() / 10f) * 2.0);

        roll = random.NextDouble() - Player.team.AverageDailyLuck();
        if (roll < 0.4 + Player.team.AverageLuckLevel() * 0.04)
        {
            roll = random.NextDouble() - Player.team.AverageDailyLuck();
            secondaryItem = Object.coalQID;

            if (roll < 0.02 + Player.team.AverageLuckLevel() * 0.002)
            {
                secondaryItem = Object.diamondQID;
                secondaryCount = 1;
            }
            else if (roll < 0.1)
            {
                secondaryItem = GemIds[random.Next(GemIds.Length)];
                secondaryCount = 1;
            }
            else if (roll < 0.36)
            {
                secondaryItem = ItemIds.OmniGeode;
                secondaryCount = Math.Max(1, secondaryCount / 2);
            }
            else if (roll < 0.5)
            {
                secondaryItem = random.NextDouble() < 0.3 ? ItemIds.FireQuartz : (random.NextDouble() < 0.5 ? ItemIds.FrozenTear : ItemIds.EarthCrystal);
                secondaryCount = 1;
            }

            if (roll < Player.team.AverageLuckLevel() * 0.002)
            {
                items.Add(ItemRegistry.Create(ItemIds.LuckyRing));
            }
        }

        items.Add(ItemRegistry.Create<Object>(oreType, oreCount));
        if (secondaryItem != null)
            items.Add(ItemRegistry.Create<Object>(secondaryItem, secondaryCount));

        if (Location is IslandNorth islandNorth && islandNorth.bridgeFixed.Value && random.NextDouble() < 0.2)
        {
            items.Add(ItemRegistry.Create<Object>(ItemIds.DragonTooth));
        }
        else if (Location is IslandLocation && random.NextDouble() < 0.2)
        {
            items.Add(ItemRegistry.Create<Object>(ItemIds.TaroTuber, random.Next(2, 6)));
        }

        if (TryAddItems(items))
        {
            Location.orePanPoint.Value = Point.Zero;
            return true;
        }
        return false;
    }
}
