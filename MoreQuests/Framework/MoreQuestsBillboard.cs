using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

namespace MoreQuests.Framework;

/// Custom Billboard menu that renders one tile per `BillboardSlots.Slot` instead of vanilla's
/// single quest slot. Clicking a tile selects that quest and spawns the standard vanilla
/// `Billboard(true)` over the top so the player sees vanilla's accept-quest UI; our Harmony
/// patches redirect `Game1.questOfTheDay` getters there to the selected slot.
internal sealed class MoreQuestsBillboard : Billboard
{
    private static readonly Rectangle BoardRect = new(78 * 4, 58 * 4, 184 * 4, 96 * 4);
    private const int CcIndexBase = -42000;

    private readonly List<ClickableTextureComponent> _components = new();
    private readonly Dictionary<int, BillboardSlots.Slot> _slotsByCc = new();
    private readonly Texture2D _billboardTexture;
    public static Billboard? InnerBillboard { get; set; }

    private string _hoverTitle = "";
    private string _hoverText = "";

    public MoreQuestsBillboard()
        : base(true)
    {
        _billboardTexture = Game1.temporaryContent.Load<Texture2D>("LooseSprites\\Billboard");
        InnerBillboard = null;
        BuildClickableComponents();

        exitFunction = delegate
        {
            if (InnerBillboard != null)
                Game1.activeClickableMenu = new MoreQuestsBillboard();
        };
        populateClickableComponentList();
    }

    private void BuildClickableComponents()
    {
        _components.Clear();
        _slotsByCc.Clear();

        var slots = BillboardSlots.Slots;
        if (slots.Count == 0)
            return;

        // Grid layout. Up to 20 tiles fit in a 5-col x 4-row grid inside BoardRect.
        int cols = Math.Min(5, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(slots.Count))));
        int rows = (int)Math.Ceiling(slots.Count / (double)cols);
        int cellW = BoardRect.Width / cols;
        int cellH = BoardRect.Height / rows;
        int padX = Math.Max(8, cellW / 12);
        int padY = Math.Max(8, cellH / 10);
        int tileW = cellW - padX * 2;
        int tileH = cellH - padY * 2;

        for (int i = 0; i < slots.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            var bounds = new Rectangle(
                xPositionOnScreen + BoardRect.X + col * cellW + padX,
                yPositionOnScreen + BoardRect.Y + row * cellH + padY,
                tileW,
                tileH);

            var cc = new ClickableTextureComponent(
                bounds,
                _billboardTexture,
                new Rectangle(140, 397, 10, 11),
                4f)
            {
                myID = CcIndexBase - i,
                leftNeighborID = col > 0 ? CcIndexBase - (i - 1) : -1,
                rightNeighborID = col < cols - 1 && i + 1 < slots.Count ? CcIndexBase - (i + 1) : -1,
                upNeighborID = row > 0 ? CcIndexBase - (i - cols) : -1,
                downNeighborID = row < rows - 1 && i + cols < slots.Count ? CcIndexBase - (i + cols) : -1
            };
            _components.Add(cc);
            _slotsByCc[cc.myID] = slots[i];
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
        foreach (var cc in _components)
        {
            if (cc.containsPoint(x, y))
            {
                var slot = _slotsByCc[cc.myID];
                _hoverTitle = slot.Quest.questTitle ?? "";
                _hoverText = slot.Posting.QuestGiver;
                cc.scale = Math.Min(cc.scale + 0.04f, 4.5f);
            }
            else
            {
                cc.scale = Math.Max(cc.scale - 0.04f, 4f);
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

        foreach (var cc in _components)
        {
            if (cc.containsPoint(x, y))
            {
                BillboardSlots.Selected = _slotsByCc[cc.myID];
                InnerBillboard = new Billboard(true);
                InnerBillboard.acceptQuestButton.visible = true;
                Game1.playSound("smallSelect");
                return;
            }
        }

        // Fall back to base IClickableMenu so the close button still works.
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
        if (_components.Count > 0)
        {
            currentlySnappedComponent = getComponentWithID(_components[0].myID);
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

        if (_components.Count == 0)
        {
            b.DrawString(
                Game1.dialogueFont,
                Game1.content.LoadString("Strings\\UI:Billboard_NothingPosted"),
                new Vector2(xPositionOnScreen + 384, yPositionOnScreen + 320),
                Game1.textColor);
        }
        else
        {
            foreach (var cc in _components)
            {
                var slot = _slotsByCc[cc.myID];
                IClickableMenu.drawTextureBox(
                    b,
                    Game1.mouseCursors,
                    new Rectangle(384, 396, 15, 15),
                    cc.bounds.X, cc.bounds.Y, cc.bounds.Width, cc.bounds.Height,
                    Color.White, 4f, drawShadow: false);

                cc.draw(b, Color.White, 1f);

                string label = slot.Posting.QuestGiver;
                if (!string.IsNullOrEmpty(label))
                {
                    var size = Game1.smallFont.MeasureString(label);
                    Utility.drawTextWithShadow(
                        b, label, Game1.smallFont,
                        new Vector2(cc.bounds.Center.X - size.X / 2f, cc.bounds.Bottom - size.Y - 8),
                        Game1.textColor);
                }
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
