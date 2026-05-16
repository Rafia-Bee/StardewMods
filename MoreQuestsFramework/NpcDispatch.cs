using System.Collections.Generic;
using MoreQuestsFramework.Dispatch;

namespace MoreQuestsFramework;

/// Backcompat facade over IMoreQuestsApi for older consumer mods.
public static class NpcDispatch
{
    public static string? Pick(string role) =>
        ModEntry.Instance.Dispatch.Pick(role);

    public static IReadOnlyList<string> All(string role) =>
        ModEntry.Instance.Dispatch.ResolvePool(role);

    public static List<string> MetHumanNpcs() => DispatchRegistry.MetHumanNpcs();

    internal static void SeedBuiltins(DispatchRegistry registry)
    {
        // SaloonChef + SaloonFestival share the same pool.
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
        registry.Register(DispatchRoles.EcologyMinded, "Dylan", ModCompat.EliAndDylan);

        registry.Register(DispatchRoles.ConservationGuide, "Linus");
        registry.Register(DispatchRoles.ConservationGuide, "Demetrius");
        registry.Register(DispatchRoles.ConservationGuide, "Kimpoi", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.ConservationGuide, "Dylan", ModCompat.EliAndDylan);
        registry.Register(DispatchRoles.ConservationGuide, "Aster", ModCompat.VisitMountVapius);

        // CombatNpcs: vanilla Marlon isn't friendable (right-click opens Adventure Guild
        // shop instead of dialogue), so social-flow handlers never fire on him.
        registry.Register(DispatchRoles.CombatNpcs, "Wizard");
        registry.Register(DispatchRoles.CombatNpcs, "MarlonFay", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.CombatNpcs, "Lance", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.CombatNpcs, "Mr. Aguar", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.CombatNpcs, "Jio", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.CombatNpcs, "Daia", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.CombatNpcs, "Eli", ModCompat.EliAndDylan);
        registry.Register(DispatchRoles.CombatNpcs, "Mariam", ModCompat.VisitMountVapius);

        registry.Register(DispatchRoles.BeachCleanup, "Elliott");
        registry.Register(DispatchRoles.BeachCleanup, "Willy");
        registry.Register(DispatchRoles.BeachCleanup, "Dylan", ModCompat.EliAndDylan);
        registry.Register(DispatchRoles.BeachCleanup, "Arumi", ModCompat.AikawaAsakaiCP);

        registry.Register(DispatchRoles.HeatWaveRelief, "Harvey");
        registry.Register(DispatchRoles.HeatWaveRelief, "Maru");
        registry.Register(DispatchRoles.HeatWaveRelief, "Paula", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.HeatWaveRelief, "Philip", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.HeatWaveRelief, "Jacob", ModCompat.EastScarp);

        registry.Register(DispatchRoles.TownFestival, "Lewis");

        // JojaCorpRep: pool collapses to zero on Community-Center-route saves (Morris
        // gone, MorrisTod only present with SVE), so the quest correctly stops posting.
        registry.Register(DispatchRoles.JojaCorpRep, "Morris");
        registry.Register(DispatchRoles.JojaCorpRep, "MorrisTod", ModCompat.StardewValleyExpanded);

        registry.Register(DispatchRoles.BulkFishBuyer, "Pierre");
        registry.Register(DispatchRoles.BulkFishBuyer, "Morris");
        registry.Register(DispatchRoles.BulkFishBuyer, "MorrisTod", ModCompat.StardewValleyExpanded);

        registry.Register(DispatchRoles.FishermenNpcs, "Willy");
        registry.Register(DispatchRoles.FishermenNpcs, "Pam");
        registry.Register(DispatchRoles.FishermenNpcs, "Elliott");
        registry.Register(DispatchRoles.FishermenNpcs, "Carmen", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.FishermenNpcs, "Blair", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.FishermenNpcs, "Arumi", ModCompat.AikawaAsakaiCP);
        registry.Register(DispatchRoles.FishermenNpcs, "Gunnar", ModCompat.HashtagBearFamGunnar);

        registry.Register(DispatchRoles.BlacksmithNpcs, "Clint");
        registry.Register(DispatchRoles.BlacksmithNpcs, "MarlonFay", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.BlacksmithNpcs, "Eli", ModCompat.EliAndDylan);
        registry.Register(DispatchRoles.BlacksmithNpcs, "Mariam", ModCompat.VisitMountVapius);
        registry.Register(DispatchRoles.BlacksmithNpcs, "Lola", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.BlacksmithNpcs, "Jio", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.BlacksmithNpcs, "Daia", ModCompat.RidgesideVillage);
    }
}
