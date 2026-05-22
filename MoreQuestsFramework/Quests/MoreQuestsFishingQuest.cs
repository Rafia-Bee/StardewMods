using System;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

// Vanilla FishingQuest.OnNpcSocialized completes as soon as numberFished is met and
// the player chats with the target, NEVER consuming the fish, so the player can sell
// every fish and still claim the reward. This subclass requires the fish to be in
// inventory at turn-in and removes the stack on completion.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_FishingQuest")]
public sealed class MoreQuestsFishingQuest : FishingQuest, IRewardedQuest
{
    public readonly NetStringList serializedRewards = new();

    // Empty/zero disables each gate.
    public readonly NetString catchLocationName = new();
    public readonly NetInt catchMinSize = new();
    public readonly NetInt catchMaxSize = new();
    public readonly NetString catchWeather = new();

    // True = counter-only (any catch passing filters counts, no specific stack needed
    // at turn-in, no fish consumed). Used by Size Overpopulation.
    public readonly NetBool catchAnyFish = new();

    // Overrides vanilla's reloadObjective rebuild for catchAnyFish quests.
    // {0} = catch counter, {1} = quota.
    public readonly NetString catchProgressTemplate = new();

    // True = report-back, not a delivery. Even though the fish is in inventory, the
    // narrative is "show the catch / tell the story" (legendary fish, ecology reports).
    // Suppresses the "Deliver X to Y" post-catch override so the journal stays on
    // vanilla "Return to <npc>".
    public readonly NetBool isReportBack = new();

