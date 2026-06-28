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

// Draws the little on-screen box that lists your pinned quests and their current objective.
// Handles clicking an entry to open the journal, and dragging the box around to reposition it (saved to config).
internal sealed class PinnedObjectiveHud
{
    private readonly IModHelper _helper;
    private readonly IMoreQuestsApi? _mqfApi;
    private readonly ExternalEntryRegistry? _externalEntries;

    private const int PanelWidth = 380;
    private const int Padding = 16;
    private const int RightMargin = 16;
    private const int TopOffset = 300;
    private const int EntryGap = 10;
    private const int ObjectiveIndent = 16;
    private const int MaxEntries = 8;

    private const int DragThreshold = 8;

    private Rectangle _lastPanelBounds;
    private readonly List<(Rectangle Bounds, string Key)> _entryBounds = new();
    private bool _dragging;
    private bool _pendingPress;
    private Vector2 _pressPos;
    private string? _pressedKey;
    private Vector2 _grabOffset;
    private int _dragBoxX;
    private int _dragBoxY;

    public PinnedObjectiveHud(IModHelper helper, IMoreQuestsApi? mqfApi, ExternalEntryRegistry? externalEntries = null)
    {
        _helper = helper;
        _mqfApi = mqfApi;
        _externalEntries = externalEntries;
    }

    public void Register()
    {
        _helper.Events.Display.RenderedHud += OnRenderedHud;
        _helper.Events.Input.ButtonPressed += OnButtonPressed;
        _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!ModEntry.Config.ShowHudPin || !Context.IsWorldReady) return;
        if (!Game1.displayHUD || Game1.eventUp || Game1.farmEvent != null) return;
        if (Game1.activeClickableMenu != null || _lastPanelBounds.Width == 0) return;

        var cursor = CursorUtil.UiSpace(e.Cursor);

        if (ModEntry.Config.HudActivateKey.JustPressed())
        {
            string? key = KeyAt(cursor);
            if (key != null)
            {
                ModEntry.Instance.OpenJournalToQuest(key);
                _helper.Input.SuppressActiveKeybinds(ModEntry.Config.HudActivateKey);
            }
            return;
        }

        if (ModEntry.Config.TogglePinKey.JustPressed())
        {
            string? key = KeyAt(cursor);
            if (key != null)
            {
                PinnedObjectivesStore.UnpinByKey(key);
                Game1.playSound("smallSelect");
                _helper.Input.SuppressActiveKeybinds(ModEntry.Config.TogglePinKey);
            }
            return;
        }

        if (e.Button != SButton.MouseLeft) return;
        if (!_lastPanelBounds.Contains((int)cursor.X, (int)cursor.Y)) return;

