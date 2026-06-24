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
}
