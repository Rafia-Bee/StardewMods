using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using QuestJournal.Api;
using QuestJournal.Menu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;
using StardewValley.SpecialOrders;

namespace QuestJournal.Hud;

// Draws the pinned quests as a top-right stack while the player is in the world.
// Each entry is the quest title plus its current objective (the active step for
// MQF Adventure quests), resolved live from player.questLog every frame so the
// text tracks progress without the journal being open.
internal sealed class PinnedObjectiveHud
{
    private readonly IModHelper _helper;
    private readonly IMoreQuestsApi? _mqfApi;

    // Box geometry. panelWidth and the top offset are the most likely things to
    // tune in-game; the offset is meant to clear the date/time/money box in the
    // top-right corner.
    private const int PanelWidth = 380;
    private const int Padding = 16;
    private const int RightMargin = 16;
    private const int TopOffset = 300;
    private const int EntryGap = 10;
    private const int ObjectiveIndent = 16;
    private const int MaxEntries = 8;

    private const int DragThreshold = 8;

    // _lastPanelBounds is the rect drawn last frame, used to hit-test a grab and
    // as the saved position on release. _entryBounds is one rect per drawn quest
    // entry (with its pin key) so a press can be hit-tested to a specific quest.
    private Rectangle _lastPanelBounds;
    private readonly List<(Rectangle Bounds, string Key)> _entryBounds = new();
    private bool _dragging;
    // A press inside the panel starts pending: it becomes a drag once the cursor
    // travels past the threshold, otherwise the release opens the clicked quest.
    private bool _pendingPress;
    private Vector2 _pressPos;
    private string? _pressedKey;
    private Vector2 _grabOffset;
    private int _dragBoxX;
    private int _dragBoxY;

    public PinnedObjectiveHud(IModHelper helper, IMoreQuestsApi? mqfApi)
    {
        _helper = helper;
        _mqfApi = mqfApi;
    }

    public void Register()
    {
        _helper.Events.Display.RenderedHud += OnRenderedHud;
        _helper.Events.Input.ButtonPressed += OnButtonPressed;
        _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (e.Button != SButton.MouseLeft) return;
        if (!ModEntry.Config.ShowHudPin || !Context.IsWorldReady) return;
        if (!Game1.displayHUD || Game1.eventUp || Game1.farmEvent != null) return;
        if (Game1.activeClickableMenu != null || _lastPanelBounds.Width == 0) return;

        var cursor = e.Cursor.GetScaledScreenPixels();
        if (!_lastPanelBounds.Contains((int)cursor.X, (int)cursor.Y)) return;

        // Arm a pending press. The per-tick poll promotes it to a drag past the
        // threshold; a release before that fires the click on _pressedKey.
        _pendingPress = true;
        _pressPos = cursor;
        _grabOffset = new Vector2(cursor.X - _lastPanelBounds.X, cursor.Y - _lastPanelBounds.Y);
        _dragBoxX = _lastPanelBounds.X;
        _dragBoxY = _lastPanelBounds.Y;
        _pressedKey = KeyAt(cursor);
        // Swallow the click so pressing the panel doesn't swing a tool.
        _helper.Input.Suppress(e.Button);
    }

    private string? KeyAt(Vector2 cursor)
    {
        foreach (var (bounds, key) in _entryBounds)
            if (bounds.Contains((int)cursor.X, (int)cursor.Y)) return key;
        return null;
    }

