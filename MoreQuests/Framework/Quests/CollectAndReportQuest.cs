using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Netcode;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuests.Framework.Quests;

/// "Collect items and report" quest. Player gathers `requiredCount` of `itemId` and
/// completes the quest by speaking to (or right-clicking with the item on)
/// `talkToNpc` while carrying that many of the item; the items are not consumed.
/// Counter is checked from inventory at speak time, not tracked over time. Items
/// lost (sold, gifted) before reporting must be re-collected.
[XmlType("Mods_RafiaBee_MoreQuests_CollectAndReportQuest")]
public sealed class CollectAndReportQuest : Quest
{
    public readonly NetStringList itemIds = new();
    public readonly NetString talkToNpc = new();
    public readonly NetInt requiredCount = new();
    public readonly NetString reportMessage = new();

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields.AddField(itemIds, "itemIds")
            .AddField(talkToNpc, "talkToNpc")
            .AddField(requiredCount, "requiredCount")
            .AddField(reportMessage, "reportMessage");
    }

    public override bool OnNpcSocialized(NPC npc, bool probe = false)
    {
        if (!IsReportableTo(npc))
            return false;
        if (CountInInventory() < requiredCount.Value)
            return false;
        if (probe)
            return true;
        Complete(npc);
        return true;
    }

    /// Suppress the gift action when the player right-clicks the report NPC while holding
    /// the requested item. Without this, vanilla would consume one item as a gift even
    /// though the quest only requires the items to be in inventory.
    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
        if (!IsReportableTo(npc))
            return false;
        if (item == null || !ItemIdMatches(item))
            return false;
        if (CountInInventory() < requiredCount.Value)
            return false;
        if (probe)
            return true;
        Complete(npc);
        return true;
    }

    private bool IsReportableTo(NPC npc)
    {
        if (completed.Value)
            return false;
        if (npc == null || !string.Equals(npc.Name, talkToNpc.Value, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private void Complete(NPC npc)
    {
        if (!string.IsNullOrEmpty(reportMessage.Value))
        {
            npc.CurrentDialogue.Push(new Dialogue(npc, null, reportMessage.Value));
            Game1.drawDialogue(npc);
        }
        questComplete();
    }

    private bool ItemIdMatches(Item item)
    {
        foreach (string id in itemIds)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            if (string.Equals(item.QualifiedItemId, id, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(item.QualifiedItemId, "(O)" + StripPrefix(id), StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(item.ItemId, StripPrefix(id), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private int CountInInventory()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in itemIds)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            ids.Add(id);
            ids.Add(StripPrefix(id));
        }
        if (ids.Count == 0)
            return 0;

        int total = 0;
        foreach (var item in Game1.player.Items)
        {
            if (item == null)
                continue;
            if (ids.Contains(item.QualifiedItemId) || ids.Contains(item.ItemId))
                total += item.Stack;
        }
        return total;
    }

    private static string StripPrefix(string id) =>
        id.StartsWith("(") ? id.Substring(id.IndexOf(')') + 1) : id;
}
