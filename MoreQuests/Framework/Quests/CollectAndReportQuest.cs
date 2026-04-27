using System;
using System.Collections.Generic;
using Netcode;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuests.Framework.Quests;

/// "Collect items and report" quest. Player gathers `requiredCount` of `itemId` and
/// completes the quest by speaking to `talkToNpc` while carrying that many of the
/// item; the items are not consumed. Counter is checked from inventory at speak
/// time, not tracked over time. Items lost (sold, gifted) before reporting must
/// be re-collected.
internal sealed class CollectAndReportQuest : Quest
{
    public readonly NetStringList itemIds = new();
    public readonly NetString talkToNpc = new();
    public readonly NetInt requiredCount = new();

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields.AddField(itemIds, "itemIds").AddField(talkToNpc, "talkToNpc").AddField(requiredCount, "requiredCount");
    }

    public override bool OnNpcSocialized(NPC npc, bool probe = false)
    {
        if (completed.Value)
            return false;
        if (npc == null || !string.Equals(npc.Name, talkToNpc.Value, StringComparison.OrdinalIgnoreCase))
            return false;
        if (CountInInventory() < requiredCount.Value)
            return false;
        if (probe)
            return true;
        questComplete();
        return true;
    }

    private int CountInInventory()
    {
        int total = 0;
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
