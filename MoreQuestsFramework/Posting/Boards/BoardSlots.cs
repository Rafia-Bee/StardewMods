using System;
using System.Collections.Generic;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Pipeline;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Posting.Boards;

// A board pin is either a quest (opens the accept popup) or a notice (opens a read-only text
// popup). The two share everything below the click: layout, the note renderer, hover, gamepad
// snap. Only the click action and the hover text differ.
internal enum SlotKind { Quest, Notice }

// Mirrors BillboardSlots for the help-wanted board so BoardLayout can render either.
internal static class CustomBoardSlots
{
    private static readonly Dictionary<string, List<Slot>> _byBoardKey
        = new(StringComparer.OrdinalIgnoreCase);

    public sealed class Slot
    {
        public string SyncId { get; }
        public SlotKind Kind { get; }
        public Quest? Quest { get; }
        public QuestPosting? Posting { get; }
        public NoticeInstance? Notice { get; }
        public string BoardKey { get; }
        public bool Accepted { get; set; }

        public Slot(Quest quest, QuestPosting posting, string boardKey)
        {
            SyncId = Guid.NewGuid().ToString("N");
            Kind = SlotKind.Quest;
            Quest = quest;
            Posting = posting;
            BoardKey = boardKey;
        }

        public Slot(NoticeInstance notice, string boardKey)
        {
            SyncId = Guid.NewGuid().ToString("N");
            Kind = SlotKind.Notice;
            Notice = notice;
            BoardKey = boardKey;
        }

        // Styling fields the shared note renderer reads, resolved from whichever payload this
        // slot carries so the draw code never has to branch on Kind.
        public string Category => Kind == SlotKind.Quest
            ? (Posting?.Category ?? QuestCategory.Social)
            : (Notice?.Category ?? QuestCategory.Social);

        public string IconValue => Kind == SlotKind.Quest
            ? (Posting?.Icon ?? "")
            : (Notice?.Icon ?? "");

        public string GiverName => Kind == SlotKind.Quest
            ? (Posting?.QuestGiver ?? "")
            : (Notice?.Giver ?? "");

        // Note-size multiplier. Quests stay auto-sized (1.0). A notice uses its own Scale, else
        // its category's NoteScale, else 1.0. The layout clamps this to the board.
        public float ScaleValue
        {
            get
            {
                if (Kind != SlotKind.Notice)
                    return 1f;
                if (Notice is { Scale: > 0 })
                    return Notice.Scale;
                return ModEntry.Categories.NoteScaleFor(Notice?.Category);
            }
        }
    }

    public static Slot? Selected { get; set; }

    public static IReadOnlyList<Slot> SlotsFor(BoardDefinition board)
    {
        string key = KeyOf(board);
        return _byBoardKey.TryGetValue(key, out var list)
            ? list
            : (IReadOnlyList<Slot>)Array.Empty<Slot>();
    }

    // Stores already-built Slots under a board key. A Slot mirrored across a home board
    // and one or more catch-all boards is the SAME object in each list, so accepting it
    // on any board clears it everywhere (see AcceptSelected).
    public static void SetSlotsByKey(string key, IReadOnlyList<Slot> slots)
    {
        if (!_byBoardKey.TryGetValue(key, out var list))
            _byBoardKey[key] = list = new List<Slot>();
        list.Clear();
        list.AddRange(slots);
        ModEntry.LogDebug($"CustomBoardSlots[{key}] populated with {list.Count} pin(s).");
    }

    public static void Clear(BoardDefinition board)
    {
        string key = KeyOf(board);
        if (_byBoardKey.TryGetValue(key, out var list))
            list.Clear();
    }

    public static void ClearAll()
    {
        _byBoardKey.Clear();
        Selected = null;
    }

    // Walks every populated board, pairing each slot with the board key it sits under.
    // A mirrored slot is yielded once per board it appears on. Used by the public API to
    // surface a snapshot without exposing the internal dictionary.
    public static IEnumerable<(string BoardKey, Slot Slot)> AllSlots()
    {
        foreach (var (key, list) in _byBoardKey)
            foreach (var slot in list)
                yield return (key, slot);
    }

    public static Quest? AcceptSelected()
    {
        if (Selected == null || Selected.Kind != SlotKind.Quest)
            return null;
        Selected.Accepted = true;
        var quest = Selected.Quest;
        // Start the per-definition cooldown only now that the player committed to the
        // quest. Posting alone never trips it; an ignored board slot is free to re-roll.
        ModEntry.Instance?.Anti?.RecordDefinitionAccepted(Selected.Posting!.DefinitionId);
        // Remove by SyncId across every board: a posting mirrored onto a catch-all board
        // is one logical quest, so accepting it on either board clears both.
        string syncId = Selected.SyncId;
        foreach (var list in _byBoardKey.Values)
            list.RemoveAll(s => s.SyncId == syncId);
        Selected = null;
        return quest;
    }

    private static string KeyOf(BoardDefinition board) =>
        (board.OwnerUniqueId ?? "") + "/" + (board.Name ?? "");
}
