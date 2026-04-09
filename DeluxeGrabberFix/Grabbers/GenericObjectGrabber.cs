using Microsoft.Xna.Framework;
using DeluxeGrabberFix.Framework;
using StardewValley;

namespace DeluxeGrabberFix.Grabbers;

internal class GenericObjectGrabber : ObjectsMapGrabber
{
    public GenericObjectGrabber(ModEntry mod, GameLocation location)
        : base(mod, location)
    {
    }

    public override bool GrabObject(Vector2 tile, Object obj)
    {
        if (!Config.forage || !Mod.IsForageGrabEnabled || !IsGrabbable(obj))
            return false;

        bool isForage = obj.isForage();

        if (TryAddItem(isForage ? Helpers.SetForageStatsBasedOnProfession(Player, obj, tile) : obj))
        {
            Location.Objects.Remove(tile);
            Mod.GrabbedTiles?.Add(tile);
            if (isForage)
                GainExperience(2, 7);
            return true;
        }
        return false;
    }

    private bool IsGrabbable(Object obj)
    {
        if (obj.bigCraftable.Value)
            return false;

        if (obj.QualifiedItemId == ItemIds.GoldenWalnut)
            return false;

        // Reject error items from broken mod spawns (e.g., ItemExtensions
        // query IDs like RANDOM_CLUMPS that failed to resolve into real items).
        if (ItemRegistry.GetData(obj.QualifiedItemId) == null)
            return false;

        // In Specialized mode, skip ground items in barns/coops --
        // animal products are handled by AnimalProductGrabber instead.
        if (Config.grabberMode == ModConfig.GrabberMode.Specialized
            && Location is StardewValley.AnimalHouse)
            return false;

        if (obj.isForage())
            return true;

        // Collect modded spawned objects (e.g., FTM forage from Sunberry Village,
        // Alchemistry) that don't have standard forage categories.
        // The game uses isSpawnedObject — not isForage() — to determine if a
        // ground item is player-collectible (see GameLocation.checkAction).
        // Artifact spots and seed spots do NOT set this flag, so they're safe.
        if (obj.IsSpawnedObject)
            return true;

        return false;
    }
}
