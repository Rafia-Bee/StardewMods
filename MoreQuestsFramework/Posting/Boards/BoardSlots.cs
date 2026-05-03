using System;
using System.Collections.Generic;
using MoreQuestsFramework.Api;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Posting.Boards;

/// Process-wide per-board slot lists. Phase 8c populates these from the
/// `TriggerSource.CustomBoard` pool draw; Phase 8b leaves every list empty so the
/// rendered menus open with vanilla's "Nothing posted" fallback. Mirrors the help-wanted
/// `BillboardSlots` shape so the layout helper can render either kind interchangeably.
public static class CustomBoardSlots
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

    /// Currently-selected slot for the inner accept-quest popup (`Billboard(true)` reused
    /// from vanilla via the `BillboardPatches` `questOfTheDay` redirect). Null when no
    /// custom-board accept popup is up. Mirrors `BillboardSlots.Selected`.
    public static Slot? Selected { get; set; }

    public static IReadOnlyList<Slot> SlotsFor(BoardDefinition board)
    {
        string key = KeyOf(board);
        return _byBoardKey.TryGetValue(key, out var list)
            ? list
            : (IReadOnlyList<Slot>)Array.Empty<Slot>();
    }

    public static void Replace(BoardDefinition board, IEnumerable<(Quest q, QuestPosting p)> entries, IMonitor? monitor = null)
    {
        string key = KeyOf(board);
        if (!_byBoardKey.TryGetValue(key, out var list))
            _byBoardKey[key] = list = new List<Slot>();
        list.Clear();
        foreach (var (q, p) in entries)
            list.Add(new Slot(q, p, key));
        monitor?.Log($"CustomBoardSlots[{key}] populated with {list.Count} quest(s).", LogLevel.Trace);
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

    /// Marks the currently selected slot accepted and removes it from the unaccepted pool
    /// for whichever board it belongs to. Mirrors `BillboardSlots.AcceptSelected`.
    public static Quest? AcceptSelected()
    {
        if (Selected == null)
            return null;
        Selected.Accepted = true;
        var quest = Selected.Quest;
        if (_byBoardKey.TryGetValue(Selected.BoardKey, out var list))
            list.Remove(Selected);
        Selected = null;
        return quest;
    }

    private static string KeyOf(BoardDefinition board) =>
        (board.OwnerUniqueId ?? "") + "/" + (board.Name ?? "");
}
