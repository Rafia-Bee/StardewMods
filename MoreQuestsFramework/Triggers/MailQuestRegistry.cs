using System;
using System.Collections.Generic;
using StardewValley.Quests;

namespace MoreQuestsFramework.Triggers;

public readonly record struct MailQuestEntry(Quest Quest, string OwnerUniqueId, string DefinitionId);

// Rebuilt from FrameworkState.PendingMailDeliveries at SaveLoaded.
internal sealed class MailQuestRegistry
{
    private readonly Dictionary<string, MailQuestEntry> _byId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _handedOff = new(StringComparer.Ordinal);

    public bool TryGet(string id, out MailQuestEntry entry) => _byId.TryGetValue(id, out entry);

    public bool IsHandedOff(string id) => _handedOff.Contains(id);

    public void MarkHandedOff(string id) => _handedOff.Add(id);

    public void Register(string id, Quest quest, string ownerUniqueId, string definitionId)
        => _byId[id] = new MailQuestEntry(quest, ownerUniqueId, definitionId);

    public void Remove(string id)
    {
        _byId.Remove(id);
        _handedOff.Remove(id);
    }

    public void Clear()
    {
        _byId.Clear();
        _handedOff.Clear();
    }

    public IReadOnlyDictionary<string, MailQuestEntry> Snapshot() => _byId;
}
