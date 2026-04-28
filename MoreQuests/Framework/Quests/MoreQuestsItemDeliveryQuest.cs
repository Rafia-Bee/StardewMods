using System.Xml.Serialization;
using MoreQuests.Framework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuests.Framework.Quests;

/// `ItemDeliveryQuest` variant that awards a configurable bonus item and friendship
/// boost on completion. Vanilla `ItemDeliveryQuest` only ever gives money + the
/// fixed 255 friendship to the recipient; this subclass layers our posting-defined
/// `ItemReward` / `FriendshipReward` on top.
[XmlType("Mods_RafiaBee_MoreQuests_ItemDeliveryQuest")]
public sealed class MoreQuestsItemDeliveryQuest : ItemDeliveryQuest
{
    public readonly NetString customItemReward = new();
    public readonly NetInt customItemRewardCount = new(1);
    public readonly NetString friendshipRewardNpc = new();
    public readonly NetInt friendshipRewardPoints = new();

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields.AddField(customItemReward, "customItemReward")
            .AddField(customItemRewardCount, "customItemRewardCount")
            .AddField(friendshipRewardNpc, "friendshipRewardNpc")
            .AddField(friendshipRewardPoints, "friendshipRewardPoints");
    }

    /// Fully replaces vanilla's `ItemDeliveryQuest.OnItemOfferedToNpc` so the implicit
    /// 150/255 friendship bump is skipped. Only the per-posting `FriendshipReward` is
    /// applied, keeping every reward explicit.
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
        RewardApplier.ApplyItemAndFriendship(
            itemId: customItemReward.Value,
            itemCount: customItemRewardCount.Value,
            npcName: friendshipRewardNpc.Value,
            friendshipPoints: friendshipRewardPoints.Value);
        base.questComplete();
    }
}
