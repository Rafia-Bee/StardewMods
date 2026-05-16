using HarmonyLib;
using StardewValley;

namespace MoreQuestsFramework.Patches;

// Winter Star's chooseSecretSantaGift bypasses tryToReceiveActiveObject (the path that
// normally pings active quests), so without this bridge an AdventureQuest Gift step
// targeting the player's secret friend never sees the festival gift.
internal static class WinterStarGiftPatch
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(NPC), nameof(NPC.receiveGift)),
            postfix: new HarmonyMethod(typeof(WinterStarGiftPatch), nameof(ReceiveGift_Postfix)));
    }

    public static void ReceiveGift_Postfix(NPC __instance, Object o, Farmer giver)
    {
        if (__instance == null || o == null || giver == null)
            return;
        var ev = Game1.CurrentEvent;
        if (ev == null || ev.secretSantaRecipient == null || ev.secretSantaRecipient != __instance)
            return;
        giver.NotifyQuests(q => q.OnItemOfferedToNpc(__instance, o, probe: false), onlyOneQuest: true);
    }
}