    public NetStringList SerializedRewards => serializedRewards;

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(serializedRewards, "serializedRewards")
            .AddField(catchLocationName, "catchLocationName")
            .AddField(catchMinSize, "catchMinSize")
            .AddField(catchMaxSize, "catchMaxSize")
            .AddField(catchWeather, "catchWeather")
            .AddField(catchAnyFish, "catchAnyFish")
            .AddField(catchProgressTemplate, "catchProgressTemplate")
            .AddField(isReportBack, "isReportBack");
    }

    public override bool OnFishCaught(string fishId, int numberCaught, int size, bool probe = false)
    {
        if (!CatchFiltersPass(fishId, size))
            return false;
        if (catchAnyFish.Value)
        {
            // Bypass base's fishId==ItemId.Value gate; mirrors the matched-fish path.
            if (numberFished.Value >= numberToFish.Value)
                return false;
            if (probe)
                return true;
            numberFished.Value = Math.Min(numberToFish.Value, numberFished.Value + numberCaught);
            Game1.dayTimeMoneyBox.pingQuest(this);
            if (numberFished.Value >= numberToFish.Value)
            {
                NPC characterFromName = Game1.getCharacterFromName(target.Value);
                objective.Value = new DescriptionElement(
                    "Strings\\Quests:ObjectiveReturnToNPC",
                    characterFromName);
                Game1.playSound("jingle1");
            }
            // Mirrors AdventureQuest.OnFishCaught: return false so any other fishing
            // quest in the log still gets to see this catch.
            return false;
        }
        bool wasComplete = numberFished.Value >= numberToFish.Value;
        bool result = base.OnFishCaught(fishId, numberCaught, size, probe);
        // Base sets "Return to <npc>" the moment the quota fills. For real delivery
        // quests (no location filter), swap to a clearer "Deliver X to Y" line.
        // Location-filtered quests (ecological pop control) stay on the vanilla
        // "Return to" line since they're narratively report-backs, not deliveries.
        if (!probe
            && !wasComplete
            && numberFished.Value >= numberToFish.Value
            && !string.IsNullOrEmpty(target.Value)
            && IsDeliveryStyle())
        {
            currentObjective = BuildDeliveryObjective();
        }
        return result;
    }

    // True for the simple "catch N, hand them over" quests. False for ecology / report
    // quests (size overpop = catchAnyFish; location overpop = location-filtered) and
    // explicitly report-back quests like the legendary fish chase.
    private bool IsDeliveryStyle()
    {
        return !catchAnyFish.Value
            && string.IsNullOrEmpty(catchLocationName.Value)
            && !isReportBack.Value;
    }

    private string BuildDeliveryObjective()
    {
        string npcDisplay = NpcDisplay.Resolve(target.Value);
        int qty = numberToFish.Value;
        string itemName = ResolveItemDisplayName();
        var t = ModEntry.Translation?.Get("quest.fishing.deliverAfterCatch", new { qty, item = itemName, npc = npcDisplay });
        return t?.ToString() ?? $"Deliver {qty} {itemName} to {npcDisplay}.";
    }

    private string ResolveItemDisplayName()
    {
        if (string.IsNullOrEmpty(ItemId.Value))
            return string.Empty;
        var data = ItemRegistry.GetData(ItemId.Value);
        return data?.DisplayName ?? string.Empty;
    }

    private bool CatchFiltersPass(string fishId, int size)
    {
        if (!string.IsNullOrEmpty(catchLocationName.Value))
        {
            string current = Game1.currentLocation?.Name ?? string.Empty;
            if (!string.Equals(current, catchLocationName.Value, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        if (catchMinSize.Value > 0 && size < catchMinSize.Value)
            return false;
        if (catchMaxSize.Value > 0 && size > catchMaxSize.Value)
            return false;
        if (!string.IsNullOrEmpty(catchWeather.Value))
        {
            if (!CurrentWeatherMatches(catchWeather.Value))
                return false;
        }
        return true;
    }

    // "Rain" matches Rain+Storm so vanilla rain-only fish data stays consistent.
    private static bool CurrentWeatherMatches(string requested)
    {
        string contextId = Game1.player?.currentLocation?.GetLocationContextId() ?? "Default";
        string actual = Game1.netWorldState?.Value?.GetWeatherForLocation(contextId)?.Weather ?? string.Empty;
        if (string.IsNullOrEmpty(actual))
            return false;

        string norm = requested.ToLowerInvariant() switch
        {
            "sunny" => "Sun",
            "rainy" => "Rain",
            "stormy" => "Storm",
            "snowy" => "Snow",
            "windy" => "Wind",
            _ => requested
        };

        if (string.Equals(norm, "Rain", StringComparison.OrdinalIgnoreCase))
            return string.Equals(actual, "Rain", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, "Storm", StringComparison.OrdinalIgnoreCase);
        return string.Equals(actual, norm, StringComparison.OrdinalIgnoreCase);
    }

    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }

    // Single-species fishing quests fall through to base for the vanilla
    // "0/5 Tuna caught" line.
    public override void reloadObjective()
    {
        if (catchAnyFish.Value && !string.IsNullOrEmpty(catchProgressTemplate.Value))
        {
            if (completed.Value)
                return;
            if (numberFished.Value >= numberToFish.Value && target.Value != null)
            {
                base.reloadObjective();
                return;
            }
            currentObjective = string.Format(
                catchProgressTemplate.Value,
                numberFished.Value,
                numberToFish.Value);
            return;
        }
        base.reloadObjective();
        // Override vanilla's post-catch "Return to <npc>" with a clearer delivery line
        // for true delivery quests. Skip ecology/report quests (location-filtered or
        // catchAnyFish), which stay on the vanilla "Return to" wording.
        if (!completed.Value
            && numberFished.Value >= numberToFish.Value
            && !string.IsNullOrEmpty(target.Value)
            && IsDeliveryStyle())
        {
            currentObjective = BuildDeliveryObjective();
        }
    }

    public override bool OnNpcSocialized(NPC npc, bool probe = false)
    {
        if (!IsReportableTo(npc))
            return false;

        if (numberFished.Value < numberToFish.Value)
            return false;

        bool delivery = IsDeliveryStyle();
        int needed = numberToFish.Value;
        if (delivery && CountInInventory(ItemId.Value) < needed)
            return false;

        if (probe)
            return true;

        if (delivery)
            ConsumeFish(ItemId.Value, needed);
        npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage));
        moneyReward.Value = reward.Value;
        questComplete();
        Game1.drawDialogue(npc);
        return true;
    }

    // Right-click-with-fish path. Vanilla FishingQuest doesn't override so it'd fall
    // through to the gift flow (donating one fish). Intercept like CollectAndReportQuest
    // but only consume on real delivery quests. Report-back quests (legendary, location
    // overpop, size overpop) complete without consuming the catch.
    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
        if (catchAnyFish.Value)
            return false;
        if (!IsReportableTo(npc))
            return false;
        if (item == null || !ItemIdMatches(item, ItemId.Value))
            return false;
        if (numberFished.Value < numberToFish.Value)
            return false;

        bool delivery = IsDeliveryStyle();
        int needed = numberToFish.Value;
        if (delivery && CountInInventory(ItemId.Value) < needed)
            return false;

        if (probe)
            return true;

        if (delivery)
            ConsumeFish(ItemId.Value, needed);
        npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage));
        moneyReward.Value = reward.Value;
        questComplete();
        Game1.drawDialogue(npc);
        return true;
    }

    private bool IsReportableTo(NPC npc)
    {
        if (completed.Value)
            return false;
        if (npc == null || !npc.IsVillager)
            return false;
        if (target.Value == null || !string.Equals(npc.Name, target.Value, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static int CountInInventory(string qualifiedItemId)
    {
        if (string.IsNullOrEmpty(qualifiedItemId))
            return 0;
        int total = 0;
        foreach (var item in Game1.player.Items)
        {
            if (item == null)
                continue;
            if (ItemIdMatches(item, qualifiedItemId))
                total += item.Stack;
        }
        return total;
    }

    private static void ConsumeFish(string qualifiedItemId, int count)
    {
        if (string.IsNullOrEmpty(qualifiedItemId) || count <= 0)
            return;
        var inv = Game1.player.Items;
        for (int i = 0; i < inv.Count && count > 0; i++)
        {
            var item = inv[i];
            if (item == null || !ItemIdMatches(item, qualifiedItemId))
                continue;
            int take = Math.Min(item.Stack, count);
            count -= take;
            if (take >= item.Stack)
                inv[i] = null;
            else
                item.Stack -= take;
        }
    }

    // Tolerates both qualified ("(O)129") and bare ("129") ids.
    private static bool ItemIdMatches(Item item, string qualifiedItemId)
    {
        if (string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            return true;
        string bare = qualifiedItemId.StartsWith("(", StringComparison.Ordinal)
            ? qualifiedItemId[(qualifiedItemId.IndexOf(')') + 1)..]
            : qualifiedItemId;
        return string.Equals(item.ItemId, bare, StringComparison.OrdinalIgnoreCase);
    }
}
