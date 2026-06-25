using System;
using System.Collections.Generic;
using QuestJournal.Api;
using StardewModdingAPI;
using StardewValley.Quests;
using StardewValley.SpecialOrders;

namespace QuestJournal.Menu;

// Pulls the display details for a quest: reward lines, who gave it, its category, and what mod it came from.
// Asks the MoreQuests API first when it's around, then falls back to reading vanilla quest fields, quest data, or sniffing NPC names out of the text.
internal static class QuestSnapshotBuilder
{
    private static string T(string key, string fallback)
        => ModEntry.Instance?.Helper?.Translation.Get(key).Default(fallback).ToString() ?? fallback;

    private static string T(string key, object tokens, string fallback)
        => ModEntry.Instance?.Helper?.Translation.Get(key, tokens).Default(fallback).ToString() ?? fallback;

    public static List<RewardLineRow> BuildRewardLines(Quest q, IMoreQuestsApi? mqfApi)
    {
        var lines = new List<RewardLineRow>();

        if (mqfApi != null)
        {
            IReadOnlyList<IQuestRewardLine>? mqfLines = null;
            try { mqfLines = mqfApi.GetRewardLines(q); } catch { }
            if (mqfLines != null)
            {
                foreach (var l in mqfLines)
                {
                    lines.Add(new RewardLineRow(
                        kind: l.Kind ?? string.Empty,
                        summary: l.Summary ?? string.Empty,
                        itemId: l.ItemId,
                        npcName: l.NpcName,
                        amount: l.Amount,
                        durationDays: l.DurationDays));
                }
            }
        }

        if (lines.Count == 0)
            SynthesiseVanillaRewardLines(q, lines);

        if (lines.Count == 0)
            lines.Add(new RewardLineRow(kind: "None", summary: T("journal.reward.none", "(none)")));

        return lines;
    }

    private static void SynthesiseVanillaRewardLines(Quest q, List<RewardLineRow> lines)
    {
        int gold = q.GetMoneyReward();
        if (gold <= 0)
            gold = ResolveSubclassReward(q);
        if (gold > 0)
            lines.Add(new RewardLineRow(kind: "Money", summary: T("journal.reward.money", new { amount = gold }, $"{gold}g"), amount: gold));

        string? desc = q.rewardDescription.Value;
        if (!string.IsNullOrEmpty(desc) && desc != "-1")
            lines.Add(new RewardLineRow(kind: "Custom", summary: desc!));

        string? friend = ResolveVanillaFriendshipTarget(q);
        if (!string.IsNullOrEmpty(friend))
            lines.Add(new RewardLineRow(
                kind: "Friendship",
                summary: T("journal.reward.friendship", new { amount = 250, npc = friend }, $"+250 friendship with {friend}"),
                npcName: friend,
                amount: 250));
    }

    private static int ResolveSubclassReward(Quest q) => q switch
    {
        ResourceCollectionQuest rcq => rcq.reward.Value,
        FishingQuest fq => fq.reward.Value,
        SlayMonsterQuest smq => smq.reward.Value,
        _ => 0
    };

    private static string? ResolveVanillaFriendshipTarget(Quest q) => q switch
    {
        ItemDeliveryQuest idq when !string.IsNullOrEmpty(idq.target.Value) => idq.target.Value,
        ResourceCollectionQuest rcq when !string.IsNullOrEmpty(rcq.target.Value) => rcq.target.Value,
        SlayMonsterQuest smq when !string.IsNullOrEmpty(smq.target.Value) && smq.target.Value != "null" => smq.target.Value,
        _ => null
    };

