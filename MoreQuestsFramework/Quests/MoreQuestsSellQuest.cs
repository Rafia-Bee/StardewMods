using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;
using SObject = StardewValley.Object;

namespace MoreQuestsFramework.Quests;

// "Sell N items into a shop" objective. The items aren't turned in to the giver, they're
// sold across a shop counter (Pierre's, Joja's, etc). The framework watches the player's
// inventory drop while that shop's menu is open and counts anything matching the filters.
// Nothing is consumed by us, the sale happens normally and we just keep a tally.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_SellQuest")]
public sealed class MoreQuestsSellQuest : Quest, IRewardedQuest
{
    public readonly NetString target = new();
    public readonly NetString shopId = new();
    public readonly NetString itemId = new();
    public readonly NetStringList alternativeItemIds = new();
    public readonly NetIntList categories = new();
    public readonly NetInt maxValue = new();
    public readonly NetInt maxQuality = new();
    public readonly NetInt numberToSell = new();
    public readonly NetInt numberSold = new();
    public readonly NetStringList serializedRewards = new();
    public readonly NetString baseObjective = new();

    [XmlIgnore]
    public NetStringList SerializedRewards => serializedRewards;

    public string targetMessage = string.Empty;

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(target, "target")
            .AddField(shopId, "shopId")
            .AddField(itemId, "itemId")
            .AddField(alternativeItemIds, "alternativeItemIds")
            .AddField(categories, "categories")
            .AddField(maxValue, "maxValue")
            .AddField(maxQuality, "maxQuality")
            .AddField(numberToSell, "numberToSell")
            .AddField(numberSold, "numberSold")
            .AddField(serializedRewards, "serializedRewards")
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

        _currentObjective = numberToSell.Value > 1
            ? $"{baseObjective.Value} ({numberSold.Value}/{numberToSell.Value})"
            : baseObjective.Value;
    }

    // Called when the player drops items in a shop. shopMenuId is the open shop's id.
    public void ObserveSale(string shopMenuId, Item item, int count)
    {
        if (completed.Value || count <= 0)
            return;
        if (!string.Equals(shopMenuId, shopId.Value, StringComparison.OrdinalIgnoreCase))
            return;
        if (!MatchesItem(item))
            return;

        int needed = numberToSell.Value;
        int credited = Math.Min(count, Math.Max(0, needed - numberSold.Value));
        if (credited <= 0)
            return;
        numberSold.Value += credited;
        if (numberSold.Value >= needed)
            questComplete();
        else
            reloadObjective();
    }

    public bool MatchesItem(Item item)
    {
        if (item == null)
            return false;
        if (item.Quality > maxQuality.Value)
            return false;
        if (maxValue.Value > 0)
        {
            if (item is not SObject obj || obj.sellToStorePrice() >= maxValue.Value)
                return false;
        }

        bool hasIdFilter = !string.IsNullOrEmpty(itemId.Value) || alternativeItemIds.Count > 0;
        bool hasCategoryFilter = categories.Count > 0;
        if (!hasIdFilter && !hasCategoryFilter)
            return true;
        if (MatchesId(item))
            return true;
        if (hasCategoryFilter && categories.Contains(item.Category))
            return true;
        return false;
    }

    private bool MatchesId(Item item)
    {
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

    public List<string> AlternativeIds()
    {
        var list = new List<string>(alternativeItemIds.Count);
        for (int i = 0; i < alternativeItemIds.Count; i++)
            list.Add(alternativeItemIds[i]);
        return list;
    }
}
