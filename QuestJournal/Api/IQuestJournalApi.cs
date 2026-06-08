using System;
using System.Collections.Generic;

namespace QuestJournal.Api;

// Public API other mods get through Helper.ModRegistry.GetApi<IQuestJournalApi>("RafiaBee.QuestJournal").
// Lets a mod push its own to-do items / tasks into the journal, where they show up like quests and can be
// pinned to the HUD. Only plain .NET types cross the boundary so a consumer can copy this interface and bind to it
// without referencing the QuestJournal DLL. Every call is a no-op when QuestJournal isn't installed (GetApi
// returns null), so consumers just null-check the API once.
public interface IQuestJournalApi
{
    // Add a new entry or replace an existing one with the same OwnerId + Key.
    void AddOrUpdateEntry(IJournalEntry entry);

    // Remove a single entry by its owner and key.
    void RemoveEntry(string ownerId, string key);

    // Remove every entry registered by a given owner.
    void ClearEntries(string ownerId);

    // Whether the entry is pinned to the HUD.
    bool IsPinned(string ownerId, string key);

    // Pin or unpin the entry on the HUD.
    void SetPinned(string ownerId, string key, bool pinned);
}

// One entry the journal will show. The owning mod creates these and keeps them up to date; the journal only
// displays them and never saves them, so re-register them when a save loads and push updates as state changes.
public interface IJournalEntry
{
    // The registering mod's UniqueID. Namespaces the Key so two mods can't clash.
    string OwnerId { get; }

    // A stable id for this entry, unique within the owner. Used for updates, removal, and pinning.
    string Key { get; }

    string Title { get; }
    string Description { get; }

    // A single objective line. Shown when there are no Steps.
    string Objective { get; }

    // Multiple step lines, for a multi-part task. May be empty.
    IReadOnlyList<string> Steps { get; }

    // Progress bar numerator. 0 hides the bar.
    int Progress { get; }

    // Progress bar denominator.
    int MaxProgress { get; }

    // Shown as the source label and used by the journal's "by source" custom-tab filter.
    string Source { get; }

    // Used by the "by category" custom-tab filter.
    string Category { get; }

    // Days left until due. Drives the days-left line and the deadline filter. Null means no deadline.
    int? DeadlineDays { get; }

    bool Completed { get; }

    // Where the entry lands: empty or "Active" puts it in the Active tab; any other value names a
    // mod-provided tab that groups this owner's entries together.
    string Placement { get; }

    // Called when the player clicks Complete on this entry. Null hides the Complete button.
    Action? OnComplete { get; }

    // Called when the player clicks Cancel on this entry. Null hides the Cancel button.
    Action? OnCancel { get; }
}
