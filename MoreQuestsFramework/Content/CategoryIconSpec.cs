using Microsoft.Xna.Framework;

namespace MoreQuestsFramework.Content;

// Parsed, draw-ready description of a category's corner icon. The renderer combines this with
// a per-quest icon override and the note's giver to decide what (if anything) to draw.
internal sealed class CategoryIconSpec
{
    // null / "" / "Portrait" = giver portrait, "None" = nothing, anything else = asset name.
    public string? Value { get; init; }

    // Source rect in the icon texture; null draws the whole texture.
    public Rectangle? Source { get; init; }

    // Fraction of the note width. 0.28 matches the old hardcoded portrait size.
    public float Scale { get; init; } = 0.28f;

    // BottomLeft / BottomRight / TopLeft / TopRight / Center.
    public string Anchor { get; init; } = "BottomLeft";

    // Fractional placement (0-1) overriding Anchor when both are set.
    public float? X { get; init; }
    public float? Y { get; init; }

    public static readonly CategoryIconSpec Default = new();
}
