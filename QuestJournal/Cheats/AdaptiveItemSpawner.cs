using System;
using System.Collections.Generic;
using QuestJournal.Api;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Quests;

namespace QuestJournal.Cheats;

// The "get quest item" cheat helper. Figures out what a quest is asking for (vanilla fishing,
// delivery, collection quests, or MoreQuests adventure steps) and drops those items into the
// player's bag. Can also auto-finish fishing quests. Resolves token items like $forage too.
internal static class AdaptiveItemSpawner
{
    public static bool CanHelp(Quest quest, IMoreQuestsApi? mqfApi, IModHelper helper, out string label)
    {
        label = string.Empty;
        if (quest == null || quest.completed.Value) return false;

        if (quest is FishingQuest fq)
        {
            if (fq.numberFished.Value >= fq.numberToFish.Value) return false;
            label = helper.Translation.Get("journal.action.catchall").Default("Catch all").ToString();
            return true;
        }

        var req = SafeGetItemRequirement(quest, mqfApi);
        if (req != null && !string.IsNullOrEmpty(req.ItemId))
        {
            label = helper.Translation.Get("journal.action.spawnitem").Default("Get quest item").ToString();
            return true;
        }

        switch (quest)
        {
            case ItemDeliveryQuest idq when !string.IsNullOrEmpty(idq.ItemId.Value):
                label = helper.Translation.Get("journal.action.spawnitem").Default("Get quest item").ToString();
                return true;
            case ResourceCollectionQuest rcq when !string.IsNullOrEmpty(rcq.ItemId.Value):
                label = helper.Translation.Get("journal.action.spawnitem").Default("Get quest item").ToString();
                return true;
        }

        if (TryGetActiveStepItems(quest, mqfApi, out _, out _, out _))
        {
            label = helper.Translation.Get("journal.action.spawnitem").Default("Get quest item").ToString();
            return true;
        }
        return false;
    }

    public static string Apply(Quest quest, IMoreQuestsApi? mqfApi, IModHelper helper)
    {
        if (quest == null || quest.completed.Value) return string.Empty;

        if (quest is FishingQuest fq)
            return CatchAll(fq, helper);

        var req = SafeGetItemRequirement(quest, mqfApi);
        if (req != null && !string.IsNullOrEmpty(req.ItemId))
            return SpawnItem(req.ItemId, System.Math.Max(1, req.Count), req.Quality, helper);

        switch (quest)
        {
            case ItemDeliveryQuest idq:
                return SpawnItem(idq.ItemId.Value, System.Math.Max(1, idq.number.Value), 0, helper);
            case ResourceCollectionQuest rcq:
                return SpawnItem(rcq.ItemId.Value, System.Math.Max(1, rcq.number.Value - rcq.numberCollected.Value), 0, helper);
        }

        if (TryGetActiveStepItems(quest, mqfApi, out string itemId, out int count, out int quality))
            return SpawnItem(itemId, count, quality, helper);

        return string.Empty;
    }

    private static IQuestItemRequirement? SafeGetItemRequirement(Quest quest, IMoreQuestsApi? mqfApi)
    {
        if (mqfApi == null) return null;
        try { return mqfApi.GetItemRequirement(quest); }
        catch { return null; }
    }

    private static string CatchAll(FishingQuest fq, IModHelper helper)
    {
        if (string.IsNullOrEmpty(fq.ItemId.Value)) return string.Empty;
        int remaining = fq.numberToFish.Value - fq.numberFished.Value;
        if (remaining <= 0) return string.Empty;
        SpawnItem(fq.ItemId.Value, remaining, 0, helper);
        try { fq.OnFishCaught(fq.ItemId.Value, remaining, 1, probe: false); } catch { }
        if (fq.numberFished.Value < fq.numberToFish.Value)
        {
            fq.numberFished.Value = fq.numberToFish.Value;
            try { fq.reloadObjective(); } catch { }
            Game1.dayTimeMoneyBox?.pingQuest(fq);
        }
        return helper.Translation.Get("journal.itemhelper.caughtall", new { total = fq.numberToFish.Value })
            .Default($"Caught all {fq.numberToFish.Value}!").ToString();
    }

    private static string SpawnItem(string itemId, int amount, int quality, IModHelper helper)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return string.Empty;
        ParsedItemData? data;
        try { data = ItemRegistry.GetData(itemId); }
        catch { data = null; }
        if (data == null) return string.Empty;

