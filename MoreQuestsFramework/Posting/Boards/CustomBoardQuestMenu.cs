using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

namespace MoreQuestsFramework.Posting.Boards;

internal sealed class CustomBoardQuestMenu : IClickableMenu
{
    private const int SourceWidth = 338;
    private const int SourceHeight = 198;
    private const int Scale = 4;
    private const int MenuWidth = SourceWidth * Scale;
    private const int MenuHeight = 792;

    private readonly CustomBoardMenu _outer;
    private readonly CustomBoardSlots.Slot _slot;
    private readonly Texture2D _backgroundTexture;
    public ClickableComponent acceptQuestButton;

    public CustomBoardQuestMenu(CustomBoardMenu outer, CustomBoardSlots.Slot slot, Texture2D backgroundTexture)
    {
        _outer = outer;
        _slot = slot;
        _backgroundTexture = backgroundTexture;

        width = MenuWidth;
        height = MenuHeight;
        var center = Utility.getTopLeftPositionForCenteringOnScreen(width, height);
        xPositionOnScreen = (int)center.X;
        yPositionOnScreen = (int)center.Y;

        string acceptText = Game1.content.LoadString("Strings\\UI:AcceptQuest");
        var size = Game1.dialogueFont.MeasureString(acceptText);
        acceptQuestButton = new ClickableComponent(
            new Rectangle(
                xPositionOnScreen + width / 2 - 128,
                yPositionOnScreen + height - 128,
                (int)size.X + 24,
                (int)size.Y + 24),
            "")
        {
            myID = 0
        };

        upperRightCloseButton = new ClickableTextureComponent(
            new Rectangle(xPositionOnScreen + width - 20, yPositionOnScreen, 48, 48),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            4f);
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        if (acceptQuestButton.visible)
        {
            float scale = acceptQuestButton.scale;
            acceptQuestButton.scale = acceptQuestButton.bounds.Contains(x, y) ? 1.5f : 1f;
            if (acceptQuestButton.scale > scale)
                Game1.playSound("Cowboy_gunshot");
        }
        if (upperRightCloseButton != null)
            upperRightCloseButton.tryHover(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton != null && upperRightCloseButton.containsPoint(x, y))
        {
            if (playSound)
                Game1.playSound("bigDeSelect");
            _outer.OnInnerAcceptClosed(reopen: false);
            return;
        }

        if (acceptQuestButton.visible && acceptQuestButton.containsPoint(x, y))
        {
            int deadline = System.Math.Max(1, _slot.Posting.DeadlineDays);
            Quest quest = _slot.Quest;
            quest.dayQuestAccepted.Value = Game1.Date.TotalDays;
            quest.accepted.Value = true;
            quest.canBeCancelled.Value = true;
            quest.daysLeft.Value = deadline;
            // dailyQuest=false: CustomBoard quests shouldn't trigger billboard milestone
            // mail or prize tickets.
            quest.dailyQuest.Value = false;
            Game1.player.questLog.Add(quest);

            CustomBoardSlots.Selected = _slot;
            CustomBoardSlots.AcceptSelected();

            Game1.playSound("newArtifact");
            _outer.OnInnerAcceptClosed(reopen: true);
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (playSound)
            Game1.playSound("bigDeSelect");
        _outer.OnInnerAcceptClosed(reopen: false);
    }

    public override void draw(SpriteBatch b)
    {
        if (!Game1.options.showClearBackgrounds)
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

        b.Draw(
            _backgroundTexture,
            new Vector2(xPositionOnScreen, yPositionOnScreen),
            new Rectangle(0, 0, SourceWidth, SourceHeight),
            Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 1f);

        SpriteFont font = LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko
            ? Game1.smallFont
            : Game1.dialogueFont;
        string description = Game1.parseText(_slot.Quest.questDescription, font, 640);
        Utility.drawTextWithShadow(
            b,
            description,
            font,
            new Vector2(xPositionOnScreen + 320 + 32, yPositionOnScreen + 256),
            Game1.textColor, 1f, -1f, -1, -1, 0.5f);

        if (acceptQuestButton.visible)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(403, 373, 9, 9),
                acceptQuestButton.bounds.X,
                acceptQuestButton.bounds.Y,
                acceptQuestButton.bounds.Width,
                acceptQuestButton.bounds.Height,
                acceptQuestButton.scale > 1f ? Color.LightPink : Color.White,
                4f * acceptQuestButton.scale);
            Utility.drawTextWithShadow(
                b,
                Game1.content.LoadString("Strings\\UI:AcceptQuest"),
                Game1.dialogueFont,
                new Vector2(
                    acceptQuestButton.bounds.X + 12,
                    acceptQuestButton.bounds.Y + (LocalizedContentManager.CurrentLanguageLatin ? 16 : 12)),
                Game1.textColor);
        }

        if (upperRightCloseButton != null)
            upperRightCloseButton.draw(b);

        Game1.mouseCursorTransparency = 1f;
        drawMouse(b);
    }
}
