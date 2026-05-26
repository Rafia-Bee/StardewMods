using System;
using System.Collections.Generic;
using QuestJournal.Api;
using StardewModdingAPI;
using StardewValley.Quests;
using StardewValley.SpecialOrders;

namespace QuestJournal.Menu;

// Shared snapshot builders for QuestJournal. Both the journal panel
// (JournalContext) and the natural-completion watcher need to derive the
// same reward lines / giver / source for a given Quest. Keeping this
// centralised avoids the two paths drifting apart and capturing
// different fields for the same quest.
internal static class QuestSnapshotBuilder
{
    public static List<RewardLineRow> BuildRewardLines(Quest q, IMoreQuestsApi? mqfApi)
    {
        var lines = new List<RewardLineRow>();

        // Always ask MQF first, regardless of IsManagedQuest. The framework's
        // _managed table is a ConditionalWeakTable populated only at quest
        // creation, so save-reloaded quests fall out of it even though their
        // SerializedRewards (NetStringList) survives the save trip. Gating
        // here on IsManagedQuest would silently drop the MQF lines for every
        // quest that pre-existed the current session. GetRewardLines itself
        // already returns an empty array for non-IRewardedQuest quests, so
        // calling it unconditionally is safe for vanilla quests too.
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
            lines.Add(new RewardLineRow(kind: "None", summary: "(none)"));

        return lines;
    }

