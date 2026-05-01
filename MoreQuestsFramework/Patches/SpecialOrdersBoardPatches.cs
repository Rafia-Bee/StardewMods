using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Pipeline;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Menus;
using StardewValley.SpecialOrders;

namespace MoreQuestsFramework.Patches;

/// Pagination layer on top of vanilla's `SpecialOrdersBoard`. Vanilla hardcodes two slots
/// (`leftOrder` / `rightOrder`) populated by `UpdateAvailableSpecialOrders`, which on the
/// weekly refresh picks two random eligible entries from `Data/SpecialOrders`. With many
/// SpecialOrder-shipping mods loaded the player only ever sees 2 of N possible orders.
///
/// These patches surface every eligible order on the board, paginated 2 per page, capped
/// at `Config.SpecialOrdersBoardPages` pages (1 = vanilla behaviour, no patches active).
/// The accept flow remains entirely vanilla — we only swap `leftOrder` / `rightOrder` to
/// the current page's slice and add prev/next chrome. Existing modded SpecialOrders flow
/// through the same `availableSpecialOrders` list and appear naturally in the rotation;
/// no orders are evicted, no slots are stolen.
internal static class SpecialOrdersBoardPatches
{
    /// Per-instance pagination state. ConditionalWeakTable lets the GC reap entries when
    /// the menu is closed without us needing an explicit cleanup hook.
    private static readonly ConditionalWeakTable<SpecialOrdersBoard, BoardState> _state = new();

    private static IMonitor? _monitor;
    private static SpecialOrderWriter? _writer;

