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
    private readonly AntiRepetition _antiRepetition;
    private NPC? _lastSpeaker;

    // Per-day chat session count keyed by NPC name. A "session" is one rising edge of
    // Game1.currentSpeaker becoming that NPC; multi-page Dialogue boxes stay in one
    // session because currentSpeaker stays the same the whole time.
    private readonly Dictionary<string, int> _chatCountToday = new(StringComparer.OrdinalIgnoreCase);

    // defIds whose offer line has already been pushed onto the NPC today. Stops the
    // falling-edge hook from injecting the same line twice if the player chats with the
    // NPC again after the inject already happened.
    private readonly HashSet<string> _injectedToday = new(StringComparer.OrdinalIgnoreCase);

    // Cached postings so the offer line and the journal entry come from the same Build()
    // call (otherwise a generator that rolls a random reward would inject one text and
    // grant a different one). Cleared on day reset and on fire.
    private readonly Dictionary<string, QuestPosting> _postingCache = new(StringComparer.OrdinalIgnoreCase);

    // Offer line per defId, kept here (not in FrameworkState) so the save format doesn't
    // change. If the cache is empty after a reload, we re-Build the def to refill it.
    private readonly Dictionary<string, string> _offerTextByDef = new(StringComparer.OrdinalIgnoreCase);

    public DialogueWatcher(
        QuestRegistry registry,
        QuestContext ctx,
        FrameworkState state,
        MoreQuestsApi api,
        IMonitor monitor,
        Func<QuestPosting, Quest?> buildAndApply,
        AntiRepetition antiRepetition)
    {
        _registry = registry;
        _ctx = ctx;
        _state = state;
        _api = api;
        _monitor = monitor;
        _buildAndApply = buildAndApply;
        _antiRepetition = antiRepetition;
    }

    public IReadOnlyDictionary<string, string> Pending => _state.PendingDialogueQuests;

    public void Enqueue(string defId, string npcName, string dialogueText = "", QuestPosting? posting = null)
    {
        _state.PendingDialogueQuests[defId] = npcName;
        if (!string.IsNullOrEmpty(dialogueText))
            _offerTextByDef[defId] = dialogueText;
        if (posting != null)
            _postingCache[defId] = posting;
        ModEntry.LogDebug($"DialogueWatcher: queued '{defId}' for next chat with {npcName}.");
    }

    public void Reset()
    {
        _lastSpeaker = null;
    }

    // Drops pending offers whose Available conditions no longer hold. Runs at day-start
    // before the re-enqueue sweep. Without this, a quest queued inside its window (e.g.
    // Vincent's egg-hunt sabotage, only offered spring 10-12) would sit in saved state
    // and fire on the next chat with the NPC long after the window closed. Date and
    // season conditions only change at day boundaries, so a day-start check is enough.
    public void PruneUnavailable()
    {
        if (_state.PendingDialogueQuests.Count == 0)
            return;

        List<string>? stale = null;
        foreach (var (defId, _) in _state.PendingDialogueQuests)
        {
            if (!_registry.TryGet(defId, out var def) || def == null)
                continue;
            if (def.IsAvailable(_ctx))
                continue;
            (stale ??= new List<string>()).Add(defId);
        }

        if (stale == null)
            return;

        foreach (var defId in stale)
        {
            _state.PendingDialogueQuests.Remove(defId);
            _injectedToday.Remove(defId);
            _offerTextByDef.Remove(defId);
            _postingCache.Remove(defId);
            ModEntry.LogDebug($"DialogueWatcher: dropped pending '{defId}'; no longer available.");
        }
    }

    public void ResetDay()
    {
        _chatCountToday.Clear();
        _injectedToday.Clear();
        _postingCache.Clear();
        _offerTextByDef.Clear();
        _lastSpeaker = null;
    }

    public void Tick()
    {
        if (!Context.IsWorldReady)
            return;

        // Events and festivals push their own dialogue and would otherwise inflate the
        // chat counter. Skip the entire watcher while one is active.
        if (Game1.eventUp || Game1.CurrentEvent != null)
            return;

        if (_state.PendingDialogueQuests.Count == 0 && _lastSpeaker == null)
            return;

        var speaker = Game1.currentSpeaker;
        if (speaker == _lastSpeaker)
            return;

        var prev = _lastSpeaker;
        _lastSpeaker = speaker;

        if (prev != null && speaker == null)
            OnChatEnded(prev.Name);

        if (speaker != null && prev == null)
            OnChatStarted(speaker.Name);
    }

    private void OnChatStarted(string speakerName)
    {
        if (!_chatCountToday.TryGetValue(speakerName, out int count))
            count = 0;
        count++;
        _chatCountToday[speakerName] = count;

        var silentReady = new List<string>();
        var loudReady = new List<string>();
        foreach (var (defId, target) in _state.PendingDialogueQuests)
        {
            if (!string.Equals(target, speakerName, StringComparison.OrdinalIgnoreCase))
                continue;
            // Player already accepted this quest in a previous session and hasn't completed
            // it. Drop the pending entry so we don't try to add a duplicate.
            if (IsAlreadyInJournal(defId))
            {
                _state.PendingDialogueQuests.Remove(defId);
                _injectedToday.Remove(defId);
                _offerTextByDef.Remove(defId);
                _postingCache.Remove(defId);
                continue;
            }
            if (HasOfferLine(defId))
                loudReady.Add(defId);
            else
                silentReady.Add(defId);
        }

        foreach (var defId in silentReady)
        {
            _state.PendingDialogueQuests.Remove(defId);
            FireQueued(defId);
        }

        if (count >= 2)
        {
            int fired = 0;
            foreach (var defId in loudReady)
            {
                if (fired > 0 && !_ctx.Config.AllowDuplicateGiverPerDay)
                    break;
                _state.PendingDialogueQuests.Remove(defId);
                _injectedToday.Remove(defId);
                _offerTextByDef.Remove(defId);
                _postingCache.Remove(defId);
                FireQueued(defId);
                fired++;
            }
        }
    }

    private void OnChatEnded(string speakerName)
    {
        if (!_chatCountToday.TryGetValue(speakerName, out int count) || count != 1)
            return;

        var npc = Game1.getCharacterFromName(speakerName);
        if (npc == null)
            return;

        int injectedThisRound = 0;
        foreach (var (defId, target) in _state.PendingDialogueQuests)
        {
            if (!string.Equals(target, speakerName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (_injectedToday.Contains(defId))
                continue;
            if (IsAlreadyInJournal(defId))
                continue;

            string text = ResolveOfferText(defId);
            if (string.IsNullOrEmpty(text))
                continue;

            if (injectedThisRound > 0 && !_ctx.Config.AllowDuplicateGiverPerDay)
                break;

            npc.CurrentDialogue.Push(new Dialogue(npc, null, text));
            _injectedToday.Add(defId);
            injectedThisRound++;
            ModEntry.LogDebug($"DialogueWatcher: pushed offer line for '{defId}' onto {speakerName}.");
        }
    }

    private bool IsAlreadyInJournal(string defId)
    {
        if (Game1.player?.questLog == null)
            return false;
        var log = Game1.player.questLog;

        // Fast path. Only works in the same session the quest was posted in, because the
        // managed-quest weak table doesn't survive save/load.
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value)
                continue;
            if (string.Equals(_api.GetDefinitionId(q), defId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Survives save/load: every framework quest sets quest.questTitle from
        // posting.Title at post time, and that field round-trips through the save XML.
        string? title = ResolvePostingTitle(defId);
        if (string.IsNullOrEmpty(title))
            return false;
        for (int i = 0; i < log.Count; i++)
        {
            var q = log[i];
            if (q == null || q.completed.Value)
                continue;
            if (string.Equals(q.questTitle, title, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private string? ResolvePostingTitle(string defId)
    {
        if (_postingCache.TryGetValue(defId, out var cached))
            return cached?.Title;
        if (!_registry.TryGet(defId, out var def) || def == null)
            return null;
        var posting = def.Build(_ctx);
        if (posting == null)
            return null;
        if (string.IsNullOrEmpty(posting.OwnerUniqueId))
            posting.OwnerUniqueId = def.OwnerUniqueId;
        _postingCache[defId] = posting;
        return posting.Title;
    }

    private bool HasOfferLine(string defId)
    {
        return !string.IsNullOrEmpty(ResolveOfferText(defId));
    }

    private string ResolveOfferText(string defId)
    {
        if (_offerTextByDef.TryGetValue(defId, out var cached))
            return cached;

        if (!_registry.TryGet(defId, out var def) || def == null)
            return string.Empty;

        var posting = def.Build(_ctx);
        if (posting == null)
            return string.Empty;
        if (string.IsNullOrEmpty(posting.OwnerUniqueId))
            posting.OwnerUniqueId = def.OwnerUniqueId;
        _postingCache[defId] = posting;
        string text = posting.DialogueText ?? string.Empty;
        _offerTextByDef[defId] = text;
        return text;
    }

    private void FireQueued(string defId)
    {
        if (!_registry.TryGet(defId, out var def) || def == null)
        {
            _monitor.Log($"DialogueWatcher: queued '{defId}' is no longer registered; dropping.", LogLevel.Warn);
            return;
        }

        QuestPosting? posting = _postingCache.TryGetValue(defId, out var cached) ? cached : null;
        if (posting == null)
            posting = def.Build(_ctx);
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
        // GenerateNpcDialogueQueue checks AntiRepetition (the daily-board cooldown store),
        // not LastFiredDay. Without this the def re-enqueues tomorrow even when CooldownDays
        // is set.
        _antiRepetition.RecordDefinitionAccepted(defId);
        _postingCache.Remove(defId);
        _offerTextByDef.Remove(defId);
        _monitor.Log($"DialogueWatcher: pushed '{defId}' into journal via dialogue with {Game1.currentSpeaker?.Name}.", LogLevel.Info);
    }
}
