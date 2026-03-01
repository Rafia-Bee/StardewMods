using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace DeluxeGrabberFix.Grabbers;

internal class ForageHoeDirtGrabber : TerrainFeaturesMapGrabber
{
    public ForageHoeDirtGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabFeature(Vector2 tile, TerrainFeature feature)
    {
        if (feature is not HoeDirt dirt || !IsForageableHoeDirt(feature))
            return false;

        Object forage = null;

        if (dirt.crop.whichForageCrop.Value == "1")
        {
            forage = Helpers.SetForageStatsBasedOnProfession(Player, ItemRegistry.Create<Object>(399.ToString()), tile, ignoreGatherer: true);
        }
        else if (dirt.crop.whichForageCrop.Value == "2")
        {
            forage = ItemRegistry.Create<Object>(829.ToString());
        }

        if (forage != null && TryAddItem(forage))
        {
            if (dirt.crop.whichForageCrop.Value == "1")
                GainExperience(2, 3);

            dirt.destroyCrop(false);
            return true;
        }
        return false;
    }

    private bool IsForageableHoeDirt(TerrainFeature feature)
    {
        return feature is HoeDirt dirt
            && dirt.crop != null
            && dirt.crop.forageCrop.Value;
    }
}