    /// Standard back/forward arrow sprite rects on `Game1.mouseCursors`, 4x scale.
    private static readonly Rectangle BackArrowSrc = new(352, 495, 12, 11);
    private static readonly Rectangle ForwardArrowSrc = new(365, 495, 12, 11);

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
    }

    private sealed class BoardState
    {
        public int PageIndex;
        /// Snapshot of every eligible order for this board's `boardType`, in stable order.
        /// Page N renders Orders[N*2] (left) + Orders[N*2 + 1] (right).
        public List<SpecialOrder> Orders = new();
        public ClickableTextureComponent? PrevButton;
        public ClickableTextureComponent? NextButton;
        public int TotalPagesCapped;

        public bool ShouldShow => Orders.Count > 2 && TotalPagesCapped > 1;
    }

    /// After vanilla's constructor populates `availableSpecialOrders` (via the early-return
    /// path or a fresh refresh) and assigns `leftOrder` / `rightOrder`, we walk every eligible
    /// `Data/SpecialOrders` entry for this board's `boardType` and add any missing ones to
    /// `availableSpecialOrders`. Then we snapshot the resulting list and re-set the slots
    /// for page 0. No-op when `SpecialOrdersBoardPages <= 1`.
    public static void Ctor_Postfix(SpecialOrdersBoard __instance, string board_type)
    {
        try
        {
            int maxPages = Math.Clamp(ModEntry.Config.SpecialOrdersBoardPages, 1, 3);
            if (maxPages <= 1)
                return;

            // Inject every eligible order vanilla didn't pick into availableSpecialOrders.
            // Use the same eligibility check vanilla does (CanStartOrderNow + dedup against
            // currently-active and currently-available orders).
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

            // Step 1: guarantee framework-emitted orders surface on the board. They were
            // explicitly emitted by a content-mod's trigger fire, so a 1/N random fall
            // would defeat the trigger's intent. Pull them out of the eligible pool first
            // and add them deterministically (in emission order, newest last).
            if (_writer != null && slotsAvailable > 0)
            {
                foreach (var emitted in _writer.EmittedOrders)
                {
                    if (slotsAvailable <= 0)
                        break;
                    int idx = eligible.IndexOf(emitted.OrderId);
                    if (idx < 0)
                        continue; // not eligible (already accepted, expired, etc.)
                    eligible.RemoveAt(idx);

                    var order = SpecialOrder.GetSpecialOrder(emitted.OrderId, rng.Next());
                    if (order == null)
                        continue;
                    team.availableSpecialOrders.Add(order);
                    slotsAvailable--;
                }
            }

            // Step 2: random-fill remaining slots from the unguaranteed pool (vanilla +
            // every other mod's entries). Seeded by save uid + day so navigation is
            // reproducible across menu open/close on the same day.
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

            // Snapshot the now-extended list (filtered to this board).
            var snapshot = new List<SpecialOrder>();
            foreach (var so in team.availableSpecialOrders)
                if (so != null && so.orderType.Value == board_type)
                    snapshot.Add(so);

            if (snapshot.Count <= 2)
                return; // vanilla view is fine

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
        }
        catch (Exception ex)
        {
            _monitor?.Log($"SpecialOrdersBoard pagination postfix failed: {ex.Message}", LogLevel.Warn);
        }
    }

    /// Renders the prev/next arrow buttons + a "Page N / M" indicator above them. Only
    /// runs when `_state` has an entry for this menu instance and pagination is meaningful.
    public static void Draw_Postfix(SpecialOrdersBoard __instance, SpriteBatch b)
    {
        try
        {
            if (!_state.TryGetValue(__instance, out var state))
                return;
            if (!state.ShouldShow || state.PrevButton == null || state.NextButton == null)
                return;

            // Arrow buttons use vanilla's standard back/forward sprites at 4x scale.
            // Disabled (greyed) when we're at the start/end of the page range.
            bool canPrev = state.PageIndex > 0;
            bool canNext = state.PageIndex < state.TotalPagesCapped - 1;

            DrawArrow(b, state.PrevButton, BackArrowSrc, canPrev);
            DrawArrow(b, state.NextButton, ForwardArrowSrc, canNext);

            // "Page N / M" indicator centered between the arrows.
            string label = $"Page {state.PageIndex + 1} / {state.TotalPagesCapped}";
            var font = Game1.smallFont;
            var size = font.MeasureString(label);
            int midX = (state.PrevButton.bounds.Right + state.NextButton.bounds.Left) / 2;
            int midY = state.PrevButton.bounds.Y + (state.PrevButton.bounds.Height - (int)size.Y) / 2;
            Utility.drawTextWithShadow(
                b, label, font,
                new Vector2(midX - size.X / 2, midY),
                Game1.textColor);

            // Re-draw mouse cursor on top so it isn't hidden behind our chrome.
            __instance.drawMouse(b);
        }
        catch (Exception ex)
        {
            _monitor?.Log($"SpecialOrdersBoard pagination draw failed: {ex.Message}", LogLevel.Warn);
        }
    }

    /// Intercepts clicks on our prev/next arrows. Returns false to swallow the event so
    /// vanilla's accept-button hit-test doesn't also fire on the same click.
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

    /// Sets `leftOrder` / `rightOrder` to the current page's slice. Vanilla's existing
    /// accept flow reads from these fields, so accepts continue to work unchanged.
    private static void ApplyPage(SpecialOrdersBoard board, BoardState state)
    {
        int leftIdx = state.PageIndex * 2;
        int rightIdx = leftIdx + 1;
        board.leftOrder = leftIdx < state.Orders.Count ? state.Orders[leftIdx] : null;
        board.rightOrder = rightIdx < state.Orders.Count ? state.Orders[rightIdx] : null;
        // Vanilla's UpdateButtons() recomputes accept-button visibility based on current
        // state (including whether the player has already accepted an order this week).
        board.UpdateButtons();
    }

    private static void BuildPagerButtons(SpecialOrdersBoard board, BoardState state)
    {
        // Place buttons centered above the close button (top-right), inside the board
        // chrome but clear of the "Choose One" banner that floats above the menu.
        int closeX = board.xPositionOnScreen + board.width - 20;
        int closeY = board.yPositionOnScreen;

        // 48x44 buttons matching vanilla's nav arrows. Stack them just under the close
        // button so they don't overlap the order portraits.
        int rowY = closeY + 60;
        int prevX = closeX - 48 - 140; // leave ~140px between arrows for the "Page N/M" label
        int nextX = closeX - 48;

        state.PrevButton = new ClickableTextureComponent(
            new Rectangle(prevX, rowY, 48, 44),
            Game1.mouseCursors, BackArrowSrc, 4f);
        state.NextButton = new ClickableTextureComponent(
            new Rectangle(nextX, rowY, 48, 44),
            Game1.mouseCursors, ForwardArrowSrc, 4f);
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