        _pendingPress = true;
        _pressPos = cursor;
        _grabOffset = new Vector2(cursor.X - _lastPanelBounds.X, cursor.Y - _lastPanelBounds.Y);
        _dragBoxX = _lastPanelBounds.X;
        _dragBoxY = _lastPanelBounds.Y;
        _pressedKey = KeyAt(cursor);
        _helper.Input.Suppress(e.Button);
    }

    private string? KeyAt(Vector2 cursor)
    {
        foreach (var (bounds, key) in _entryBounds)
            if (bounds.Contains((int)cursor.X, (int)cursor.Y)) return key;
        return null;
    }

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
                ModEntry.Instance.OpenJournalToQuest(_pressedKey);
            }
            _dragging = false;
            _pendingPress = false;
            _pressedKey = null;
            return;
        }

        var cursor = CursorUtil.UiSpace(_helper.Input.GetCursorPosition());
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
        _helper.Input.Suppress(SButton.MouseLeft);
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        _lastPanelBounds = Rectangle.Empty;
        _entryBounds.Clear();

        if (!ModEntry.Config.ShowHudPin) return;
        if (!Context.IsWorldReady || Game1.player == null) return;
        if (!Game1.displayHUD || Game1.eventUp || Game1.farmEvent != null) return;
        if (Game1.activeClickableMenu != null) return;

        var pinned = PinnedObjectivesStore.Load();
        if (pinned.Count == 0) return;

        var entries = new List<(string Title, string Objective, IReadOnlyList<string> All, string Key)>();
        int hiddenOverflow = 0;
        var log = Game1.player.questLog;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value || q.IsHidden()) continue;
            string key = PinnedObjectivesStore.KeyFor(q);
            if (string.IsNullOrEmpty(key) || !pinned.Contains(key)) continue;
            if (entries.Count >= MaxEntries) { hiddenOverflow++; continue; }
            var (hud, all) = ResolveObjective(q);
            entries.Add((q.questTitle ?? string.Empty, hud, all, key));
        }

        var orders = Game1.player.team?.specialOrders;
        if (orders != null)
        {
            foreach (var so in orders)
            {
                if (so == null || so.questState.Value != SpecialOrderStatus.InProgress) continue;
                string key = PinnedObjectivesStore.KeyFor(so);
                if (string.IsNullOrEmpty(key) || !pinned.Contains(key)) continue;
                if (entries.Count >= MaxEntries) { hiddenOverflow++; continue; }
                var soAll = ResolveSpecialOrderObjectives(so);
                entries.Add((ResolveSpecialOrderTitle(so), soAll.Count > 0 ? soAll[0] : string.Empty, soAll, key));
            }
        }

        if (_externalEntries != null)
        {
            foreach (var entry in _externalEntries.Snapshot())
            {
                if (entry.Completed) continue;
                string key = PinnedObjectivesStore.KeyFor(entry.OwnerId, entry.Key);
                if (string.IsNullOrEmpty(key) || !pinned.Contains(key)) continue;
                if (entries.Count >= MaxEntries) { hiddenOverflow++; continue; }
                var (hud, all) = ResolveExternalObjective(entry);
                string title = string.IsNullOrEmpty(entry.BannerTitle) ? (entry.Title ?? string.Empty) : entry.BannerTitle;
                entries.Add((title, hud, all, key));
            }
        }

        if (entries.Count == 0) return;

        DrawStack(e.SpriteBatch, entries, hiddenOverflow);
    }

    private static string ResolveSpecialOrderTitle(SpecialOrder so)
    {
        string? raw = so.questName.Value;
        if (string.IsNullOrEmpty(raw)) return so.GetName() ?? string.Empty;
        try { return SpecialOrder.MakeLocalizationReplacements(raw).Trim(); }
        catch { return so.GetName() ?? string.Empty; }
    }

    private static IReadOnlyList<string> ResolveSpecialOrderObjectives(SpecialOrder so)
    {
        var lines = new List<string>();
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
                lines.Add(desc);
            }
        }
        catch { }
        return lines;
    }

    private static (string Hud, IReadOnlyList<string> All) ResolveExternalObjective(IJournalEntry e)
    {
        var all = new List<string>();
        if (e.Steps != null)
            foreach (var s in e.Steps)
                if (!string.IsNullOrEmpty(s)) all.Add(s);
        string hud = all.Count > 0 ? all[0] : (e.Objective ?? string.Empty);
        if (all.Count == 0 && !string.IsNullOrEmpty(hud)) all.Add(hud);
        return (hud, all);
    }

    private (string Hud, IReadOnlyList<string> All) ResolveObjective(Quest q)
    {
        if (_mqfApi != null)
        {
            try
            {
                var steps = _mqfApi.GetAdventureSteps(q);
                if (steps != null && steps.Count > 0)
                {
                    var all = new List<string>();
                    foreach (var s in steps)
                    {
                        if (s.Done) continue;
                        string d = string.IsNullOrEmpty(s.Description) ? s.Kind : s.Description;
                        if (s.Count > 1) d = $"{d} ({s.Progress}/{s.Count})";
                        all.Add(d);
                    }

                    string hud;
                    int? idx = _mqfApi.GetActiveStepIndex(q);
                    if (idx.HasValue && idx.Value >= 0 && idx.Value < steps.Count)
                    {
                        var s = steps[idx.Value];
                        string d = string.IsNullOrEmpty(s.Description) ? s.Kind : s.Description;
                        if (s.Count > 1 && !s.Done) d = $"{d} ({s.Progress}/{s.Count})";
                        hud = d;
                    }
                    else
                        hud = all.Count > 0 ? all[0] : (q.currentObjective ?? string.Empty);

                    if (all.Count == 0 && !string.IsNullOrEmpty(hud))
                        all.Add(hud);
                    return (hud, all);
                }

                var objLines = _mqfApi.GetObjectiveLines(q);
                if (objLines != null && objLines.Count > 0)
                    return (objLines[0], new List<string>(objLines));
            }
            catch { }
        }
        string obj = q.currentObjective ?? string.Empty;
        return (obj, string.IsNullOrEmpty(obj) ? new List<string>() : new List<string> { obj });
    }

    private void DrawStack(SpriteBatch b, List<(string Title, string Objective, IReadOnlyList<string> All, string Key)> entries, int hiddenOverflow)
    {
        var font = Game1.smallFont;
        int innerWidth = PanelWidth - Padding * 2;

        var blocks = new List<(string Title, string Objective, float TitleH, float ObjH, string Key, string RawTitle, IReadOnlyList<string> All, bool ObjTruncated)>();
        float totalHeight = Padding * 2;
        foreach (var (title, objective, all, key) in entries)
        {
            string wrappedTitle = FitToWidth(title, font, innerWidth);
            bool objTruncated = false;
            string wrappedObj = string.IsNullOrEmpty(objective)
                ? string.Empty
                : FitToWidth(objective, font, innerWidth - ObjectiveIndent, out objTruncated);
            float titleH = font.MeasureString(wrappedTitle).Y;
            float objH = wrappedObj.Length == 0 ? 0f : font.MeasureString(wrappedObj).Y + 2f;
            blocks.Add((wrappedTitle, wrappedObj, titleH, objH, key, title, all, objTruncated));
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
        boxX = Math.Clamp(boxX, 0, Math.Max(0, Game1.uiViewport.Width - PanelWidth));
        boxY = Math.Clamp(boxY, 0, Math.Max(0, Game1.uiViewport.Height - height));
        _lastPanelBounds = new Rectangle(boxX, boxY, PanelWidth, height);

        float opacity = Math.Clamp(ModEntry.Config.HudPinOpacity, 0.2f, 1f);

        IClickableMenu.drawTextureBox(
            b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
            boxX, boxY, PanelWidth, height, Color.White * opacity, 1f, drawShadow: true);

        var cursor = CursorUtil.UiSpace(_helper.Input.GetCursorPosition());
        bool canHover = !_dragging;

        string? tooltipTitle = null;
        string? tooltipBody = null;

        float textX = boxX + Padding;
        float y = boxY + Padding;
        foreach (var (title, objective, titleH, objH, key, rawTitle, all, objTruncated) in blocks)
        {
            var entryRect = new Rectangle(boxX + 4, (int)y - 2, PanelWidth - 8, (int)(titleH + objH) + 4);
            _entryBounds.Add((entryRect, key));
            if (canHover && entryRect.Contains((int)cursor.X, (int)cursor.Y))
            {
                b.Draw(Game1.staminaRect, entryRect, JournalTheme.HoverTint * opacity);
                // Multi-step entries show every line; a single objective only needs the tooltip when it was
                // truncated to fit, so the full text is still reachable on hover.
                if (ModEntry.Config.HudHoverObjectiveTooltip && (all.Count > 1 || objTruncated))
                {
                    tooltipTitle = rawTitle;
                    tooltipBody = string.Join("\n", all);
                }
            }

            b.DrawString(font, title, new Vector2(textX, y), Game1.textColor * opacity);
            y += titleH;
            if (objective.Length > 0)
            {
                y += 2f;
                b.DrawString(font, objective, new Vector2(textX + ObjectiveIndent, y), Game1.textColor * (0.8f * opacity));
                y += font.MeasureString(objective).Y;
            }
            y += EntryGap;
        }
        if (overflowLine != null)
        {
            y += 2f;
            b.DrawString(font, overflowLine, new Vector2(textX, y), Game1.textColor * (0.7f * opacity));
        }

        if (tooltipBody != null)
            IClickableMenu.drawHoverText(b, tooltipBody, font, 0, 0, -1, tooltipTitle);
    }

    // Word-wrap to the panel width, but parseText only breaks on spaces. If a line is still too wide (an
    // unbreakable token like a giant number), fall back to a hard single-line cut so nothing spills past the
    // border, no matter what title a mod hands us.
    private static string FitToWidth(string text, SpriteFont font, int maxWidth)
        => FitToWidth(text, font, maxWidth, out _);

    private static string FitToWidth(string text, SpriteFont font, int maxWidth, out bool truncated)
    {
        truncated = false;
        if (string.IsNullOrEmpty(text)) return text;
        string wrapped = Game1.parseText(text, font, maxWidth);
        if (font.MeasureString(wrapped).X <= maxWidth)
            return wrapped;
        truncated = true;
        return HardTruncate(text, font, maxWidth);
    }

    private static string HardTruncate(string s, SpriteFont font, int maxWidth)
    {
        const string ellipsis = "...";
        float ellipsisW = font.MeasureString(ellipsis).X;
        var sb = new System.Text.StringBuilder();
        foreach (char ch in s)
        {
            if (ch == '\n') break;
            sb.Append(ch);
            if (font.MeasureString(sb).X + ellipsisW > maxWidth)
            {
                sb.Length--;
                break;
            }
        }
        return sb.ToString().TrimEnd() + ellipsis;
    }
}
