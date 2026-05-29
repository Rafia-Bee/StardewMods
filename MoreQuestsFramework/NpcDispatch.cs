using System.Collections.Generic;
using MoreQuestsFramework.Dispatch;

namespace MoreQuestsFramework;

/// Backcompat facade over IMoreQuestsApi for older consumer mods.
internal static class NpcDispatch
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

        // MagicianNPCs: ritual/offering givers. Wizard also stays in CombatNpcs above.
        registry.Register(DispatchRoles.MagicianNPCs, "Wizard");
        registry.Register(DispatchRoles.MagicianNPCs, "Lance", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.MagicianNPCs, "Vael", ModCompat.TalesOfTheAetherGlade);
        registry.Register(DispatchRoles.MagicianNPCs, "Ivaras", ModCompat.CustomNpcIvaras);
        registry.Register(DispatchRoles.MagicianNPCs, "Alecto", ModCompat.NpcAlecto);

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

        // ArchaeologyNpcs: vanilla Gunther isn't friendable (right-click opens the museum
        // donation menu instead of dialogue), so social-flow handlers never fire on him.
        // SVE swaps him for "GuntherSilvian", who is friendable.
        registry.Register(DispatchRoles.ArchaeologyNpcs, "GuntherSilvian", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.ArchaeologyNpcs, "Jasper", ModCompat.EastScarp);

        // FarmerNPCs: villagers who farm or run a farm-adjacent shop. Pierre runs the
        // seed store; Evelyn keeps her town garden; the modded entries are each farm
        // owners in their own packs. CoalPointFarm uses prefixed ids (APTAshida,
        // APTIsaacI); Ripley's id includes the author prefix (Clara.Ripley).
        registry.Register(DispatchRoles.FarmerNPCs, "Pierre");
        registry.Register(DispatchRoles.FarmerNPCs, "Evelyn");
        registry.Register(DispatchRoles.FarmerNPCs, "Andy", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.FarmerNPCs, "Sophia", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.FarmerNPCs, "Susan", ModCompat.StardewValleyExpanded);
        registry.Register(DispatchRoles.FarmerNPCs, "Jeric", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.FarmerNPCs, "Alissa", ModCompat.RidgesideVillage);
        registry.Register(DispatchRoles.FarmerNPCs, "Jessie", ModCompat.EastScarp);
        registry.Register(DispatchRoles.FarmerNPCs, "APTAshida", ModCompat.CoalPointFarm);
        registry.Register(DispatchRoles.FarmerNPCs, "APTIsaacI", ModCompat.CoalPointFarm);
        registry.Register(DispatchRoles.FarmerNPCs, "Clara.Ripley", ModCompat.Ripley);
    }
}
