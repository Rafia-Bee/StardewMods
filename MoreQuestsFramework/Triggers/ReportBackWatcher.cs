using System;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Quests;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Triggers;

// Watches for the player talking to the report-to NPC of an AdventureQuest that has an
// active report-back Custom step (Targets[0] = a registered ReportBackPrompt). When that
// happens, the NPC asks the prompt's Question and the player picks an answer; the picked
// option's OnChosen runs and the step is marked done. Ticks once a second alongside the
// other dialogue watchers.
internal sealed class ReportBackWatcher
{
    private readonly ReportBackRegistry _registry;
    private readonly Func<Quest, string?> _resolveOwner;
    private readonly IMonitor _monitor;

    private NPC? _lastSpeaker;
    private bool _armedThisChat;

    public ReportBackWatcher(ReportBackRegistry registry, Func<Quest, string?> resolveOwner, IMonitor monitor)
    {
        _registry = registry;
        _resolveOwner = resolveOwner;
        _monitor = monitor;
    }

    public void Reset()
    {
        _lastSpeaker = null;
        _armedThisChat = false;
    }

    public void Tick()
    {
        if (!Context.IsWorldReady || _registry.IsEmpty)
            return;
        // Festivals and events drive their own dialogue; stay out of the way.
        if (Game1.eventUp || Game1.CurrentEvent != null)
            return;

        var speaker = Game1.currentSpeaker;
        if (speaker == _lastSpeaker)
            return;
        var prev = _lastSpeaker;
        _lastSpeaker = speaker;

        if (speaker == null)
        {
            _armedThisChat = false;
            return;
        }
        if (prev == null)
            OnChatStarted(speaker);
    }

    private void OnChatStarted(NPC npc)
    {
        if (_armedThisChat)
            return;
        if (!TryFindMatch(npc.Name, out var quest, out int stepIndex, out var prompt))
            return;

        _armedThisChat = true;
        // Wait for the NPC's current line to close, then have them ask the question.
        // Chain any behavior the game already queued so we don't drop it.
        var prev = Game1.afterDialogues;
        Game1.afterDialogues = () =>
        {
            prev?.Invoke();
            PresentQuestion(quest, stepIndex, prompt!, npc);
        };
    }

    private bool TryFindMatch(string speakerName, out AdventureQuest quest, out int stepIndex, out ReportBackPrompt? prompt)
    {
        quest = null!;
        stepIndex = -1;
        prompt = null;

        var log = Game1.player?.questLog;
        if (log == null)
            return false;

        for (int i = 0; i < log.Count; i++)
        {
            if (log[i] is not AdventureQuest aq || aq.completed.Value)
                continue;
            string owner = _resolveOwner(aq) ?? string.Empty;
            foreach (var (idx, _, handler) in aq.ActiveCustomStepInfos())
            {
                if (string.IsNullOrEmpty(handler))
                    continue;
                var found = _registry.Resolve(owner, handler);
                if (found == null)
                    continue;

                var step = aq.PeekCustomStep(idx);
                if (step == null)
                    continue;
                string reportTo = step.Targets.Count >= 2 && !string.IsNullOrEmpty(step.Targets[1])
                    ? step.Targets[1]
                    : aq.giverNpc.Value;
                if (!string.Equals(reportTo, speakerName, StringComparison.OrdinalIgnoreCase))
                    continue;

                quest = aq;
                stepIndex = idx;
                prompt = found;
                return true;
            }
        }
        return false;
    }

    private void PresentQuestion(AdventureQuest quest, int stepIndex, ReportBackPrompt prompt, NPC npc)
    {
        if (quest.completed.Value || !quest.IsCustomStepActive(stepIndex))
            return;
        var location = Game1.currentLocation;
        if (location == null || prompt.Options.Count == 0)
            return;

        var responses = new Response[prompt.Options.Count];
        for (int i = 0; i < prompt.Options.Count; i++)
            responses[i] = new Response(i.ToString(), prompt.Options[i].Answer);

        location.createQuestionDialogue(
            prompt.Question,
            responses,
            (_, whichAnswer) => OnAnswer(quest, stepIndex, prompt, npc, whichAnswer),
            npc);
    }

    private void OnAnswer(AdventureQuest quest, int stepIndex, ReportBackPrompt prompt, NPC npc, string whichAnswer)
    {
        if (!int.TryParse(whichAnswer, out int idx) || idx < 0 || idx >= prompt.Options.Count)
            return;
        var option = prompt.Options[idx];
        var ctx = new ReportBackContext
        {
            Quest = quest,
            Npc = npc,
            Player = Game1.player,
            ChoiceIndex = idx
        };

        void Finish()
        {
            try
            {
                option.OnChosen?.Invoke(ctx);
            }
            catch (Exception ex)
            {
                _monitor.Log($"ReportBackWatcher: OnChosen for option {idx} threw: {ex.Message}", LogLevel.Error);
            }
            quest.TryMarkCustomStepDone(stepIndex);
        }

        if (!string.IsNullOrEmpty(option.Reply))
        {
            Game1.afterDialogues = Finish;
            npc.CurrentDialogue.Push(new Dialogue(npc, null, option.Reply));
            Game1.drawDialogue(npc);
        }
        else
        {
            Finish();
        }
    }
}
