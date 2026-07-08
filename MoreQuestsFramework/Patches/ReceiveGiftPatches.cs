using HarmonyLib;
using MoreQuestsFramework.Quests;
using StardewValley;
using StardewValley.Quests;
using System.Collections.Generic;

namespace MoreQuestsFramework.Patches;

// Bridges NPC.receiveGift into the quest hooks for gifts that bypass the normal
// in-person path (Farmer.tryToReceiveActiveObject, which pings quests first):
// - Winter Star's chooseSecretSantaGift: full OnItemOfferedToNpc, so deliver AND
//   gift steps targeting the secret friend see the festival gift.
// - Everything else (notably Mail Services Mod's gift service, which calls
//   receiveGift directly): gift-kind steps only. In-person gifts already ticked
//   via tryToReceiveActiveObject, and gift steps only ever count once, so this
//   second look does nothing there. Mailed gifts only get counted here.
internal static class ReceiveGiftPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(NPC), nameof(NPC.receiveGift)),
            postfix: new HarmonyMethod(typeof(ReceiveGiftPatches), nameof(ReceiveGift_Postfix)));
    }

    public static void ReceiveGift_Postfix(NPC __instance, Object o, Farmer giver)
    {
        if (__instance == null || o == null || giver == null || giver != Game1.player)
            return;

        var ev = Game1.CurrentEvent;
        if (ev?.secretSantaRecipient != null && ev.secretSantaRecipient == __instance)
        {
            giver.NotifyQuests(q => q.OnItemOfferedToNpc(__instance, o, probe: false), onlyOneQuest: true);
            return;
        }

        // Snapshot: a completing quest removes itself from questLog mid-walk.
        var quests = new List<Quest>(giver.questLog);
        foreach (Quest quest in quests)
        {
            if (quest is AdventureQuest adventure && !adventure.completed.Value)
                adventure.ObserveGiftGiven(__instance, o);
        }
    }
}
