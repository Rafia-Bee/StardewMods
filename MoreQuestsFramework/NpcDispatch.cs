using System.Collections.Generic;
using MoreQuestsFramework.Dispatch;

namespace MoreQuestsFramework;

/// Thin facade kept around so consumer mods that referenced the pre-Phase-5 static
/// API still compile. New code should call `IMoreQuestsApi.PickDispatchNpc(role)` /
/// `IMoreQuestsApi.GetMetHumanNpcs()` instead. Internally delegates to the runtime
/// `DispatchRegistry` instance owned by `ModEntry`.
public static class NpcDispatch
{
    public static string? Pick(string role) =>
        ModEntry.Instance.Dispatch.Pick(role);

    public static IReadOnlyList<string> All(string role) =>
        ModEntry.Instance.Dispatch.ResolvePool(role);

    public static List<string> MetHumanNpcs() => DispatchRegistry.MetHumanNpcs();

    /// Seed the framework's built-in role pools through the same registration path
    /// third parties use. Called once at GameLaunched, before consumer mods see
    /// `RegistrationOpen`.
    internal static void SeedBuiltins(DispatchRegistry registry)
    {
        // SaloonChef + SaloonFestival share the same pool (both want a saloon-side cook).
        foreach (var role in new[] { DispatchRoles.SaloonChef, DispatchRoles.SaloonFestival })
        {
            registry.Register(role, "Gus");
            registry.Register(role, "Pika", ModCompat.RidgesideVillage);
            registry.Register(role, "Rosa", ModCompat.EastScarp);
            registry.Register(role, "Celestine", ModCompat.VisitMountVapius);
        }

        registry.Register(DispatchRoles.EcologyMinded, "Demetrius");
        registry.Register(DispatchRoles.EcologyMinded, "Maddie", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.EcologyMinded, "Mr. Aguar", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.EcologyMinded, "Dylan", ModCompat.EastScarp);

        registry.Register(DispatchRoles.ConservationGuide, "Linus");
        registry.Register(DispatchRoles.ConservationGuide, "Demetrius");
        registry.Register(DispatchRoles.ConservationGuide, "Kimpoi", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.ConservationGuide, "Dylan", ModCompat.EastScarp);
        registry.Register(DispatchRoles.ConservationGuide, "Aster", ModCompat.VisitMountVapius);

        // Vanilla Marlon isn't friendable and right-clicking him opens the Adventure
        // Guild shop instead of dialogue, so OnNpcSocialized never fires. SVE replaces
        // him with the friendable "MarlonFay"; everywhere else we route slime/combat
        // quests to Wizard or Lewis.
        registry.Register(DispatchRoles.CombatVendor, "Wizard");
        registry.Register(DispatchRoles.CombatVendor, "Lewis");
        registry.Register(DispatchRoles.CombatVendor, "Abigail");
        registry.Register(DispatchRoles.CombatVendor, "MarlonFay", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.CombatVendor, "Lance", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.CombatVendor, "Mr. Aguar", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.CombatVendor, "Eli", ModCompat.EastScarp);
        registry.Register(DispatchRoles.CombatVendor, "Maryam", ModCompat.VisitMountVapius);

        registry.Register(DispatchRoles.BeachCleanup, "Elliott");
        registry.Register(DispatchRoles.BeachCleanup, "Willy");
        registry.Register(DispatchRoles.BeachCleanup, "Dylan", ModCompat.EastScarp);

        registry.Register(DispatchRoles.RainyDayFishing, "Willy");
        registry.Register(DispatchRoles.RainyDayFishing, "Blair", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.RainyDayFishing, "Carmen", ModCompat.RidgesideVillage);

        registry.Register(DispatchRoles.HeatWaveRelief, "Harvey");
        registry.Register(DispatchRoles.HeatWaveRelief, "Paula", ModCompat.RidgesideVillage);

        registry.Register(DispatchRoles.TownFestival, "Lewis");

        // JojaCorpRep: vanilla Morris is the JojaMart manager; SVE renames the same
        // character to MorrisTod (full body + dialogue + house). When SVE is loaded
        // both entries are eligible — the picker lands on whichever the save actually
        // has. Joja-route saves keep him; Community-Center-route saves lose him and
        // the role's pool collapses to zero (quest correctly stops posting).
        registry.Register(DispatchRoles.JojaCorpRep, "Morris");
        registry.Register(DispatchRoles.JojaCorpRep, "MorrisTod", ModCompat.StardewValleyExpanded);

        // BulkFishBuyer: Pierre is always in the pool (vanilla shopkeeper, never lost
        // on any save path). Morris / MorrisTod join him on Joja-route saves.
        registry.Register(DispatchRoles.BulkFishBuyer, "Pierre");
        registry.Register(DispatchRoles.BulkFishBuyer, "Morris");
        registry.Register(DispatchRoles.BulkFishBuyer, "MorrisTod", ModCompat.StardewValleyExpanded);

        // MonsterPartsBuyer: matches CSV row 53 verbatim. Wizard always available;
        // SVE adds Lance + MarlonFay (vanilla Marlon isn't friendable so we never
        // route social quests to him); Mr. Aguar (RSV); Eli (East Scarp); Maryam (VMV).
        registry.Register(DispatchRoles.MonsterPartsBuyer, "Wizard");
        registry.Register(DispatchRoles.MonsterPartsBuyer, "Abigail");
        registry.Register(DispatchRoles.MonsterPartsBuyer, "Lance", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.MonsterPartsBuyer, "MarlonFay", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.MonsterPartsBuyer, "Mr. Aguar", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.MonsterPartsBuyer, "Eli", ModCompat.EastScarp);
        registry.Register(DispatchRoles.MonsterPartsBuyer, "Maryam", ModCompat.VisitMountVapius);
    }
}
