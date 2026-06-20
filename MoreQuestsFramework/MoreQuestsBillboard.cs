using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MoreQuestsFramework.Posting.Boards;
using StardewValley;
using StardewValley.Menus;

namespace MoreQuestsFramework;

// Cork-board of quest notes (pad + pin + portrait). Clicking spawns a vanilla
// Billboard(true) overlay for accept-quest UI; Harmony redirects Game1.questOfTheDay
// to the selected slot.
internal sealed class MoreQuestsBillboard : Billboard
{
    private readonly List<Note> _notes = new();
    private readonly Dictionary<int, Note> _notesByCc = new();
    private readonly Texture2D _billboardTexture;
    private readonly Texture2D _padTexture;
    private readonly Texture2D _pinTexture;
    public static Billboard? InnerBillboard { get; set; }

    // Prize ticket lands every 3rd completed daily-board quest. Counter is vanilla's
    // cumulative BillboardQuestsDone stat, so it never resets day to day.
    private const int PrizeCadence = 3;
    private string _prizeHoverText = "";

    private const int CcIndexBase = -42000;
    // IClickableMenu has no named constant for this; -99998 routes to automaticSnapBehavior,
    // which picks the nearest CC in the requested direction. Lets D-pad navigate scattered notes.
    private const int SnapAutomatic = -99998;

    // Tracks the frame InnerBillboard was closed via menu-button (B/Esc/Start). Better Crafting
    // and other mods call readyToClose() every frame on every button, so readyToClose itself
    // must stay pure. Instead, the close-on-menu-button logic lives in receiveKeyPress, and this
    // counter blocks the same-frame line 13402 path (activeMenu.readyToClose then exitActiveMenu)
    // from then closing the outer board in the same press.
    private int _innerClosedFrame = -1;

    private string _hoverTitle = "";
    private string _hoverText = "";
    private Note? _hoveredNote;

    private sealed class Note
    {
        public ClickableTextureComponent Cc { get; init; } = null!;
        public BillboardSlots.Slot Slot { get; init; } = null!;
        public Color PadColor { get; init; }
        public Color PinColor { get; init; }
        public Texture2D? Portrait { get; init; }
        public float Tilt { get; init; }
    }

    public MoreQuestsBillboard()
        : base(true)
    {
        _billboardTexture = Game1.temporaryContent.Load<Texture2D>("LooseSprites\\Billboard");
        _padTexture = Game1.content.Load<Texture2D>(ModEntry.PadAssetRoot);
        _pinTexture = Game1.content.Load<Texture2D>(ModEntry.PinAssetRoot);
        InnerBillboard = null;

        BuildNotes();

        exitFunction = delegate
        {
            if (InnerBillboard != null)
                Game1.activeClickableMenu = new MoreQuestsBillboard();
        };
        populateClickableComponentList();
    }

