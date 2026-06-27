using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Pipeline;
using StardewValley;
using StardewValley.Menus;

namespace MoreQuestsFramework.Posting.Boards;

// The read-only popup a notice pin opens. Built to match CustomBoardQuestMenu (same parchment
// board skin, same close button, same auto-shrink text) but with no accept button: a notice
// just shows its title and body. Close routes back through the outer menu the same way the
// accept popup does, so Esc / B / right-click / the close button all behave identically.
internal sealed class CustomBoardNoticeMenu : IClickableMenu
{
    private const int SourceWidth = 338;
    private const int SourceHeight = 198;
    private const int Scale = 4;
    private const int MenuWidth = SourceWidth * Scale;
    private const int MenuHeight = 792;
    private const int CloseButtonId = 1;

    private readonly CustomBoardMenu _outer;
    private readonly NoticeInstance _notice;
    private readonly Texture2D _backgroundTexture;

    public CustomBoardNoticeMenu(CustomBoardMenu outer, NoticeInstance notice, Texture2D backgroundTexture)
    {
        _outer = outer;
        _notice = notice;
        _backgroundTexture = backgroundTexture;

        width = MenuWidth;
        height = MenuHeight;
        var center = Utility.getTopLeftPositionForCenteringOnScreen(width, height);
        xPositionOnScreen = (int)center.X;
        yPositionOnScreen = (int)center.Y;

        upperRightCloseButton = new ClickableTextureComponent(
            new Rectangle(xPositionOnScreen + width - 20, yPositionOnScreen, 48, 48),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            4f)
        {
            myID = CloseButtonId
        };

        populateClickableComponentList();
    }

    public override void populateClickableComponentList()
    {
        allClickableComponents = new List<ClickableComponent>();
        if (upperRightCloseButton != null)
            allClickableComponents.Add(upperRightCloseButton);
    }

    public override void snapToDefaultClickableComponent()
    {
        currentlySnappedComponent = upperRightCloseButton;
        snapCursorToCurrentSnappedComponent();
    }

    public override void performHoverAction(int x, int y)
    {
        base.performHoverAction(x, y);
        if (upperRightCloseButton != null)
            upperRightCloseButton.tryHover(x, y);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (upperRightCloseButton != null && upperRightCloseButton.containsPoint(x, y))
        {
            if (playSound)
                Game1.playSound("bigDeSelect");
            _outer.OnInnerPopupClosed(reopen: false);
        }
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (playSound)
            Game1.playSound("bigDeSelect");
        _outer.OnInnerPopupClosed(reopen: false);
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

        const float textLeft = 320 + 32;
        const float titleTop = 116f;
        const float bottomPadding = 48f;
        float textX = xPositionOnScreen + textLeft;

        float bodyTop = titleTop;
        if (!string.IsNullOrEmpty(_notice.Title))
        {
            string title = Game1.parseText(_notice.Title, Game1.dialogueFont, 640);
            Utility.drawTextWithShadow(
                b, title, Game1.dialogueFont,
                new Vector2(textX, yPositionOnScreen + titleTop),
                Game1.textColor);
            bodyTop = titleTop + Game1.dialogueFont.MeasureString(title).Y + 24f;
        }

        if (!string.IsNullOrEmpty(_notice.Body))
        {
            string body = Game1.parseText(_notice.Body, font, 640);
            // A custom font with tall line metrics can overflow the parchment; shrink the body
            // just enough to fit between its top and the bottom margin, same as the quest popup.
            float availableHeight = height - bodyTop - bottomPadding;
            float textScale = 1f;
            Vector2 size = font.MeasureString(body);
            if (size.Y > availableHeight && size.Y > 0f)
                textScale = availableHeight / size.Y;
            Utility.drawTextWithShadow(
                b, body, font,
                new Vector2(textX, yPositionOnScreen + bodyTop),
                Game1.textColor, textScale, -1f, -1, -1, 0.5f);
        }

        if (upperRightCloseButton != null)
            upperRightCloseButton.draw(b);

        Game1.mouseCursorTransparency = 1f;
        drawMouse(b);
    }
}
