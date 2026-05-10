using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;

namespace MoreQuests.Quests;

internal static partial class Generators
{
    private static string StripPrefix(string id) =>
        id.StartsWith("(O)") ? id[3..] : id;

    /// English-style comma-and join: 1 item → "A", 2 → "A and B", 3+ → "A, B, and C"
    /// (Oxford comma). Used for description copy that lists a quest's requested item
    /// variations naturally.
    private static string JoinItemList(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            2 => $"{list[0]} and {list[1]}",
            _ => string.Join(", ", list.Take(list.Count - 1)) + ", and " + list[^1]
        };
    }

    // -------------------- Festival quests (Phase 7b) --------------------

    /// Single-objective Ship quest. Player ships Battery Pack OR Coal into the farm
    /// shipping bin; the framework's DayEnding observer credits each match by weight,
    /// where one Battery Pack equals 15 Coal of "fuel". The reward Pearl arrives by mail
    /// the next morning. Mining-skill scaling: base = 15 fuel (= 1 battery / 15 coal),
    /// scales 1.5× per mining level when DifficultyScaling is on. With scaling off it's
    /// a fixed 30 fuel target (= 2 batteries / 30 coal).

    /// Walks `Data/Crops` for a row whose `HarvestItemId` matches the requested crop, and
    /// returns the qualified seed id (the dictionary key is the seed's bare item id). Used
    /// to scope Pierre's seed-shop discount to the seeds for the quested crops.
    private static string? ResolveSeedIdForHarvest(QuestContext ctx, string harvestQualifiedId)
    {
        if (string.IsNullOrEmpty(harvestQualifiedId))
            return null;
        string bareHarvest = harvestQualifiedId.StartsWith("(O)", StringComparison.Ordinal)
            ? harvestQualifiedId[3..]
            : harvestQualifiedId;
        foreach (var (seedId, data) in ctx.Data.Crops)
        {
            if (string.Equals(data.HarvestItemId, bareHarvest, StringComparison.OrdinalIgnoreCase))
                return "(O)" + seedId;
        }
        return null;
    }

    // -------------------- Phase 9a: Massive Harvest Request --------------------

    /// CSV row 48. Daily-board single-objective Ship quest. Morris (JojaMart manager)
    /// asks the farmer to dump `qty` of one seasonal crop into the farm shipping bin;
    /// vanilla sells the items normally and the framework's DayEnding observer credits
    /// the count. Reward = `RewardMultiplierBelowSell` of the crop's price (Joja pays
    /// below sell so the headline gold figure looks high while the crop's lost-value
    /// brings it back near break-even).
    ///
    /// Tier 1 consequence keyed off `Data/NPCGiftTastes`: villagers who love the chosen
    /// crop comment positively + gain `+FriendshipBasic`; villagers who hate it
    /// comment negatively + lose `-FriendshipBasic`. Lines are pre-resolved against
    /// the content mod's i18n at build-time so the persisted dialogue queue holds
    /// plain strings.

    /// Picks a single random loved or liked item id from the NPC's `Data/NPCGiftTastes`
    /// entry, then resolves it through `ItemResolver`. Skips items that can't be
    /// resolved (modded id whose source mod isn't loaded, etc.) and falls back through
    /// the candidate pool until one resolves.
    private static ResolvedItem? PickLovedOrLikedItem(QuestContext ctx, string npc)
    {
        if (!ctx.Data.GiftTastes.TryGetValue(npc, out var tasteData))
            return null;
        var fields = tasteData.Split('/');
        if (fields.Length < 4)
            return null;

        var candidates = new List<string>();
        AppendIds(candidates, fields[1]); // loved
        AppendIds(candidates, fields[3]); // liked
        if (candidates.Count == 0)
            return null;

        // Try a handful of candidates so a stale or modded-only id doesn't poison the pick.
        for (int i = 0; i < 10 && candidates.Count > 0; i++)
        {
            int idx = Game1.random.Next(candidates.Count);
            string id = candidates[idx];
            candidates.RemoveAt(idx);
            // Skip negative category tokens — those are "any item in category N" and
            // can't be resolved to a single item directly.
            if (int.TryParse(id, out int n) && n < 0)
                continue;
            var resolved = ctx.Items.TryResolveItem(id);
            if (resolved != null)
                return resolved;
        }
        return null;
    }

    private static void AppendIds(List<string> sink, string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return;
        foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            sink.Add(part);
    }

    /// Returns up to `max` display names of items the NPC loves (with liked items
    /// appended if loved doesn't fill the cap). Used by the Secret Gift Hint
    /// description so the journal entry actually carries the hint.

    /// Returns up to `max` display names of items the NPC loves (with liked items
    /// appended if loved doesn't fill the cap). Used by the Secret Gift Hint
    /// description so the journal entry actually carries the hint.
    private static List<string> ResolveLovedItemNames(QuestContext ctx, string npc, int max)
    {
        var names = new List<string>(max);
        if (!ctx.Data.GiftTastes.TryGetValue(npc, out var tasteData))
            return names;
        var fields = tasteData.Split('/');
        if (fields.Length < 4)
            return names;

        AppendResolvedNames(ctx, fields[1], names, max);
        if (names.Count < max)
            AppendResolvedNames(ctx, fields[3], names, max);
        return names;
    }

    private static void AppendResolvedNames(QuestContext ctx, string raw, List<string> sink, int max)
    {
        if (string.IsNullOrEmpty(raw))
            return;
        foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (sink.Count >= max)
                return;
            if (int.TryParse(part, out int n) && n < 0)
                continue;
            var resolved = ctx.Items.TryResolveItem(part);
            if (resolved == null)
                continue;
            if (sink.Contains(resolved.DisplayName, StringComparer.Ordinal))
                continue;
            sink.Add(resolved.DisplayName);
        }
    }

    /// Cold-food vanilla pool. Plain melons (vanilla farm) plus Ice Cream (Traveling
    /// Cart / shop-bought) and Triple Shot Espresso (cold drink). Modded cold items
    /// get folded in below via the context-tag scan, so a content pack adding a cold
    /// drink that flags itself with `cold_drink_item` lands in the pool automatically.

    /// Picks a current-season seed via Data/Crops + the existing seed-resolver pattern
    /// from PierresStockUp. Returns null if no seasonal crops resolve (e.g. on saves
    /// where every seasonal crop's seed got removed by a content pack).
    private static ResolvedItem? PickSeasonalSeed(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;
        // Try a handful of crops so a missing seed entry doesn't kill the reward.
        var pool = new List<ResolvedItem>(crops);
        for (int i = 0; i < 10 && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            var crop = pool[idx];
            pool.RemoveAt(idx);
            string? seedId = ResolveSeedIdForHarvest(ctx, crop.QualifiedItemId);
            if (string.IsNullOrEmpty(seedId))
                continue;
            var seed = ctx.Items.TryResolveItem(seedId!);
            if (seed != null)
                return seed;
        }
        return null;
    }

    private static ResolvedItem? PickResolved(QuestContext ctx, (string Id, string Name)[] pool)
    {
        if (pool.Length == 0)
            return null;
        // Try a few to skip missing modded ids.
        var indices = new List<int>(pool.Length);
        for (int i = 0; i < pool.Length; i++)
            indices.Add(i);
        while (indices.Count > 0)
        {
            int j = Game1.random.Next(indices.Count);
            var resolved = ctx.Items.TryResolveItem(pool[indices[j]].Id);
            indices.RemoveAt(j);
            if (resolved != null)
                return resolved;
        }
        return null;
    }

    // -------------------- Phase 9.5b: Quality-aware delivery quests --------------------

    /// CSV row 58. Daily-board ItemDelivery for Gold-quality (Quality=2) seasonal crops.
    /// Picks any met NPC as the requester. Reward scales with the crop's sell price
    /// between `GoldBasicBase` and `GoldIntermediateBase` plus `FriendshipBasic` to the
    /// requester. Skill-gated to Farming 4 via the JSON `SkillLevel` filter so the quest
    /// only surfaces once the player can plausibly produce gold-quality crops (gold
    /// requires the Tiller profession or a high farming level + fertilizer).

    /// Maps a vanilla quality value (0/1/2/4) to its translated display name. Quality 3
    /// is unused by vanilla; the `_` fallback keeps the helper safe if a future
    /// definition somehow ships with that value.
    private static string QualityName(int quality) => quality switch
    {
        1 => ModEntry.I18n.Get("quest.quality.silver"),
        2 => ModEntry.I18n.Get("quest.quality.gold"),
        4 => ModEntry.I18n.Get("quest.quality.iridium"),
        _ => ModEntry.I18n.Get("quest.quality.normal")
    };

    // -------------------- Phase 9.5d: Festival decor-supply quests --------------------

    /// Curated decor pool for the Dance of the Moonlight Jellies festival reward.
    /// Vanilla Big-Craftable / Furniture ids picked for thematic fit (lights, decor).
    /// Unknown ids silently no-op via `RewardApplier`, so over-listing is safe across
    /// game versions.

    // -------------------- Adult-human giver pools --------------------

    /// Met villagers narrowed to adult humans who can plausibly receive a quest gift.
    /// Programmatic filters: not a child (NPC.Age != Child), not Dwarvish-speaking
    /// (catches Dwarf and Dwarvish modded NPCs), can socialize per Data/Characters,
    /// has a Data/NPCGiftTastes row, and CanReceiveGifts() returns true. These
    /// collectively keep modded animals/monsters out of the pool without naming them.
    /// Krobus is excluded by name as the canonical "non-human exception": he passes
    /// every other filter (speaks Default, can socialize, accepts gifts), so vanilla
    /// data offers no programmatic marker. Quests that explicitly involve Krobus
    /// should bypass this helper and reference him directly.
    private static List<string> MetAdultHumanGiftReceivers()
    {
        var results = new List<string>();
        foreach (var name in DispatchRegistry.MetHumanNpcs())
        {
            if (string.Equals(name, "Krobus", StringComparison.OrdinalIgnoreCase))
                continue;
            var npc = Game1.getCharacterFromName(name);
            if (npc == null)
                continue;
            if (npc.Age == 2)
                continue;
            var data = npc.GetData();
            if (data == null)
                continue;
            if (data.Language == NpcLanguage.Dwarvish)
                continue;
            if (!npc.CanReceiveGifts())
                continue;
            results.Add(name);
        }
        return results;
    }

    /// Subset of `MetAdultHumanGiftReceivers` whose loved+liked Data/NPCGiftTastes pool
    /// contains at least one item with object category Fish (-4) or the fish category
    /// sentinel. Used by fishing quests where the giver narratively wants the fish for
    /// themselves; an NPC who doesn't enjoy any fish wouldn't ask for one.
    private static List<string> MetAdultHumanFishLovers(QuestContext ctx)
    {
        var results = new List<string>();
        foreach (var name in MetAdultHumanGiftReceivers())
        {
            if (NpcLikesAnyFish(ctx, name))
                results.Add(name);
        }
        return results;
    }

    private static bool NpcLikesAnyFish(QuestContext ctx, string npc)
    {
        if (!ctx.Data.GiftTastes.TryGetValue(npc, out var raw))
            return false;
        var fields = raw.Split('/');
        if (fields.Length < 4)
            return false;
        return TasteContainsFish(fields[1]) || TasteContainsFish(fields[3]);
    }

    private static bool TasteContainsFish(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        foreach (var token in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, out int n) && n < 0)
            {
                if (n == StardewValley.Object.FishCategory)
                    return true;
                continue;
            }
            string id = token.StartsWith("(", StringComparison.Ordinal) ? token : "(O)" + token;
            var data = StardewValley.ItemRegistry.GetData(id);
            if (data?.Category == StardewValley.Object.FishCategory)
                return true;
        }
        return false;
    }

    /// Returns true when the fish's Data/Fish time-window pairs union covers the full
    /// vanilla 600-2600 day. Used for the difficulty-scaling-off branch of quests
    /// that want fish catchable any time of day.
    private static bool IsAllDayFish(QuestContext ctx, string fishQualifiedId)
    {
        string bare = fishQualifiedId.StartsWith("(O)", StringComparison.Ordinal)
            ? fishQualifiedId.Substring(3)
            : fishQualifiedId;
        if (!ctx.Data.Fish.TryGetValue(bare, out var raw))
            return false;
        var fields = raw.Split('/');
        if (fields.Length < 6 || fields[1] == "trap")
            return false;
        var tokens = fields[5].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2 || tokens.Length % 2 != 0)
            return false;
        var pairs = new List<(int Start, int End)>(tokens.Length / 2);
        for (int i = 0; i + 1 < tokens.Length; i += 2)
        {
            if (!int.TryParse(tokens[i], out int s) || !int.TryParse(tokens[i + 1], out int e))
                return false;
            pairs.Add((s, e));
        }
        pairs.Sort((a, b) => a.Start.CompareTo(b.Start));
        int reach = pairs[0].End;
        if (pairs[0].Start > 600)
            return false;
        for (int i = 1; i < pairs.Count; i++)
        {
            if (pairs[i].Start > reach)
                return false;
            reach = Math.Max(reach, pairs[i].End);
        }
        return reach >= 2600;
    }
}
