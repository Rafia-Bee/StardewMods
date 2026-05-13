using HarmonyLib;
using StardewValley;

namespace MoreQuestsFramework.Patches;

/// Postfix on `NPC.receiveGift` so the Winter Star festival's secret-santa exchange
/// fires the quest log's `OnItemOfferedToNpc` hook. The festival's `chooseSecretSantaGift`
/// calls `receiveGift` directly with `updateGiftLimitInfo: false`, bypassing
/// `tryToReceiveActiveObject`, which is normally the only path that pings active quests.
/// Without this, an `AdventureQuest` Gift step targeting the player's secret friend never
/// sees the festival gift even though every other gameplay-side effect (friendship gain,
/// onGiftGiven dictionary update) still runs.
///
/// Scoped tight: only forwards when `Game1.CurrentEvent.secretSantaRecipient` matches
/// `__instance`. Out-of-festival gifts already route through `tryToReceiveActiveObject`,
/// so they get their `OnItemOfferedToNpc` hit from vanilla and don't need this bridge.
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
