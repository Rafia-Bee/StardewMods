using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MoreQuestsFramework.Api;
using StardewValley;

namespace MoreQuestsFramework.Posting.Boards;

// How a board arranges its notes.
internal enum LayoutMode { TiltedGrid, Scatter, Zoned }

// One note's final placement: the visible paper rect and its lean. The renderer expands the
// paper rect back out to the full sprite, so gap 0 = papers touching.
internal sealed class NotePlacement
{
    public Rectangle PaperBounds { get; init; }
    public float Tilt { get; init; }
}

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

    private enum ZoneStyle { TiltedGrid, Scatter }

    public static LayoutMode ParseMode(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "scatter" => LayoutMode.Scatter,
        "zoned" => LayoutMode.Zoned,
        _ => LayoutMode.TiltedGrid
    };

    private static ZoneStyle ParseZoneStyle(string? s) => s?.Trim().ToLowerInvariant() switch
    {
        "scatter" => ZoneStyle.Scatter,
        _ => ZoneStyle.TiltedGrid
    };

    // The one layout entry point. Returns a placement per slot (aligned to `categories`), or
    // null for a note a zoned board dropped because it matched no zone and there's no catch-all.
    // board == null means the daily board: a full-area tilted grid. `types` is an optional
    // per-slot pin type ("Quest" / "Notice"), aligned to `categories`, used only by Zoned
    // boards that route by Type; pass null when type routing isn't needed.
    public static List<NotePlacement?> ComputeLayout(
        BoardDefinition? board, IReadOnlyList<string> categories,
        int xPositionOnScreen, int yPositionOnScreen, int daySeed, Random rng,
        IReadOnlyList<string>? types = null)
    {
        int count = categories.Count;
        var result = new List<NotePlacement?>(count);
        for (int i = 0; i < count; i++)
            result.Add(null);
        if (count == 0)
            return result;

        var fullArea = new Rectangle(
            xPositionOnScreen + BoardRect.X, yPositionOnScreen + BoardRect.Y,
            BoardRect.Width, BoardRect.Height);

        LayoutMode mode = ParseMode(board?.Layout);
        if (mode == LayoutMode.Zoned && board?.Zones is { Count: > 0 })
        {
            LayoutZoned(board, categories, types, fullArea, daySeed, rng, result);
            return result;
        }

        var all = new List<int>(count);
        for (int i = 0; i < count; i++)
            all.Add(i);
        LayoutZoneInArea(mode == LayoutMode.Scatter ? ZoneStyle.Scatter : ZoneStyle.TiltedGrid,
            all, fullArea, daySeed, rng, result);
        return result;
    }

    private static void LayoutZoned(
        BoardDefinition board, IReadOnlyList<string> categories, IReadOnlyList<string>? types,
        Rectangle fullArea, int daySeed, Random rng, List<NotePlacement?> result)
    {
        var zones = board.Zones!;
        var buckets = new List<int>[zones.Count];
        for (int z = 0; z < zones.Count; z++)
            buckets[z] = new List<int>();

        int catchAll = -1;
        for (int z = 0; z < zones.Count; z++)
        {
            bool noCats = zones[z].Categories == null || zones[z].Categories!.Count == 0;
            bool noTypes = zones[z].Types == null || zones[z].Types!.Count == 0;
            if (noCats && noTypes)
            {
                catchAll = z;
                break;
            }
        }

        for (int i = 0; i < categories.Count; i++)
        {
            string? type = types != null && i < types.Count ? types[i] : null;
            int zoneIdx = MatchZone(zones, categories[i], type);
            if (zoneIdx < 0)
                zoneIdx = catchAll;
            if (zoneIdx < 0)
            {
                ModEntry.LogDebug($"Zoned board '{board.Name}': pin (category '{categories[i]}', type '{type ?? "?"}') matched no zone and there's no catch-all zone; dropping note.");
                continue;
            }
            buckets[zoneIdx].Add(i);
        }

        for (int z = 0; z < zones.Count; z++)
        {
            if (buckets[z].Count == 0)
                continue;
            Rectangle area = ZoneArea(zones[z], fullArea);
            LayoutZoneInArea(ParseZoneStyle(zones[z].Style), buckets[z], area, daySeed, rng, result);
        }
    }

    // First zone that claims the pin wins. A zone claims it when the pin's category is in the
    // zone's Categories OR (for type routing) the pin's type is in the zone's Types. A zone
    // with neither list is the catch-all and is handled separately.
    private static int MatchZone(List<BoardZone> zones, string category, string? type)
    {
        for (int z = 0; z < zones.Count; z++)
        {
            var cats = zones[z].Categories;
            if (cats != null && cats.Count > 0)
            {
                for (int c = 0; c < cats.Count; c++)
                    if (string.Equals(cats[c], category, StringComparison.OrdinalIgnoreCase))
                        return z;
            }

            var zoneTypes = zones[z].Types;
            if (type != null && zoneTypes != null && zoneTypes.Count > 0)
            {
                for (int tIdx = 0; tIdx < zoneTypes.Count; tIdx++)
                    if (string.Equals(zoneTypes[tIdx], type, StringComparison.OrdinalIgnoreCase))
                        return z;
            }
        }
        return -1;
    }

    // Maps a zone's percentage rect onto the cork area, clamped so it never spills past it.
    private static Rectangle ZoneArea(BoardZone zone, Rectangle full)
    {
        int x = full.X + (int)Math.Round(zone.RectX / 100f * full.Width);
        int y = full.Y + (int)Math.Round(zone.RectY / 100f * full.Height);
        x = Math.Clamp(x, full.X, full.Right - 1);
        y = Math.Clamp(y, full.Y, full.Bottom - 1);
        int w = (int)Math.Round(zone.RectW / 100f * full.Width);
        int h = (int)Math.Round(zone.RectH / 100f * full.Height);
        w = Math.Max(1, Math.Min(w, full.Right - x));
        h = Math.Max(1, Math.Min(h, full.Bottom - y));
        return new Rectangle(x, y, w, h);
    }

    private static void LayoutZoneInArea(
        ZoneStyle style, List<int> slotIndices, Rectangle area,
        int daySeed, Random rng, List<NotePlacement?> result)
    {
        if (slotIndices.Count == 0)
            return;
        if (style == ZoneStyle.Scatter)
            LayoutScatter(slotIndices, area, rng, result);
        else
            LayoutGrid(slotIndices, area, daySeed, rng, result);
    }

    private static void LayoutGrid(
        List<int> slotIndices, Rectangle area, int daySeed, Random rng, List<NotePlacement?> result)
    {
        var bounds = ComputeGridInArea(area, slotIndices.Count, rng);
        for (int k = 0; k < slotIndices.Count; k++)
        {
            int orig = slotIndices[k];
            result[orig] = new NotePlacement { PaperBounds = bounds[k], Tilt = TiltFor(daySeed, orig) };
        }
    }

    private static void LayoutScatter(
        List<int> slotIndices, Rectangle area, Random rng, List<NotePlacement?> result)
    {
        int n = slotIndices.Count;
        int scaleSprite = (int)(PadSpriteSize * ChooseScale(n));
        var (_, _, gridFit) = ChooseGridInArea(area, n);
        int noteSprite = Math.Clamp(Math.Min(scaleSprite, gridFit), PadSpriteSize, MaxNoteSize);
        float scale = noteSprite / (float)PadSpriteSize;
        int paperW = (int)Math.Round(PadPaperWidth * scale);
        int paperH = (int)Math.Round(PadPaperHeight * scale);

        // Grid positions for the same notes, used when a note can't find a non-overlapping
        // scatter spot so it still lands inside the area rather than vanishing.
        var fallback = ComputeGridInArea(area, n, rng);
        var placed = new List<Rectangle>(n);
        for (int k = 0; k < n; k++)
        {
            Rectangle b = ScatterInArea(area, paperW, paperH, placed, rng) ?? fallback[k];
            placed.Add(b);
            result[slotIndices[k]] = new NotePlacement { PaperBounds = b, Tilt = 0f };
        }
    }

    // Picks the column/row split that makes the notes as large as possible while still
    // fitting `count` of them in `area` at the average spacing. Returns the full sprite size;
    // paper dims are derived from it.
    private static (int cols, int rows, int noteSize) ChooseGridInArea(Rectangle area, int count)
    {
        if (count <= 0)
            return (1, 1, MaxNoteSize);

        float maxGap = NoteSpacing + GapJitter;
        int bestCols = 1, bestRows = count, bestSize = 0;
        for (int cols = 1; cols <= count; cols++)
        {
            int rows = (count + cols - 1) / cols;
            float scaleW = (area.Width - (cols - 1) * maxGap) / (cols * PadPaperWidth);
            float scaleH = (area.Height - (rows - 1) * maxGap) / (rows * PadPaperHeight);
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

    // Computes the clickable paper bounds for every note in one pass within `area`. Gaps are
    // jittered around the average and rows + the whole block are centered, so the board never
    // looks lopsided. Bounds are the visible paper rect. rng makes the jitter deterministic.
    private static List<Rectangle> ComputeGridInArea(Rectangle area, int count, Random rng)
    {
        var result = new List<Rectangle>(Math.Max(0, count));
        if (count <= 0)
            return result;

        var (cols, rows, noteSize) = ChooseGridInArea(area, count);
        float scale = noteSize / (float)PadSpriteSize;
        float paperW = PadPaperWidth * scale;
        float paperH = PadPaperHeight * scale;
        int spacing = NoteSpacing;

        float totalH = rows * paperH;
        var rowGaps = new float[Math.Max(0, rows - 1)];
        for (int r = 0; r < rowGaps.Length; r++)
        {
            rowGaps[r] = Jitter(spacing, rng);
            totalH += rowGaps[r];
        }
        float cy = area.Y + (area.Height - totalH) / 2f;

        for (int row = 0; row < rows; row++)
        {
            int itemsInRow = Math.Min(cols, count - row * cols);

            float rowW = itemsInRow * paperW;
            var colGaps = new float[Math.Max(0, itemsInRow - 1)];
            for (int c = 0; c < colGaps.Length; c++)
            {
                colGaps[c] = Jitter(spacing, rng);
                rowW += colGaps[c];
            }
            float px = area.X + (area.Width - rowW) / 2f;

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

    // Returns null after 4000 tries with no non-overlapping spot inside `area`; caller falls
    // back to the grid position.
    private static Rectangle? ScatterInArea(
        Rectangle area, int w, int h, List<Rectangle> placed, Random rng)
    {
        if (w >= area.Width || h >= area.Height)
            return null;

        const float xOverlap = 0.7f;
        const float yOverlap = 0.7f;
        for (int tries = 0; tries < 4000; tries++)
        {
            var rect = new Rectangle(
                area.X + rng.Next(0, area.Width - w),
                area.Y + rng.Next(0, area.Height - h),
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
