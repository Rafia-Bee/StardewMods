using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

// Observes the shipping bin at DayEnding (vanilla sells items normally, we don't consume).
[XmlType("Mods_RafiaBee_MoreQuestsFramework_ShipQuest")]
public sealed class MoreQuestsShipQuest : Quest, IRewardedQuest
{
    public readonly NetString target = new();
    public readonly NetString itemId = new();
    public readonly NetStringList alternativeItemIds = new();
    // Higher = one item id is worth N units of progress (Submarine Fuel uses 15 on
    // Battery Pack so 1 battery = 15 coal toward the same shipping bar).
    public readonly NetInt itemWeight = new();
    public readonly NetIntList alternativeItemWeights = new();
    public readonly NetInt numberToShip = new();
    public readonly NetInt numberShipped = new();
    public readonly NetStringList serializedRewards = new();
    public readonly NetBool allowDecorShipping = new();

    public readonly NetString baseObjective = new();

    public NetStringList SerializedRewards => serializedRewards;

    public string objectiveItemName = string.Empty;

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
            .AddField(allowDecorShipping, "allowDecorShipping")
            .AddField(baseObjective, "baseObjective");
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
        if (string.IsNullOrEmpty(baseObjective.Value) && !string.IsNullOrEmpty(_currentObjective))
            baseObjective.Value = _currentObjective;
        if (string.IsNullOrEmpty(baseObjective.Value))
            return;

        _currentObjective = numberToShip.Value > 1
            ? $"{baseObjective.Value} ({numberShipped.Value}/{numberToShip.Value})"
            : baseObjective.Value;
    }

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
