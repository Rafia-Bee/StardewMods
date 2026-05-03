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

    public NetStringList SerializedRewards => serializedRewards;

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(serializedRewards, "serializedRewards")
            .AddField(alternativeItemIds, "alternativeItemIds");
    }

    /// Fully replaces vanilla's `ItemDeliveryQuest.OnItemOfferedToNpc` so the implicit
    /// 150/255 friendship bump is skipped. The declarative `Rewards` block is the only
    /// payout path.
    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
        if (completed.Value)
            return false;
        if (!npc.IsVillager || npc.Name != target.Value || !ItemMatchesObjective(item))
            return false;

        if (item.Stack < number.Value)
        {
            if (!probe)
            {
                npc.CurrentDialogue.Push(Dialogue.FromTranslation(npc, "Strings\\StringsFromCSFiles:ItemDeliveryQuest.cs.13615", number.Value));
                Game1.drawDialogue(npc);
            }
            return false;
        }

        if (probe)
            return true;

        Game1.player.Items.Reduce(item, number.Value);
        reloadDescription();
        npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage));
        Game1.drawDialogue(npc);
        questComplete();
        return true;
    }

    /// Compare offered item against `ItemId` plus any `alternativeItemIds`. Both qualified
    /// and bare ids are tolerated so author input can use either form.
    private bool ItemMatchesObjective(Item item)
    {
        if (item == null)
            return false;
        if (Match(item, ItemId.Value))
            return true;
        for (int i = 0; i < alternativeItemIds.Count; i++)
        {
            if (Match(item, alternativeItemIds[i]))
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
