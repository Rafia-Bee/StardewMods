using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MoreQuestsFramework.Api;
using StardewValley;
using StardewValley.Menus;

namespace MoreQuestsFramework.Posting.Boards;

// Does NOT inherit vanilla Billboard. Shares scatter layout with MoreQuestsBillboard
// via BoardLayout.
internal sealed class CustomBoardMenu : IClickableMenu
{
    private const int MenuWidth = 338 * 4;
    private const int MenuHeight = 198 * 4;
    private const int CcIndexBase = -42100;
    // -99998 routes a direction press to automaticSnapBehavior, which picks the nearest
    // note in that direction. Lets the D-pad walk the scattered notes. (-7777 would route
    // to customSnapBehavior, which we don't implement, so the cursor would never move.)
    private const int SnapAutomatic = -99998;

    private readonly BoardDefinition _board;
    private readonly Texture2D _billboardTexture;
    private readonly Texture2D _padTexture;
    private readonly Texture2D _pinTexture;
    private readonly List<Note> _notes = new();
    private readonly Dictionary<int, Note> _notesByCc = new();
    private string _hoverTitle = "";
    private string _hoverText = "";
    private Note? _hoveredNote;

    // The open inner popup, or null. A quest pin opens CustomBoardQuestMenu (accept), a notice
    // pin opens CustomBoardNoticeMenu (read-only). Both are IClickableMenu, so all the input
    // forwarding below works on either.
    private IClickableMenu? _innerPopup;

    private sealed class Note
    {
        public ClickableTextureComponent Cc { get; init; } = null!;
        public CustomBoardSlots.Slot Slot { get; init; } = null!;
        public Color PadColor { get; init; }
        public Color PinColor { get; init; }
        public Texture2D PadTexture { get; init; } = null!;
        public Texture2D PinTexture { get; init; } = null!;
        public BoardNoteRenderer.NoteIcon? Icon { get; init; }
        public float Tilt { get; init; }
    }

    public CustomBoardMenu(BoardDefinition board)
    {
        _board = board;

        width = MenuWidth;
        height = MenuHeight;
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

        _billboardTexture = LoadOrFallback(
            board.Background ?? board.Texture,
            "LooseSprites\\Billboard");
        _padTexture = LoadOrFallback(
            board.Pad?.Texture,
            ModEntry.PadAssetRoot);
        _pinTexture = LoadOrFallback(
            board.Pin?.Texture,
            ModEntry.PinAssetRoot);

        upperRightCloseButton = new ClickableTextureComponent(
            new Rectangle(xPositionOnScreen + width - 36, yPositionOnScreen - 8, 48, 48),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            4f);

        BuildNotes();
        populateClickableComponentList();
        if (Game1.options.SnappyMenus)
            snapToDefaultClickableComponent();
    }

    // The base reflection scan can't find the note components (they live inside private Note
    // wrappers, not a List<ClickableComponent>), so register them by hand for gamepad snap nav.
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

    private static Texture2D LoadOrFallback(string? assetName, string fallback)
    {
        if (!string.IsNullOrEmpty(assetName))
        {
            try
            {
                return Game1.content.Load<Texture2D>(assetName);
            }
            catch
            {
            }
        }
        return Game1.content.Load<Texture2D>(fallback);
    }

    private void BuildNotes()
    {
        _notes.Clear();
        _notesByCc.Clear();
        _hoveredNote = null;

        var slots = CustomBoardSlots.SlotsFor(_board);
        if (slots.Count == 0)
            return;

        var categories = new List<string>(slots.Count);
        var types = new List<string>(slots.Count);
        foreach (var s in slots)
        {
            categories.Add(s.Category);
            types.Add(s.Kind == SlotKind.Notice ? "Notice" : "Quest");
        }

        int daySeed = Game1.Date.TotalDays + (_board.Name?.GetHashCode() ?? 0);
        var rng = new Random(Game1.Date.TotalDays * 7919 + slots.Count + (_board.Name?.GetHashCode() ?? 0));
        var placements = BoardLayout.ComputeLayout(
            _board, categories, xPositionOnScreen, yPositionOnScreen, daySeed, rng, types);
        var texCache = new Dictionary<string, Texture2D>();

        for (int i = 0; i < slots.Count; i++)
        {
            var placement = placements[i];
            if (placement == null)
                continue;
            var slot = slots[i];

            (Color padColor, Color pinColor) = BoardLayout.ColorsFor(slot.Category);

            var cc = new ClickableTextureComponent(
                placement.PaperBounds,
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
                PadTexture = BoardNoteRenderer.ResolvePad(slot.Category, _padTexture, texCache),
                PinTexture = BoardNoteRenderer.ResolvePin(slot.Category, _pinTexture, texCache),
                Icon = BoardNoteRenderer.ResolveIcon(slot.IconValue, slot.Category, slot.GiverName, texCache),
                Tilt = placement.Tilt
            };
            _notes.Add(note);
            _notesByCc[cc.myID] = note;
        }
    }

    public override void performHoverAction(int x, int y)
    {
        if (_innerPopup != null)
        {
            _innerPopup.performHoverAction(x, y);
            return;
        }
        _hoverTitle = "";
        _hoverText = "";
        _hoveredNote = null;
        foreach (var note in _notes)
        {
            if (note.Cc.containsPoint(x, y))
            {
                (_hoverTitle, _hoverText) = HoverTextFor(note.Slot);
                _hoveredNote = note;
            }
        }
        if (upperRightCloseButton != null)
            upperRightCloseButton.tryHover(x, y);
    }

