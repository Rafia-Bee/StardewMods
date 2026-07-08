using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;
using System;
using System.Xml.Serialization;

namespace MoreQuestsFramework.Quests;

// Replaces vanilla ItemDeliveryQuest's money + fixed-255 friendship payout with the
// framework's declarative RewardSpec list applied through RewardApplier.
[XmlType("Mods_RafiaBee_MoreQuestsFramework_ItemDeliveryQuest")]
public sealed class MoreQuestsItemDeliveryQuest : ItemDeliveryQuest, IRewardedQuest
{
    public readonly NetStringList serializedRewards = new();

    public readonly NetStringList alternativeItemIds = new();

    // Per-alt required stack size. Lets one quest accept e.g. 100 Stone OR 10 Clay
    // OR 5 Copper Bars. Missing/zero entries fall back to number.Value.
    public readonly NetIntList alternativeItemQuantities = new();

    // 0 = any quality. 1 = silver+, 2 = gold+, 4 = iridium (3 unused by vanilla).
    public readonly NetInt minQuality = new();

    // Captured before consume so QuestCompleted listeners can read it (e.g. Gunther's
    // Dinosaur Study returns a one-tier-higher egg).
    public readonly NetInt deliveredQuality = new();

    public readonly NetInt delivered = new();

    // For mixed-alt quests, first delivery locks the quest to that id; other matched
    // alts fall through to vanilla gifting. Empty for uniform-qty alts (e.g. $edible-egg).
    public readonly NetString lockedItemId = new();

    public readonly NetInt lockedRequiredQty = new();

    // Captured on first journal read so reloadObjective can rebuild "<base> (X/Y)".
    public readonly NetString baseObjective = new();

    [XmlIgnore]
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

    // Adds partial-stack accumulation: 4 of 7 eggs counts as progress instead of
    // falling through to the gift flow.
    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
        => TryAccept(npc, item, probe, showNpcDialogue: true);

    // Mail Services Mod's mailbox flow goes through here too, with showNpcDialogue
    // driven by its ShowDialogOnItemDelivery setting (the NPC isn't actually present).
    internal bool TryAccept(NPC npc, Item item, bool probe, bool showNpcDialogue)
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
                // First delivery for a mixed-alt quest: lock this id.
                requiredTotal = matchedQty;
            }
            else if (string.Equals(matchedId, lockedItemId.Value, StringComparison.OrdinalIgnoreCase))
            {
                requiredTotal = lockedRequiredQty.Value;
            }
            else
            {
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
            if (showNpcDialogue)
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage));
                Game1.drawDialogue(npc);
            }
            questComplete();
        }
        else
        {
            Game1.playSound("give_gift");
            string partial = TryGetPartialDialogue(requiredTotal - delivered.Value);
            if (!string.IsNullOrEmpty(partial))
            {
                if (showNpcDialogue)
                {
                    npc.CurrentDialogue.Push(new Dialogue(npc, null, partial));
                    Game1.drawDialogue(npc);
                }
                else
                {
                    Game1.drawObjectDialogue(partial);
                }
            }
        }
        return true;
    }

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

    // Rewards live here, not in OnItemOfferedToNpc, so any completion path (in-person,
    // Mail Services Mod's mailbox flow, etc.) produces the same payout.
    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }

    // Quest.currentObjective calls reloadObjective on every read. Vanilla
    // ItemDeliveryQuest clobbers _currentObjective from objective.Value, which our
    // framework quests don't populate, so this override is the only path.
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

        _currentObjective = total > 1
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
