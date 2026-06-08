using QuestJournal.Hud;

namespace QuestJournal.Api;

// The object handed to other mods from ModEntry.GetApi(). Thin wrapper over the registry and the pin store.
// Must be public so SMAPI can expose it to other mods.
public sealed class QuestJournalApi : IQuestJournalApi
{
    private readonly ExternalEntryRegistry _registry;

    public QuestJournalApi(ExternalEntryRegistry registry) => _registry = registry;

    public void AddOrUpdateEntry(IJournalEntry entry) => _registry.AddOrUpdate(entry);

    public void RemoveEntry(string ownerId, string key) => _registry.Remove(ownerId, key);

    public void ClearEntries(string ownerId) => _registry.Clear(ownerId);

    public bool IsPinned(string ownerId, string key) => PinnedObjectivesStore.IsPinned(ownerId, key);

    public void SetPinned(string ownerId, string key, bool pinned)
        => PinnedObjectivesStore.SetExternalPinned(ownerId, key, pinned);
}
