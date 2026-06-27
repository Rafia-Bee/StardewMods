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
