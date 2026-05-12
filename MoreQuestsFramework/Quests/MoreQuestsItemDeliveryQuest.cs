using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;
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

    public NetStringList SerializedRewards => serializedRewards;

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(serializedRewards, "serializedRewards")
            .AddField(alternativeItemIds, "alternativeItemIds")
            .AddField(alternativeItemQuantities, "alternativeItemQuantities")
            .AddField(minQuality, "minQuality")
            .AddField(deliveredQuality, "deliveredQuality");
    }

    /// Fully replaces vanilla's `ItemDeliveryQuest.OnItemOfferedToNpc` so the implicit
    /// 150/255 friendship bump is skipped. The declarative `Rewards` block is the only
    /// payout path.
    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
        if (completed.Value)
            return false;
        if (!npc.IsVillager || npc.Name != target.Value)
            return false;
        if (!TryMatchObjective(item, out int requiredQty))
            return false;
        if (minQuality.Value > 0 && (item is not StardewValley.Object obj || obj.Quality < minQuality.Value))
            return false;

        if (item.Stack < requiredQty)
        {
            if (!probe)
            {
                npc.CurrentDialogue.Push(Dialogue.FromTranslation(npc, "Strings\\StringsFromCSFiles:ItemDeliveryQuest.cs.13615", requiredQty));
                Game1.drawDialogue(npc);
            }
            return false;
        }

        if (probe)
            return true;

        deliveredQuality.Value = (item as StardewValley.Object)?.Quality ?? 0;
        Game1.player.Items.Reduce(item, requiredQty);
        reloadDescription();
        npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage));
        Game1.drawDialogue(npc);
        questComplete();
        return true;
    }

    /// Compare offered item against `ItemId` plus any `alternativeItemIds`. Both qualified
    /// and bare ids are tolerated so author input can use either form. Emits the required
    /// stack size for the matched id (primary uses `number.Value`, alternatives use the
    /// parallel `alternativeItemQuantities` entry when present, otherwise fall back to
    /// `number.Value`).
    private bool TryMatchObjective(Item item, out int requiredQty)
    {
        requiredQty = number.Value;
        if (item == null)
            return false;
        if (Match(item, ItemId.Value))
            return true;
        for (int i = 0; i < alternativeItemIds.Count; i++)
        {
            if (!Match(item, alternativeItemIds[i]))
                continue;
            if (i < alternativeItemQuantities.Count && alternativeItemQuantities[i] > 0)
                requiredQty = alternativeItemQuantities[i];
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
        if (string.Equals(author, qid, System.StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(author, id, System.StringComparison.OrdinalIgnoreCase))
            return true;
        if (author.StartsWith("(", System.StringComparison.Ordinal)
            && string.Equals(author.Substring(author.IndexOf(')') + 1), id, System.StringComparison.OrdinalIgnoreCase))
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
}
