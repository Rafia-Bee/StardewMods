using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Api;
using StardewValley;
using StardewValley.Menus;

namespace MoreQuestsFramework.Posting.Boards;

/// Generic cork-board menu rendered when the player interacts with a `BoardDefinition`'s
/// anchor tile. Shares its scatter layout / pad-pin sprite logic with `MoreQuestsBillboard`
/// via `BoardLayout`, but does NOT inherit vanilla `Billboard` — accept-quest popup
/// integration for custom boards lands in Phase 8c when `TriggerSource.CustomBoard` quests
/// start populating slots. Until then this menu renders the cork-board background and the
/// "Nothing posted" fallback string, matching vanilla's empty-board behaviour.
public sealed class CustomBoardMenu : IClickableMenu
{
    private const int MenuWidth = 338 * 4;
    private const int MenuHeight = 198 * 4;
    private const int CcIndexBase = -42100;

    private readonly BoardDefinition _board;
    private readonly Texture2D _billboardTexture;
    private readonly Texture2D _padTexture;
    private readonly Texture2D _pinTexture;
    private readonly List<Note> _notes = new();
    private readonly Dictionary<int, Note> _notesByCc = new();
    private string _hoverTitle = "";
    private string _hoverText = "";

    private sealed class Note
    {
        public ClickableTextureComponent Cc { get; init; } = null!;
        public CustomBoardSlots.Slot Slot { get; init; } = null!;
        public Color PadColor { get; init; }
        public Color PinColor { get; init; }
        public Texture2D? Portrait { get; init; }
    }

    public CustomBoardMenu(BoardDefinition board)
    {
        _board = board;

        width = MenuWidth;
        height = MenuHeight;
        xPositionOnScreen = (Game1.uiViewport.Width - width) / 2;
        yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

        _billboardTexture = LoadOrFallback(
            board.Texture,
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
                // Asset not registered or missing — fall through to the framework default.
            }
        }
        return Game1.content.Load<Texture2D>(fallback);
    }

    private void BuildNotes()
    {
        _notes.Clear();
        _notesByCc.Clear();

        var slots = CustomBoardSlots.SlotsFor(_board);
        if (slots.Count == 0)
            return;

        float scale = BoardLayout.ChooseScale(slots.Count);
        int side = (int)(BoardLayout.PadSpriteSize * scale);

        var placed = new List<Rectangle>(slots.Count);
        var rng = new Random(Game1.Date.TotalDays * 7919 + slots.Count + (_board.Name?.GetHashCode() ?? 0));

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            Rectangle bounds = BoardLayout.ScatterBounds(xPositionOnScreen, yPositionOnScreen, side, side, placed, rng)
                ?? BoardLayout.FallbackGridBounds(xPositionOnScreen, yPositionOnScreen, i, slots.Count, side);
            placed.Add(bounds);

            (Color padColor, Color pinColor) = BoardLayout.ColorsFor(slot.Posting.QuestType);

            var cc = new ClickableTextureComponent(
                bounds,
                _padTexture,
                new Rectangle(0, 0, BoardLayout.PadSpriteSize, BoardLayout.PadSpriteSize),
                scale)
            {
                myID = CcIndexBase - i,
                leftNeighborID = -7777,
                rightNeighborID = -7777,
                upNeighborID = -7777,
                downNeighborID = -7777
            };

            _notes.Add(new Note
            {
                Cc = cc,
                Slot = slot,
                PadColor = padColor,
                PinColor = pinColor,
                Portrait = BoardLayout.TryGetPortrait(slot.Posting.QuestGiver)
            });
            _notesByCc[cc.myID] = _notes[^1];
        }
    }

    public override void performHoverAction(int x, int y)
    {
        _hoverTitle = "";
        _hoverText = "";
        foreach (var note in _notes)
        {
            var cc = note.Cc;
            if (cc.containsPoint(x, y))
            {
                _hoverTitle = note.Slot.Quest.questTitle ?? "";
                _hoverText = note.Slot.Posting.QuestGiver;
                cc.scale = Math.Min(cc.scale + 0.04f, cc.baseScale + 0.5f);
            }
            else
            {
                cc.scale = Math.Max(cc.scale - 0.04f, cc.baseScale);
            }
        }
        if (upperRightCloseButton != null)
            upperRightCloseButton.tryHover(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton != null && readyToClose() && upperRightCloseButton.containsPoint(x, y))
        {
            exitThisMenu();
            if (playSound)
                Game1.playSound("bigDeSelect");
            return;
        }
        // Phase 8c: route note clicks through an accept-popup. For 8b the menu has no
        // active slots, so left-clicks on the empty board just fall through to base.
        base.receiveLeftClick(x, y, playSound);
    }

    public override void snapToDefaultClickableComponent()
    {
        if (_notes.Count > 0)
        {
            currentlySnappedComponent = getComponentWithID(_notes[0].Cc.myID);
            snapCursorToCurrentSnappedComponent();
        }
    }

    public override void draw(SpriteBatch b)
    {
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
}
