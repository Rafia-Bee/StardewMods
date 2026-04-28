using System;
using System.Xml.Serialization;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuests.Framework.Quests;

/// `FishingQuest` variant that turns the second half into an actual delivery.
/// Vanilla `FishingQuest.OnNpcSocialized` ([decomp/FishingQuest.cs:214](../../docs/decomp/FishingQuest.cs#L214))
/// completes the quest as soon as `numberFished >= numberToFish` and the player
/// chats with the target NPC - the fish are never consumed. That means the
/// player can sell or eat every fish they caught and still claim the reward.
///
/// We override `OnNpcSocialized` (and `OnItemOfferedToNpc`, for the right-click-
/// with-fish path) to require the player to have the fish in their inventory at
/// turn-in time and remove the stack on completion. If they don't have enough,
/// the quest simply doesn't complete - vanilla NPC dialogue plays as normal and
/// the journal still shows the catch counter so the player can see the quest is
/// open.
[XmlType("Mods_RafiaBee_MoreQuests_FishingQuest")]
public sealed class MoreQuestsFishingQuest : FishingQuest
{
    public override bool OnNpcSocialized(NPC npc, bool probe = false)
    {
        if (!IsReportableTo(npc))
            return false;

        // Player still has fishing to do - let vanilla's OnFishCaught keep tracking.
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

    /// Right-click on the target NPC while holding the requested fish. Vanilla
    /// `FishingQuest` doesn't override this so it would fall through to the gift
    /// flow, donating one fish as a gift. Intercept the same way `CollectAndReportQuest`
    /// does, but consume the requested count rather than treat it as a gift.
    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
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
