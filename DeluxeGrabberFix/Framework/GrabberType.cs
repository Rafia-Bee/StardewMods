using System;

namespace DeluxeGrabberFix.Framework;

internal enum GrabberType
{
    All,
    Animal,
    Crop,
    Forage,
    Tree,
    Scavenger,
    Machine
}

internal static class GrabberTypeHelper
{
    public static GrabberType GetGrabberType(StardewValley.Object obj)
    {
        if (obj.QualifiedItemId == BigCraftableIds.AutoGrabber
            && obj.modData.TryGetValue(SpecializedGrabberPatches.ModDataGrabberType, out string typeStr)
            && Enum.TryParse(typeStr, out GrabberType type))
        {
            return type;
        }
        return GetGrabberType(obj.QualifiedItemId);
    }

    public static GrabberType GetGrabberType(string qualifiedItemId)
    {
        return qualifiedItemId switch
        {
            BigCraftableIds.AutoGrabber => GrabberType.Animal,
            BigCraftableIds.CropGrabber => GrabberType.Crop,
            BigCraftableIds.ForageGrabber => GrabberType.Forage,
            BigCraftableIds.TreeGrabber => GrabberType.Tree,
            BigCraftableIds.ScavengerGrabber => GrabberType.Scavenger,
            BigCraftableIds.MachineGrabber => GrabberType.Machine,
            _ => GrabberType.All
        };
    }

    public static bool IsGrabber(string qualifiedItemId)
    {
        return qualifiedItemId == BigCraftableIds.AutoGrabber
            || IsSpecializedGrabberItem(qualifiedItemId);
    }

    public static bool IsSpecializedGrabberItem(string qualifiedItemId)
    {
        return qualifiedItemId == BigCraftableIds.CropGrabber
            || qualifiedItemId == BigCraftableIds.ForageGrabber
            || qualifiedItemId == BigCraftableIds.TreeGrabber
            || qualifiedItemId == BigCraftableIds.ScavengerGrabber
            || qualifiedItemId == BigCraftableIds.MachineGrabber;
    }
}
