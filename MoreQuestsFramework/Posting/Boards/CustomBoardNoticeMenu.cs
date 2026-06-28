using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Pipeline;
using StardewValley;
using StardewValley.Menus;

namespace MoreQuestsFramework.Posting.Boards;

// The read-only popup a notice pin opens. Built to match CustomBoardQuestMenu (same parchment
// board skin, same close button, same auto-shrink text) but with no accept button: a notice
// just shows its title and body. A photo notice (one with an Image) shows the picture with the
// body as its caption instead. Font, parchment skin, and text color come from the notice's
// category, so two categories read like two notice "types". Close routes back through the outer
// menu the same way the accept popup does, so Esc / B / right-click / the close button all match.
internal sealed class CustomBoardNoticeMenu : IClickableMenu
{
    private const int SourceWidth = 338;
    private const int SourceHeight = 198;
    private const int Scale = 4;
    private const int MenuWidth = SourceWidth * Scale;
    private const int MenuHeight = 792;
    private const int CloseButtonId = 1;
    private const float TextLeft = 320 + 32;
    private const float TitleTop = 116f;
    // The parchment doesn't fill the whole popup; the board frame eats the bottom. Keep all
    // content this many pixels clear of the bottom edge so text/images stay on the paper.
    private const float ContentBottomMargin = 150f;
    private const float TextWrapWidth = 640f;

    private readonly CustomBoardMenu _outer;
    private readonly NoticeInstance _notice;
    private readonly Texture2D _backgroundTexture;
    private readonly SpriteFont _bodyFont;
    private readonly Color _textColor;
    private readonly Texture2D? _image;
    private readonly Rectangle? _imageSource;

    public CustomBoardNoticeMenu(CustomBoardMenu outer, NoticeInstance notice, Texture2D backgroundTexture)
    {
        _outer = outer;
        _notice = notice;

        _backgroundTexture = ResolveBackground(notice.Category, backgroundTexture);
        _bodyFont = ResolveFont(ModEntry.Categories.FontFor(notice.Category));
        _textColor = ModEntry.Categories.TextColorFor(notice.Category) ?? Game1.textColor;
        _image = string.IsNullOrWhiteSpace(notice.Image) ? null : TryLoad(notice.Image);
        _imageSource = notice.ImageSource;

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

        if (_image != null)
            DrawPhotoPopup(b);
        else
            DrawTextPopup(b);

        if (upperRightCloseButton != null)
            upperRightCloseButton.draw(b);

        Game1.mouseCursorTransparency = 1f;
        drawMouse(b);
    }

    // Plain text notice: title and body left-aligned, the look the first notices shipped with.
    // The body shrinks to fit above the bottom frame if it runs long.
    private void DrawTextPopup(SpriteBatch b)
    {
        float textX = xPositionOnScreen + TextLeft;
        float bodyTop = TitleTop;
        if (!string.IsNullOrEmpty(_notice.Title))
        {
            string title = Game1.parseText(_notice.Title, Game1.dialogueFont, (int)TextWrapWidth);
            Utility.drawTextWithShadow(
                b, title, Game1.dialogueFont,
                new Vector2(textX, yPositionOnScreen + TitleTop),
                _textColor);
            bodyTop = TitleTop + Game1.dialogueFont.MeasureString(title).Y + 24f;
        }

        if (string.IsNullOrEmpty(_notice.Body))
            return;
        string body = Game1.parseText(_notice.Body, _bodyFont, (int)TextWrapWidth);
        float availableHeight = height - bodyTop - ContentBottomMargin;
        float textScale = 1f;
        Vector2 size = _bodyFont.MeasureString(body);
        if (size.Y > availableHeight && size.Y > 0f)
            textScale = availableHeight / size.Y;
        Utility.drawTextWithShadow(
            b, body, _bodyFont,
            new Vector2(textX, yPositionOnScreen + bodyTop),
            _textColor, textScale, -1f, -1, -1, 0.5f);
    }

