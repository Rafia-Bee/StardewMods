using System;
using System.Collections.Generic;
using MoreQuestsFramework.Api;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Posting.Boards;

// Mirrors BillboardSlots for the help-wanted board so BoardLayout can render either.
internal static class CustomBoardSlots
{
    private static readonly Dictionary<string, List<Slot>> _byBoardKey
        = new(StringComparer.OrdinalIgnoreCase);

    public sealed class Slot
    {
        public string SyncId { get; }
        public Quest Quest { get; }
        public QuestPosting Posting { get; }
        public string BoardKey { get; }
        public bool Accepted { get; set; }

        public Slot(Quest quest, QuestPosting posting, string boardKey)
        {
            SyncId = Guid.NewGuid().ToString("N");
            Quest = quest;
            Posting = posting;
            BoardKey = boardKey;
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
        ModEntry.LogDebug($"CustomBoardSlots[{key}] populated with {list.Count} quest(s).");
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
        if (Selected == null)
            return null;
        Selected.Accepted = true;
        var quest = Selected.Quest;
        // Start the per-definition cooldown only now that the player committed to the
        // quest. Posting alone never trips it; an ignored board slot is free to re-roll.
        ModEntry.Instance?.Anti?.RecordDefinitionAccepted(Selected.Posting.DefinitionId);
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
