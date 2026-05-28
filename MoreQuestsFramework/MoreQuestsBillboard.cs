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

    private sealed class Note
    {
        public ClickableTextureComponent Cc { get; init; } = null!;
        public BillboardSlots.Slot Slot { get; init; } = null!;
        public Color PadColor { get; init; }
        public Color PinColor { get; init; }
        public Texture2D? Portrait { get; init; }
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

        float scale = BoardLayout.ChooseScale(slots.Count);
        int side = (int)(BoardLayout.PadSpriteSize * scale);

        var placed = new List<Rectangle>(slots.Count);
        var rng = new Random(Game1.Date.TotalDays * 7919 + slots.Count);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            Rectangle bounds = BoardLayout.ScatterBounds(xPositionOnScreen, yPositionOnScreen, side, side, placed, rng)
                ?? BoardLayout.FallbackGridBounds(xPositionOnScreen, yPositionOnScreen, i, slots.Count, side);
            placed.Add(bounds);

            (Color padColor, Color pinColor) = BoardLayout.ColorsFor(slot.Posting.Category);

            var cc = new ClickableTextureComponent(
                bounds,
                _padTexture,
                new Rectangle(0, 0, BoardLayout.PadSpriteSize, BoardLayout.PadSpriteSize),
                scale)
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
                Portrait = BoardLayout.TryGetPortrait(slot.Posting.QuestGiver)
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
        foreach (var note in _notes)
        {
            var cc = note.Cc;
            if (cc.containsPoint(x, y))
            {
                _hoverTitle = note.Slot.Quest.questTitle ?? "";
                _hoverText = QuestTooltip.BodyFor(note.Slot.Quest);
                cc.scale = Math.Min(cc.scale + 0.04f, cc.baseScale + 0.5f);
            }
            else
            {
                cc.scale = Math.Max(cc.scale - 0.04f, cc.baseScale);
            }
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (InnerBillboard != null)
        {
            InnerBillboard.receiveLeftClick(x, y, playSound);
            return;
        }

        foreach (var note in _notes)
        {
            if (note.Cc.containsPoint(x, y))
            {
                BillboardSlots.Selected = note.Slot;
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
        }

        InvokeBaseLeftClick(x, y, playSound);
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
            var padSource = new Rectangle(0, 0, BoardLayout.PadSpriteSize, BoardLayout.PadSpriteSize);
            foreach (var note in _notes)
            {
                var cc = note.Cc;

                b.Draw(_padTexture, cc.bounds, padSource, note.PadColor);

                if (note.Portrait != null)
                {
                    int portraitSide = (int)(cc.bounds.Width * 0.28f);
                    int padding = (int)(cc.bounds.Width * 0.08f);
                    int px = cc.bounds.Left + padding;
                    int py = cc.bounds.Bottom - portraitSide - padding;
                    b.Draw(
                        note.Portrait,
                        new Rectangle(px, py, portraitSide, portraitSide),
                        new Rectangle(0, 0, 64, 64),
                        Color.White);
                }

                b.Draw(_pinTexture, cc.bounds, padSource, note.PinColor);
            }
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
}
