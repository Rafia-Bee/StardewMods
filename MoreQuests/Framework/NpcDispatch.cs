using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework;

/// Resolves which NPC posts a quest based on installed mods. Vanilla quest-givers always work; modded ones only
/// fall through if their host mod is loaded. Caller passes the dispatch role and the resolver picks the best match.
internal static class NpcDispatch
{
    public enum Role
    {
        SaloonChef,
        EcologyMinded,
        ConservationGuide,
        CombatVendor,
        SaloonFestival,
        BeachCleanup,
        RainyDayFishing,
        HeatWaveRelief,
        TownFestival
    }

    public static string? Pick(IModRegistry registry, Role role)
    {
        // Prefer NPCs the player has actually met (so quest dialogue mechanics work)
        // and fall back to "exists in the world" only if nobody in the role is met yet.
        var built = Build(registry, role).Where(IsKnownNpc).ToList();
        var met = built.Where(IsSocializable).ToList();
        var pool = met.Count > 0 ? met : built;
        if (pool.Count == 0)
            return null;
        return pool[Game1.random.Next(pool.Count)];
    }

    public static IEnumerable<string> All(IModRegistry registry, Role role) =>
        Build(registry, role).Where(IsKnownNpc);

    private static IEnumerable<string> Build(IModRegistry registry, Role role)
    {
        bool rsv = ModCompat.HasRsv(registry);
        bool es = ModCompat.HasEs(registry);
        bool vmv = ModCompat.HasVmv(registry);
        bool sve = ModCompat.HasSve(registry);

        switch (role)
        {
            case Role.SaloonChef:
            case Role.SaloonFestival:
                yield return "Gus";
                if (rsv) yield return "Pika";
                if (es) yield return "Rosa";
                if (vmv) yield return "Celestine";
                yield break;

            case Role.EcologyMinded:
                yield return "Demetrius";
                if (rsv) { yield return "Maddie"; yield return "Mr. Aguar"; }
                if (es) yield return "Dylan";
                yield break;

            case Role.ConservationGuide:
                yield return "Linus";
                yield return "Demetrius";
                if (rsv) yield return "Kimpoi";
                if (es) yield return "Dylan";
                if (vmv) yield return "Aster";
                yield break;

            case Role.CombatVendor:
                // Vanilla Marlon isn't friendable and right-clicking him opens the Adventure
                // Guild shop instead of dialogue, so OnNpcSocialized never fires. SVE replaces
                // him with the friendable "MarlonFay"; everywhere else we route slime/combat
                // quests to Wizard or Lewis (matching how vanilla SlayMonsterQuest assigns them).
                yield return "Wizard";
                yield return "Lewis";
                yield return "Abigail";
                if (sve) yield return "MarlonFay";
                if (sve) yield return "Lance";
                if (rsv) yield return "Mr. Aguar";
                if (es) yield return "Eli";
                if (vmv) yield return "Maryam";
                yield break;

            case Role.BeachCleanup:
                yield return "Elliott";
                yield return "Willy";
                if (es) yield return "Dylan";
                yield break;

            case Role.RainyDayFishing:
                yield return "Willy";
                if (rsv) { yield return "Blair"; yield return "Carmen"; }
                yield break;

            case Role.HeatWaveRelief:
                yield return "Harvey";
                if (rsv) yield return "Paula";
                yield break;

            case Role.TownFestival:
                yield return "Lewis";
                yield break;
        }
    }

    private static bool IsKnownNpc(string name) => Game1.getCharacterFromName(name) != null;

    /// True if the player has friendship data for this NPC, i.e. they've met them and
    /// the NPC is one that participates in the normal social/dialogue flow. Filters out
    /// shopkeepers like vanilla Marlon whose right-click opens a shop instead of a dialogue.
    private static bool IsSocializable(string name) =>
        Game1.player.friendshipData.ContainsKey(name);

    public static List<string> MetHumanNpcs()
    {
        var results = new List<string>();
        foreach (var (name, _) in Game1.player.friendshipData.Pairs)
        {
            var npc = Game1.getCharacterFromName(name);
            if (npc == null || npc.IsMonster)
                continue;
            if (!npc.IsVillager)
                continue;
            results.Add(name);
        }
        return results;
    }
}
