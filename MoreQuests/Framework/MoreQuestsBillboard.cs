using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace MoreQuests.Framework;

/// Custom Billboard menu that renders a scattered "cork-board" of quest notes (pad + pin +
/// NPC portrait) instead of vanilla's single quest slot. Clicking a note selects that quest
/// and spawns a vanilla `Billboard(true)` over the top so the player sees vanilla's
/// accept-quest UI; our Harmony patches redirect `Game1.questOfTheDay` getters there to the
/// selected slot.
internal sealed class MoreQuestsBillboard : Billboard
{
    private static readonly Rectangle BoardRect = new(78 * 4, 58 * 4, 184 * 4, 96 * 4);
    private const int CcIndexBase = -42000;
    private const int PadSpriteSize = 64;

    private static readonly Color ItemDeliveryPadColor = new(244, 212, 130);
    private static readonly Color ItemDeliveryPinColor = new(200, 126, 52);
    private static readonly Color ResourceCollectionPadColor = new(182, 223, 158);
    private static readonly Color ResourceCollectionPinColor = new(98, 157, 86);
    private static readonly Color SlayMonsterPadColor = new(231, 166, 166);
    private static readonly Color SlayMonsterPinColor = new(173, 79, 79);
    private static readonly Color FishingPadColor = new(173, 207, 235);
    private static readonly Color FishingPinColor = new(85, 137, 186);
    private static readonly Color SocialPadColor = new(229, 200, 232);
    private static readonly Color SocialPinColor = new(151, 96, 175);

    private readonly List<Note> _notes = new();
    private readonly Dictionary<int, Note> _notesByCc = new();
    private readonly Texture2D _billboardTexture;
    private readonly Texture2D _padTexture;
    private readonly Texture2D _pinTexture;
    public static Billboard? InnerBillboard { get; set; }

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

        float scale = ChooseScale(slots.Count);
        int side = (int)(PadSpriteSize * scale);

        var placed = new List<Rectangle>(slots.Count);
        var rng = new Random(Game1.Date.TotalDays * 7919 + slots.Count);

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            Rectangle bounds = ScatterBounds(side, side, placed, rng)
                ?? FallbackGridBounds(i, slots.Count, side);
            placed.Add(bounds);

            (Color padColor, Color pinColor) = ColorsFor(slot.Posting.QuestType);

            var cc = new ClickableTextureComponent(
                bounds,
                _padTexture,
                new Rectangle(0, 0, PadSpriteSize, PadSpriteSize),
                scale)
            {
                myID = CcIndexBase - i,
                leftNeighborID = -7777,
                rightNeighborID = -7777,
                upNeighborID = -7777,
                downNeighborID = -7777
            };

            var note = new Note
            {
                Cc = cc,
                Slot = slot,
                PadColor = padColor,
                PinColor = pinColor,
                Portrait = TryGetPortrait(slot.Posting.QuestGiver)
            };
            _notes.Add(note);
            _notesByCc[cc.myID] = note;
        }
    }

    private static float ChooseScale(int count) =>
        count switch
        {
            <= 4 => 4f,
            <= 8 => 3f,
            <= 14 => 2.5f,
            _ => 2f
        };

    private Rectangle? ScatterBounds(int w, int h, List<Rectangle> placed, Random rng)
    {
        if (w >= BoardRect.Width || h >= BoardRect.Height)
            return null;

        const float xOverlap = 0.7f;
        const float yOverlap = 0.7f;
        for (int tries = 0; tries < 4000; tries++)
        {
            var rect = new Rectangle(
                xPositionOnScreen + BoardRect.X + rng.Next(0, BoardRect.Width - w),
                yPositionOnScreen + BoardRect.Y + rng.Next(0, BoardRect.Height - h),
                w, h);

            bool clash = false;
            foreach (var p in placed)
            {
                if (Math.Abs(p.Center.X - rect.Center.X) < rect.Width * xOverlap
                    && Math.Abs(p.Center.Y - rect.Center.Y) < rect.Height * yOverlap)
                {
                    clash = true;
                    break;
                }
            }
            if (!clash)
                return rect;
        }
        return null;
    }

    private Rectangle FallbackGridBounds(int i, int total, int side)
    {
        int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(total)));
        int rows = (int)Math.Ceiling(total / (double)cols);
        int cellW = BoardRect.Width / cols;
        int cellH = BoardRect.Height / rows;
        int col = i % cols;
        int row = i / cols;
        int x = xPositionOnScreen + BoardRect.X + col * cellW + (cellW - side) / 2;
        int y = yPositionOnScreen + BoardRect.Y + row * cellH + (cellH - side) / 2;
        return new Rectangle(x, y, side, side);
    }

    private static (Color pad, Color pin) ColorsFor(BoardQuestType type) =>
        type switch
        {
            BoardQuestType.ResourceCollection => (ResourceCollectionPadColor, ResourceCollectionPinColor),
            BoardQuestType.SlayMonster => (SlayMonsterPadColor, SlayMonsterPinColor),
            BoardQuestType.Fishing => (FishingPadColor, FishingPinColor),
            BoardQuestType.Socialize => (SocialPadColor, SocialPinColor),
            _ => (ItemDeliveryPadColor, ItemDeliveryPinColor)
        };

    private static Texture2D? TryGetPortrait(string npcName)
    {
        if (string.IsNullOrEmpty(npcName))
            return null;
        try
        {
            var npc = Game1.getCharacterFromName(npcName);
            return npc?.Portrait;
        }
        catch
        {
            return null;
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
                _hoverText = note.Slot.Posting.QuestGiver;
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
        if (InnerBillboard != null)
        {
            InnerBillboard = null;
            BillboardSlots.Selected = null;
            return false;
        }
        return true;
    }

    public override void snapToDefaultClickableComponent()
    {
        if (InnerBillboard != null)
        {
            InnerBillboard.snapToDefaultClickableComponent();
            return;
        }
        if (_notes.Count > 0)
        {
            currentlySnappedComponent = getComponentWithID(_notes[0].Cc.myID);
            snapCursorToCurrentSnappedComponent();
        }
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
            var padSource = new Rectangle(0, 0, PadSpriteSize, PadSpriteSize);
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