    private static void SynthesiseVanillaRewardLines(Quest q, List<RewardLineRow> lines)
    {
        // GetMoneyReward() is virtual on Quest; ItemDeliveryQuest overrides
        // it to fall back to `item.Price * 3` when moneyReward.Value hasn't
        // been materialised. Other subclasses (ResourceCollection, Fishing,
        // SlayMonster) don't override and instead store their payout in a
        // sibling `reward` NetInt, so we have to probe those explicitly.
        int gold = q.GetMoneyReward();
        if (gold <= 0)
            gold = ResolveSubclassReward(q);
        if (gold > 0)
            lines.Add(new RewardLineRow(kind: "Money", summary: $"{gold}g", amount: gold));

        string? desc = q.rewardDescription.Value;
        // Vanilla quest data uses "-1" as the "no reward description" sentinel
        // (see Data/Quests). Suppress it so the panel doesn't show "- -1".
        if (!string.IsNullOrEmpty(desc) && desc != "-1")
            lines.Add(new RewardLineRow(kind: "Custom", summary: desc!));

        string? friend = ResolveVanillaFriendshipTarget(q);
        if (!string.IsNullOrEmpty(friend))
            lines.Add(new RewardLineRow(
                kind: "Friendship",
                summary: $"+250 friendship with {friend}",
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

        // MQF first. AdventureQuest stores the giver in giverNpc.Value (a NetString that
        // survives save reload), and MQF's GetGiverNpc also returns the vanilla subclass
        // fields when applicable. Doing this first means save-reloaded MQF mail/board
        // quests don't fall through to a null giver.
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

        // Data/Quests fallback. Vanilla ItemDelivery / LostItem / SecretLostItem /
        // Monster all carry the giver NPC in the first conditions field. This
        // catches cases where the subclass field is empty (Basic quest type
        // overriding a row that originally had an NPC, scripted quest creation
        // that bypassed loadQuestInfo, etc.).
        string? fromData = ResolveGiverFromQuestData(q.id?.Value);
        if (!string.IsNullOrEmpty(fromData)) return fromData;

        // Heuristic last resort: story quests (base Quest, ItemHarvestQuest,
        // CraftingQuest, SocializeQuest, ...) don't have an NPC field at all,
        // but the title and description nearly always name the giver in plain
        // text. Scan the combined text for any string that matches a known NPC
        // (internal name) and return the first one found. Falls back to the
        // possessive form ("Marnie's") so "Marnie's Request" resolves to Marnie.
        string? fromText = ResolveGiverFromText(q);
        if (!string.IsNullOrEmpty(fromText)) return fromText;

        // Diagnostic: log the quest type + id when we can't infer a giver, so
        // future bug reports include enough info to extend this resolver.
        try
        {
            ModEntry.Instance?.Monitor?.Log(
                $"No giver inferred for quest type={q.GetType().Name} id='{q.id?.Value ?? "(none)"}' title='{q.questTitle ?? "(none)"}'.",
                LogLevel.Trace);
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

        // Game1.characterData keys are NPC internal names ("Marnie", "Marlon",
        // "Clint", ...) plus Pet/Horse template ids we want to skip. Match each
        // candidate as a whole word, allowing an optional possessive "'s" suffix
        // so titles like "Marnie's Request" resolve. Title is searched first so
        // a quest titled after one NPC but mentioning others in the body still
        // attributes to the title NPC.
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
        // Track the earliest index so we return the first-mentioned NPC, not
        // whatever the dictionary happened to enumerate first.
        string best = string.Empty;
        int bestIdx = int.MaxValue;
        foreach (string name in npcs)
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Length < 3) continue; // skip stub ids ("X1", "Pet", etc.)
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
            // Require a non-letter boundary on both sides. Trailing apostrophe
            // ("Marnie's") counts as a non-letter so possessives still match.
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
            // First conditions token is the NPC name for the quest types that name one.
            switch (type)
            {
                case "ItemDelivery":
                case "LostItem":
                case "SecretLostItem":
                    return string.IsNullOrEmpty(split[0]) ? null : split[0];
                case "Monster":
                    // Monster quests: conditions are "<monster> <count> [<targetNpc> [<ignoreFarm>]]".
                    // The optional target NPC is the giver; fall back to null when it's missing.
                    if (split.Length >= 3 && !string.IsNullOrEmpty(split[2]) && split[2] != "null")
                        return split[2];
                    return null;
                default:
                    return null;
            }
        }
        catch { return null; }
    }

    public static string ResolveGiverDisplay(Quest q, IMoreQuestsApi? mqfApi = null)
    {
        string? name = ResolveGiverNpcName(q, mqfApi);
        return string.IsNullOrEmpty(name) ? "Unknown" : name!;
    }

    public static string ResolveSourceDisplay(Quest q, IMoreQuestsApi? mqfApi, IModHelper helper)
    {
        // MQF first. Many framework quests run as vanilla subclasses at
        // runtime (a MoreQuests "bring 10 potatoes to Robin" is still an
        // ItemDeliveryQuest), so the subclass check below would
        // misattribute them to vanilla if we ran it first. GetDefinitionId
        // returns null for anything MQF doesn't claim, so falling through
        // for vanilla billboard quests is safe.
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

        // Modded quest id by dotted-prefix convention. Both mod-authored vanilla
        // quests (e.g. "TheLimeyDragon.Ayeisha_Quest2") and most CP quest packs
        // namespace their ids with the author/mod prefix. Try longest-UniqueID
        // prefix match first so "RSV.Foo.Bar" doesn't get claimed by a plain
        // "RSV" mod when a more specific "RSV.Foo" pack is loaded.
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

        // Vanilla quest subclasses + base Quest. Story quests (Getting Started,
        // Mr. Qi's Plane Ride, etc.) are plain Quest instances with no
        // subclass, so we have to whitelist the bare type, not just the
        // subclasses. For these types we still have to decide whether the
        // entry came from vanilla Data/Quests or from a mod that edited the
        // same asset. We don't have a clean signal for that without scanning
        // content packs, so we lean on the id shape: pure-numeric ids ("1",
        // "21", "126") are how vanilla story / billboard quests are keyed, so
        // those stay "Stardew Valley". An id that contains any letter
        // ("VincentCheezeQuest") is almost certainly a mod-injected row, so
        // we surface the id verbatim. The player gets a hint that something
        // outside vanilla is involved without us guessing the wrong mod.
        if (q is ItemDeliveryQuest or FishingQuest or ResourceCollectionQuest or SlayMonsterQuest
            || q.GetType() == typeof(Quest))
        {
            if (LooksLikeModdedDataQuestId(questId))
                return questId!;
            return "Stardew Valley";
        }

        // Other modded Quest subclass. Best-effort: match the defining
        // assembly's short name against each loaded mod's manifest name
        // or UniqueID. Falls back to the assembly name itself (better than
        // a bare "Modded") and only "Modded" when even that's empty.
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

    // Special Orders don't go through MQF, so we can't ask the framework
    // who owns them. The next-best thing is the questKey itself: most
    // modded SOs prefix the key with their UniqueID or namespace
    // (e.g. "Lumisteria.MtVapiusSpecialOrders.MtVapiusFlowers",
    // "RSV.SpecialOrder.BugSteak"). Vanilla keys are unprefixed plain
    // strings ("Caroline", "Lewis", "Wizard2", ...), so a dot in the
    // key is a strong signal it's modded. Match against loaded mods by
    // longest-UniqueID-prefix.
    public static string ResolveSpecialOrderSource(SpecialOrder so, IModHelper helper)
    {
        if (so == null) return "Stardew Valley";

        // Qi board first; orderType.Value == "Qi" is set by vanilla.
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
            // Prefix match at a "." boundary so "RSV" doesn't accidentally
            // claim "RSVMod.Other.Thing". Also accept exact match.
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

        // Dot present but no mod claimed it. Best effort: surface the
        // first dotted segment so the player at least sees the namespace
        // rather than a wrong "Stardew Valley" attribution.
        int dot = key.IndexOf('.');
        return dot > 0 ? key.Substring(0, dot) : "Stardew Valley";
    }
}
