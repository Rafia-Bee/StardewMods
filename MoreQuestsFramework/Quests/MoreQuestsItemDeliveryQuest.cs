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

    public NetStringList SerializedRewards => serializedRewards;

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields.AddField(serializedRewards, "serializedRewards");
    }

    /// Fully replaces vanilla's `ItemDeliveryQuest.OnItemOfferedToNpc` so the implicit
    /// 150/255 friendship bump is skipped. The declarative `Rewards` block is the only
    /// payout path.
    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
        if (completed.Value)
            return false;
        if (!npc.IsVillager || npc.Name != target.Value || item.QualifiedItemId != ItemId.Value)
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

    /// Reward awarding lives here (not in `OnItemOfferedToNpc`) so any completion path
    /// produces the same payout: vanilla in-person delivery, Mail Services Mod's
    /// mailbox-delivery flow, or any other mod that funnels into `questComplete`.
    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        base.questComplete();
    }
}
