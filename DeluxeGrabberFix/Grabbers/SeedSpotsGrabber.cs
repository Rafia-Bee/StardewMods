using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class SeedSpotsGrabber : ObjectsMapGrabber
{
    public SeedSpotsGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.seedSpots || obj.QualifiedItemId != ItemIds.SeedSpot)
            return false;

        Item seed = Utility.getRaccoonSeedForCurrentTimeOfYear(
            Game1.player,
            Utility.CreateDaySaveRandom(-tile.X * 7f, tile.Y * 777f, Game1.netWorldState.Value.TreasureTotemsUsed * 777),
            -1);

        if (seed != null && TryAddItem(seed))
        {
            Location.Objects.Remove(tile);
            Mod.GrabbedTiles?.Add(tile);
            return true;
        }
        return false;
    }
}
