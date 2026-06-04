using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

// A "report back" interaction for an AdventureQuest. When the player talks to the
// report-to NPC while a Custom step keyed to this prompt is active (all its Requires
// done), the framework has the NPC ask Question, shows the answer buttons, and runs
// the picked option. Register one with IMoreQuestsModApi.RegisterReportBackChoice and
// point a Custom step's Targets[0] at the registered name. The report-to NPC is the
// step's Targets[1] when set, otherwise the quest's giver.
public sealed class ReportBackPrompt
{
    // Spoken by the NPC (with portrait) when the player talks to them. This is the
    // lead-in line shown above the answer buttons.
    public string Question { get; set; } = string.Empty;

    // The answers the player can pick. Two to four reads best in the question box.
    public List<ReportBackOption> Options { get; set; } = new();
}

public sealed class ReportBackOption
{
    // The button label the player clicks.
    public string Answer { get; set; } = string.Empty;

    // Optional line the NPC says back after the player picks this answer. Shown before
    // OnChosen runs so any item popups land after the NPC finishes talking.
    public string Reply { get; set; } = string.Empty;

    // Runs when the player picks this answer. Hand out the reward here. The step is
    // marked done (completing the quest if it's the last step) right after this returns.
    public Action<ReportBackContext>? OnChosen { get; set; }
}

public sealed class ReportBackContext
{
    public Quest Quest { get; init; } = null!;
    public NPC Npc { get; init; } = null!;
    public Farmer Player { get; init; } = null!;

    // Which option was picked, 0-based into ReportBackPrompt.Options.
    public int ChoiceIndex { get; init; }
}
