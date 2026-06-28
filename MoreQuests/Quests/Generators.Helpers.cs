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

    /// Oxford-comma join: "A", "A and B", "A, B, and C". For listing requested items naturally.
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

    /// Looks up the qualified seed id for a harvest item via Data/Crops. Used to scope
    /// Pierre's seed-shop discount to the quested crop's seed.
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

    /// Picks one random loved or liked item from the NPC's NPCGiftTastes and resolves it
    /// through ItemResolver. Tries a few candidates so a missing modded id doesn't kill the pick.
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
            // Skip negative category tokens. Those are "any item in category N" and can't
            // resolve to a single item.
            if (int.TryParse(id, out int n) && n < 0)
                continue;
            var resolved = ctx.Items.TryResolveItem(id);
            // Skip never-reward items here too so the quest picks a different loved item
            // instead of losing its item reward when the framework later drops it.
            if (resolved != null && !RewardExclusionList.IsExcludedReward(resolved.QualifiedItemId))
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
    /// appended if loved doesn't fill the cap). Used by the Secret Gift Hint description.
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

    /// Total base days from sowing the seed to first harvest, summed across Data/Crops
    /// phases. Returns 0 when the seed isn't found. Doesn't account for Speed-Gro,
    /// Agriculturist, or Deluxe Speed-Gro: the bonuses are friendly to the player, so
    /// using base time as the gate means we don't over-promise a crop the player can
    /// only finish with the right fertilizer.
    private static int GetCropMaturityDays(QuestContext ctx, string seedQualifiedId)
    {
        if (string.IsNullOrEmpty(seedQualifiedId))
            return 0;
        string bareSeed = seedQualifiedId.StartsWith("(O)", StringComparison.Ordinal)
            ? seedQualifiedId[3..]
            : seedQualifiedId;
        if (!ctx.Data.Crops.TryGetValue(bareSeed, out var data) || data?.DaysInPhase == null)
            return 0;
        int total = 0;
        foreach (int phase in data.DaysInPhase)
            total += phase;
        return total;
    }

    /// Filters the current-season crop pool down to crops whose seed-to-first-harvest
    /// time plus a one-day harvest-and-deliver reserve fits in the supplied window.
    /// `harvestWindow` should already be capped to min(quest deadline, days left in
    /// season) so end-of-season acceptances don't pick a crop that can't mature in time.
    private static List<ResolvedItem> FilterCropsByGrowthWindow(QuestContext ctx, int harvestWindow)
    {
        const int reserveDays = 1;
        var pool = ctx.Items.GetSeasonalCrops(ctx.Season);
        var viable = new List<ResolvedItem>(pool.Count);
        foreach (var crop in pool)
        {
            string? seedId = ResolveSeedIdForHarvest(ctx, crop.QualifiedItemId);
            if (seedId == null)
                continue;
            int maturity = GetCropMaturityDays(ctx, seedId);
            if (maturity > 0 && maturity + reserveDays <= harvestWindow)
                viable.Add(crop);
        }
        return viable;
    }

    /// Picks a current-season seed via Data/Crops. Returns null when no seasonal crop's
    /// seed resolves.
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

    /// Maps a vanilla quality value (0/1/2/4) to its translated display name. Quality 3
    /// is unused by vanilla.
    private static string QualityName(int quality) => quality switch
    {
        1 => ModEntry.I18n.Get("quest.quality.silver"),
        2 => ModEntry.I18n.Get("quest.quality.gold"),
        4 => ModEntry.I18n.Get("quest.quality.iridium"),
        _ => ModEntry.I18n.Get("quest.quality.normal")
    };

    /// Met villagers whose `Age == Child`, with positive friendship. Used by the child-only
    /// daily-board quests (Feed Wild Critters). Vanilla returns Jas / Vincent / Leo when met;
    /// modded child NPCs come along for free. Friendable monsters with a NpcAge.Child data
    /// row don't sneak in because of the IsMonster / IsVillager check. We also run
    /// IsBoardEligible(allowChild: true) so non-human "child" NPCs like RSV's Torts (a
    /// tortoise tagged Age=Child) get caught by the framework's IneligibleGivers list.
    private static List<string> MetChildHumanGivers()
    {
        var results = new List<string>();
        foreach (var (name, _) in Game1.player.friendshipData.Pairs)
        {
            var npc = Game1.getCharacterFromName(name);
            if (npc == null || npc.IsMonster || !npc.IsVillager)
                continue;
            var data = npc.GetData();
            if (data == null || data.Age != NpcAge.Child)
                continue;
            if (!Game1.player.friendshipData.TryGetValue(name, out var friendship) || friendship == null || friendship.Points <= 0)
                continue;
            if (!NpcDisplay.IsBoardEligible(name, allowChild: true))
                continue;
            results.Add(name);
        }
        return results;
    }

    // -------------------- Adult-human giver pools --------------------

    /// Met villagers narrowed to adult humans who can plausibly receive a quest gift.
    /// The Age / CanSocialize / non-human checks (DuckNPC, Sen, Leximonster, Krobus...)
    /// happen inside DispatchRegistry.MetHumanNpcs via NpcDisplay.IsBoardEligible. This
    /// layer adds the gift-receiver specifics: not Dwarvish-speaking, CanReceiveGifts,
    /// and friendship.Points > 0 so pre-seeded-but-never-actually-met NPCs (East Scarp's
    /// ToriLK) don't get picked.
    internal static List<string> MetAdultHumanGiftReceivers()
    {
        var results = new List<string>();
        foreach (var name in DispatchRegistry.MetHumanNpcs())
        {
            var npc = Game1.getCharacterFromName(name);
            if (npc == null)
                continue;
            var data = npc.GetData();
            if (data == null || data.Language == NpcLanguage.Dwarvish)
                continue;
            if (!npc.CanReceiveGifts())
                continue;
            if (!Game1.player.friendshipData.TryGetValue(name, out var friendship) || friendship == null || friendship.Points <= 0)
                continue;
            results.Add(name);
        }
        return results;
    }

    /// Subset of MetAdultHumanGiftReceivers whose loved/liked pool contains at least one
    /// fish-category item. For fishing quests where the giver narratively wants the fish
    /// themselves. EcologyMinded NPCs (Demetrius, Maddie, Mr. Aguar, Dylan) are excluded:
    /// they shouldn't be commissioning fish hauls outside of their dedicated ecology quests.
    private static List<string> MetAdultHumanFishLovers(QuestContext ctx)
    {
        var ecology = EcologyMindedSet(ctx);
        var results = new List<string>();
        foreach (var name in MetAdultHumanGiftReceivers())
        {
            if (ecology.Contains(name))
                continue;
            if (NpcLikesAnyFish(ctx, name))
                results.Add(name);
        }
        return results;
    }

    /// Met NPCs registered under the EcologyMinded dispatch role. Used to filter generic
    /// fish-haul quests so ecology NPCs only commission their own ecology quests.
    private static HashSet<string> EcologyMindedSet(QuestContext ctx)
    {
        var pool = ctx.Dispatch.ResolvePool(DispatchRoles.EcologyMinded);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < pool.Count; i++)
            set.Add(pool[i]);
        return set;
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

    /// True when the fish's Data/Fish time-windows union covers the full 600-2600 day.
    /// For the difficulty-scaling-off branch of quests that want all-day-catchable fish.
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
