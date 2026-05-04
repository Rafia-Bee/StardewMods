using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

/// Single-objective shipping quest. The player ships `numberToShip` units of `itemId`
/// (or any id in `alternativeItemIds`) through the farm shipping bin; the framework
/// observes the bin at `DayEnding` and increments `numberShipped` by the matching count.
/// Vanilla sells the items normally — we observe, we don't consume. When `numberShipped`
/// reaches `numberToShip`, `questComplete()` runs the declarative reward block.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_ShipQuest")]
public sealed class MoreQuestsShipQuest : Quest, IRewardedQuest
{
    public readonly NetString target = new();
    public readonly NetString itemId = new();
    public readonly NetStringList alternativeItemIds = new();
    /// Per-stack credit applied when the primary `itemId` is shipped. Defaults to 1, so a
    /// quest counts items 1:1. Higher weights let one item id be worth N "fuel units" of
    /// progress — Submarine Fuel uses this so 1 Battery Pack = 15 Coal toward the same
    /// shipping bar.
    public readonly NetInt itemWeight = new();
    /// Parallel to `alternativeItemIds`. Each entry is the credit applied per matched
    /// stack of that alternative. Missing entries default to 1.
    public readonly NetIntList alternativeItemWeights = new();
    public readonly NetInt numberToShip = new();
    public readonly NetInt numberShipped = new();
    public readonly NetStringList serializedRewards = new();
    /// Mirrors `QuestPosting.AllowDecorShipping`. While this quest is in the active log
    /// the framework's `DecorShippingPatches` postfix on `Object.canBeShipped` returns
    /// true so the player can deposit furniture / decor through the bin.
    public readonly NetBool allowDecorShipping = new();

    public NetStringList SerializedRewards => serializedRewards;

    /// Friendly item name shown in the "Ship X / Y <name>" objective line. Stored as a
    /// plain string field (not net-synced) — host writes it once at posting time.
    public string objectiveItemName = string.Empty;

    /// Spoken/letter line shown when the quest completes. Empty for board-posted ship
    /// quests; mail-delivered quests usually leave this empty since the reward letter
    /// itself is the thank-you message.
    public string targetMessage = string.Empty;

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(target, "target")
            .AddField(itemId, "itemId")
            .AddField(alternativeItemIds, "alternativeItemIds")
            .AddField(itemWeight, "itemWeight")
            .AddField(alternativeItemWeights, "alternativeItemWeights")
            .AddField(numberToShip, "numberToShip")
            .AddField(numberShipped, "numberShipped")
            .AddField(serializedRewards, "serializedRewards")
            .AddField(allowDecorShipping, "allowDecorShipping");
    }

    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }

    public override void reloadObjective()
    {
        if (completed.Value)
            return;
        // currentObjective text may already be set at posting time; refresh the count
        // suffix when the journal pulls this string. Numbers display via the standard
        // `(progress/count)` suffix the rest of the framework uses.
    }

    /// True when the bin item matches `itemId` or any of `alternativeItemIds`. Tolerates
    /// both qualified (`(O)787`) and bare (`787`) author input.
    public bool MatchesItem(Item item)
    {
        if (item == null)
            return false;
        if (Matches(item, itemId.Value))
            return true;
        for (int i = 0; i < alternativeItemIds.Count; i++)
        {
            if (Matches(item, alternativeItemIds[i]))
                return true;
        }
        return false;
    }

    private static bool Matches(Item item, string author)
    {
        if (string.IsNullOrEmpty(author))
            return false;
        string qid = item.QualifiedItemId ?? string.Empty;
        string id = item.ItemId ?? string.Empty;
        if (string.Equals(author, qid, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(author, id, StringComparison.OrdinalIgnoreCase))
            return true;
        if (author.StartsWith("(", StringComparison.Ordinal) &&
            string.Equals(author.Substring(author.IndexOf(')') + 1), id, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// Walks the shipping bin once and increments `numberShipped` by the weighted count of
    /// matching entries. Each match's contribution is `weight * stack` so a single Battery
    /// Pack with weight 15 contributes 15 toward a `numberToShip` of 30 (= 2 batteries to
    /// finish). Called by the framework's DayEnding observer for each active ship quest.
    public void ObserveShippingBin(IList<Item> bin, IMonitor? monitor = null)
    {
        if (completed.Value || bin == null || bin.Count == 0)
            return;
        int primaryWeight = Math.Max(1, itemWeight.Value);
        int matched = 0;
        for (int i = 0; i < bin.Count; i++)
        {
            var item = bin[i];
            if (item == null) continue;
            int w = MatchedWeight(item, primaryWeight);
            if (w > 0)
                matched += w * item.Stack;
        }
        if (matched <= 0)
            return;

        int needed = numberToShip.Value;
        int credited = Math.Min(matched, Math.Max(0, needed - numberShipped.Value));
        if (credited <= 0)
            return;
        numberShipped.Value = numberShipped.Value + credited;
        if (numberShipped.Value >= needed)
            questComplete();
        else
            reloadObjective();
    }

    private int MatchedWeight(Item item, int primaryWeight)
    {
        if (Matches(item, itemId.Value))
            return primaryWeight;
        for (int i = 0; i < alternativeItemIds.Count; i++)
        {
            if (Matches(item, alternativeItemIds[i]))
                return i < alternativeItemWeights.Count
                    ? Math.Max(1, alternativeItemWeights[i])
                    : 1;
        }
        return 0;
    }

    public List<string> AlternativeIds()
    {
        var list = new List<string>(alternativeItemIds.Count);
        for (int i = 0; i < alternativeItemIds.Count; i++)
            list.Add(alternativeItemIds[i]);
        return list;
    }
}
