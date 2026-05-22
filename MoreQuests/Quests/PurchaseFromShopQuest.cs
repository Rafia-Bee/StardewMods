using System;
using System.Xml.Serialization;
using MoreQuestsFramework.Rewards;
using Netcode;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuests.Quests;

/// Completes when the player buys `itemId` from the NPC named in `shopOwnerNpc`.
/// Used for items the framework's ItemDelivery flow can't handle, like tools (a
/// Milk Pail is a Tool, not an Object, so it can't be "delivered" via gifting).
/// The shop-purchase event is raised externally; this class just exposes the
/// completion entry point. Items aren't consumed, the player keeps what they buy.
[XmlType("Mods_RafiaBee_MoreQuests_PurchaseFromShopQuest")]
public sealed class PurchaseFromShopQuest : Quest, IRewardedQuest
{
    public readonly NetString itemId = new();
    public readonly NetString shopOwnerNpc = new();
    public readonly NetString targetMessage = new();
    public readonly NetStringList serializedRewards = new();

    public NetStringList SerializedRewards => serializedRewards;

    public PurchaseFromShopQuest()
    {
        // Keep the posting's title so vanilla doesn't regen it.
        _loadedTitle = true;
    }

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields
            .AddField(itemId, "itemId")
            .AddField(shopOwnerNpc, "shopOwnerNpc")
            .AddField(targetMessage, "targetMessage")
            .AddField(serializedRewards, "serializedRewards");
    }

    public override void questComplete()
    {
        if (completed.Value)
            return;
        RewardApplier.ApplyEncoded(serializedRewards);
        RewardApplier.FireEncodedConsequence(serializedRewards);
        base.questComplete();
    }

    /// True when this quest expects the given item to be purchased from the given
    /// shop owner. The caller passes the NPC who owns the active shop and the item
    /// that just landed in the player's inventory; case-insensitive match on both.
    public bool Matches(string ownerNpcName, string purchasedItemId)
    {
        if (completed.Value)
            return false;
        if (string.IsNullOrEmpty(ownerNpcName) || string.IsNullOrEmpty(purchasedItemId))
            return false;
        if (!string.Equals(ownerNpcName, shopOwnerNpc.Value, StringComparison.OrdinalIgnoreCase))
            return false;
        return ItemIdMatches(purchasedItemId);
    }

    /// External-purchase completion path. Optionally shows targetMessage as a Marnie
    /// dialogue if she's reachable when this fires.
    public void CompletePurchase()
    {
        if (completed.Value)
            return;
        if (!string.IsNullOrEmpty(targetMessage.Value) && !string.IsNullOrEmpty(shopOwnerNpc.Value))
        {
            var npc = Game1.getCharacterFromName(shopOwnerNpc.Value);
            if (npc != null)
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, targetMessage.Value));
                Game1.drawDialogue(npc);
            }
        }
        questComplete();
    }

    private bool ItemIdMatches(string purchasedItemId)
    {
        string expected = itemId.Value ?? string.Empty;
        if (string.IsNullOrEmpty(expected))
            return false;
        if (string.Equals(expected, purchasedItemId, StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(StripPrefix(expected), StripPrefix(purchasedItemId), StringComparison.OrdinalIgnoreCase);
    }

    private static string StripPrefix(string id) =>
        id.StartsWith("(", StringComparison.Ordinal) ? id.Substring(id.IndexOf(')') + 1) : id;
}
