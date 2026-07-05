using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.Pipeline;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Menus;
using StardewValley.SpecialOrders;

namespace MoreQuestsFramework.Patches;

// Paginates SpecialOrdersBoard so every eligible Data/SpecialOrders entry surfaces
// (capped at Config.SpecialOrdersBoardPages, 1 = vanilla, no patches active). Accept
// flow stays vanilla; we only swap leftOrder/rightOrder to the current page's slice.
internal static class SpecialOrdersBoardPatches
{
    private static readonly ConditionalWeakTable<SpecialOrdersBoard, BoardState> _state = new();

    private static IMonitor? _monitor;
    private static SpecialOrderWriter? _writer;

    private static readonly Rectangle BackArrowSrc = new(352, 495, 12, 11);
    private static readonly Rectangle ForwardArrowSrc = new(365, 495, 12, 11);

    // Picked far from vanilla's 0/1 (accept buttons) so getComponentWithID lookups can't collide.
    private const int PrevButtonId = -42100;
    private const int NextButtonId = -42101;
    private const int SnapAutomatic = -99998;

    public static void Apply(Harmony harmony, IMonitor monitor, SpecialOrderWriter writer)
    {
        _monitor = monitor;
        _writer = writer;

        harmony.Patch(
            original: AccessTools.Constructor(typeof(SpecialOrdersBoard), new[] { typeof(string) }),
            postfix: new HarmonyMethod(typeof(SpecialOrdersBoardPatches), nameof(Ctor_Postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(SpecialOrdersBoard), nameof(SpecialOrdersBoard.draw), new[] { typeof(SpriteBatch) }),
            postfix: new HarmonyMethod(typeof(SpecialOrdersBoardPatches), nameof(Draw_Postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(SpecialOrdersBoard), nameof(SpecialOrdersBoard.receiveLeftClick)),
            prefix: new HarmonyMethod(typeof(SpecialOrdersBoardPatches), nameof(ReceiveLeftClick_Prefix)));

        // populateClickableComponentList is declared on IClickableMenu, not overridden on
        // SpecialOrdersBoard, so Harmony makes us patch the declaring type. The postfix filters
        // by instance type so we don't touch other menus.
        harmony.Patch(
            original: AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.populateClickableComponentList)),
            postfix: new HarmonyMethod(typeof(SpecialOrdersBoardPatches), nameof(Populate_Postfix)));
    }

    private sealed class BoardState
    {
        public int PageIndex;
        // Page N renders Orders[N*2] (left) + Orders[N*2 + 1] (right).
        public List<SpecialOrder> Orders = new();
        public ClickableTextureComponent? PrevButton;
        public ClickableTextureComponent? NextButton;
        public int TotalPagesCapped;

        public bool ShouldShow => Orders.Count > 2 && TotalPagesCapped > 1;
    }

