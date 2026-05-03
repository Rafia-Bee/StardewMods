using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Posting.Boards;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

namespace MoreQuestsFramework.Patches;

/// Harmony patches that splice MoreQuestsBillboard into the vanilla Billboard pipeline.
/// Strategy mirrors the aedenthorn/HelpWanted reference: redirect every `Game1.questOfTheDay`
/// getter inside Billboard to our currently-selected slot's Quest, and on draw, swap the
/// vanilla daily-quest Billboard out for our multi-slot subclass.
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

    /// Replaces every call to `Game1.questOfTheDay` getter with our `BillboardPatches.GetSelectedQuest`.
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

    /// Used by the redirected callsites to fetch the selected quest (or fall back to vanilla's).
    public static Quest? GetSelectedQuest()
    {
        var sel = BillboardSlots.Selected;
        if (sel != null)
            return sel.Quest;
        var custom = CustomBoardSlots.Selected;
        if (custom != null)
            return custom.Quest;
        return Game1.questOfTheDay;
    }

    public static bool CanAccept_Prefix(ref bool __result)
    {
        if (CustomBoardSlots.Selected != null)
        {
            __result = !CustomBoardSlots.Selected.Accepted;
            return false;
        }
        if (BillboardSlots.Slots.Count > 0)
        {
            __result = BillboardSlots.Selected != null && !BillboardSlots.Selected.Accepted;
            return false;
        }
        return true;
    }

    /// If the player opens the daily-quest Billboard, swap it for our subclass (unless we're
    /// already showing a `MoreQuestsBillboard`, a `CustomBoardMenu` (whose inner accept popup
    /// reuses `Billboard(true)`), or there are no slot postings to display).
    public static bool Draw_Prefix(Billboard __instance, bool ___dailyQuestBoard)
    {
        if (!___dailyQuestBoard)
            return true;
        if (__instance is MoreQuestsBillboard)
            return true;
        if (Game1.activeClickableMenu is MoreQuestsBillboard)
            return true;
        if (Game1.activeClickableMenu is CustomBoardMenu)
            return true;
        if (BillboardSlots.Slots.Count == 0)
            return true;

        Game1.activeClickableMenu = new MoreQuestsBillboard();
        return false;
    }

    /// Records pre-click state so the postfix can detect whether the accept button was hit.
    public static void Click_Prefix(Billboard __instance, bool ___dailyQuestBoard,
        int x, int y, out bool __state)
    {
        __state = false;
        if (!___dailyQuestBoard)
            return;
        if (BillboardSlots.Selected == null && CustomBoardSlots.Selected == null)
            return;
        if (__instance.acceptQuestButton == null || !__instance.acceptQuestButton.visible)
            return;
        __state = __instance.acceptQuestButton.containsPoint(x, y);
    }

    /// If the click landed on the accept button while the inner Billboard was up, vanilla has
    /// just written the selected quest to questLog. Drop the slot and close the inner popup.
    public static void Click_Postfix(Billboard __instance, bool ___dailyQuestBoard,
        int x, int y, bool __state)
    {
        if (!___dailyQuestBoard)
            return;

        // Help-wanted board accept flow.
        if (Game1.activeClickableMenu is MoreQuestsBillboard)
        {
            if (__state)
            {
                // Capture posting before AcceptSelected drops the slot; vanilla's accept logic
                // hardcodes `daysLeft.Value = 2`, overwriting the configured deadline.
                var sel = BillboardSlots.Selected;
                int deadline = sel != null ? Math.Max(1, sel.Posting.DeadlineDays) : 2;
                var accepted = BillboardSlots.AcceptSelected();
                // Keep `dailyQuest.Value = true` (set by vanilla's accept logic) so vanilla's
                // billboard-quest side-effects on completion still fire: stats increment, prize
                // ticket every 3rd quest, the milestone mail flags. Just override the deadline,
                // which vanilla otherwise hardcodes to 2.
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
            return;
        }

        // Custom-board accept flow. The outer menu is a `CustomBoardMenu`; its inner
        // `Billboard(true)` popup runs the same accept-quest UX as the help-wanted board.
        if (Game1.activeClickableMenu is CustomBoardMenu customBoard)
        {
            if (__state)
            {
                var sel = CustomBoardSlots.Selected;
                int deadline = sel != null ? Math.Max(1, sel.Posting.DeadlineDays) : 2;
                var accepted = CustomBoardSlots.AcceptSelected();
                if (accepted != null)
                {
                    accepted.daysLeft.Value = deadline;
                    // CustomBoard quests are not "daily" quests in the vanilla
                    // `dailyQuest`/prize-ticket sense; clear the flag vanilla's accept
                    // logic flipped on so completion doesn't trigger billboard-quest
                    // milestone mail.
                    accepted.dailyQuest.Value = false;
                }
                customBoard.OnInnerAcceptClosed(reopen: true);
                return;
            }
            if (__instance.upperRightCloseButton != null && __instance.upperRightCloseButton.containsPoint(x, y))
            {
                customBoard.OnInnerAcceptClosed(reopen: false);
            }
        }
    }
}