    // A quest pin shows its title + the quest's objective tooltip. A notice pin shows no
    // tooltip (the whole notice reads in the popup on click; dumping the full body into a hover
    // box was too much), so return empty and the draw skips the tooltip entirely.
    private static (string title, string text) HoverTextFor(CustomBoardSlots.Slot slot)
    {
        if (slot.Kind == SlotKind.Notice)
            return ("", "");
        return (slot.Quest?.questTitle ?? "", slot.Quest != null ? QuestTooltip.BodyFor(slot.Quest) : "");
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (_innerPopup != null)
        {
            _innerPopup.receiveLeftClick(x, y, playSound);
            return;
        }

        if (upperRightCloseButton != null && readyToClose() && upperRightCloseButton.containsPoint(x, y))
        {
            exitThisMenu();
            if (playSound)
                Game1.playSound("bigDeSelect");
            return;
        }

        var target = NoteAt(x, y);
        if (target != null)
        {
            if (target.Slot.Kind == SlotKind.Notice)
            {
                _innerPopup = new CustomBoardNoticeMenu(this, target.Slot.Notice!, _billboardTexture);
            }
            else
            {
                var accept = new CustomBoardQuestMenu(this, target.Slot, _billboardTexture);
                accept.acceptQuestButton.visible = !target.Slot.Accepted;
                _innerPopup = accept;
            }
            if (playSound)
                Game1.playSound("smallSelect");
            if (Game1.options.SnappyMenus)
            {
                _innerPopup.snapToDefaultClickableComponent();
                currentlySnappedComponent = _innerPopup.currentlySnappedComponent;
                snapCursorToCurrentSnappedComponent();
            }
        }
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

    public override bool readyToClose()
    {
        return _innerPopup == null;
    }

    // Vanilla's IClickableMenu Esc/menu-button handler calls readyToClose then exits the
    // outer menu. With a popup open we want Esc to close the popup instead, so intercept
    // here and route to the same close path the close button uses.
    public override void receiveKeyPress(Keys key)
    {
        if (_innerPopup != null && IsMenuButton(key))
        {
            OnInnerPopupClosed(reopen: false);
            if (Game1.options.SnappyMenus)
                snapToDefaultClickableComponent();
            return;
        }
        base.receiveKeyPress(key);
    }

    public override void applyMovementKey(int direction)
    {
        if (_innerPopup != null)
        {
            _innerPopup.applyMovementKey(direction);
            currentlySnappedComponent = _innerPopup.currentlySnappedComponent;
            return;
        }
        base.applyMovementKey(direction);
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (_innerPopup != null)
        {
            _innerPopup.receiveGamePadButton(b);
            return;
        }
        base.receiveGamePadButton(b);
    }

    private static bool IsMenuButton(Keys key)
    {
        var buttons = Game1.options.menuButton;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].key == key)
                return true;
        }
        return false;
    }

    public override void snapToDefaultClickableComponent()
    {
        if (_innerPopup != null)
        {
            _innerPopup.snapToDefaultClickableComponent();
            currentlySnappedComponent = _innerPopup.currentlySnappedComponent;
            return;
        }
        if (_notes.Count > 0)
        {
            currentlySnappedComponent = getComponentWithID(_notes[0].Cc.myID);
            snapCursorToCurrentSnappedComponent();
        }
    }

    // reopen=true rebuilds the cork board so the just-accepted slot drops out. A notice popup
    // never reopens (the board is unchanged), but a quest accept does.
    public void OnInnerPopupClosed(bool reopen)
    {
        _innerPopup = null;
        if (reopen)
            Game1.activeClickableMenu = new CustomBoardMenu(_board);
    }

    public override void draw(SpriteBatch b)
    {
        if (_innerPopup != null)
        {
            _innerPopup.draw(b);
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
            if (active != null)
                DrawNote(b, active, 1.12f);
        }

        if (!string.IsNullOrEmpty(_board.Title))
        {
            var titleVec = Game1.dialogueFont.MeasureString(_board.Title);
            b.DrawString(
                Game1.dialogueFont,
                _board.Title,
                new Vector2(xPositionOnScreen + (width - titleVec.X) / 2f, yPositionOnScreen - 48),
                Game1.textColor);
        }

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

        Game1.mouseCursorTransparency = 1f;
        drawMouse(b);
    }

    private void DrawNote(SpriteBatch b, Note note, float sizeBoost) =>
        BoardNoteRenderer.DrawNote(
            b, note.PadTexture, note.PinTexture, note.PadColor, note.PinColor,
            note.Icon, note.Cc.bounds, note.Tilt, sizeBoost);

    // The note under the mouse, or (for a gamepad) the snapped one, drawn last and a touch
    // bigger so the current selection reads clearly.
    private Note? ActiveNote()
    {
        if (_hoveredNote != null)
            return _hoveredNote;
        if (currentlySnappedComponent != null
            && _notesByCc.TryGetValue(currentlySnappedComponent.myID, out var snapped))
            return snapped;
        return null;
    }
}
