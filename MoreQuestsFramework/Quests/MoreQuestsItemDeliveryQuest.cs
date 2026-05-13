using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;
using System;
using System.Xml.Serialization;

namespace MoreQuestsFramework.Quests;

/// `ItemDeliveryQuest` variant that runs the framework's declarative reward block on
/// completion. Vanilla `ItemDeliveryQuest` only ever gives money + the fixed 255
/// friendship to the recipient; this subclass replaces both: vanilla's bonus
/// friendship is suppressed and the per-posting `RewardSpec` list is paid out via
/// `RewardApplier`.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_ItemDeliveryQuest")]
public sealed class MoreQuestsItemDeliveryQuest : ItemDeliveryQuest, IRewardedQuest
{
    public readonly NetStringList serializedRewards = new();

    /// OR-alternative item ids accepted in place of `ItemId`. Empty for single-item delivery
    /// (vanilla behaviour). Populated from a declarative `"Item": [...]` JSON objective so
    /// e.g. a "bring batteries OR coal" quest can satisfy on either id.
    public readonly NetStringList alternativeItemIds = new();

    /// Per-alternative required stack size, parallel to `alternativeItemIds`. When an
    /// alternative matches, the player must offer a stack of at least its quantity (instead
    /// of `number.Value`). Used by Robin's Silo Offer so 100 Stone OR 10 Clay OR 5 Copper
    /// Bars all satisfy the same posting. Entries missing or non-positive fall back to
    /// `number.Value` (vanilla ItemDelivery behaviour).
    public readonly NetIntList alternativeItemQuantities = new();

    /// Minimum `Object.Quality` required for a delivered item to count. 0 = base
    /// (vanilla behaviour, any quality accepted), 1 = silver, 2 = gold, 4 = iridium.
    /// Quality 3 is unused by vanilla; the matcher is `>=` so silver-or-better at 1,
    /// gold-or-better at 2, iridium only at 4. Populated from `QuestPosting.MinQuality`
    /// at posting time; serialized so the gate survives save/load.
    public readonly NetInt minQuality = new();

    /// Quality of the item the player actually delivered. Captured in
    /// `OnItemOfferedToNpc` before the stack is consumed so a `QuestCompleted` listener
    /// can read it and return a quality-tier-upgraded item (e.g. Gunther's Dinosaur
    /// Study returns a one-tier-higher Dinosaur Egg). 0 if never offered.
    public readonly NetInt deliveredQuality = new();

    /// Cumulative units the player has handed in toward this quest. Partial stacks count;
    /// when this reaches the locked-or-primary target the quest completes. Drives the
    /// "(delivered/total)" progress suffix on the journal objective line.
    public readonly NetInt delivered = new();

    /// First-offered item id when this quest has mixed alternativeItemQuantities (e.g.
    /// Robin's silo "100 Stone OR 10 Clay OR 5 Copper Bars"). Once set, only that id is
    /// accepted for the remaining deliveries; other matched alts fall through to vanilla
    /// gifting. Empty when no lock applies (uniform-qty alts like `$edible-egg` accept
    /// any matched id and accumulate freely).
    public readonly NetString lockedItemId = new();

    /// Required total for the locked id (mirrors alternativeItemQuantities[i] or
    /// number.Value for the primary). Zero when no lock is in effect.
    public readonly NetInt lockedRequiredQty = new();

    /// Captured at first journal read so `reloadObjective` can rebuild
    /// `_currentObjective` as `"<base> (X/Y)"` once partial progress exists. Without
    /// this we'd lose the base text once we appended a suffix.
    public readonly NetString baseObjective = new();

