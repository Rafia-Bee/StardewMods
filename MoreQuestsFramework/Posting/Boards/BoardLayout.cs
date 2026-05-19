using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace MoreQuestsFramework.Posting.Boards;

internal static class BoardLayout
{
    public static readonly Rectangle BoardRect = new(78 * 4, 58 * 4, 184 * 4, 96 * 4);
    public const int PadSpriteSize = 64;

    // One distinct pad/pin pair per QuestCategory. Pad is the light note paper,
    // pin is the darker tack. Pairs are picked so the nine categories stay
    // visually separable on the cork board even at small scales.
    // Pins are picked from a contrasting hue family from their pad (warm pad =
    // cool pin and vice versa) so the tack always pops off the note paper.
    public static readonly Color AnimalPadColor = new(244, 195, 155);
    public static readonly Color AnimalPinColor = new(35, 75, 100);
    public static readonly Color CookingPadColor = new(250, 215, 140);
    public static readonly Color CookingPinColor = new(95, 45, 115);
    public static readonly Color FarmingPadColor = new(210, 225, 140);
    public static readonly Color FarmingPinColor = new(130, 40, 80);
    public static readonly Color FestivalPadColor = new(240, 175, 175);
    public static readonly Color FestivalPinColor = new(50, 95, 70);
    public static readonly Color FishingPadColor = new(175, 210, 235);
    public static readonly Color FishingPinColor = new(160, 75, 30);
    public static readonly Color ForagingPadColor = new(180, 220, 165);
    public static readonly Color ForagingPinColor = new(140, 50, 80);
    public static readonly Color MiningPadColor = new(210, 205, 200);
    public static readonly Color MiningPinColor = new(130, 35, 50);
    public static readonly Color SeasonalPadColor = new(175, 225, 220);
    public static readonly Color SeasonalPinColor = new(155, 60, 45);
    public static readonly Color SocialPadColor = new(235, 195, 225);
    public static readonly Color SocialPinColor = new(50, 110, 75);

    public static float ChooseScale(int count) =>
        count switch
        {
            <= 4 => 4f,
            <= 8 => 3f,
            <= 14 => 2.5f,
            _ => 2f
        };

    // Returns null after 4000 tries with no non-overlapping spot; caller falls back to grid.
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

    public static (Color pad, Color pin) ColorsFor(QuestCategory category) =>
        category switch
        {
            QuestCategory.Animal => (AnimalPadColor, AnimalPinColor),
            QuestCategory.Cooking => (CookingPadColor, CookingPinColor),
            QuestCategory.Farming => (FarmingPadColor, FarmingPinColor),
            QuestCategory.Festival => (FestivalPadColor, FestivalPinColor),
            QuestCategory.Fishing => (FishingPadColor, FishingPinColor),
            QuestCategory.Foraging => (ForagingPadColor, ForagingPinColor),
            QuestCategory.Mining => (MiningPadColor, MiningPinColor),
            QuestCategory.Seasonal => (SeasonalPadColor, SeasonalPinColor),
            _ => (SocialPadColor, SocialPinColor)
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
