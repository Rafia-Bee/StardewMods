using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace MoreQuestsFramework.Posting.Boards;

// Draws one cork-board note (pad + corner icon + pin) and resolves the per-note pad/pin
// textures and icon. Shared by the daily board (MoreQuestsBillboard) and the custom board
// (CustomBoardMenu) so both look the same and both pick up per-category textures/icons.
// The default path (default textures, giver portrait, bottom-left at 0.28 scale) reproduces
// the old daily-board look exactly, so nothing changes unless an author edits the Categories
// asset or a quest's Icon.
internal static class BoardNoteRenderer
{
    public sealed class NoteIcon
    {
        public Texture2D Texture { get; init; } = null!;
        public Rectangle Source { get; init; }
        public float Scale { get; init; } = 0.28f;
        public string Anchor { get; init; } = "BottomLeft";
        public float? X { get; init; }
        public float? Y { get; init; }
    }

    // Pad texture for a note: the category override if set, else `fallback` (which the caller
    // has already resolved to the board's own pad texture or the framework default). Loaded
    // once per asset name into `cache`.
    public static Texture2D ResolvePad(string? category, Texture2D fallback, Dictionary<string, Texture2D> cache)
        => Resolve(ModEntry.Categories.PadTextureFor(category), fallback, cache);

    public static Texture2D ResolvePin(string? category, Texture2D fallback, Dictionary<string, Texture2D> cache)
        => Resolve(ModEntry.Categories.PinTextureFor(category), fallback, cache);

    private static Texture2D Resolve(string? assetName, Texture2D fallback, Dictionary<string, Texture2D> cache)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return fallback;
        if (cache.TryGetValue(assetName, out var cached))
            return cached;
        Texture2D tex = fallback;
        try
        {
            tex = Game1.content.Load<Texture2D>(assetName);
        }
        catch
        {
            // Bad path falls back to the default sprite, same as the menu's own loader.
        }
        cache[assetName] = tex;
        return tex;
    }

    // Resolves what to draw in the note corner. Per-quest Icon beats the category Icon; both
    // accept "Portrait" (giver portrait, the default), "None" (nothing), or an asset name.
    // Returns null when there's nothing to draw.
    public static NoteIcon? ResolveIcon(string? questIcon, string? category, string? giverName, Dictionary<string, Texture2D> cache)
    {
        var spec = ModEntry.Categories.IconFor(category);
        bool fromOverride = !string.IsNullOrWhiteSpace(questIcon);
        string? value = fromOverride ? questIcon!.Trim() : spec.Value;

        if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
            return null;

        Texture2D? texture;
        Rectangle source;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "Portrait", StringComparison.OrdinalIgnoreCase))
        {
            texture = BoardLayout.TryGetPortrait(giverName ?? "");
            if (texture == null)
                return null;
            source = new Rectangle(0, 0, 64, 64);
        }
        else
        {
            texture = TryLoad(value, cache);
            if (texture == null)
                return null;
            source = !fromOverride && spec.Source.HasValue
                ? spec.Source.Value
                : new Rectangle(0, 0, texture.Width, texture.Height);
        }

        return new NoteIcon
        {
            Texture = texture,
            Source = source,
            Scale = spec.Scale,
            Anchor = spec.Anchor,
            X = spec.X,
            Y = spec.Y,
        };
    }

    private static Texture2D? TryLoad(string assetName, Dictionary<string, Texture2D> cache)
    {
        if (cache.TryGetValue(assetName, out var cached))
            return cached;
        try
        {
            var tex = Game1.content.Load<Texture2D>(assetName);
            cache[assetName] = tex;
            return tex;
        }
        catch
        {
            cache[assetName] = null!;
            return null;
        }
    }

    // Draws the note rotated about its center by `tilt` and scaled by `sizeBoost`. Bounds are
    // the visible paper rect; the full sprite is scaled up from the paper width to put the
    // transparent margins back, so the paper lines up with the clickable bounds.
    public static void DrawNote(
        SpriteBatch b, Texture2D pad, Texture2D pin, Color padColor, Color pinColor,
        NoteIcon? icon, Rectangle paperBounds, float tilt, float sizeBoost)
    {
        float side = paperBounds.Width * (BoardLayout.PadSpriteSize / (float)BoardLayout.PadPaperWidth) * sizeBoost;
        var center = new Vector2(paperBounds.Center.X, paperBounds.Center.Y);

        DrawSheet(b, pad, center, padColor, tilt, side, 0.86f);

        if (icon != null)
        {
            float iconSide = side * Math.Clamp(icon.Scale, 0.01f, 1.5f);
            var offset = IconOffset(icon, side, iconSide);
            var pos = center + Rotate(offset, tilt);
            var origin = new Vector2(icon.Source.Width / 2f, icon.Source.Height / 2f);
            float iconScale = icon.Source.Width > 0 ? iconSide / icon.Source.Width : 0f;
            b.Draw(icon.Texture, pos, icon.Source, Color.White, tilt, origin, iconScale, SpriteEffects.None, 0.87f);
        }

        DrawSheet(b, pin, center, pinColor, tilt, side, 0.88f);
    }

    // Draws a square sheet centered at `center`, sized to `side` pixels regardless of the
    // texture's own pixel size, tinted and rotated.
    private static void DrawSheet(SpriteBatch b, Texture2D tex, Vector2 center, Color color, float tilt, float side, float depth)
    {
        var source = new Rectangle(0, 0, tex.Width, tex.Height);
        var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
        float scale = tex.Width > 0 ? side / tex.Width : 0f;
        b.Draw(tex, center, source, color, tilt, origin, scale, SpriteEffects.None, depth);
    }

    // The icon center as an offset from the note center, before tilt. IconX/IconY (fractions
    // of the note, from top-left) win when both are set; otherwise the anchor hugs a corner
    // inset by a small margin. The default (BottomLeft, 0.28 scale) reproduces the old
    // hardcoded portrait position exactly.
    private static Vector2 IconOffset(NoteIcon icon, float side, float iconSide)
    {
        float cx, cy;
        if (icon.X.HasValue && icon.Y.HasValue)
        {
            cx = icon.X.Value * side;
            cy = icon.Y.Value * side;
        }
        else
        {
            float margin = 0.08f * side;
            float half = iconSide / 2f;
            (cx, cy) = icon.Anchor?.ToLowerInvariant() switch
            {
                "bottomright" => (side - margin - half, side - margin - half),
                "topleft" => (margin + half, margin + half),
                "topright" => (side - margin - half, margin + half),
                "center" => (side / 2f, side / 2f),
                _ => (margin + half, side - margin - half),
            };
        }
        return new Vector2(cx - side / 2f, cy - side / 2f);
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = (float)Math.Cos(angle);
        float sin = (float)Math.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}
