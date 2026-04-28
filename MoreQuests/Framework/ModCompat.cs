using StardewModdingAPI;

namespace MoreQuests.Framework;

internal static class ModCompat
{
    public const string LivestockFollowsYou = "RafiaBee.LivestockFollowsYou";
    public const string RidgesideVillage = "Rafseazz.RidgesideVillage";
    public const string EastScarp = "FlashShifter.EastScarpMod";
    public const string VisitMountVapius = "FlashShifter.VisitMountVapius";
    public const string StardewValleyExpanded = "FlashShifter.StardewValleyExpanded";
    public const string EliAndDylan = "Devilduke.EliandDylan";
    public const string LurkingInTheDark = "drbirbdev.LurkingInTheDark";
    public const string SiExtraCraftingMaterials = "Si.ExtraCraftingMaterials";
    public const string GenericModConfigMenu = "spacechase0.GenericModConfigMenu";
    public const string SpaceCore = "spacechase0.SpaceCore";

    public static bool IsLoaded(IModRegistry registry, string uniqueId) => registry.IsLoaded(uniqueId);

    public static bool HasRsv(IModRegistry registry) => registry.IsLoaded(RidgesideVillage);
    public static bool HasEs(IModRegistry registry) => registry.IsLoaded(EastScarp) || registry.IsLoaded(EliAndDylan) || registry.IsLoaded(LurkingInTheDark);
    public static bool HasVmv(IModRegistry registry) => registry.IsLoaded(VisitMountVapius);
    public static bool HasSve(IModRegistry registry) => registry.IsLoaded(StardewValleyExpanded);
    public static bool HasLfy(IModRegistry registry) => registry.IsLoaded(LivestockFollowsYou);
}