    // Photo notice: title, picture, and caption are centered and kept inside the parchment. The
    // picture fits a box with its aspect ratio kept; the caption (the Body) sits below it and
    // shrinks if long. The whole picture + caption block is centered in the space under the title.
    private void DrawPhotoPopup(SpriteBatch b)
    {
        const float maxW = 600f;
        const float gap = 16f;
        float centerX = xPositionOnScreen + width / 2f;
        float top = yPositionOnScreen + TitleTop;
        float bottom = yPositionOnScreen + height - ContentBottomMargin;

        float y = top;
        if (!string.IsNullOrEmpty(_notice.Title))
        {
            string title = Game1.parseText(_notice.Title, Game1.dialogueFont, (int)maxW);
            Vector2 ts = Game1.dialogueFont.MeasureString(title);
            Utility.drawTextWithShadow(
                b, title, Game1.dialogueFont,
                new Vector2(centerX - ts.X / 2f, y), _textColor);
            y += ts.Y + 16f;
        }

        float regionH = Math.Max(1f, bottom - y);

        Rectangle src = _imageSource ?? new Rectangle(0, 0, _image!.Width, _image.Height);
        if (src.Width <= 0 || src.Height <= 0)
            src = new Rectangle(0, 0, _image!.Width, _image.Height);

        string caption = string.IsNullOrEmpty(_notice.Body)
            ? ""
            : Game1.parseText(_notice.Body, _bodyFont, (int)maxW);
        float captionScale = 1f;
        float captionH = caption.Length > 0 ? _bodyFont.MeasureString(caption).Y : 0f;
        float maxCaptionH = regionH * 0.30f;
        if (captionH > maxCaptionH && captionH > 0f)
        {
            captionScale = maxCaptionH / captionH;
            captionH = maxCaptionH;
        }

        float imageMaxH = Math.Max(1f, regionH - captionH - (caption.Length > 0 ? gap : 0f));
        float imageScale = Math.Min(maxW / src.Width, imageMaxH / src.Height);
        if (imageScale <= 0f)
            imageScale = 0.01f;
        float drawW = src.Width * imageScale;
        float drawH = src.Height * imageScale;

        float blockH = drawH + (caption.Length > 0 ? gap + captionH : 0f);
        float blockTop = y + Math.Max(0f, (regionH - blockH) / 2f);

        b.Draw(
            _image, new Vector2(centerX - drawW / 2f, blockTop), src,
            Color.White, 0f, Vector2.Zero, imageScale, SpriteEffects.None, 0.86f);

        if (caption.Length > 0)
        {
            float capW = _bodyFont.MeasureString(caption).X * captionScale;
            Utility.drawTextWithShadow(
                b, caption, _bodyFont,
                new Vector2(centerX - capW / 2f, blockTop + drawH + gap),
                _textColor, captionScale, -1f, -1, -1, 0.5f);
        }
    }

    // The board background is the fallback; a category PopupBackground swaps the parchment skin.
    private static Texture2D ResolveBackground(string? category, Texture2D fallback)
    {
        string? asset = ModEntry.Categories.PopupBackgroundFor(category);
        if (string.IsNullOrWhiteSpace(asset))
            return fallback;
        return TryLoad(asset) ?? fallback;
    }

    // "Dialogue" / "Small" / "Tiny" map to the built-in fonts; anything else is loaded as a
    // SpriteFont asset. A missing/unknown value keeps the language-aware default (the small
    // font for Korean, which the dialogue font can't render, else the dialogue font).
    private static SpriteFont ResolveFont(string? name)
    {
        SpriteFont fallback = LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko
            ? Game1.smallFont
            : Game1.dialogueFont;
        if (string.IsNullOrWhiteSpace(name))
            return fallback;
        switch (name.Trim().ToLowerInvariant())
        {
            case "dialogue":
                return Game1.dialogueFont;
            case "small":
                return Game1.smallFont;
            case "tiny":
                return Game1.tinyFont;
        }
        try
        {
            return Game1.content.Load<SpriteFont>(name.Trim());
        }
        catch
        {
            return fallback;
        }
    }

    private static Texture2D? TryLoad(string assetName)
    {
        try
        {
            return Game1.content.Load<Texture2D>(assetName);
        }
        catch
        {
            return null;
        }
    }
}
