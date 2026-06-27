namespace MoreQuestsFramework.Content;

// One entry in the Categories asset (Mods/RafiaBee.MoreQuestsFramework/Categories).
// Pad and pin are two independent colors; each accepts #RRGGBB, #RRGGBBAA, or R,G,B
// and falls back to Social's color when missing or unparseable. Skill is the skill a
// quest in this category scales its difficulty against (see Difficulty.GetSkillLevel).
public sealed class CategoryDefinition
{
    public string? DisplayName { get; set; }
    public string? PadColor { get; set; }
    public string? PinColor { get; set; }
    public string? Skill { get; set; }

    // Optional per-category note textures. When set, every note in this category draws this
    // pad/pin sprite instead of the board's or the framework default. Asset names, loaded as
    // game content (so Content Patcher can serve them). Missing falls back to the board's
    // Pad/Pin texture, then the framework default. Tinted by PadColor/PinColor like the
    // default sprite, so paint them light if you want the tint to show.
    public string? PadTexture { get; set; }
    public string? PinTexture { get; set; }

    // The little picture in the corner of the note. One of:
    //   - absent / "" / "Portrait": the quest giver's NPC portrait (the default look).
    //   - "None": no picture (anonymous note).
    //   - anything else: an asset name to draw instead (e.g. a "!" icon).
    public string? Icon { get; set; }

    // Source rectangle [x, y, w, h] inside the Icon texture. Absent uses the whole texture.
    public int[]? IconSource { get; set; }

    // Icon size as a fraction of the note width (0-1). Default 0.28 matches the portrait.
    public float? IconScale { get; set; }

    // Where the icon sits on the note: BottomLeft (default), BottomRight, TopLeft, TopRight,
    // or Center. Ignored when IconX/IconY are both set.
    public string? IconAnchor { get; set; }

    // Fine icon placement as fractions of the note (0-1, from the top-left). When both are
    // set they override IconAnchor; the icon is centered on this point.
    public float? IconX { get; set; }
    public float? IconY { get; set; }
}