    public static string? ResolveGiverNpcName(Quest q, IMoreQuestsApi? mqfApi = null)
    {
        if (q == null) return null;

        if (mqfApi != null)
        {
            string? fromMqf = null;
            try { fromMqf = mqfApi.GetGiverNpc(q); } catch { }
            if (!string.IsNullOrEmpty(fromMqf))
                return fromMqf;
        }

        switch (q)
        {
            case ItemDeliveryQuest idq when !string.IsNullOrEmpty(idq.target.Value):
                return idq.target.Value;
            case SlayMonsterQuest smq when !string.IsNullOrEmpty(smq.target.Value) && smq.target.Value != "null":
                return smq.target.Value;
            case LostItemQuest liq when !string.IsNullOrEmpty(liq.npcName.Value):
                return liq.npcName.Value;
            case SecretLostItemQuest sliq when !string.IsNullOrEmpty(sliq.npcName.Value):
                return sliq.npcName.Value;
        }

        string? fromData = ResolveGiverFromQuestData(q.id?.Value);
        if (!string.IsNullOrEmpty(fromData)) return fromData;

        string? fromText = ResolveGiverFromText(q);
        if (!string.IsNullOrEmpty(fromText)) return fromText;

        try
        {
            ModEntry.DebugLog(
                $"No giver inferred for quest type={q.GetType().Name} id='{q.id?.Value ?? "(none)"}' title='{q.questTitle ?? "(none)"}'.");
        }
        catch { }
        return null;
    }

    private static string? ResolveGiverFromText(Quest q)
    {
        string title;
        string description;
        try { title = q.questTitle ?? string.Empty; } catch { title = string.Empty; }
        try { description = q.questDescription ?? string.Empty; } catch { description = string.Empty; }
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(description))
            return null;

        IEnumerable<string> candidates;
        try
        {
            candidates = StardewValley.Game1.characterData?.Keys
                ?? System.Linq.Enumerable.Empty<string>();
        }
        catch { return null; }

