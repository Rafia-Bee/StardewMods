using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class SlimeHutchGrabber : ObjectsMapGrabber
{
    private readonly Func<Object, Vector2, GameLocation, KeyValuePair<Object, int>> GetSlimeHarvest;

    public SlimeHutchGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
        GetSlimeHarvest = Mod.Api.GetSlimeHarvest ?? DefaultGetSlimeHarvest;
    }

    private KeyValuePair<Object, int> DefaultGetSlimeHarvest(Object slimeBall, Vector2 tile, GameLocation location)
    {
        return new KeyValuePair<Object, int>(slimeBall, 0);
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.slimeHutch || obj.Name is not "Slime Ball")
            return false;

        var harvest = GetSlimeHarvest(obj, tile, Location);
        if (harvest.Key != obj)
        {
            // API override returned a custom item
            if (TryAddItem(harvest.Key))
            {
                GainExperience(0, harvest.Value);
                Location.Objects.Remove(tile);
                Mod.GrabbedTiles?.Add(tile);
                return true;
            }
            return false;
        }

        // Default slime ball drop logic
        var items = new List<Object>();
        Random random = new((int)Game1.stats.DaysPlayed + (int)Game1.uniqueIDForThisGame + (int)tile.X * 77 + (int)tile.Y * 777 + 2);

        int slimeCount = random.Next(10, 21);
        items.Add(ItemRegistry.Create<Object>(ItemIds.Slime, slimeCount));

        int petrifiedCount = 0;
        while (random.NextDouble() < 0.33)
            petrifiedCount++;

        if (petrifiedCount > 0)
            items.Add(ItemRegistry.Create<Object>(ItemIds.PetrifiedSlime, petrifiedCount));

        if (TryAddItems((IEnumerable<Item>)items))
        {
            Location.Objects.Remove(tile);
            Mod.GrabbedTiles?.Add(tile);
            return true;
        }
        return false;
    }
}