        Item? item;
        try { item = ItemRegistry.Create(itemId, amount, quality); }
        catch { return string.Empty; }
        if (item == null) return string.Empty;

        Item? leftover = Game1.player.addItemToInventory(item);
        if (leftover != null && leftover.Stack > 0)
            Game1.createItemDebris(leftover, Game1.player.getStandingPosition(),
                Game1.player.FacingDirection, Game1.player.currentLocation);

        string name = string.IsNullOrEmpty(data.DisplayName) ? itemId : data.DisplayName;
        return helper.Translation.Get("journal.itemhelper.spawned", new { amount, item = name })
            .Default($"Added {amount} {name}").ToString();
    }

    private static bool TryGetActiveStepItems(Quest quest, IMoreQuestsApi? mqfApi, out string itemId, out int count, out int quality)
    {
        itemId = string.Empty;
        count = 0;
        quality = 0;
        if (mqfApi == null) return false;
        try
        {
            var steps = mqfApi.GetAdventureSteps(quest);
            if (steps == null || steps.Count == 0) return false;
            int? idx = mqfApi.GetActiveStepIndex(quest);
            if (!idx.HasValue || idx.Value < 0 || idx.Value >= steps.Count) return false;
            var step = steps[idx.Value];
            var items = step.Items;
            if (items == null || items.Count == 0) return false;
            foreach (var candidate in items)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                if (candidate[0] == '$')
                {
                    string resolved = ResolveTokenItem(candidate);
                    if (!string.IsNullOrEmpty(resolved)) { itemId = resolved; break; }
                    continue;
                }
                try { if (ItemRegistry.GetData(candidate) == null) continue; }
                catch { continue; }
                itemId = candidate;
                break;
            }
            if (string.IsNullOrEmpty(itemId)) return false;
            count = step.Count > 0 ? System.Math.Max(1, step.Count - step.Progress) : 1;
            quality = step.MinQuality;
            return true;
        }
        catch { return false; }
    }

    private static readonly Dictionary<string, string> TokenItemCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static string ResolveTokenItem(string token)
    {
        if (string.IsNullOrEmpty(token)) return string.Empty;
        if (TokenItemCache.TryGetValue(token, out string? cached)) return cached;

        string found = string.Empty;
        try
        {
            foreach (var itemType in ItemRegistry.ItemTypes)
            {
                if (itemType.Identifier != "(O)") continue;
                foreach (string id in itemType.GetAllIds())
                {
                    string qid = itemType.Identifier + id;
                    if (ItemRegistry.GetData(qid)?.RawData is not ObjectData obj) continue;
                    if (TokenMatches(token, obj, qid)) { found = qid; break; }
                }
                if (!string.IsNullOrEmpty(found)) break;
            }
        }
        catch { found = string.Empty; }

        TokenItemCache[token] = found;
        return found;
    }

    private static bool TokenMatches(string token, ObjectData obj, string qid)
    {
        const int eggCategory = -5;
        const int inedible = -300;
        if (token.Equals("$edible-egg", StringComparison.OrdinalIgnoreCase))
            return obj.Category == eggCategory && obj.Edibility != inedible;
        if (token.Equals("$forage", StringComparison.OrdinalIgnoreCase))
            return HasTag(qid, "forage_item");
        if (token.StartsWith("$category:", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(token.Substring("$category:".Length), out int cat) && obj.Category == cat;
        if (token.StartsWith("$tag:", StringComparison.OrdinalIgnoreCase))
        {
            string tag = token.Substring("$tag:".Length).Trim();
            return tag.Length > 0 && HasTag(qid, tag);
        }
        if (token.StartsWith("$edibility:", StringComparison.OrdinalIgnoreCase))
        {
            var bounds = token.Substring("$edibility:".Length).Split(':');
            return bounds.Length == 2
                && int.TryParse(bounds[0], out int lo)
                && int.TryParse(bounds[1], out int hi)
                && obj.Edibility >= lo && obj.Edibility <= hi;
        }
        return false;
    }

    private static bool HasTag(string qid, string tag)
    {
        try { return ItemContextTagManager.HasBaseTag(qid, tag); }
        catch { return false; }
    }
}
