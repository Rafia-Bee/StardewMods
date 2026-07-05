using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
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
            transpiler: new HarmonyMethod(typeof(BillboardPatches), nameof(Draw_Transpiler)));

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

    // First redirect questOfTheDay (like the other patched methods), then redirect the
    // description draw so long quest text shrinks to fit instead of spilling past the panel.
    public static IEnumerable<CodeInstruction> Draw_Transpiler(IEnumerable<CodeInstruction> instructions)
        => FitDescription(RedirectQuestOfTheDay(instructions));

    private static IEnumerable<CodeInstruction> FitDescription(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var parseText = AccessTools.Method(typeof(Game1), nameof(Game1.parseText),
            new[] { typeof(string), typeof(SpriteFont), typeof(int) });
        var replacement = AccessTools.Method(typeof(BillboardPatches), nameof(DrawFittedDescription));

        int parseIndex = -1;
        for (int i = 0; i < codes.Count; i++)
        {
            if (parseIndex < 0)
            {
                if (codes[i].Calls(parseText))
                    parseIndex = i;
                continue;
            }

            if (codes[i].opcode == OpCodes.Call
                && codes[i].operand is System.Reflection.MethodInfo mi
                && mi.DeclaringType == typeof(Utility)
                && mi.Name == nameof(Utility.drawTextWithShadow))
            {
                codes[i] = new CodeInstruction(OpCodes.Call, replacement) { labels = codes[i].labels };
                break;
            }
        }
        return codes;
    }

    // Drop-in for Utility.drawTextWithShadow's 11-arg string overload. Shrinks the rendered
    // quest description until it fits the vertical space between yPos+256 (text top) and the
    // accept button / reward icon area. Width was already constrained by parseText(.., 640),
    // so a uniform scale just shrinks lines further into the same column.
    public static void DrawFittedDescription(SpriteBatch b, string text, SpriteFont font,
        Vector2 position, Color color, float scale, float layerDepth,
        int horizontalShadowOffset, int verticalShadowOffset,
        float shadowIntensity, int numShadows)
    {
        // The reward icon is drawn at yPos+576 (panel-relative) when BillboardQuestsDone % 3 == 2.
        // Without the icon, we have until the accept button at yPos+664. Description top is at
        // yPos+256, so usable height is 320 (with icon) or 408 (without).
        bool rewardIconShowing = Game1.stats.Get("BillboardQuestsDone") % 3 == 2;
        float maxHeight = rewardIconShowing ? 320f : 408f;

        float drawScale = scale;
        if (!string.IsNullOrEmpty(text))
        {
            Vector2 size = font.MeasureString(text) * scale;
            if (size.Y > maxHeight)
                drawScale = Math.Max(0.55f, scale * (maxHeight / size.Y));
        }

        Utility.drawTextWithShadow(b, text, font, position, color, drawScale,
            layerDepth, horizontalShadowOffset, verticalShadowOffset,
            shadowIntensity, numShadows);
    }

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
                // the slot so we can restore the configured deadline. Pass the captured slot
                // explicitly so a third-party Harmony patch swapping Selected mid-call can't
                // route the deadline onto a different quest.
                var sel = BillboardSlots.Selected;
                int deadline = sel != null ? Math.Max(1, sel.Posting.DeadlineDays) : 2;
                var accepted = BillboardSlots.AcceptSelected(sel);
                // dailyQuest.Value=true (set by vanilla) is preserved so completion side
                // effects fire: stats increment, prize ticket every 3rd quest, milestone mail.
                if (accepted != null)
                {
                    accepted.daysLeft.Value = deadline;
                    // Vanilla's accept hardcodes canBeCancelled=true on the quest. Re-apply
                    // the posting's setting so an author who opted a board quest out of
                    // cancelling is honored.
                    if (sel != null)
                        accepted.canBeCancelled.Value = sel.Posting.CanBeCancelled;
                }
                MoreQuestsBillboard.InnerBillboard = null;
                Game1.activeClickableMenu = new MoreQuestsBillboard();
                return;
            }
            if (__instance.upperRightCloseButton != null && __instance.upperRightCloseButton.containsPoint(x, y))
            {
                MoreQuestsBillboard.InnerBillboard = null;
                BillboardSlots.Selected = null;
                // Re-snap so gamepad nav recovers from the orphaned accept-button snap.
                if (Game1.options.SnappyMenus && Game1.activeClickableMenu is MoreQuestsBillboard outer)
                    outer.snapToDefaultClickableComponent();
            }
        }
    }
}
