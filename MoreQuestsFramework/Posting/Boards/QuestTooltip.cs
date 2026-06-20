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
                return BuildWithItemIds("deliver", ids) ?? FallbackObjective(quest);
            }
            case ItemDeliveryQuest deliver:
                return BuildWithItemIds("deliver", new[] { deliver.ItemId.Value }) ?? FallbackObjective(quest);
            case MoreQuestsShipQuest mqShip:
                return BuildWithItemIds("ship", new[] { mqShip.itemId.Value }) ?? FallbackObjective(quest);
            case FishingQuest fish:
                return BuildWithItemIds("fish", new[] { fish.ItemId.Value }) ?? FallbackObjective(quest);
            case ResourceCollectionQuest collect:
                return BuildWithItemIds("collect", new[] { collect.ItemId.Value }) ?? FallbackObjective(quest);
            case SlayMonsterQuest slay:
                return BuildLiteral("slay", new[] { slay.monsterName.Value }) ?? FallbackObjective(quest);
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

    // Returns the i18n verb-line key suffix (lowercase) for an adventure step kind.
    private static string? VerbForKind(string kind) => kind switch
    {
        nameof(AdventureStepKind.Deliver) => "deliver",
        nameof(AdventureStepKind.Gift) => "gift",
        nameof(AdventureStepKind.GiftUniqueNpcs) => "gift",
        nameof(AdventureStepKind.Catch) => "catch",
        nameof(AdventureStepKind.Slay) => "slay",
        nameof(AdventureStepKind.Ship) => "ship",
        nameof(AdventureStepKind.Plant) => "plant",
        nameof(AdventureStepKind.Collect) => "collect",
        nameof(AdventureStepKind.Craft) => "craft",
        nameof(AdventureStepKind.DropItems) => "drop",
        nameof(AdventureStepKind.DropItemsInRadius) => "drop",
        _ => null
    };

    private static string? BuildWithItemIds(string verbKey, IEnumerable<string> ids)
    {
        var names = new List<string>();
        foreach (var raw in ids)
        {
            string? name = DisplayNameForItemId(raw);
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }
        if (names.Count == 0) return null;
        return FormatLine(verbKey, OxfordJoin(names));
    }

    private static string? BuildLiteral(string verbKey, IEnumerable<string> names)
    {
        var list = names.Where(n => !string.IsNullOrEmpty(n)).ToList();
        if (list.Count == 0) return null;
        return FormatLine(verbKey, OxfordJoin(list));
    }

    // Looks up "Deliver {{items}}" etc. so translators control both the verb and word order.
    private static string FormatLine(string verbKey, string items)
        => Translate($"tooltip.line.{verbKey}", new { items });

    private static string Translate(string key, object? tokens = null)
    {
        var translation = ModEntry.Translation;
        if (translation == null) return key;
        return (tokens == null ? translation.Get(key) : translation.Get(key, tokens)).ToString();
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

    private static string CategoryName(int category)
    {
        string key = category switch
        {
            -2 => "gem",
            -4 => "fish",
            -5 => "egg",
            -6 => "milk",
            -7 => "cookedDish",
            -8 => "craftedItem",
            -12 => "mineral",
            -15 => "metalBar",
            -16 => "resource",
            -17 => "shippedItem",
            -18 => "animalProduct",
            -19 => "fertilizer",
            -20 => "junk",
            -21 => "bait",
            -22 => "tackle",
            -23 => "shell",
            -24 => "decor",
            -25 => "cookingIngredient",
            -26 => "artisanGood",
            -27 => "tapperItem",
            -28 => "monsterLoot",
            -29 => "equipment",
            -74 => "seed",
            -75 => "vegetable",
            -79 => "fruit",
            -80 => "flower",
            -81 => "forageItem",
            -95 => "hat",
            -96 => "ring",
            -98 => "weapon",
            -99 => "tool",
            _ => "item"
        };
        return Translate($"tooltip.category.{key}");
    }

    private static string OxfordJoin(IReadOnlyList<string> list) => list.Count switch
    {
        0 => string.Empty,
        1 => list[0],
        2 => Translate("tooltip.join.two", new { first = list[0], second = list[1] }),
        _ => Translate("tooltip.join.many", new { items = string.Join(", ", list.Take(list.Count - 1)), last = list[^1] })
    };

    private static string FallbackObjective(Quest quest)
    {
        return quest.currentObjective ?? string.Empty;
    }
}