    private void BuildNotes()
    {
        _notes.Clear();
        _notesByCc.Clear();

        var slots = BillboardSlots.Slots;
        if (slots.Count == 0)
            return;

        var rng = new Random(Game1.Date.TotalDays * 7919 + slots.Count);
        var layout = BoardLayout.ComputeGridLayout(xPositionOnScreen, yPositionOnScreen, slots.Count, rng);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            Rectangle bounds = layout[i];

            (Color padColor, Color pinColor) = BoardLayout.ColorsFor(slot.Posting.Category);

            var cc = new ClickableTextureComponent(
                bounds,
                _padTexture,
                new Rectangle(0, 0, BoardLayout.PadSpriteSize, BoardLayout.PadSpriteSize),
                1f)
            {
                myID = CcIndexBase - i,
                leftNeighborID = SnapAutomatic,
                rightNeighborID = SnapAutomatic,
                upNeighborID = SnapAutomatic,
                downNeighborID = SnapAutomatic
            };

            var note = new Note
            {
                Cc = cc,
                Slot = slot,
                PadColor = padColor,
                PinColor = pinColor,
                Portrait = BoardLayout.TryGetPortrait(slot.Posting.QuestGiver),
                Tilt = BoardLayout.TiltFor(Game1.Date.TotalDays, i)
            };
            _notes.Add(note);
            _notesByCc[cc.myID] = note;
        }
    }

    public override void performHoverAction(int x, int y)
    {
        if (InnerBillboard != null)
        {
            InnerBillboard.performHoverAction(x, y);
            return;
        }
        _hoverTitle = "";
        _hoverText = "";
        _hoveredNote = null;
        _prizeHoverText = "";
        foreach (var note in _notes)
        {
            if (note.Cc.containsPoint(x, y))
            {
                _hoverTitle = note.Slot.Quest.questTitle ?? "";
                _hoverText = QuestTooltip.BodyFor(note.Slot.Quest);
                _hoveredNote = note;
            }
        }

        if (_hoveredNote == null && PrizeProgressBounds().Contains(x, y))
            _prizeHoverText = PrizeProgressTooltip();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (InnerBillboard != null)
        {
            InnerBillboard.receiveLeftClick(x, y, playSound);
            return;
        }

        var target = NoteAt(x, y);
        if (target != null)
        {
            BillboardSlots.Selected = target.Slot;
            InnerBillboard = new Billboard(true);
            InnerBillboard.acceptQuestButton.visible = true;
            Game1.playSound("smallSelect");
            if (Game1.options.SnappyMenus)
            {
                InnerBillboard.snapToDefaultClickableComponent();
                currentlySnappedComponent = InnerBillboard.currentlySnappedComponent;
                snapCursorToCurrentSnappedComponent();
            }
            return;
        }

        InvokeBaseLeftClick(x, y, playSound);
    }

    // Notes can overlap on the cork board. When the gamepad cursor is snapped onto a note,
    // prefer that note so a covered one is still selectable (a plain hit-test would pick
    // whichever note sits on top). Falls back to the topmost note under the point for mouse.
    private Note? NoteAt(int x, int y)
    {
        if (currentlySnappedComponent != null
            && _notesByCc.TryGetValue(currentlySnappedComponent.myID, out var snapped)
            && snapped.Cc.containsPoint(x, y))
            return snapped;

        foreach (var note in _notes)
        {
            if (note.Cc.containsPoint(x, y))
                return note;
        }
        return null;
    }

    private void InvokeBaseLeftClick(int x, int y, bool playSound)
    {
        var method = AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.receiveLeftClick));
        var ftn = method.MethodHandle.GetFunctionPointer();
        var func = (Action<int, int, bool>)Activator.CreateInstance(typeof(Action<int, int, bool>), this, ftn)!;
        func.Invoke(x, y, playSound);
    }

    public override bool readyToClose()
    {
        // Pure query. Other mods (Better Crafting's ClickRecycle, for one) call this every
        // frame on every button press, so any side effect here corrupts our state.
        if (InnerBillboard != null) return false;
        // Block the same-frame cascade where the menu button both closes inner (in
        // receiveKeyPress) and then closes outer (Game1's flag4 → activeMenu.readyToClose).
        if (_innerClosedFrame == Game1.ticks) return false;
        return true;
    }

    public override void receiveKeyPress(Keys key)
    {
        if (InnerBillboard != null && Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            InnerBillboard = null;
            BillboardSlots.Selected = null;
            _innerClosedFrame = Game1.ticks;
            if (Game1.options.SnappyMenus)
                snapToDefaultClickableComponent();
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void snapToDefaultClickableComponent()
    {
        if (InnerBillboard != null)
        {
            InnerBillboard.snapToDefaultClickableComponent();
            currentlySnappedComponent = InnerBillboard.currentlySnappedComponent;
            return;
        }
        if (_notes.Count > 0)
        {
            currentlySnappedComponent = getComponentWithID(_notes[0].Cc.myID);
            snapCursorToCurrentSnappedComponent();
        }
    }

    // Reflection-based base impl can't find the note CCs (they live inside private Note
    // wrappers), so register them manually for gamepad snap navigation.
    public override void populateClickableComponentList()
    {
        allClickableComponents = new List<ClickableComponent>();
        foreach (var note in _notes)
            allClickableComponents.Add(note.Cc);
        if (upperRightCloseButton != null)
        {
            upperRightCloseButton.leftNeighborID = SnapAutomatic;
            upperRightCloseButton.downNeighborID = SnapAutomatic;
            allClickableComponents.Add(upperRightCloseButton);
        }
    }

    public override void applyMovementKey(int direction)
    {
        if (InnerBillboard != null)
        {
            InnerBillboard.applyMovementKey(direction);
            currentlySnappedComponent = InnerBillboard.currentlySnappedComponent;
            return;
        }
        base.applyMovementKey(direction);
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (InnerBillboard != null)
        {
            InnerBillboard.receiveGamePadButton(b);
            return;
        }
        base.receiveGamePadButton(b);
    }

    public override void draw(SpriteBatch b)
    {
        if (InnerBillboard != null)
        {
            InnerBillboard.draw(b);
            return;
        }

        if (!Game1.options.showClearBackgrounds)
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

        b.Draw(
            _billboardTexture,
            new Vector2(xPositionOnScreen, yPositionOnScreen),
            new Rectangle(0, 0, 338, 198),
            Color.White,
            0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

        if (_notes.Count == 0)
        {
            b.DrawString(
                Game1.dialogueFont,
                Game1.content.LoadString("Strings\\UI:Billboard_NothingPosted"),
                new Vector2(xPositionOnScreen + 384, yPositionOnScreen + 320),
                Game1.textColor);
        }
        else
        {
            Note? active = ActiveNote();
            foreach (var note in _notes)
            {
                if (note != active)
                    DrawNote(b, note, 1f);
            }
            // Draw the focused note last and a touch bigger so the current selection reads
            // clearly, on controller especially.
            if (active != null)
                DrawNote(b, active, 1.12f);
        }

        DrawPrizeStars(b);

        if (upperRightCloseButton != null && shouldDrawCloseButton())
            upperRightCloseButton.draw(b);

        if (!string.IsNullOrEmpty(_hoverText) || !string.IsNullOrEmpty(_hoverTitle))
        {
            IClickableMenu.drawHoverText(
                b,
                _hoverText ?? "",
                Game1.smallFont,
                0, 0, -1,
                string.IsNullOrEmpty(_hoverTitle) ? null : _hoverTitle);
        }
        else if (!string.IsNullOrEmpty(_prizeHoverText))
        {
            IClickableMenu.drawHoverText(b, _prizeHoverText, Game1.smallFont);
        }

        Game1.mouseCursorTransparency = 1f;
        drawMouse(b);
    }

    // Vanilla's prize-ticket stars (every 3rd completed daily-board quest drops a PrizeTicket).
    // The single-quest panel already draws these, but the cork board overview replaces vanilla's
    // whole draw, so without this you never see them until you open a note. Mirrors vanilla
    // Billboard.draw exactly: normally BillboardQuestsDone % 3 stars, but a full row of 3 right
    // after the completion that lands on a multiple of 3, in the same spot and sprite.
    private void DrawPrizeStars(SpriteBatch b)
    {
        var pos = new Vector2(xPositionOnScreen, yPositionOnScreen);
        int done = (int)Game1.stats.Get("BillboardQuestsDone");
        bool full = done % PrizeCadence == 0 && Game1.questOfTheDay != null && Game1.questOfTheDay.completed.Value;
        int stars = full ? PrizeCadence : done % PrizeCadence;
        for (int j = 0; j < stars; j++)
        {
            b.Draw(
                _billboardTexture,
                pos + new Vector2(18 + 12 * j, 36) * 4f,
                new Rectangle(140, 397, 10, 11),
                Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.6f);
        }
    }

    // Hover zone over the three star slots so the player can read how close they are even when
    // the row is empty (0/3 draws no stars).
    private Rectangle PrizeProgressBounds()
    {
        int left = xPositionOnScreen + 18 * 4;
        int top = yPositionOnScreen + 36 * 4;
        int right = xPositionOnScreen + (18 + 12 * (PrizeCadence - 1) + 10) * 4;
        int bottom = yPositionOnScreen + (36 + 11) * 4;
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private static string PrizeProgressTooltip()
    {
        int remaining = PrizeCadence - (int)Game1.stats.Get("BillboardQuestsDone") % PrizeCadence;
        var t = ModEntry.Translation;
        if (t == null)
            return remaining == 1
                ? "One more quest until a prize ticket."
                : $"{remaining} more quests until a prize ticket.";
        string key = remaining == 1 ? "billboard.prizeProgress.one" : "billboard.prizeProgress.many";
        return t.Get(key, new { count = remaining }).ToString();
    }

    // The note under the mouse, or (for a gamepad) the snapped one.
    private Note? ActiveNote()
    {
        if (_hoveredNote != null)
            return _hoveredNote;
        if (currentlySnappedComponent != null
            && _notesByCc.TryGetValue(currentlySnappedComponent.myID, out var snapped))
            return snapped;
        return null;
    }

    // Draws a note (pad, portrait, pin) rotated about its center by its tilt, scaled by
    // sizeBoost. The portrait offset is rotated with it so the whole note tilts as one piece.
    // Bounds are the visible paper, so the full sprite is scaled up from the paper width to
    // put the transparent margins back; the paper then lines up with the clickable bounds.
    private void DrawNote(SpriteBatch b, Note note, float sizeBoost)
    {
        var padSource = new Rectangle(0, 0, BoardLayout.PadSpriteSize, BoardLayout.PadSpriteSize);
        var origin = new Vector2(BoardLayout.PadSpriteSize / 2f, BoardLayout.PadSpriteSize / 2f);
        var center = new Vector2(note.Cc.bounds.Center.X, note.Cc.bounds.Center.Y);
        float side = note.Cc.bounds.Width * (BoardLayout.PadSpriteSize / (float)BoardLayout.PadPaperWidth) * sizeBoost;
        float scale = side / BoardLayout.PadSpriteSize;

        b.Draw(_padTexture, center, padSource, note.PadColor, note.Tilt, origin, scale, SpriteEffects.None, 0.86f);

        if (note.Portrait != null)
        {
            float portraitSide = side * 0.28f;
            // Lower-left corner of the note, matching the old static layout.
            var offset = new Vector2(-0.28f * side, 0.28f * side);
            var pos = center + Rotate(offset, note.Tilt);
            b.Draw(
                note.Portrait, pos, new Rectangle(0, 0, 64, 64), Color.White,
                note.Tilt, new Vector2(32, 32), portraitSide / 64f, SpriteEffects.None, 0.87f);
        }

        b.Draw(_pinTexture, center, padSource, note.PinColor, note.Tilt, origin, scale, SpriteEffects.None, 0.88f);
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}
