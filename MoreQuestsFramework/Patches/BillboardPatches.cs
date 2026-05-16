using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

namespace MoreQuestsFramework.Patches;

// Redirects every Game1.questOfTheDay getter inside Billboard to the currently-selected
// slot's Quest, and on draw swaps vanilla's daily-quest Billboard for our multi-slot subclass.
internal static class BillboardPatches
{
    public static void Apply(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Constructor(typeof(Billboard), new[] { typeof(bool) }),
            transpiler: new HarmonyMethod(typeof(BillboardPatches), nameof(Ctor_Transpiler)));

        harmony.Patch(
            original: AccessTools.Method(typeof(Billboard), nameof(Billboard.draw), new[] { typeof(SpriteBatch) }),
            prefix: new HarmonyMethod(typeof(BillboardPatches), nameof(Draw_Prefix)),
            transpiler: new HarmonyMethod(typeof(BillboardPatches), nameof(Generic_Transpiler)));

        harmony.Patch(
            original: AccessTools.Method(typeof(Billboard), nameof(Billboard.receiveLeftClick)),
            prefix: new HarmonyMethod(typeof(BillboardPatches), nameof(Click_Prefix)),
            postfix: new HarmonyMethod(typeof(BillboardPatches), nameof(Click_Postfix)),
            transpiler: new HarmonyMethod(typeof(BillboardPatches), nameof(Generic_Transpiler)));

        harmony.Patch(
            original: AccessTools.Method(typeof(Billboard), nameof(Billboard.performHoverAction)),
            transpiler: new HarmonyMethod(typeof(BillboardPatches), nameof(Generic_Transpiler)));

        harmony.Patch(
            original: AccessTools.Method(typeof(Game1), nameof(Game1.CanAcceptDailyQuest)),
            prefix: new HarmonyMethod(typeof(BillboardPatches), nameof(CanAccept_Prefix)));
    }

    private static IEnumerable<CodeInstruction> RedirectQuestOfTheDay(IEnumerable<CodeInstruction> instructions)
    {
        var questOfTheDayGetter = AccessTools.PropertyGetter(typeof(Game1), nameof(Game1.questOfTheDay));
        var replacement = AccessTools.Method(typeof(BillboardPatches), nameof(GetSelectedQuest));
        var codes = new List<CodeInstruction>(instructions);
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(questOfTheDayGetter))
            {
                var newInsn = new CodeInstruction(OpCodes.Call, replacement) { labels = codes[i].labels };
                codes[i] = newInsn;
            }
        }
        return codes;
    }

    public static IEnumerable<CodeInstruction> Ctor_Transpiler(IEnumerable<CodeInstruction> instructions)
        => RedirectQuestOfTheDay(instructions);

    public static IEnumerable<CodeInstruction> Generic_Transpiler(IEnumerable<CodeInstruction> instructions)
        => RedirectQuestOfTheDay(instructions);

    public static Quest? GetSelectedQuest()
    {
        var sel = BillboardSlots.Selected;
        if (sel != null)
            return sel.Quest;
        return Game1.questOfTheDay;
    }

    public static bool CanAccept_Prefix(ref bool __result)
    {
        if (BillboardSlots.Slots.Count > 0)
        {
            __result = BillboardSlots.Selected != null && !BillboardSlots.Selected.Accepted;
            return false;
        }
        return true;
    }

    public static bool Draw_Prefix(Billboard __instance, bool ___dailyQuestBoard)
    {
        if (!___dailyQuestBoard)
            return true;
        if (__instance is MoreQuestsBillboard)
            return true;
        if (Game1.activeClickableMenu is MoreQuestsBillboard)
            return true;
        if (BillboardSlots.Slots.Count == 0)
            return true;

        Game1.activeClickableMenu = new MoreQuestsBillboard();
        return false;
    }

    public static void Click_Prefix(Billboard __instance, bool ___dailyQuestBoard,
        int x, int y, out bool __state)
    {
        __state = false;
        if (!___dailyQuestBoard)
            return;
        if (BillboardSlots.Selected == null)
            return;
        if (__instance.acceptQuestButton == null || !__instance.acceptQuestButton.visible)
            return;
        __state = __instance.acceptQuestButton.containsPoint(x, y);
    }

    public static void Click_Postfix(Billboard __instance, bool ___dailyQuestBoard,
        int x, int y, bool __state)
    {
        if (!___dailyQuestBoard)
            return;

        if (Game1.activeClickableMenu is MoreQuestsBillboard)
        {
            if (__state)
            {
                // Vanilla's accept hardcodes daysLeft=2; capture before AcceptSelected drops
                // the slot so we can restore the configured deadline.
                var sel = BillboardSlots.Selected;
                int deadline = sel != null ? Math.Max(1, sel.Posting.DeadlineDays) : 2;
                var accepted = BillboardSlots.AcceptSelected();
                // dailyQuest.Value=true (set by vanilla) is preserved so completion side
                // effects fire: stats increment, prize ticket every 3rd quest, milestone mail.
                if (accepted != null)
                    accepted.daysLeft.Value = deadline;
                MoreQuestsBillboard.InnerBillboard = null;
                Game1.activeClickableMenu = new MoreQuestsBillboard();
                return;
            }
            if (__instance.upperRightCloseButton != null && __instance.upperRightCloseButton.containsPoint(x, y))
            {
                MoreQuestsBillboard.InnerBillboard = null;
                BillboardSlots.Selected = null;
            }
        }
    }
}
