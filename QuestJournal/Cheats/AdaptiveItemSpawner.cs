using QuestJournal.Api;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Quests;

namespace QuestJournal.Cheats;

// The "item helper" behind AllowItemCheats. For item-style quests it drops the
// target item(s) into the player's bag (at the required quality) so they can
// still do the real turn-in; for fishing quests, where holding the fish doesn't
// help, it advances the catch counter instead. Only called from the journal's
// action panel.
internal static class AdaptiveItemSpawner
{
    // True when the helper has something useful to do, and sets the button label
    // to match (catch-all for fishing, otherwise spawn). Reads the live quest so
    // a finished step hides the button on its own.
    public static bool CanHelp(Quest quest, IMoreQuestsApi? mqfApi, IModHelper helper, out string label)
    {
        label = string.Empty;
        if (quest == null || quest.completed.Value) return false;

        // Fishing: advance the catch counter (a fish in the bag doesn't help).
        if (quest is FishingQuest fq)
        {
            if (fq.numberFished.Value >= fq.numberToFish.Value) return false;
            label = helper.Translation.Get("journal.action.catchall").Default("Catch all").ToString();
            return true;
        }

        // Ask MoreQuests what the quest wants. This handles required quality and
        // "$any" tokens, plus MoreQuests delivery/ship and plain vanilla quests.
        var req = SafeGetItemRequirement(quest, mqfApi);
        if (req != null && !string.IsNullOrEmpty(req.ItemId))
        {
            label = helper.Translation.Get("journal.action.spawnitem").Default("Get quest item").ToString();
            return true;
        }

        // Vanilla fallback when MoreQuests isn't installed.
        switch (quest)
        {
            case ItemDeliveryQuest idq when !string.IsNullOrEmpty(idq.ItemId.Value):
                label = helper.Translation.Get("journal.action.spawnitem").Default("Get quest item").ToString();
                return true;
            case ResourceCollectionQuest rcq when !string.IsNullOrEmpty(rcq.ItemId.Value):
                label = helper.Translation.Get("journal.action.spawnitem").Default("Get quest item").ToString();
                return true;
        }

        // MoreQuests Adventure quest: spawn the active step's item, if any.
        if (TryGetActiveStepItems(quest, mqfApi, out _, out _, out _))
        {
            label = helper.Translation.Get("journal.action.spawnitem").Default("Get quest item").ToString();
            return true;
        }
        return false;
    }

    // Does the help and returns a short HUD message (empty when nothing was done).
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
        // Drop the actual fish in the bag too, like the item helper does for
        // delivery quests, so the player ends up holding what they "caught".
        SpawnItem(fq.ItemId.Value, remaining, 0, helper);
        // Run the game's own catch hook for the whole remaining count first, so it
        // arms completion / the return-to-NPC objective. Then force the counter to
        // the target as a fallback, since OnFishCaught's fish-id match wasn't
        // advancing some pre-existing quests. The quest finishes the normal way
        // after this (talk to the giver, or the Complete button).
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
        // Guard against unresolved tokens (e.g. "$forage") so we never spawn an
        // Error Item; a real id has registry data.
        ParsedItemData? data;
        try { data = ItemRegistry.GetData(itemId); }
        catch { data = null; }
        if (data == null) return string.Empty;

        Item? item;
        try { item = ItemRegistry.Create(itemId, amount, quality); }
        catch { return string.Empty; }
        if (item == null) return string.Empty;

        // Try the bag first; anything that doesn't fit drops at the player's feet.
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
            // First item the registry recognises (skip "$any" tokens).
            foreach (var candidate in items)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                try { if (ItemRegistry.GetData(candidate) == null) continue; }
                catch { continue; }
                itemId = candidate;
                break;
            }
            if (string.IsNullOrEmpty(itemId)) return false;
            // Give what's left for the step, or one when the step has no count.
            count = step.Count > 0 ? System.Math.Max(1, step.Count - step.Progress) : 1;
            quality = step.MinQuality;
            return true;
        }
        catch { return false; }
    }
}
