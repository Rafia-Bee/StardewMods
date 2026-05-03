namespace MoreQuestsFramework.Dispatch;

/// String role keys for NPC dispatch. Authors can add new roles by passing any
/// other string to `RegisterDispatchNpc` / `PickDispatchNpc`; the framework only
/// hard-codes these as a convenience for the built-in quest categories.
public static class DispatchRoles
{
    public const string SaloonChef = "SaloonChef";
    public const string EcologyMinded = "EcologyMinded";
    public const string ConservationGuide = "ConservationGuide";
    public const string CombatVendor = "CombatVendor";
    public const string SaloonFestival = "SaloonFestival";
    public const string BeachCleanup = "BeachCleanup";
    public const string RainyDayFishing = "RainyDayFishing";
    public const string HeatWaveRelief = "HeatWaveRelief";
    public const string TownFestival = "TownFestival";

    /// JojaMart corporate representative: Morris (vanilla) or whichever NPC the loaded
    /// Joja-side mods substitute (SVE renames Morris to MorrisTod, etc.). Used by
    /// quest rows that ask the player to bail Joja out of a supply mishap.
    public const string JojaCorpRep = "JojaCorpRep";
}
