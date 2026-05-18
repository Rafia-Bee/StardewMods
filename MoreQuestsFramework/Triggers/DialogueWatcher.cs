using System;
using System.Collections.Generic;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.State;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Triggers;

internal sealed class DialogueWatcher
{
    private readonly QuestRegistry _registry;
    private readonly QuestContext _ctx;
    private readonly FrameworkState _state;
    private readonly MoreQuestsApi _api;
    private readonly IMonitor _monitor;
    private readonly Func<QuestPosting, Quest?> _buildAndApply;
    private NPC? _lastSpeaker;

    public DialogueWatcher(
        QuestRegistry registry,
        QuestContext ctx,
        FrameworkState state,
        MoreQuestsApi api,
        IMonitor monitor,
        Func<QuestPosting, Quest?> buildAndApply)
    {
        _registry = registry;
        _ctx = ctx;
        _state = state;
        _api = api;
        _monitor = monitor;
        _buildAndApply = buildAndApply;
    }

    public IReadOnlyDictionary<string, string> Pending => _state.PendingDialogueQuests;

    public void Enqueue(string defId, string npcName)
    {
        if (_state.PendingDialogueQuests.ContainsKey(defId))
            return;
        _state.PendingDialogueQuests[defId] = npcName;
        ModEntry.LogDebug($"DialogueWatcher: queued '{defId}' for next chat with {npcName}.");
    }

    public void Reset()
    {
        _lastSpeaker = null;
    }

    public void Tick()
    {
        if (!Context.IsWorldReady || _state.PendingDialogueQuests.Count == 0)
            return;
        var speaker = Game1.currentSpeaker;
        if (speaker == null)
        {
            _lastSpeaker = null;
            return;
        }
        if (_lastSpeaker == speaker)
            return;
        _lastSpeaker = speaker;

        string speakerName = speaker.Name;
        var ready = new List<string>();
        foreach (var (defId, target) in _state.PendingDialogueQuests)
        {
            if (string.Equals(target, speakerName, StringComparison.OrdinalIgnoreCase))
                ready.Add(defId);
        }
        foreach (var defId in ready)
        {
            _state.PendingDialogueQuests.Remove(defId);
            FireQueued(defId);
        }

        // If a Build() side-effect enqueued more pending entries for this same speaker,
        // let the next tick pick them up instead of waiting for the speaker to change.
        foreach (var target in _state.PendingDialogueQuests.Values)
        {
            if (string.Equals(target, speakerName, StringComparison.OrdinalIgnoreCase))
            {
                _lastSpeaker = null;
                break;
            }
        }
    }

    private void FireQueued(string defId)
    {
        if (!_registry.TryGet(defId, out var def) || def == null)
        {
            _monitor.Log($"DialogueWatcher: queued '{defId}' is no longer registered; dropping.", LogLevel.Warn);
            return;
        }
        var posting = def.Build(_ctx);
        if (posting == null)
        {
            ModEntry.LogDebug($"DialogueWatcher: '{defId}' Build() returned null.");
            return;
        }
        if (string.IsNullOrEmpty(posting.OwnerUniqueId))
            posting.OwnerUniqueId = def.OwnerUniqueId;
        var quest = _buildAndApply(posting);
        if (quest == null)
        {
            ModEntry.LogDebug($"DialogueWatcher: '{defId}' produced no Quest; dropping.");
            return;
        }
        _api.TrackPosted(quest, posting.OwnerUniqueId, posting.DefinitionId);
        Game1.player.questLog.Add(quest);
        _state.LastFiredDay[defId] = Game1.Date.TotalDays;
        _monitor.Log($"DialogueWatcher: pushed '{defId}' into journal via dialogue with {Game1.currentSpeaker?.Name}.", LogLevel.Info);
    }
}
