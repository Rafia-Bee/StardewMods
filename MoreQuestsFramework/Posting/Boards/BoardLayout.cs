using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace MoreQuestsFramework.Posting.Boards;

/// Shared cork-board layout primitives. Used by both the help-wanted-board renderer
/// (`MoreQuestsBillboard`) and the generic `CustomBoardMenu` so both render notes with
/// identical scatter behaviour, sizing curves, and per-quest-type tinting.
internal static class BoardLayout
{
    /// Inner area of the cork-board background where notes can be scattered.
    public static readonly Rectangle BoardRect = new(78 * 4, 58 * 4, 184 * 4, 96 * 4);
    public const int PadSpriteSize = 64;

    public static readonly Color ItemDeliveryPadColor = new(244, 212, 130);
    public static readonly Color ItemDeliveryPinColor = new(200, 126, 52);
    public static readonly Color ResourceCollectionPadColor = new(182, 223, 158);
    public static readonly Color ResourceCollectionPinColor = new(98, 157, 86);
    public static readonly Color SlayMonsterPadColor = new(231, 166, 166);
    public static readonly Color SlayMonsterPinColor = new(173, 79, 79);
    public static readonly Color FishingPadColor = new(173, 207, 235);
    public static readonly Color FishingPinColor = new(85, 137, 186);
    public static readonly Color SocialPadColor = new(229, 200, 232);
    public static readonly Color SocialPinColor = new(151, 96, 175);

    public static float ChooseScale(int count) =>
        count switch
        {
            <= 4 => 4f,
            <= 8 => 3f,
            <= 14 => 2.5f,
            _ => 2f
        };

    /// Scatter-place a note rectangle inside `BoardRect` (anchored at `xPositionOnScreen` /
    /// `yPositionOnScreen`). Returns null if no non-overlapping position was found in 4000
    /// tries, the caller should fall back to the grid layout.
    public static Rectangle? ScatterBounds(
        int xPositionOnScreen, int yPositionOnScreen,
        int w, int h, List<Rectangle> placed, Random rng)
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

    public static Rectangle FallbackGridBounds(
        int xPositionOnScreen, int yPositionOnScreen,
        int i, int total, int side)
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

    public static (Color pad, Color pin) ColorsFor(BoardQuestType type) =>
        type switch
        {
            BoardQuestType.ResourceCollection => (ResourceCollectionPadColor, ResourceCollectionPinColor),
            BoardQuestType.SlayMonster => (SlayMonsterPadColor, SlayMonsterPinColor),
            BoardQuestType.Fishing => (FishingPadColor, FishingPinColor),
            BoardQuestType.Socialize => (SocialPadColor, SocialPinColor),
            _ => (ItemDeliveryPadColor, ItemDeliveryPinColor)
        };

    public static Texture2D? TryGetPortrait(string npcName)
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
}