        string titleMatch = MatchNpcInText(title, candidates);
        if (!string.IsNullOrEmpty(titleMatch)) return titleMatch;
        return MatchNpcInText(description, candidates);
    }

    private static string MatchNpcInText(string text, IEnumerable<string> npcs)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string best = string.Empty;
        int bestIdx = int.MaxValue;
        foreach (string name in npcs)
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Length < 3) continue;
            int idx = IndexOfWholeWord(text, name);
            if (idx >= 0 && idx < bestIdx)
            {
                best = name;
                bestIdx = idx;
            }
        }
        return best;
    }

    private static int IndexOfWholeWord(string haystack, string needle)
    {
        int from = 0;
        while (from <= haystack.Length - needle.Length)
        {
            int found = haystack.IndexOf(needle, from, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return -1;
            bool leftOk = found == 0 || !char.IsLetter(haystack[found - 1]);
            int after = found + needle.Length;
            bool rightOk = after >= haystack.Length || !char.IsLetter(haystack[after]);
            if (leftOk && rightOk) return found;
            from = found + 1;
        }
        return -1;
    }

    private static string? ResolveGiverFromQuestData(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try
        {
            string[]? fields = Quest.GetRawQuestFields(id);
            if (fields == null || fields.Length < 5) return null;
            string type = fields[0];
            string conditions = fields[4];
            if (string.IsNullOrEmpty(conditions)) return null;
            string[] split = conditions.Split(' ');
            if (split.Length == 0) return null;
            switch (type)
            {
                case "ItemDelivery":
                case "LostItem":
                case "SecretLostItem":
                    return string.IsNullOrEmpty(split[0]) ? null : split[0];
                case "Monster":
                    if (split.Length >= 3 && !string.IsNullOrEmpty(split[2]) && split[2] != "null")
                        return split[2];
                    return null;
                default:
                    return null;
            }
        }
        catch { return null; }
    }

    public static (string Category, string Kind) ResolveCategoryKind(Quest q, IMoreQuestsApi? mqfApi)
    {
        if (q == null) return (string.Empty, string.Empty);
        if (mqfApi != null)
        {
            try
            {
                string? defId = mqfApi.GetDefinitionId(q);
                if (!string.IsNullOrEmpty(defId))
                {
                    var info = mqfApi.GetQuestInfo(defId!);
                    if (info != null)
                        return (info.Category ?? string.Empty, info.Kind.ToString());
                }
            }
            catch { }
        }

        return (string.Empty, ClassifyVanillaKind(q));
    }

    private static string ClassifyVanillaKind(Quest q) => q switch
    {
        ItemDeliveryQuest => "ItemDelivery",
        ResourceCollectionQuest => "ResourceCollection",
        FishingQuest => "Fishing",
        SlayMonsterQuest => "SlayMonster",
        _ => "Story"
    };

    public static string ResolveGiverDisplay(Quest q, IMoreQuestsApi? mqfApi = null)
    {
        string? name = ResolveGiverNpcName(q, mqfApi);
        return string.IsNullOrEmpty(name) ? T("journal.unknown", "Unknown") : name!;
    }

    public static string ResolveSourceDisplay(Quest q, IMoreQuestsApi? mqfApi, IModHelper helper)
    {
        if (mqfApi != null)
        {
            try
            {
                string? defId = mqfApi.GetDefinitionId(q);
                if (!string.IsNullOrEmpty(defId))
                {
                    var info = mqfApi.GetQuestInfo(defId!);
                    string? ownerId = info?.OwnerUniqueId;
                    if (!string.IsNullOrEmpty(ownerId))
                    {
                        var modInfo = helper.ModRegistry.Get(ownerId!);
                        return modInfo?.Manifest?.Name ?? ownerId!;
                    }
                }
            }
            catch { }
        }

        string? questId = q.id?.Value;
        if (!string.IsNullOrEmpty(questId) && questId!.Contains('.'))
        {
            IModInfo? bestMatch = null;
            int bestLen = 0;
            foreach (var mod in helper.ModRegistry.GetAll())
            {
                var m = mod.Manifest;
                if (m == null || string.IsNullOrEmpty(m.UniqueID)) continue;
                string uid = m.UniqueID;
                if (questId.Equals(uid, StringComparison.OrdinalIgnoreCase)
                    || questId.StartsWith(uid + ".", StringComparison.OrdinalIgnoreCase)
                    || questId.StartsWith(uid + "_", StringComparison.OrdinalIgnoreCase))
                {
                    if (uid.Length > bestLen)
                    {
                        bestMatch = mod;
                        bestLen = uid.Length;
                    }
                }
            }
            if (bestMatch?.Manifest != null)
                return bestMatch.Manifest.Name;
        }

        if (q is ItemDeliveryQuest or FishingQuest or ResourceCollectionQuest or SlayMonsterQuest
            || q.GetType() == typeof(Quest))
        {
            if (LooksLikeModdedDataQuestId(questId))
                return questId!;
            return "Stardew Valley";
        }

        string? asmName = q.GetType().Assembly.GetName().Name;
        if (string.IsNullOrEmpty(asmName)) return "Modded";

        foreach (var mod in helper.ModRegistry.GetAll())
        {
            var m = mod.Manifest;
            if (m == null) continue;
            if (string.Equals(m.Name, asmName, StringComparison.OrdinalIgnoreCase))
                return m.Name;
            if (string.Equals(m.UniqueID, asmName, StringComparison.OrdinalIgnoreCase))
                return m.Name;
            if (m.UniqueID.EndsWith("." + asmName, StringComparison.OrdinalIgnoreCase))
                return m.Name;
        }
        return asmName!;
    }

    private static bool LooksLikeModdedDataQuestId(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (char c in id)
        {
            if (char.IsLetter(c)) return true;
        }
        return false;
    }

    public static string ResolveSpecialOrderSource(SpecialOrder so, IModHelper helper)
    {
        if (so == null) return "Stardew Valley";

        if (so.orderType.Value == "Qi") return "Mr. Qi";

        string key = so.questKey.Value ?? string.Empty;
        if (string.IsNullOrEmpty(key) || !key.Contains('.'))
            return "Stardew Valley";

        IModInfo? bestMatch = null;
        int bestLen = 0;
        foreach (var mod in helper.ModRegistry.GetAll())
        {
            var m = mod.Manifest;
            if (m == null || string.IsNullOrEmpty(m.UniqueID)) continue;
            string uid = m.UniqueID;
            if (key.Equals(uid, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(uid + ".", StringComparison.OrdinalIgnoreCase))
            {
                if (uid.Length > bestLen)
                {
                    bestMatch = mod;
                    bestLen = uid.Length;
                }
            }
        }
        if (bestMatch?.Manifest != null)
            return bestMatch.Manifest.Name;

        int dot = key.IndexOf('.');
        return dot > 0 ? key.Substring(0, dot) : "Stardew Valley";
    }
}
