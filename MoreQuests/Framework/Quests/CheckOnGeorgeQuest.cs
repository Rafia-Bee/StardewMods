using System;
using System.Xml.Serialization;
using Netcode;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuests.Framework.Quests;

/// Custom quest where the player gifts George a liked or loved item, then reports
/// to Evelyn. Both NPCs are configurable via fields so the same class could be
/// repurposed in future. The gift is not consumed by this quest, gifting goes
/// through the normal NPC flow.
[XmlType("Mods_RafiaBee_MoreQuests_CheckOnGeorgeQuest")]
public sealed class CheckOnGeorgeQuest : Quest
{
    public readonly NetString giftRecipient = new();
    public readonly NetString reportTo = new();
    public readonly NetBool gifted = new();
    public readonly NetString reportMessage = new();

    protected override void initNetFields()
    {
        base.initNetFields();
        NetFields.AddField(giftRecipient, "giftRecipient")
            .AddField(reportTo, "reportTo")
            .AddField(gifted, "gifted")
            .AddField(reportMessage, "reportMessage");
    }

    public override bool OnItemOfferedToNpc(NPC npc, Item item, bool probe = false)
    {
        if (completed.Value || gifted.Value)
            return false;
        if (npc == null || item == null)
            return false;
        if (!string.Equals(npc.Name, giftRecipient.Value, StringComparison.OrdinalIgnoreCase))
            return false;

        int taste = npc.getGiftTasteForThisItem(item);
        if (taste != NPC.gift_taste_love && taste != NPC.gift_taste_like)
            return false;

        if (probe)
            return false;
        gifted.Value = true;
        return false;
    }

    public override bool OnNpcSocialized(NPC npc, bool probe = false)
    {
        if (completed.Value)
            return false;
        if (npc == null || !gifted.Value)
            return false;
        if (!string.Equals(npc.Name, reportTo.Value, StringComparison.OrdinalIgnoreCase))
            return false;
        if (probe)
            return true;

        if (!string.IsNullOrEmpty(reportMessage.Value))
        {
            npc.CurrentDialogue.Push(new Dialogue(npc, null, reportMessage.Value));
            Game1.drawDialogue(npc);
        }
        questComplete();
        return true;
    }
}