    public NetStringList SerializedRewards => serializedRewards;

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(serializedRewards, "serializedRewards")
            .AddField(alternativeItemIds, "alternativeItemIds")
            .AddField(alternativeItemQuantities, "alternativeItemQuantities")
            .AddField(minQuality, "minQuality")
            .AddField(deliveredQuality, "deliveredQuality")
            .AddField(delivered, "delivered")
            .AddField(lockedItemId, "lockedItemId")
            .AddField(lockedRequiredQty, "lockedRequiredQty")
            .AddField(baseObjective, "baseObjective");
    }

    /// Fully replaces vanilla's `ItemDeliveryQuest.OnItemOfferedToNpc` so the implicit
    /// 150/255 friendship bump is skipped. The declarative `Rewards` block is the only
    /// payout path. Also adds partial-stack accumulation: offering 4 of 7 eggs counts as
    /// progress instead of falling through to the gift flow.
    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
        if (completed.Value)
            return false;
        if (!npc.IsVillager || npc.Name != target.Value)
            return false;
        if (!TryMatchObjective(item, out int matchedQty, out string matchedId))
            return false;
        if (minQuality.Value > 0 && (item is not StardewValley.Object obj || obj.Quality < minQuality.Value))
            return false;

        bool mixedAltQty = HasMixedAlternativeQuantities();
        int requiredTotal;

        if (mixedAltQty)
        {
            if (lockedRequiredQty.Value <= 0)
            {
                // First delivery for a mixed-alt quest: this id becomes the only acceptable one.
                requiredTotal = matchedQty;
            }
            else if (string.Equals(matchedId, lockedItemId.Value, StringComparison.OrdinalIgnoreCase))
            {
                requiredTotal = lockedRequiredQty.Value;
            }
            else
            {
                // Quest already committed to a different alt; let vanilla gifting handle this one.
                return false;
            }
        }
        else
        {
            requiredTotal = number.Value;
        }

        int remaining = requiredTotal - delivered.Value;
        if (remaining <= 0)
            return false;

        if (probe)
            return true;

        if (mixedAltQty && lockedRequiredQty.Value <= 0)
        {
            lockedItemId.Value = matchedId;
            lockedRequiredQty.Value = matchedQty;
        }

        int accept = Math.Min(item.Stack, remaining);
        deliveredQuality.Value = (item as StardewValley.Object)?.Quality ?? 0;
        Game1.player.Items.Reduce(item, accept);
        delivered.Value += accept;

        if (delivered.Value >= requiredTotal)
        {
            npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage));
            Game1.drawDialogue(npc);
            questComplete();
        }
        else
        {
            Game1.playSound("give_gift");
            string partial = TryGetPartialDialogue(requiredTotal - delivered.Value);
            if (!string.IsNullOrEmpty(partial))
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, partial));
                Game1.drawDialogue(npc);
            }
        }
        return true;
    }

    /// Compare offered item against `ItemId` plus any `alternativeItemIds`. Both qualified
    /// and bare ids are tolerated so author input can use either form. Emits the required
    /// stack size for the matched id (primary uses `number.Value`, alternatives use the
    /// parallel `alternativeItemQuantities` entry when present, otherwise fall back to
    /// `number.Value`).
    private bool TryMatchObjective(Item item, out int requiredQty, out string matchedId)
    {
        requiredQty = number.Value;
        matchedId = string.Empty;
        if (item == null)
            return false;
        if (Match(item, ItemId.Value))
        {
            matchedId = ItemId.Value ?? string.Empty;
            return true;
        }
        for (int i = 0; i < alternativeItemIds.Count; i++)
        {
            if (!Match(item, alternativeItemIds[i]))
                continue;
            if (i < alternativeItemQuantities.Count && alternativeItemQuantities[i] > 0)
                requiredQty = alternativeItemQuantities[i];
            matchedId = alternativeItemIds[i] ?? string.Empty;
            return true;
        }
        return false;
    }

    /// True when at least one alternative carries a non-zero `alternativeItemQuantities`
    /// entry, signalling that alternatives have their own (possibly different) totals
    /// (Robin's silo, Submarine Fuel). When false (all alternatives share `number.Value`,
    /// e.g. `$edible-egg`), partial deliveries can mix-and-match alts freely. When true,
    /// the first-offered id locks the quest to that id for the remainder.
    private bool HasMixedAlternativeQuantities()
    {
        for (int i = 0; i < alternativeItemQuantities.Count; i++)
        {
            if (alternativeItemQuantities[i] > 0)
                return true;
        }
        return false;
    }

    private static bool Match(Item item, string author)
    {
        if (string.IsNullOrEmpty(author))
            return false;
        string qid = item.QualifiedItemId ?? string.Empty;
        string id = item.ItemId ?? string.Empty;
        if (string.Equals(author, qid, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(author, id, StringComparison.OrdinalIgnoreCase))
            return true;
        if (author.StartsWith("(", StringComparison.Ordinal)
            && string.Equals(author.Substring(author.IndexOf(')') + 1), id, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// Reward awarding lives here (not in `OnItemOfferedToNpc`) so any completion path
    /// produces the same payout: vanilla in-person delivery, Mail Services Mod's
    /// mailbox-delivery flow, or any other mod that funnels into `questComplete`.
    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }

    /// Vanilla's `Quest.currentObjective` getter calls `reloadObjective` on every read,
    /// so this is the seam for showing "(X/Y)" progress while a partial delivery is in
    /// flight. We capture the base text on first read (set by `QuestPoster` into
    /// `_currentObjective`) and rebuild from it each time. Vanilla `ItemDeliveryQuest`
    /// overrides this to clobber `_currentObjective` from `objective.Value`, but our
    /// framework quests don't populate `objective` so this override is the only path.
    public override void reloadObjective()
    {
        if (completed.Value)
            return;
        if (string.IsNullOrEmpty(baseObjective.Value) && !string.IsNullOrEmpty(_currentObjective))
            baseObjective.Value = _currentObjective;
        if (string.IsNullOrEmpty(baseObjective.Value))
            return;

        int total = HasMixedAlternativeQuantities() && lockedRequiredQty.Value > 0
            ? lockedRequiredQty.Value
            : number.Value;

        _currentObjective = delivered.Value > 0
            ? $"{baseObjective.Value} ({delivered.Value}/{total})"
            : baseObjective.Value;
    }

    private static string TryGetPartialDialogue(int remaining)
    {
        var translation = ModEntry.Translation;
        if (translation == null)
            return string.Empty;
        string text = translation.Get("quest.itemDelivery.partial.thanks", new { remaining }).ToString();
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
    }
}
