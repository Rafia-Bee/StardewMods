using System;
using System.Collections.Generic;
using StardewValley.Quests;

namespace MoreQuestsFramework.Triggers;

public readonly record struct MailQuestEntry(Quest Quest, string OwnerUniqueId, string DefinitionId);

// Rebuilt from FrameworkState.PendingMailDeliveries at SaveLoaded.
public sealed class MailQuestRegistry
{
    private readonly Dictionary<string, MailQuestEntry> _byId = new(StringComparer.Ordinal);

    public bool TryGet(string id, out MailQuestEntry entry) => _byId.TryGetValue(id, out entry);

    public void Register(string id, Quest quest, string ownerUniqueId, string definitionId)
        => _byId[id] = new MailQuestEntry(quest, ownerUniqueId, definitionId);

    public void Remove(string id) => _byId.Remove(id);

    public void Clear() => _byId.Clear();

    public IReadOnlyDictionary<string, MailQuestEntry> Snapshot() => _byId;
}
