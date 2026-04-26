using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuests.Framework;

/// Process-wide state for the day's daily-board postings. Holds the unaccepted Quest objects
/// so the custom Billboard menu can render tiles for each, and so the Harmony patch redirecting
/// `Game1.questOfTheDay` getters to our currently-selected quest can find the right one.
internal static class BillboardSlots
{
    private static readonly List<Slot> _slots = new();

    public static IReadOnlyList<Slot> Slots => _slots;
    public static Slot? Selected { get; set; }

    public sealed class Slot
    {
        public string SyncId { get; }
        public Quest Quest { get; }
        public QuestPosting Posting { get; }
        public bool Accepted { get; set; }

        public Slot(Quest quest, QuestPosting posting)
        {
            SyncId = Guid.NewGuid().ToString("N");
            Quest = quest;
            Posting = posting;
        }
    }

    public static void Replace(IEnumerable<(Quest q, QuestPosting p)> entries, IMonitor? monitor = null)
    {
        _slots.Clear();
        Selected = null;
        foreach (var (q, p) in entries)
            _slots.Add(new Slot(q, p));
        monitor?.Log($"BillboardSlots populated with {_slots.Count} quest(s).", LogLevel.Trace);
    }

    public static void Clear()
    {
        _slots.Clear();
        Selected = null;
    }

    /// Marks the currently selected slot accepted and removes it from the unaccepted pool.
    public static Quest? AcceptSelected()
    {
        if (Selected == null)
            return null;
        Selected.Accepted = true;
        Quest q = Selected.Quest;
        _slots.Remove(Selected);
        Selected = null;
        return q;
    }

    public static Slot? FindBySyncId(string id) =>
        _slots.FirstOrDefault(s => s.SyncId == id);
}
