using StardewModdingAPI;

namespace MoreQuestsFramework;

public static class ModCompat
{
    public const string LivestockFollowsYou = "RafiaBee.LivestockFollowsYou";
    public const string RidgesideVillage = "Rafseazz.RidgesideVillage";
    public const string EastScarp = "FlashShifter.EastScarpMod";
    public const string VisitMountVapius = "FlashShifter.VisitMountVapius";
    /// SVE ships as two mods: a C# mod (`FlashShifter.SVECode`) plus a CP content pack
    /// (`FlashShifter.StardewValleyExpandedCP`). The C# mod is the authoritative one,
    /// it's what places SVE NPCs (MarlonFay, MorrisTod, Lance, etc.) into the world.
    /// Earlier versions of this constant used `FlashShifter.StardewValleyExpanded` which
    /// matches neither install ID and silently dropped every SVE-gated dispatch entry.
    public const string StardewValleyExpanded = "FlashShifter.SVECode";
    public const string EliAndDylan = "Devilduke.EliandDylan";

    public const string LurkingInTheDark = "7thAxis.LitD.CP";
    public const string AikawaAsakaiCP = "NassiLove.AikawaAsakaiCP";
    public const string HashtagBearFamGunnar = "MadDog.HashtagBearFam.Gunnar";
    public const string SiExtraCraftingMaterials = "Si.ExtraCraftingMaterials";
    public const string GenericModConfigMenu = "spacechase0.GenericModConfigMenu";
    public const string SpaceCore = "spacechase0.SpaceCore";
    /// Moonslime's Cooking Skill Redux. Adds a SpaceCore custom skill with id
    /// `spacechase0.Cooking` (inherited from Spacechase0's original mod for save
    /// compatibility, even though the original is unmaintained and not supported here).
    public const string CookingSkillRedux = "moonslime.CookingSkill";

    public static bool IsLoaded(IModRegistry registry, string uniqueId) => registry.IsLoaded(uniqueId);

    public static bool HasRsv(IModRegistry registry) => registry.IsLoaded(RidgesideVillage);
    public static bool HasEs(IModRegistry registry) => registry.IsLoaded(EastScarp) || registry.IsLoaded(EliAndDylan) || registry.IsLoaded(LurkingInTheDark);
    public static bool HasVmv(IModRegistry registry) => registry.IsLoaded(VisitMountVapius);
    public static bool HasSve(IModRegistry registry) => registry.IsLoaded(StardewValleyExpanded);
    public static bool HasLfy(IModRegistry registry) => registry.IsLoaded(LivestockFollowsYou);
    public static bool HasCookingSkill(IModRegistry registry) => registry.IsLoaded(CookingSkillRedux);
}
