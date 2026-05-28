using StardewModdingAPI;
using StardewValley;

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
    public const string CoalPointFarm = "AndrewJC.CoalPointFarm";
    public const string Ripley = "Clara.Ripley";
    public const string SiExtraCraftingMaterials = "Si.ExtraCraftingMaterials";
    public const string GenericModConfigMenu = "spacechase0.GenericModConfigMenu";
    public const string SpaceCore = "spacechase0.SpaceCore";
    // moonslime's Cooking Skill Redux (a SpaceCore continuation of Spacechase0's
    // original mod). Custom SpaceCore skill id is "spacechase0.Cooking" for save compat.
    public const string CookingSkillRedux = "moonslime.CookingSkill";
    internal const string CookingSkillReduxSkillId = "spacechase0.Cooking";
    // b-b-blueberry's Love of Cooking, also SpaceCore-based. Registers its skill
    // under the prefixed id below (see LoveOfCooking/Objects/CookingSkill.cs).
    public const string LoveOfCooking = "blueberry.LoveOfCooking";
    internal const string LoveOfCookingSkillId = "blueberry.LoveOfCooking.CookingSkill";
    // moonslime's Archaeology Skill, also SpaceCore-based. The skill is registered
    // under "moonslime.Archaeology" (no trailing "Skill").
    public const string ArchaeologySkill = "moonslime.ArchaeologySkill";
    internal const string ArchaeologySkillId = "moonslime.Archaeology";

    public static bool IsLoaded(IModRegistry registry, string uniqueId) => registry.IsLoaded(uniqueId);

    public static bool HasRsv(IModRegistry registry) => registry.IsLoaded(RidgesideVillage);
    public static bool HasEs(IModRegistry registry) => registry.IsLoaded(EastScarp) || registry.IsLoaded(EliAndDylan) || registry.IsLoaded(LurkingInTheDark);
    public static bool HasVmv(IModRegistry registry) => registry.IsLoaded(VisitMountVapius);
    public static bool HasSve(IModRegistry registry) => registry.IsLoaded(StardewValleyExpanded);
    public static bool HasLfy(IModRegistry registry) => registry.IsLoaded(LivestockFollowsYou);

    // True when any supported cooking-skill mod is installed (Love of Cooking or
    // moonslime's CookingSkillRedux). Both store their skill in SpaceCore, so the
    // level lookup just picks the right skill id (see GetCookingLevel).
    public static bool HasCookingSkill(IModRegistry registry)
        => registry.IsLoaded(LoveOfCooking) || registry.IsLoaded(CookingSkillRedux);

    public static bool HasLoveOfCooking(IModRegistry registry) => registry.IsLoaded(LoveOfCooking);

    public static bool HasArchaeologySkill(IModRegistry registry) => registry.IsLoaded(ArchaeologySkill);

    public static int GetArchaeologyLevel(IModRegistry registry)
        => registry.IsLoaded(ArchaeologySkill)
            ? SpaceCoreSkills.GetLevel(Game1.player, ArchaeologySkillId)
            : 0;

    // Returns the current player's cooking skill level from whichever supported
    // cooking-skill mod is installed. Love of Cooking wins when both are present
    // (it's the more focused cooking overhaul of the two). Returns 0 when neither
    // is loaded so fail-closed "Cooking N" checks still fail.
    public static int GetCookingLevel(IModRegistry registry)
    {
        if (registry.IsLoaded(LoveOfCooking))
            return SpaceCoreSkills.GetLevel(Game1.player, LoveOfCookingSkillId);
        if (registry.IsLoaded(CookingSkillRedux))
            return SpaceCoreSkills.GetLevel(Game1.player, CookingSkillReduxSkillId);
        return 0;
    }
}
