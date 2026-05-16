using System.Collections.Generic;
using HarmonyLib;
using MoreQuestsFramework.Quests;
using StardewValley.Quests;

namespace MoreQuestsFramework.Patches;

// Quest.GetObjectiveDescriptions is non-virtual, so a subclass override won't reach
// the journal call site at QuestLog.cs:462. Hence this postfix bridge.
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
