using System.Collections.Generic;
using HarmonyLib;
using MoreQuestsFramework.Quests;
using StardewValley.Quests;

namespace MoreQuestsFramework.Patches;

/// Harmony postfix on `Quest.GetObjectiveDescriptions` so the journal can render an
/// `AdventureQuest`'s currently-active steps as parallel bullets. Vanilla's method is
/// non-virtual (see Quest.cs:655 — `public List<string> GetObjectiveDescriptions()`), so a
/// subclass override won't reach the journal call site at QuestLog.cs:462.
///
/// Patch is gated on `__instance is AdventureQuest`: every other Quest takes one type
/// check and returns the vanilla single-entry list unchanged. Aligns with §8.1.
internal static class AdventureQuestPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(Quest), nameof(Quest.GetObjectiveDescriptions)),
            postfix: new HarmonyMethod(typeof(AdventureQuestPatches), nameof(GetObjectiveDescriptions_Postfix)));
    }

    public static void GetObjectiveDescriptions_Postfix(Quest __instance, ref List<string> __result)
    {
        if (__instance is not AdventureQuest adventure)
            return;
        __result = adventure.BuildActiveObjectiveDescriptions();
    }
}
