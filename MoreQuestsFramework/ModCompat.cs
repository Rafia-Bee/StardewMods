using StardewModdingAPI;

namespace MoreQuestsFramework;

public static class ModCompat
{
    public const string LivestockFollowsYou = "RafiaBee.LivestockFollowsYou";
    public const string RidgesideVillage = "Rafseazz.RidgesideVillage";
    public const string EastScarp = "Lemurkat.EastScarpNPCs";
    public const string VisitMountVapius = "Lumisteria.MtVapius";
    // Must be SVECode (the C# mod that places SVE NPCs), NOT StardewValleyExpanded
    // (matches neither install ID and silently drops every SVE-gated entry).
    public const string StardewValleyExpanded = "FlashShifter.SVECode";
    public const string EliAndDylan = "TenebrousNova.EliDylan.CP";

    public const string LurkingInTheDark = "7thAxis.LitD.CP";
    public const string AikawaAsakaiCP = "NassiLove.AikawaAsakaiCP";
    public const string HashtagBearFamGunnar = "MadDog.HashtagBearFam";
    public const string SiExtraCraftingMaterials = "Si.ExtraCraftingMaterials";
    public const string GenericModConfigMenu = "spacechase0.GenericModConfigMenu";
    public const string SpaceCore = "spacechase0.SpaceCore";
    // Custom SpaceCore skill id is "spacechase0.Cooking" (inherited from the original
    // unmaintained Spacechase0 mod for save compat).
    public const string CookingSkillRedux = "moonslime.CookingSkill";

    public static bool IsLoaded(IModRegistry registry, string uniqueId) => registry.IsLoaded(uniqueId);

    public static bool HasRsv(IModRegistry registry) => registry.IsLoaded(RidgesideVillage);
    public static bool HasEs(IModRegistry registry) => registry.IsLoaded(EastScarp) || registry.IsLoaded(EliAndDylan) || registry.IsLoaded(LurkingInTheDark);
    public static bool HasVmv(IModRegistry registry) => registry.IsLoaded(VisitMountVapius);
    public static bool HasSve(IModRegistry registry) => registry.IsLoaded(StardewValleyExpanded);
    public static bool HasLfy(IModRegistry registry) => registry.IsLoaded(LivestockFollowsYou);
    public static bool HasCookingSkill(IModRegistry registry) => registry.IsLoaded(CookingSkillRedux);
}
