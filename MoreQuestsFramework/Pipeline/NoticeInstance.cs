using Microsoft.Xna.Framework;
using MoreQuestsFramework.Api;

namespace MoreQuestsFramework.Pipeline;

// A notice materialized for the day, ready to sit in a board slot. The notice analogue of
// QuestPosting, but far smaller: a notice is just text plus the styling fields the shared
// note renderer needs (category, optional giver portrait, optional icon override).
internal sealed class NoticeInstance
{
    public string DefinitionId { get; init; } = "";
    public string OwnerUniqueId { get; init; } = "";
    public string Category { get; init; } = QuestCategory.Social;
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public string Giver { get; init; } = "";
    public string Icon { get; init; } = "";

    // Per-notice size multiplier override, or 0 when the category's NoteScale should win.
    public float Scale { get; init; }

    // Photo notice: the picture shown in the popup (Body is its caption), or "" for plain text.
    public string Image { get; init; } = "";

    // Sub-rect of Image, or null for the whole texture.
    public Rectangle? ImageSource { get; init; }
}

// One built notice and the board it shows on. Mirrors CustomBoardDraw, but a Phase 1 notice
// only ever has a single home board (no catch-all mirroring yet), so it carries one board.
internal sealed class NoticeDraw
{
    public NoticeInstance Notice { get; }
    public BoardDefinition Board { get; }

    public NoticeDraw(NoticeInstance notice, BoardDefinition board)
    {
        Notice = notice;
        Board = board;
    }
}
