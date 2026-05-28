using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Quests;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Posting.Boards;

// Builds the billboard hover-tooltip body line for a posting. Reads the quest's
// requested item(s) and prefixes a verb based on the quest type ("Deliver", "Ship",
// "Fish", "Slay", "Collect", etc.). Falls back to the quest's currentObjective when
// the quest type has no natural item list (Talk, Visit, Build, ReachLevel, etc.).
internal static class QuestTooltip
{
    public static string BodyFor(Quest? quest)
    {
        if (quest == null) return string.Empty;

        switch (quest)
        {
            case MoreQuestsItemDeliveryQuest mqDeliver:
            {
                var ids = new List<string>();
                if (!string.IsNullOrEmpty(mqDeliver.ItemId.Value))
                    ids.Add(mqDeliver.ItemId.Value);
                foreach (var alt in mqDeliver.alternativeItemIds)
                {
                    if (!string.IsNullOrEmpty(alt))
                        ids.Add(alt);
                }
                return BuildWithItemIds("Deliver", ids) ?? FallbackObjective(quest);
            }
            case ItemDeliveryQuest deliver:
                return BuildWithItemIds("Deliver", new[] { deliver.ItemId.Value }) ?? FallbackObjective(quest);
            case MoreQuestsShipQuest mqShip:
                return BuildWithItemIds("Ship", new[] { mqShip.itemId.Value }) ?? FallbackObjective(quest);
            case FishingQuest fish:
                return BuildWithItemIds("Fish", new[] { fish.ItemId.Value }) ?? FallbackObjective(quest);
            case ResourceCollectionQuest collect:
                return BuildWithItemIds("Collect", new[] { collect.ItemId.Value }) ?? FallbackObjective(quest);
            case SlayMonsterQuest slay:
                return BuildLiteral("Slay", new[] { slay.monsterName.Value }) ?? FallbackObjective(quest);
            case AdventureQuest adv:
                return BuildFromAdventure(adv) ?? FallbackObjective(quest);
        }

        return FallbackObjective(quest);
    }

    private static string? BuildFromAdventure(AdventureQuest adv)
    {
        var infos = adv.BuildStepInfos();
        for (int i = 0; i < infos.Count; i++)
        {
            var step = infos[i];
            if (step.Done || !step.Active) continue;
            string? verb = VerbForKind(step.Kind);
            if (verb == null) return null;
            // Slay step holds monster names (literal strings), not item ids.
            if (step.Kind == nameof(AdventureStepKind.Slay))
                return BuildLiteral(verb, step.Items);
            return BuildWithItemIds(verb, step.Items);
        }
        return null;
    }

    private static string? VerbForKind(string kind) => kind switch
    {
        nameof(AdventureStepKind.Deliver) => "Deliver",
        nameof(AdventureStepKind.Gift) => "Gift",
        nameof(AdventureStepKind.GiftUniqueNpcs) => "Gift",
        nameof(AdventureStepKind.Catch) => "Catch",
        nameof(AdventureStepKind.Slay) => "Slay",
        nameof(AdventureStepKind.Ship) => "Ship",
        nameof(AdventureStepKind.Plant) => "Plant",
        nameof(AdventureStepKind.Collect) => "Collect",
        nameof(AdventureStepKind.DropItems) => "Drop",
        _ => null
    };

    private static string? BuildWithItemIds(string verb, IEnumerable<string> ids)
    {
        var names = new List<string>();
        foreach (var raw in ids)
        {
            string? name = DisplayNameForItemId(raw);
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }
        if (names.Count == 0) return null;
        return $"{verb} {OxfordJoin(names)}";
    }

    private static string? BuildLiteral(string verb, IEnumerable<string> names)
    {
        var list = names.Where(n => !string.IsNullOrEmpty(n)).ToList();
        if (list.Count == 0) return null;
        return $"{verb} {OxfordJoin(list)}";
    }

    // Category sentinels (e.g. "-4" for fish) get a friendly name; real item ids resolve
    // through ItemRegistry.
    private static string? DisplayNameForItemId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (int.TryParse(id, out int n) && n < 0)
            return CategoryName(n);
        try
        {
            var data = ItemRegistry.GetData(id);
            if (data == null) return null;
            string display = data.DisplayName;
            if (string.IsNullOrEmpty(display) || display.Contains("Error", System.StringComparison.OrdinalIgnoreCase))
                return null;
            return display;
        }
        catch
        {
            return null;
        }
    }

    private static string CategoryName(int category) => category switch
    {
        -2 => "gem",
        -4 => "fish",
        -5 => "egg",
        -6 => "milk",
        -7 => "cooked dish",
        -8 => "crafted item",
        -12 => "mineral",
        -15 => "metal bar",
        -16 => "resource",
        -17 => "shipped item",
        -18 => "animal product",
        -19 => "fertilizer",
        -20 => "junk",
        -21 => "bait",
        -22 => "tackle",
        -23 => "shell",
        -24 => "decor",
        -25 => "cooking ingredient",
        -26 => "artisan good",
        -27 => "tapper item",
        -28 => "monster loot",
        -29 => "equipment",
        -74 => "seed",
        -75 => "vegetable",
        -79 => "fruit",
        -80 => "flower",
        -81 => "forage item",
        -95 => "hat",
        -96 => "ring",
        -98 => "weapon",
        -99 => "tool",
        _ => "item"
    };

    private static string OxfordJoin(IReadOnlyList<string> list) => list.Count switch
    {
        0 => string.Empty,
        1 => list[0],
        2 => $"{list[0]} and {list[1]}",
        _ => string.Join(", ", list.Take(list.Count - 1)) + ", and " + list[^1]
    };

    private static string FallbackObjective(Quest quest)
    {
        return quest.currentObjective ?? string.Empty;
    }
}
