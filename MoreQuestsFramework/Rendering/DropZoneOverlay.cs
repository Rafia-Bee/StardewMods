using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Quests;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuestsFramework.Rendering;

// Draws the ritual marker for any active DropItemsInRadius quest step in the current
// location: a tall pulsing beam of light (spottable from across the map) plus a ground ring.
// Pure overlay, it only draws on the render event and never touches tiles, objects, or
// collision, so NPCs and monsters walk straight through it and nothing gets destroyed.
internal static class DropZoneOverlay
{
    private const int TilePixels = 64;

    public static void Register(IModHelper helper)
    {
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
    }

    private static void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.currentLocation == null || Game1.eventUp || Game1.activeClickableMenu != null)
            return;
        var log = Game1.player?.questLog;
        if (log == null || log.Count == 0)
            return;

        var loc = Game1.currentLocation;
        foreach (var q in log)
        {
            if (q is not AdventureQuest aq)
                continue;
            if (!aq.TryGetDropZone(loc, out var center, out int radius) || radius <= 0)
                continue;
            DrawZone(e.SpriteBatch, center, radius);
        }
    }

    private static void DrawZone(SpriteBatch b, Point centerTile, int radius)
    {
        double now = Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0.0;
        float pulse = 0.5f + 0.5f * (float)Math.Sin(now * (Math.PI * 2 / 1.5));

        var worldCenter = new Vector2(centerTile.X * TilePixels + TilePixels / 2f,
                                      centerTile.Y * TilePixels + TilePixels / 2f);
        var screen = Game1.GlobalToLocal(Game1.viewport, worldCenter);
        int cx = (int)screen.X;

        // Beam of light: a ~9-tile column (wide-faint to narrow-bright glow) tall enough to
        // clear the trees, so the player can spot the spot from across the map.
        int baseY = (int)screen.Y + 24;
        int topY = baseY - TilePixels * 9;
        DrawColumn(b, cx, topY, baseY, 96, Color.MediumPurple * (0.10f + 0.06f * pulse));
        DrawColumn(b, cx, topY, baseY, 52, Color.MediumPurple * (0.16f + 0.08f * pulse));
        DrawColumn(b, cx, topY, baseY, 20, Color.Lavender * (0.35f + 0.20f * pulse));

        int n = Math.Max(32, radius * 16);
        float r = radius * TilePixels;
        Color ring = Color.MediumPurple * (0.65f + 0.35f * pulse);
        for (int i = 0; i < n; i++)
        {
            double ang = i / (double)n * Math.PI * 2.0;
            var world = worldCenter + new Vector2((float)Math.Cos(ang) * r, (float)Math.Sin(ang) * r);
            var p = Game1.GlobalToLocal(Game1.viewport, world);
            b.Draw(Game1.staminaRect, new Rectangle((int)p.X - 3, (int)p.Y - 3, 6, 6), ring);
        }

        b.Draw(Game1.staminaRect, new Rectangle(cx - 4, (int)screen.Y - 4, 8, 8), Color.Lavender * (0.7f + 0.3f * pulse));
    }

    private static void DrawColumn(SpriteBatch b, int centerX, int topY, int baseY, int width, Color color)
    {
        b.Draw(Game1.staminaRect, new Rectangle(centerX - width / 2, topY, width, baseY - topY), color);
    }
}
