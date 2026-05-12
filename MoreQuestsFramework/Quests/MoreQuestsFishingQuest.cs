using System;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

/// `FishingQuest` variant that turns the second half into an actual delivery.
/// Vanilla `FishingQuest.OnNpcSocialized` completes the quest as soon as
/// `numberFished >= numberToFish` and the player chats with the target NPC - the
/// fish are never consumed. That means the player can sell or eat every fish
/// they caught and still claim the reward.
///
/// We override `OnNpcSocialized` (and `OnItemOfferedToNpc`, for the right-click-
/// with-fish path) to require the player to have the fish in their inventory at
/// turn-in time and remove the stack on completion. If they don't have enough,
/// the quest simply doesn't complete - vanilla NPC dialogue plays as normal and
/// the journal still shows the catch counter so the player can see the quest is
/// open.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_FishingQuest")]
public sealed class MoreQuestsFishingQuest : FishingQuest, IRewardedQuest
{
    public readonly NetStringList serializedRewards = new();

    /// Phase 9.5e Catch filters. When non-empty, the catch only credits when the player's
    /// current location matches; when > 0, the reported catch size (inches) must be ≥ the
    /// threshold; when non-empty, the runtime weather must match. All three are independent
    /// — empty / zero defaults disable each gate, so a vanilla single-objective fishing
    /// quest with none of these set behaves exactly like base `FishingQuest`.
    public readonly NetString catchLocationName = new();
    public readonly NetInt catchMinSize = new();
    public readonly NetInt catchMaxSize = new();
    public readonly NetString catchWeather = new();

    /// When true, the quest counts ANY caught fish that passes the size / location /
    /// weather filters, regardless of `ItemId.Value`. Turn-in then only requires the
    /// catch counter to be full — no specific fish stack is needed in inventory and no
    /// fish are consumed. Used by the Size Overpopulation quest (and any future "any
    /// fish in bucket X" content). Defaults to false so single-species quests keep the
    /// vanilla item-id gate + the existing consume-on-turn-in semantics.
    public readonly NetBool catchAnyFish = new();

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
            .AddField(catchAnyFish, "catchAnyFish");
    }

    /// Apply the Phase 9.5e Catch filters before letting the base `FishingQuest`
    /// increment its counter. When a filter is unset (empty / zero) the gate is
    /// pass-through; when set, a mismatch returns false without crediting (vanilla's
    /// "this counted toward a quest" UI cue stays silent).
    public override bool OnFishCaught(string fishId, int numberCaught, int size, bool probe = false)
    {
        if (!CatchFiltersPass(fishId, size))
            return false;
        if (catchAnyFish.Value)
        {
            // Bypass base.OnFishCaught's `fishId == ItemId.Value` gate. Any caught fish
            // that passes the filters counts. Mirror the increment + completion-cue path
            // that base FishingQuest.OnFishCaught walks for a matched fish.
            if (numberFished.Value >= numberToFish.Value)
                return false;
            if (probe)
                return true;
            numberFished.Value = Math.Min(numberToFish.Value, numberFished.Value + numberCaught);
            Game1.dayTimeMoneyBox.pingQuest(this);
            if (numberFished.Value >= numberToFish.Value)
            {
                if (target.Value == null)
                    target.Value = "Willy";
                NPC characterFromName = Game1.getCharacterFromName(target.Value);
                objective.Value = new DescriptionElement(
                    "Strings\\Quests:ObjectiveReturnToNPC",
                    characterFromName);
                Game1.playSound("jingle1");
            }
            return true;
        }
        return base.OnFishCaught(fishId, numberCaught, size, probe);
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

    /// Reads the runtime weather at the player's current location and compares to the
    /// requested label. Mirrors `ConditionEvaluator.MatchesWeather`'s alias handling.
    /// `Rain` matches Rain or Storm so vanilla rain-only fish data stays consistent (Storm
    /// is a thunderstorm variant that still fires rain spawns in `Data/Fish`).
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

    public override bool OnNpcSocialized(NPC npc, bool probe = false)
    {
        if (!IsReportableTo(npc))
            return false;

        // Player still has fishing to do - let vanilla's OnFishCaught keep tracking.
        if (numberFished.Value < numberToFish.Value)
            return false;

        int needed = numberToFish.Value;
        // Fish-agnostic quests (Size Overpopulation, etc) only require the catch counter
        // to be full. No specific fish is held back for turn-in and no stack is consumed.
        if (!catchAnyFish.Value && CountInInventory(ItemId.Value) < needed)
            return false;

        if (probe)
            return true;

        if (!catchAnyFish.Value)
            ConsumeFish(ItemId.Value, needed);
        npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage));
        moneyReward.Value = reward.Value;
        questComplete();
        Game1.drawDialogue(npc);
        return true;
    }

    /// Right-click on the target NPC while holding the requested fish. Vanilla
    /// `FishingQuest` doesn't override this so it would fall through to the gift
    /// flow, donating one fish as a gift. Intercept the same way `CollectAndReportQuest`
    /// does, but consume the requested count rather than treat it as a gift.
    /// `catchAnyFish` quests don't use this path (no specific item required), so the
    /// override no-ops for them and right-click falls back to vanilla's gift behaviour.
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

        int needed = numberToFish.Value;
        if (CountInInventory(ItemId.Value) < needed)
            return false;

        if (probe)
            return true;

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

    /// Removes `count` of `qualifiedItemId` from the active player's inventory.
    /// Walks the inventory, deducting from each matching stack until `count` is hit.
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

    /// Match either the qualified ID (`(O)129`) or the bare ID (`129`), so the quest
    /// is robust to whichever form vanilla / mods produced the player's stack with.
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
