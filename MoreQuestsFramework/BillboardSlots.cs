using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework;

// Backs the custom Billboard menu and the Harmony patch that redirects
// Game1.questOfTheDay getters to the currently-selected quest.
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

    public static Quest? AcceptSelected(Slot? explicitSlot = null)
    {
        var target = explicitSlot ?? Selected;
        if (target == null)
            return null;
        target.Accepted = true;
        Quest q = target.Quest;
        // Start the per-definition cooldown only now that the player committed to the
        // quest. Posting alone never trips it; an ignored board slot is free to re-roll.
        ModEntry.Instance?.Anti?.RecordDefinitionAccepted(target.Posting.DefinitionId);
        _slots.Remove(target);
        if (Selected == target)
            Selected = null;
        return q;
    }

    public static Slot? FindBySyncId(string id) =>
        _slots.FirstOrDefault(s => s.SyncId == id);
}
