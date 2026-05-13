using System;
using System.Collections.Generic;
using StardewValley.Quests;

namespace MoreQuestsFramework.Triggers;

/// Bookkeeping pair stored alongside a prepared mail quest so the framework can
/// fire `QuestAccepted` / `QuestCompleted` events with the correct owner +
/// definition attribution once the player opens the letter.
public readonly record struct MailQuestEntry(Quest Quest, string OwnerUniqueId, string DefinitionId);

/// In-memory map of mail key → prepared `Quest` instance, consulted by the Harmony
/// prefix on `Quest.getQuestFromId` to plug our subclass into the vanilla
/// `Farmer.addQuest` path. Vanilla's `getQuestFromId` is a hard-coded switch on
/// quest type strings (`ItemDelivery`, `Monster`, ...) with no extension hook,
/// the only way to return our framework subclasses (with their `serializedRewards`
/// NetFields) through the standard `%item quest <id> 1 %%` mail token is to
/// intercept the lookup here.
///
/// Persistence: rebuilt from `FrameworkState.PendingMailDeliveries` at SaveLoaded
/// so a mail letter sitting unread across a save still resolves correctly.
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
