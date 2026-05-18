using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;
using SObject = StardewValley.Object;

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

    // Built lazily on first ObserveShippingBin call. NetFields are stamped at construction
    // (factory or codec decode) and effectively frozen after, so caching is safe.
    private Dictionary<string, int>? _weightLookup;
    private Dictionary<SObject.PreserveType, int>? _preserveLookup;

    // Vanilla preserve outputs whose ItemId stays the same regardless of the source fruit
    // or fish. Modded wines, jellies, etc, often ship as their own ItemId (e.g. Fairy Cap
    // Wine is ItemId 358, not 348) but still set Object.preserve to the matching type.
    // When a quest accepts one of these bases we also accept anything with that preserve
    // tag so wines made from modded fruits count toward "ship wine" quests.
    private static readonly Dictionary<string, SObject.PreserveType> VanillaPreserveBases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["348"] = SObject.PreserveType.Wine,
            ["350"] = SObject.PreserveType.Juice,
            ["344"] = SObject.PreserveType.Jelly,
            ["342"] = SObject.PreserveType.Pickle,
            ["340"] = SObject.PreserveType.Honey,
            ["447"] = SObject.PreserveType.AgedRoe,
            ["812"] = SObject.PreserveType.Roe,
            ["DriedFruit"] = SObject.PreserveType.DriedFruit,
            ["DriedMushrooms"] = SObject.PreserveType.DriedMushroom,
            ["SmokedFish"] = SObject.PreserveType.SmokedFish,
        };

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
        return LookupWeight(WeightLookup(), item) > 0;
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
        var lookup = WeightLookup();
        int matched = 0;
        for (int i = 0; i < bin.Count; i++)
        {
            var item = bin[i];
            if (item == null) continue;
            int w = LookupWeight(lookup, item);
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

    private Dictionary<string, int> WeightLookup()
    {
        if (_weightLookup != null)
            return _weightLookup;
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var preserve = new Dictionary<SObject.PreserveType, int>();
        AddWeightKey(dict, preserve, itemId.Value, Math.Max(1, itemWeight.Value));
        for (int i = 0; i < alternativeItemIds.Count; i++)
        {
            int weight = i < alternativeItemWeights.Count
                ? Math.Max(1, alternativeItemWeights[i])
                : 1;
            AddWeightKey(dict, preserve, alternativeItemIds[i], weight);
        }
        _weightLookup = dict;
        _preserveLookup = preserve;
        return dict;
    }

    private static void AddWeightKey(Dictionary<string, int> dict, Dictionary<SObject.PreserveType, int> preserve, string id, int weight)
    {
        if (string.IsNullOrEmpty(id))
            return;
        dict[id] = weight;
        string bareId = id;
        // Mirror the old Matches() third branch: a qualified id like "(O)174" should
        // also resolve when the bin item only exposes the bare ItemId "174".
        if (id.StartsWith("(", StringComparison.Ordinal))
        {
            int closeIdx = id.IndexOf(')');
            if (closeIdx >= 0 && closeIdx + 1 < id.Length)
            {
                bareId = id.Substring(closeIdx + 1);
                dict[bareId] = weight;
            }
        }
        if (VanillaPreserveBases.TryGetValue(bareId, out var type) && !preserve.ContainsKey(type))
            preserve[type] = weight;
    }

    private int LookupWeight(Dictionary<string, int> dict, Item item)
    {
        string qid = item.QualifiedItemId ?? string.Empty;
        if (!string.IsNullOrEmpty(qid) && dict.TryGetValue(qid, out int w))
            return w;
        string id = item.ItemId ?? string.Empty;
        if (!string.IsNullOrEmpty(id) && dict.TryGetValue(id, out w))
            return w;
        if (_preserveLookup != null && _preserveLookup.Count > 0
            && item is SObject obj && obj.preserve.Value.HasValue
            && _preserveLookup.TryGetValue(obj.preserve.Value.Value, out int pw))
            return pw;
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
