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
    public const string HeatWaveRelief = "HeatWaveRelief";
    public const string TownFestival = "TownFestival";

    /// JojaMart corporate representative: Morris (vanilla) or whichever NPC the loaded
    /// Joja-side mods substitute (SVE renames Morris to MorrisTod, etc.). Used by
    /// quest rows that ask the player to bail Joja out of a supply mishap.
    public const string JojaCorpRep = "JojaCorpRep";

    /// Bulk fish buyer: shopkeepers who'd plausibly broker a large fish haul. Used by
    /// Medium Fishing Haul (CSV row 49). Pool is Pierre + the JojaCorpRep set so saves
    /// where Morris was lost to the Community Center route still get Pierre.
    public const string BulkFishBuyer = "BulkFishBuyer";

    /// Adventurers / mages who'd pay for rare monster drops. Used by Monster Parts
    /// (CSV row 53). Distinct from `CombatVendor` because the latter includes Lewis,
    /// who narratively isn't in the market for bat wings or solar essence.
    public const string MonsterPartsBuyer = "MonsterPartsBuyer";

    /// Fishermen / fishing-adjacent villagers who'd realistically commission a quality
    /// fish haul. Used by Quality Fish Delivery (CSV row 59). Pool stays Willy-anchored
    /// in vanilla saves and picks up modded fishermen (RSV's Carmen / Blair, Arumi,
    /// Gunnar) when their content packs are loaded.
    public const string FishermenNpcs = "FishermenNpcs";

    /// Blacksmiths / smelter shopkeepers who'd commission a bar order. Used by Bar
    /// Delivery (CSV row 2). Clint anchors vanilla; MarlonFay (SVE), Eli (EliAndDylan),
    /// Maryam (VMV), and Lola / Jio / Daia (RSV) join the pool when their mods are loaded.
    public const string BlacksmithNpcs = "BlacksmithNpcs";
}