    // Drives the whole drag from a per-tick poll. The cursor is polled (CursorMoved
    // doesn't fire while a button is held) and the END is detected from the raw XNA
    // mouse state, NOT SMAPI's ButtonReleased: Suppress() makes SMAPI fire a release
    // the next tick, which would kill the drag instantly. The raw state reflects the
    // real hardware button regardless of suppression.
    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!_pendingPress && !_dragging) return;

        bool held = Mouse.GetState().LeftButton == ButtonState.Pressed;
        if (!held || !ModEntry.Config.ShowHudPin || Game1.activeClickableMenu != null)
        {
            if (_dragging)
            {
                ModEntry.Config.HudPinX = _lastPanelBounds.X;
                ModEntry.Config.HudPinY = _lastPanelBounds.Y;
                _helper.WriteConfig(ModEntry.Config);
            }
            else if (_pendingPress && !held && Game1.activeClickableMenu == null && _pressedKey != null)
            {
                // Released without dragging: a click. Open the journal on it.
                ModEntry.Instance.OpenJournalToQuest(_pressedKey);
            }
            _dragging = false;
            _pendingPress = false;
            _pressedKey = null;
            return;
        }

        var cursor = _helper.Input.GetCursorPosition().GetScaledScreenPixels();
        if (!_dragging)
        {
            if (System.Math.Abs(cursor.X - _pressPos.X)
                + System.Math.Abs(cursor.Y - _pressPos.Y) <= DragThreshold)
                return;
            _dragging = true;
            _pendingPress = false;
        }

        _dragBoxX = (int)(cursor.X - _grabOffset.X);
        _dragBoxY = (int)(cursor.Y - _grabOffset.Y);
        // Keep the held button from swinging a tool under the panel.
        _helper.Input.Suppress(SButton.MouseLeft);
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        // Clear the hit-test rects up front so a stale panel can't catch a click
        // on a frame where the HUD isn't actually drawn.
        _lastPanelBounds = Rectangle.Empty;
        _entryBounds.Clear();

        if (!ModEntry.Config.ShowHudPin) return;
        if (!Context.IsWorldReady || Game1.player == null) return;
        // Don't draw over a menu, an event/cutscene, or when the player hid the HUD.
        if (!Game1.displayHUD || Game1.eventUp || Game1.farmEvent != null) return;
        if (Game1.activeClickableMenu != null) return;

        var pinned = PinnedObjectivesStore.Load();
        if (pinned.Count == 0) return;

        var entries = new List<(string Title, string Objective, string Key)>();
        int hiddenOverflow = 0;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value) continue;
            string key = PinnedObjectivesStore.KeyFor(q);
            if (string.IsNullOrEmpty(key) || !pinned.Contains(key)) continue;
            if (entries.Count >= MaxEntries) { hiddenOverflow++; continue; }
            entries.Add((q.questTitle ?? string.Empty, ResolveObjective(q), key));
        }

        // Special orders live in team.specialOrders, not the quest log, so they
        // need a parallel pass. Only in-progress ones are shown.
        var orders = Game1.player.team?.specialOrders;
        if (orders != null)
        {
            foreach (var so in orders)
            {
                if (so == null || so.questState.Value != SpecialOrderStatus.InProgress) continue;
                string key = PinnedObjectivesStore.KeyFor(so);
                if (string.IsNullOrEmpty(key) || !pinned.Contains(key)) continue;
                if (entries.Count >= MaxEntries) { hiddenOverflow++; continue; }
                entries.Add((ResolveSpecialOrderTitle(so), ResolveSpecialOrderObjective(so), key));
            }
        }

        if (entries.Count == 0) return;

        DrawStack(e.SpriteBatch, entries, hiddenOverflow);
    }

    // Resolved fresh from the raw field, not the cached GetName(), so a late
    // string patch still reads right (mirrors the journal).
    private static string ResolveSpecialOrderTitle(SpecialOrder so)
    {
        string? raw = so.questName.Value;
        if (string.IsNullOrEmpty(raw)) return so.GetName() ?? string.Empty;
        try { return SpecialOrder.MakeLocalizationReplacements(raw).Trim(); }
        catch { return so.GetName() ?? string.Empty; }
    }

    // First not-yet-complete objective, with its progress count when it has one.
    private static string ResolveSpecialOrderObjective(SpecialOrder so)
    {
        try
        {
            foreach (var obj in so.objectives)
            {
                if (obj == null || obj.IsComplete()) continue;
                string desc = obj.GetDescription() ?? string.Empty;
                try { desc = so.Parse(desc) ?? desc; } catch { }
                int max = obj.GetMaxCount();
                if (max > 1)
                    desc = $"{desc} ({obj.GetCount()}/{max})";
                return desc;
            }
        }
        catch { }
        return string.Empty;
    }

    // Active step for MQF Adventure quests, otherwise the vanilla currentObjective.
    private string ResolveObjective(Quest q)
    {
        if (_mqfApi != null)
        {
            try
            {
                var steps = _mqfApi.GetAdventureSteps(q);
                if (steps != null && steps.Count > 0)
                {
                    IAdventureStepInfo? step = null;
                    int? idx = _mqfApi.GetActiveStepIndex(q);
                    if (idx.HasValue && idx.Value >= 0 && idx.Value < steps.Count)
                        step = steps[idx.Value];
                    else
                    {
                        foreach (var s in steps)
                            if (s.Active && !s.Done) { step = s; break; }
                    }
                    if (step != null)
                    {
                        string desc = string.IsNullOrEmpty(step.Description) ? step.Kind : step.Description;
                        if (step.Count > 1 && !step.Done)
                            desc = $"{desc} ({step.Progress}/{step.Count})";
                        return desc;
                    }
                }
            }
            catch { }
        }
        return q.currentObjective ?? string.Empty;
    }

    private void DrawStack(SpriteBatch b, List<(string Title, string Objective, string Key)> entries, int hiddenOverflow)
    {
        var font = Game1.smallFont;
        int innerWidth = PanelWidth - Padding * 2;

        var blocks = new List<(string Title, string Objective, float TitleH, float ObjH, string Key)>();
        float totalHeight = Padding * 2;
        foreach (var (title, objective, key) in entries)
        {
            string wrappedTitle = Game1.parseText(title, font, innerWidth);
            string wrappedObj = string.IsNullOrEmpty(objective)
                ? string.Empty
                : Game1.parseText(objective, font, innerWidth - ObjectiveIndent);
            float titleH = font.MeasureString(wrappedTitle).Y;
            float objH = wrappedObj.Length == 0 ? 0f : font.MeasureString(wrappedObj).Y + 2f;
            blocks.Add((wrappedTitle, wrappedObj, titleH, objH, key));
            totalHeight += titleH + objH + EntryGap;
        }

        string? overflowLine = hiddenOverflow > 0
            ? _helper.Translation.Get("hud.more", new { count = hiddenOverflow })
                .Default($"... and {hiddenOverflow} more").ToString()
            : null;
        float overflowH = 0f;
        if (overflowLine != null)
        {
            overflowH = font.MeasureString(overflowLine).Y;
            totalHeight += overflowH + 2f;
        }

        int height = (int)totalHeight;
        int boxX, boxY;
        if (_dragging)
        {
            boxX = _dragBoxX;
            boxY = _dragBoxY;
        }
        else if (ModEntry.Config.HudPinX >= 0 && ModEntry.Config.HudPinY >= 0)
        {
            boxX = ModEntry.Config.HudPinX;
            boxY = ModEntry.Config.HudPinY;
        }
        else
        {
            boxX = Game1.uiViewport.Width - PanelWidth - RightMargin;
            boxY = TopOffset;
        }
        // Keep the panel on screen whatever the position source.
        boxX = Math.Clamp(boxX, 0, Math.Max(0, Game1.uiViewport.Width - PanelWidth));
        boxY = Math.Clamp(boxY, 0, Math.Max(0, Game1.uiViewport.Height - height));
        _lastPanelBounds = new Rectangle(boxX, boxY, PanelWidth, height);

        IClickableMenu.drawTextureBox(
            b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            boxX, boxY, PanelWidth, height, Color.White, 1f, drawShadow: true);

        // Hover highlight uses the same tint as the journal's quest-list rows, so
        // the HUD reads as a clickable list. Suppressed while dragging the panel.
        var cursor = _helper.Input.GetCursorPosition().GetScaledScreenPixels();
        bool canHover = !_dragging;

        float textX = boxX + Padding;
        float y = boxY + Padding;
        foreach (var (title, objective, titleH, objH, key) in blocks)
        {
            var entryRect = new Rectangle(boxX + 4, (int)y - 2, PanelWidth - 8, (int)(titleH + objH) + 4);
            _entryBounds.Add((entryRect, key));
            if (canHover && entryRect.Contains((int)cursor.X, (int)cursor.Y))
                b.Draw(Game1.staminaRect, entryRect, JournalTheme.HoverTint);

            b.DrawString(font, title, new Vector2(textX, y), Game1.textColor);
            y += titleH;
            if (objective.Length > 0)
            {
                y += 2f;
                b.DrawString(font, objective, new Vector2(textX + ObjectiveIndent, y), Game1.textColor * 0.8f);
                y += font.MeasureString(objective).Y;
            }
            y += EntryGap;
        }
        if (overflowLine != null)
        {
            y += 2f;
            b.DrawString(font, overflowLine, new Vector2(textX, y), Game1.textColor * 0.7f);
        }
    }
}
