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

    // The pad sprite is 64x64 but the actual note paper only fills this box inside it (the
    // rest is transparent margin). Layout works in paper pixels so a note's clickable bounds
    // match what you see, and "spacing 0" means the paper edges touch (not bounds touch, which
    // left a visible gap from the ~10px side margins).
    public const int PadPaperWidth = 43;
    public const int PadPaperHeight = 56;

    // Grid layout tunables, player-configurable via GMCM. NoteSpacing is the average gap in
    // pixels between paper edges (0 = touching, negative = overlapping); MaxNoteSize caps a
    // single note when only a few are posted.
    public static int NoteSpacing => ModEntry.Config.BoardNoteSpacing;
    public static int MaxNoteSize => ModEntry.Config.BoardMaxNoteSize;

    // Max tilt either way, in radians (~7 degrees). Each note picks a random angle in
    // [-this, +this], so they lean left and right by varying amounts.
    public const float MaxTiltRadians = 0.12f;

    // Each gap is jittered to spacing +/- this, so the layout reads as hand-pinned rather
    // than a perfect grid. The player tunes the average; this varies it note to note.
    public const float GapJitter = 8f;

    // Picks the column/row split that makes the notes as large as possible while still
    // fitting count of them in the cork area at the average spacing. Height is the tighter
    // dimension, so the sweet spot usually has more columns than rows (20 notes land on a
    // 7x3 grid). Returns the full sprite size; paper dims are derived from it.
    public static (int cols, int rows, int noteSize) ChooseGrid(int count)
    {
        if (count <= 0)
            return (1, 1, MaxNoteSize);

        // Size for the largest a gap can jitter to, so the random spacing never pushes the
        // outer notes off the board.
        float maxGap = NoteSpacing + GapJitter;
        int bestCols = 1, bestRows = count, bestSize = 0;
        for (int cols = 1; cols <= count; cols++)
        {
            int rows = (count + cols - 1) / cols;
            float scaleW = (BoardRect.Width - (cols - 1) * maxGap) / (cols * PadPaperWidth);
            float scaleH = (BoardRect.Height - (rows - 1) * maxGap) / (rows * PadPaperHeight);
            int size = (int)(Math.Min(scaleW, scaleH) * PadSpriteSize);
            if (size > bestSize)
            {
                bestSize = size;
                bestCols = cols;
                bestRows = rows;
            }
        }

        int noteSize = Math.Clamp(Math.Min(bestSize, MaxNoteSize), PadSpriteSize, MaxNoteSize);
        return (bestCols, bestRows, noteSize);
    }

    // Computes the clickable paper bounds for every note in one pass. Each gap is jittered
    // around the average so the spacing isn't uniform; rows and the whole block are centered
    // so the board never looks lopsided. Bounds are the visible paper rect (the drawing code
    // expands them back out to the full sprite), so gap 0 = papers touching, negative = paper
    // overlap. rng makes the jitter deterministic per day.
    public static List<Rectangle> ComputeGridLayout(
        int xPositionOnScreen, int yPositionOnScreen, int count, Random rng)
    {
        var result = new List<Rectangle>(Math.Max(0, count));
        if (count <= 0)
            return result;

        var (cols, rows, noteSize) = ChooseGrid(count);
        float scale = noteSize / (float)PadSpriteSize;
        float paperW = PadPaperWidth * scale;
        float paperH = PadPaperHeight * scale;
        int spacing = NoteSpacing;

        // Vertical gaps between rows, then center the stack.
        float totalH = rows * paperH;
        var rowGaps = new float[Math.Max(0, rows - 1)];
        for (int r = 0; r < rowGaps.Length; r++)
        {
            rowGaps[r] = Jitter(spacing, rng);
            totalH += rowGaps[r];
        }
        float cy = yPositionOnScreen + BoardRect.Y + (BoardRect.Height - totalH) / 2f;

        for (int row = 0; row < rows; row++)
        {
            int itemsInRow = Math.Min(cols, count - row * cols);

            // Horizontal gaps for this row, then center the row.
            float rowW = itemsInRow * paperW;
            var colGaps = new float[Math.Max(0, itemsInRow - 1)];
            for (int c = 0; c < colGaps.Length; c++)
            {
                colGaps[c] = Jitter(spacing, rng);
                rowW += colGaps[c];
            }
            float px = xPositionOnScreen + BoardRect.X + (BoardRect.Width - rowW) / 2f;

            for (int col = 0; col < itemsInRow; col++)
            {
                result.Add(new Rectangle(
                    (int)Math.Round(px), (int)Math.Round(cy),
                    (int)Math.Round(paperW), (int)Math.Round(paperH)));
                px += paperW + (col < colGaps.Length ? colGaps[col] : 0f);
            }

            cy += paperH + (row < rowGaps.Length ? rowGaps[row] : 0f);
        }

        return result;
    }

    private static float Jitter(int spacing, Random rng) =>
        spacing + (float)(rng.NextDouble() * 2 - 1) * GapJitter;

    // A tilt in [-MaxTiltRadians, +MaxTiltRadians] for note `index` on day `daySeed`. Uses a
    // hash instead of the shared Random because System.Random, seeded per day, hands back
    // poorly spread values at the same sequence positions, which made every note lean the
    // same way. The hash gives each note an independent, evenly mixed left/right lean that
    // still stays stable for the day.
    public static float TiltFor(int daySeed, int index)
    {
        uint h = (uint)daySeed * 2654435761u + (uint)index * 40503u + 0x9E3779B9u;
        h ^= h >> 15;
        h *= 0x2C1B3C6Du;
        h ^= h >> 12;
        h *= 0x297A2D39u;
        h ^= h >> 15;
        float unit = h / (float)uint.MaxValue;
        return (unit * 2f - 1f) * MaxTiltRadians;
    }

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

    // Pad/pin colors come from the Categories asset (parsed once into the registry).
    // Unknown category falls back to Social's pair.
    public static (Color pad, Color pin) ColorsFor(string category)
        => ModEntry.Categories.ColorsFor(category);

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
