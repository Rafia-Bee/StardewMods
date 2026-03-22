using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class GreenRainWeedGrabber : MapGrabber
{
    public GreenRainWeedGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabItems()
    {
        if (!Config.harvestGreenRainWeeds)
            return false;

        if (Location.resourceClumps.Count == 0)
            return false;

        bool result = false;

        for (int i = Location.resourceClumps.Count - 1; i >= 0; i--)
        {
            ResourceClump clump = Location.resourceClumps[i];

            if (!clump.IsGreenRainBush())
                continue;

            Vector2 tile = clump.Tile;
            var items = new List<Item>();

            Random random = Utility.CreateRandom(
                Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed,
                (double)tile.X * 7.0, (double)tile.Y * 11.0);

            items.Add(ItemRegistry.Create(ItemIds.Moss, random.Next(2, 4)));
            items.Add(ItemRegistry.Create(ItemIds.Fiber, random.Next(2, 4)));

            if (random.NextDouble() < 0.05)
                items.Add(ItemRegistry.Create(ItemIds.MossySeed));

            if (TryAddItems((IEnumerable<Item>)items))
            {
                Location.resourceClumps.RemoveAt(i);
                GainExperience(Skills.Foraging, 15);
                result = true;
            }
        }

        return result;
    }
}