    public static void Ctor_Postfix(SpecialOrdersBoard __instance, string board_type)
    {
        try
        {
            int maxPages = Math.Clamp(ModEntry.Config.SpecialOrdersBoardPages, MoreQuestsFrameworkConfig.SpecialOrdersBoardPagesMin, MoreQuestsFrameworkConfig.SpecialOrdersBoardPagesMax);
            if (maxPages <= 1)
                return;

            // Same eligibility check vanilla uses (CanStartOrderNow + dedup).
            var team = Game1.player?.team;
            if (team == null)
                return;

            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var so in team.availableSpecialOrders)
                if (so?.questKey?.Value != null && so.orderType.Value == board_type)
                    existing.Add(so.questKey.Value);

            var data = DataLoader.SpecialOrders(Game1.content);
            var eligible = new List<string>();
            foreach (var (key, value) in data)
            {
                if (value == null)
                    continue;
                if (value.OrderType != board_type)
                    continue;
                if (existing.Contains(key))
                    continue;
                if (!SpecialOrder.CanStartOrderNow(key, value))
                    continue;
                eligible.Add(key);
            }

            int slotsAvailable = (maxPages * 2) - existing.Count;
            var rng = Utility.CreateRandom(Game1.uniqueIDForThisGame, (double)Game1.stats.DaysPlayed * 1.7);

            // Framework-emitted orders go in first (deterministic, newest last): a 1/N
            // random fall would defeat the trigger's intent.
            if (_writer != null && slotsAvailable > 0)
            {
                foreach (var emitted in _writer.EmittedOrders)
                {
                    if (slotsAvailable <= 0)
                        break;
                    int idx = eligible.IndexOf(emitted.OrderId);
                    if (idx < 0)
                        continue;
                    eligible.RemoveAt(idx);

                    var order = SpecialOrder.GetSpecialOrder(emitted.OrderId, rng.Next());
                    if (order == null)
                        continue;
                    team.availableSpecialOrders.Add(order);
                    slotsAvailable--;
                }
            }

            // Seeded by save uid + day so navigation is reproducible across reopens.
            if (slotsAvailable > 0 && eligible.Count > 0)
            {
                int take = Math.Min(slotsAvailable, eligible.Count);
                for (int i = 0; i < take; i++)
                {
                    int pick = rng.Next(eligible.Count);
                    string id = eligible[pick];
                    eligible.RemoveAt(pick);

                    var order = SpecialOrder.GetSpecialOrder(id, rng.Next());
                    if (order == null)
                        continue;
                    team.availableSpecialOrders.Add(order);
                }
            }

            var snapshot = new List<SpecialOrder>();
            foreach (var so in team.availableSpecialOrders)
                if (so != null && so.orderType.Value == board_type)
                    snapshot.Add(so);

            if (snapshot.Count <= 2)
                return;

            int totalPages = (snapshot.Count + 1) / 2;
            int cappedPages = Math.Min(maxPages, totalPages);

            var state = new BoardState
            {
                Orders = snapshot,
                PageIndex = 0,
                TotalPagesCapped = cappedPages
            };
            BuildPagerButtons(__instance, state);
            ApplyPage(__instance, state);
            _state.AddOrUpdate(__instance, state);

            // Vanilla's ctor already ran populateClickableComponentList before we existed in
            // _state, so the pager buttons aren't in the menu's CC list yet. Adding them now
            // means controller D-pad can auto-snap up from the accept buttons to Prev/Next.
            if (Game1.options.SnappyMenus && state.ShouldShow && __instance.allClickableComponents != null)
            {
                __instance.allClickableComponents.Add(state.PrevButton!);
                __instance.allClickableComponents.Add(state.NextButton!);
            }
        }
        catch (Exception ex)
        {
            _monitor?.Log($"SpecialOrdersBoard pagination postfix failed: {ex.Message}", LogLevel.Warn);
        }
    }

    public static void Draw_Postfix(SpecialOrdersBoard __instance, SpriteBatch b)
    {
        try
        {
            if (!_state.TryGetValue(__instance, out var state))
                return;
            if (!state.ShouldShow || state.PrevButton == null || state.NextButton == null)
                return;

            bool canPrev = state.PageIndex > 0;
            bool canNext = state.PageIndex < state.TotalPagesCapped - 1;

            DrawArrow(b, state.PrevButton, BackArrowSrc, canPrev);
            DrawArrow(b, state.NextButton, ForwardArrowSrc, canNext);

            string label = $"Page {state.PageIndex + 1} / {state.TotalPagesCapped}";
            var font = Game1.smallFont;
            var size = font.MeasureString(label);
            int midX = (state.PrevButton.bounds.Right + state.NextButton.bounds.Left) / 2;
            int midY = state.PrevButton.bounds.Y + (state.PrevButton.bounds.Height - (int)size.Y) / 2;
            Utility.drawTextWithShadow(
                b, label, font,
                new Vector2(midX - size.X / 2, midY),
                Game1.textColor);

            // Cursor on top so chrome doesn't hide it.
            __instance.drawMouse(b);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"SpecialOrdersBoard pagination draw failed: {ex.Message}", LogLevel.Warn);
        }
    }

    public static bool ReceiveLeftClick_Prefix(SpecialOrdersBoard __instance, int x, int y)
    {
        try
        {
            if (!_state.TryGetValue(__instance, out var state))
                return true;
            if (!state.ShouldShow || state.PrevButton == null || state.NextButton == null)
                return true;

            if (state.PrevButton.containsPoint(x, y) && state.PageIndex > 0)
            {
                state.PageIndex--;
                ApplyPage(__instance, state);
                Game1.playSound("smallSelect");
                return false;
            }
            if (state.NextButton.containsPoint(x, y) && state.PageIndex < state.TotalPagesCapped - 1)
            {
                state.PageIndex++;
                ApplyPage(__instance, state);
                Game1.playSound("smallSelect");
                return false;
            }
        }
        catch (Exception ex)
        {
            _monitor?.Log($"SpecialOrdersBoard pagination click failed: {ex.Message}", LogLevel.Warn);
        }
        return true;
    }

    private static void ApplyPage(SpecialOrdersBoard board, BoardState state)
    {
        int leftIdx = state.PageIndex * 2;
        int rightIdx = leftIdx + 1;
        board.leftOrder = leftIdx < state.Orders.Count ? state.Orders[leftIdx] : null;
        board.rightOrder = rightIdx < state.Orders.Count ? state.Orders[rightIdx] : null;
        board.UpdateButtons();
    }

    private static void BuildPagerButtons(SpecialOrdersBoard board, BoardState state)
    {
        int closeX = board.xPositionOnScreen + board.width - 20;
        int closeY = board.yPositionOnScreen;

        int rowY = closeY + 60;
        int prevX = closeX - 48 - 140;
        int nextX = closeX - 48;

        state.PrevButton = new ClickableTextureComponent(
            new Rectangle(prevX, rowY, 48, 44),
            Game1.mouseCursors, BackArrowSrc, 4f)
        {
            myID = PrevButtonId,
            leftNeighborID = SnapAutomatic,
            rightNeighborID = SnapAutomatic,
            upNeighborID = SnapAutomatic,
            downNeighborID = SnapAutomatic
        };
        state.NextButton = new ClickableTextureComponent(
            new Rectangle(nextX, rowY, 48, 44),
            Game1.mouseCursors, ForwardArrowSrc, 4f)
        {
            myID = NextButtonId,
            leftNeighborID = SnapAutomatic,
            rightNeighborID = SnapAutomatic,
            upNeighborID = SnapAutomatic,
            downNeighborID = SnapAutomatic
        };
    }

    // Vanilla's populateClickableComponentList uses reflection over the SpecialOrdersBoard's own
    // fields, so our pager buttons (held in BoardState, not on the menu) never get picked up.
    // Append them after vanilla finishes so gamepad auto-snap can land on them. Patches the
    // IClickableMenu declaring type since SOB doesn't override it; filter here so we don't
    // touch unrelated menus.
    public static void Populate_Postfix(IClickableMenu __instance)
    {
        if (__instance is not SpecialOrdersBoard board)
            return;
        try
        {
            if (!_state.TryGetValue(board, out var state))
                return;
            if (!state.ShouldShow || state.PrevButton == null || state.NextButton == null)
                return;
            if (board.allClickableComponents == null)
                return;
            board.allClickableComponents.Add(state.PrevButton);
            board.allClickableComponents.Add(state.NextButton);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"SpecialOrdersBoard pagination populate failed: {ex.Message}", LogLevel.Warn);
        }
    }

    private static void DrawArrow(SpriteBatch b, ClickableTextureComponent button, Rectangle sourceRect, bool enabled)
    {
        var tint = enabled ? Color.White : Color.White * 0.4f;
        b.Draw(
            button.texture,
            new Vector2(button.bounds.X, button.bounds.Y),
            sourceRect,
            tint,
            0f,
            Vector2.Zero,
            button.baseScale,
            SpriteEffects.None,
            0.88f);
    }
}
