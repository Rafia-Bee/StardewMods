# Quest Journal API

Quest Journal lets your mod add its own entries (tasks, reminders, to-dos, anything) to the journal. They show up in the list like quests, the player can pin them to the on-screen box, and you can give each one a "mark done" or "remove" button that calls back into your mod. You can also group your entries into their own tab.

You don't need to reference Quest Journal's DLL. Copy the two interfaces below into your mod and get the API through SMAPI. Everything is a no-op when Quest Journal isn't installed, so a single null check is all you need.

## 1. Copy these interfaces into your mod

Only plain .NET types cross the boundary, so this is all you need. Put it in any namespace you like.

```csharp
using System;
using System.Collections.Generic;

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

public interface IJournalEntry
{
    // Your mod's UniqueID. Namespaces the Key so two mods can't clash.
    string OwnerId { get; }

    // A stable id for this entry, unique within your mod. Used for updates, removal, and pinning.
    string Key { get; }

    string Title { get; }

    // Optional. Shown in the detail header and pinned HUD instead of Title. Leave empty to just use Title.
    // Handy when Title is a specific label for the list rows but you want a short fixed header up top.
    string BannerTitle { get; }

    string Description { get; }

    // A single objective line. Shown when there are no Steps.
    string Objective { get; }

    // Multiple step lines, for a multi-part task. May be empty.
    IReadOnlyList<string> Steps { get; }

    // Progress bar numerator. 0 hides the bar.
    int Progress { get; }

    // Progress bar denominator.
    int MaxProgress { get; }

    // Shown as the source label and used by the journal's "by source" tab filter.
    string Source { get; }

    // Used by the "by category" tab filter.
    string Category { get; }

    // Days left until due. Drives the days-left line and the deadline filter. Null means no deadline.
    int? DeadlineDays { get; }

    bool Completed { get; }

    // Where the entry lands: empty or "Active" puts it in the Active tab; any other value names a
    // tab that groups your entries together.
    string Placement { get; }

    // Called when the player clicks the done button. Null hides that button.
    Action? OnComplete { get; }

    // Called when the player clicks the remove button. Null hides that button.
    Action? OnCancel { get; }
}
```

## 2. A small class for your entries

You can implement `IJournalEntry` however you want. The easiest way is a plain class with settable properties:

```csharp
public sealed class JournalEntry : IJournalEntry
{
    public string OwnerId { get; set; } = "";
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string BannerTitle { get; set; } = "";
    public string Description { get; set; } = "";
    public string Objective { get; set; } = "";
    public IReadOnlyList<string> Steps { get; set; } = new List<string>();
    public int Progress { get; set; }
    public int MaxProgress { get; set; }
    public string Source { get; set; } = "";
    public string Category { get; set; } = "";
    public int? DeadlineDays { get; set; }
    public bool Completed { get; set; }
    public string Placement { get; set; } = "";
    public Action? OnComplete { get; set; }
    public Action? OnCancel { get; set; }
}
```

## 3. Get the API and add an entry

Grab the API once Quest Journal has loaded (after `GameLaunched` is fine), and re-add your entries every time a save loads. Quest Journal does not save your entries for you, so you own that.

```csharp
private IQuestJournalApi? _journal;

private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
{
    _journal = Helper.ModRegistry.GetApi<IQuestJournalApi>("RafiaBee.QuestJournal");
    // _journal is null if Quest Journal isn't installed. That's fine, just skip everything below.
}

private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
{
    if (_journal == null) return;

    string owner = ModManifest.UniqueID;
    string key = "strawberries";

    _journal.AddOrUpdateEntry(new JournalEntry
    {
        OwnerId = owner,
        Key = key,
        Title = "Find 5 strawberries",
        Objective = "Look in the spring forage",
        Source = "My Mod",
        DeadlineDays = 3,
        OnComplete = () => _journal.RemoveEntry(owner, key)
    });
}
```

When your task changes (progress, title, done), call `AddOrUpdateEntry` again with the same `OwnerId` and `Key` and the journal updates in place. To take it away, call `RemoveEntry`, or `ClearEntries(owner)` to drop all of yours at once.

## Notes

- **Re-add on save load.** Entries live only for the session. Register them on `SaveLoaded` and keep them current. Quest Journal never writes them to disk.
- **Keys must be stable.** The same task should keep the same `Key` across the session, since pins are remembered by `OwnerId` + `Key`.
- **Buttons are optional.** Set `OnComplete` and/or `OnCancel` to show a "mark done" and/or "remove" button. Leave them null and the entry just displays. Most mods make the callback remove or update the entry.
- **Your own tab.** Set `Placement` to a name (like "My Mod") and your entries get their own tab in the journal. Leave it empty (or "Active") and they sit in the Active tab with the player's quests. Either way, they also show in any matching player-made custom tab.
- **Pinning.** The player can pin your entries from the journal. You can also pin or check pins yourself with `SetPinned` and `IsPinned`.
- **Call on the game thread.** Treat these like any other SMAPI call and make them from event handlers, not a background thread.
