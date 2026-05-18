using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Registry;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuestsFramework.Posting.Boards;

// SMAPI-only (no Harmony). Boards failing Available are silently hidden.
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
        _helper.Events.Display.RenderedWorld += OnRenderedWorld;
        _helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null || Game1.eventUp || Game1.activeClickableMenu != null)
            return;

        string locationName = Game1.currentLocation.Name;
        foreach (var board in _boards.InLocation(locationName))
        {
            if (!IsAvailable(board))
                continue;

            var texture = GetTextureFor(board);
            if (texture != null)
            {
                var worldPos = new Vector2(
                    board.TileX * TilePixels + board.DrawOffsetX,
                    board.TileY * TilePixels + board.DrawOffsetY);
                var screenPos = Game1.GlobalToLocal(Game1.viewport, worldPos);
                float scale = board.WorldScale > 0 ? board.WorldScale : 2f;
                e.SpriteBatch.Draw(
                    texture,
                    screenPos,
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    layerDepth: (board.TileY + 1) * TilePixels / 10000f + 0.001f);
            }

            if (ShouldDrawIndicator(board))
                DrawIndicator(e.SpriteBatch, board);
        }
    }

    private bool ShouldDrawIndicator(BoardDefinition board)
    {
        if (board.Indicator == null || !board.Indicator.Show)
            return false;
        return CustomBoardSlots.SlotsFor(board).Count > 0;
    }

    private static void DrawIndicator(SpriteBatch b, BoardDefinition board)
    {
        var anchor = new Vector2(
            board.TileX * TilePixels + board.DrawOffsetX,
            board.TileY * TilePixels + board.DrawOffsetY);
        if (board.Indicator != null)
            anchor += new Vector2(board.Indicator.OffsetX, board.Indicator.OffsetY);
        var screenPos = Game1.GlobalToLocal(Game1.viewport, anchor);

        float bob = 4f * (float)System.Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0);
        b.Draw(
            Game1.mouseCursors,
            screenPos + new Vector2(28, -32 + bob),
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

    // Anchor tile is always clickable, even when the footprint floats off-grid
    // (sub-tile pixel DrawOffset).
    private static bool IsClickInFootprint(Api.BoardDefinition board, int tileX, int tileY)
    {
        if (board.TileX == tileX && board.TileY == tileY)
            return true;
        int fpX = board.TileX + (board.DrawOffsetX / TilePixels);
        int fpY = board.TileY + (board.DrawOffsetY / TilePixels);
        return tileX >= fpX
            && tileY >= fpY
            && tileX < fpX + board.FootprintWidth
            && tileY < fpY + board.FootprintHeight;
    }

    private static bool IsPlayerWithinReach(Api.BoardDefinition board, int tileX, int tileY)
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
