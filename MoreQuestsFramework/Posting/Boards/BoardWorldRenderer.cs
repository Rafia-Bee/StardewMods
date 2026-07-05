using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Mods;

namespace MoreQuestsFramework.Posting.Boards;

internal sealed class BoardWorldRenderer
{
    private const int TilePixels = 64;
    private const int IndicatorSourceX = 395;
    private const int IndicatorSourceY = 497;
    private const int IndicatorSourceW = 3;
    private const int IndicatorSourceH = 8;

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly BoardRegistry _boards;
    private readonly Dictionary<string, Texture2D?> _textureCache = new();

    public BoardWorldRenderer(IModHelper helper, IMonitor monitor, BoardRegistry boards)
    {
        _helper = helper;
        _monitor = monitor;
        _boards = boards;
    }

    public void Register()
    {
        // World_Sorted uses FrontToBack so the board sorts correctly against characters.
        // RenderedWorld would always paint on top because it fires in a fresh batch.
        _helper.Events.Display.RenderedStep += OnRenderedStep;
        _helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    // The anchor tile is the board's bottom-left tile. The sprite grows up and to the right
    // from there. PixelOffset nudges the whole rect for fine alignment.
    public static Rectangle GetSpriteRect(BoardDefinition board)
    {
        int widthPx = board.BoardWidth * TilePixels;
        int heightPx = board.BoardHeight * TilePixels;
        int x = board.TileX * TilePixels + board.PixelOffsetX;
        int y = (board.TileY + 1) * TilePixels - heightPx + board.PixelOffsetY;
        return new Rectangle(x, y, widthPx, heightPx);
    }

    private void OnRenderedStep(object? sender, RenderedStepEventArgs e)
    {
        if (e.Step != RenderSteps.World_Sorted)
            return;
        // No activeClickableMenu check: the world still draws behind the pause/escape menu,
        // so the board should stay drawn too, like any other world object.
        if (!Context.IsWorldReady || Game1.currentLocation == null || Game1.eventUp)
            return;

        string locationName = Game1.currentLocation.Name;
        foreach (var board in _boards.InLocation(locationName))
        {
            if (!IsAvailable(board))
                continue;

            var texture = GetTextureFor(board);
            var rect = GetSpriteRect(board);
            if (texture != null)
            {
                var screenPos = Game1.GlobalToLocal(Game1.viewport, new Vector2(rect.X, rect.Y));
                float scaleX = rect.Width / (float)texture.Width;
                float scaleY = rect.Height / (float)texture.Height;
                e.SpriteBatch.Draw(
                    texture,
                    new Vector2(screenPos.X, screenPos.Y),
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    new Vector2(scaleX, scaleY),
                    SpriteEffects.None,
                    layerDepth: rect.Bottom / 10000f);
            }

            if (ShouldDrawIndicator(board))
                DrawIndicator(e.SpriteBatch, board, rect);
        }
    }

    private bool ShouldDrawIndicator(BoardDefinition board)
    {
        if (board.Indicator == null || !board.Indicator.Show)
            return false;
        return CustomBoardSlots.SlotsFor(board).Count > 0;
    }

    private static void DrawIndicator(SpriteBatch b, BoardDefinition board, Rectangle spriteRect)
    {
        var anchor = new Vector2(
            spriteRect.X + spriteRect.Width / 2f,
            spriteRect.Y);
        if (board.Indicator != null)
            anchor += new Vector2(board.Indicator.OffsetX, board.Indicator.OffsetY);
        var screenPos = Game1.GlobalToLocal(Game1.viewport, anchor);

        float bob = 4f * (float)System.Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0);
        b.Draw(
            Game1.mouseCursors,
            screenPos + new Vector2(-IndicatorSourceW * 4 / 2f, -32 + bob),
            new Rectangle(IndicatorSourceX, IndicatorSourceY, IndicatorSourceW, IndicatorSourceH),
            Color.White,
            0f,
            Vector2.Zero,
            4f,
            SpriteEffects.None,
            layerDepth: 0.99f);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null || Game1.activeClickableMenu != null)
            return;
        if (!e.Button.IsActionButton())
            return;

        var tile = e.Cursor.GrabTile;
        int tx = (int)tile.X;
        int ty = (int)tile.Y;

        string locationName = Game1.currentLocation.Name;
        foreach (var board in _boards.InLocation(locationName))
        {
            if (!IsClickInFootprint(board, tx, ty))
                continue;
            if (!IsAvailable(board))
                continue;

            if (!IsPlayerWithinReach(board, tx, ty))
                continue;

            Game1.activeClickableMenu = new CustomBoardMenu(board);
            Game1.playSound("bigSelect");
            _helper.Input.Suppress(e.Button);
            ModEntry.LogDebug($"Opened CustomBoardMenu for '{board.OwnerUniqueId}/{board.Name}'.");
            return;
        }
    }

    private static bool IsClickInFootprint(BoardDefinition board, int tileX, int tileY)
    {
        if (board.TileX == tileX && board.TileY == tileY)
            return true;
        var rect = GetSpriteRect(board);
        var tilePx = new Rectangle(tileX * TilePixels, tileY * TilePixels, TilePixels, TilePixels);
        return rect.Intersects(tilePx);
    }

    private static bool IsPlayerWithinReach(BoardDefinition board, int tileX, int tileY)
    {
        var player = Game1.player;
        if (player == null)
            return false;
        int px = player.TilePoint.X;
        int py = player.TilePoint.Y;
        int dx = System.Math.Abs(px - tileX);
        int dy = System.Math.Abs(py - tileY);
        return dx <= 1 && dy <= 1;
    }

    private bool IsAvailable(BoardDefinition board)
    {
        if (board.Available == null || board.Available.Count == 0)
            return true;
        return ConditionEvaluator.Evaluate(board.Available, _helper.ModRegistry);
    }

    private Texture2D? GetTextureFor(BoardDefinition board)
    {
        if (string.IsNullOrEmpty(board.Texture))
            return null;
        if (_textureCache.TryGetValue(board.Texture, out var cached))
            return cached;

        Texture2D? loaded = null;
        try
        {
            loaded = Game1.content.Load<Texture2D>(board.Texture);
        }
        catch
        {
            _monitor.Log(
                $"Board '{board.OwnerUniqueId}/{board.Name}': failed to load Texture '{board.Texture}'. " +
                "The board's anchor tile remains clickable; the in-world sprite will not render.",
                LogLevel.Warn);
        }
        _textureCache[board.Texture] = loaded;
        return loaded;
    }
}
