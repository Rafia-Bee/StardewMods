using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Framework;

/// <summary>
/// Handles friendship consequences when quests are completed.
/// Applies friendship gains/losses and injects one-time NPC dialogues.
/// </summary>
internal sealed class ConsequenceManager
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly List<PendingConsequence> _pendingConsequences = new();

    public ConsequenceManager(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
    }

    public void QueueConsequences(GeneratedQuest quest)
    {
        if (!ModEntry.Config.ConsequencesEnabled)
            return;

        foreach (var consequence in quest.Consequences)
        {
            _pendingConsequences.Add(new PendingConsequence
            {
                NpcName = consequence.NpcName,
                FriendshipChange = consequence.FriendshipChange,
                DialogueKey = consequence.DialogueKey,
                QuestTitle = quest.ObjectiveItemName,
                DaysRemaining = consequence.Tier == ConsequenceTier.Significant ? 3 : 1
            });
        }
    }

    public void ProcessDayEndConsequences()
    {
        if (_pendingConsequences.Count == 0)
            return;

        foreach (var pending in _pendingConsequences.ToList())
        {
            var npc = Game1.getCharacterFromName(pending.NpcName);
            if (npc == null)
            {
                _pendingConsequences.Remove(pending);
                continue;
            }

            // Apply friendship change
            if (pending.FriendshipChange != 0)
            {
                Game1.player.changeFriendship(pending.FriendshipChange, npc);
                _monitor.Log(
                    $"Applied {pending.FriendshipChange} friendship to {pending.NpcName} (quest: {pending.QuestTitle})",
                    LogLevel.Trace);
            }

            // Inject dialogue for next interaction
            if (!string.IsNullOrEmpty(pending.DialogueKey))
            {
                var dialogueText = _helper.Translation.Get(pending.DialogueKey, new
                {
                    item = pending.QuestTitle
                });

                if (dialogueText.HasValue())
                {
                    // TODO: inject NPC dialogue via Data/Characters or Dialogue asset edit
                }
            }

            pending.DaysRemaining--;
            if (pending.DaysRemaining <= 0)
                _pendingConsequences.Remove(pending);
        }
    }

    private sealed class PendingConsequence
    {
        public string NpcName { get; set; } = "";
        public int FriendshipChange { get; set; }
        public string DialogueKey { get; set; } = "";
        public string QuestTitle { get; set; } = "";
        public int DaysRemaining { get; set; } = 1;
    }
}
