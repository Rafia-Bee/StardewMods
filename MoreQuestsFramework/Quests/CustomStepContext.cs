using System.Collections.Generic;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

// Passed to handlers registered via IMoreQuestsModApi.RegisterCustomAdventureStep.
// The handler returns an int delta each tick. Returning enough to push Progress >= Count
// marks the step Done.
public sealed class CustomStepContext
{
    public Quest Quest { get; }
    public string StepName { get; }
    public IReadOnlyList<string> Targets { get; }
    public IReadOnlyList<string> Items { get; }
    public int Count { get; }
    public int Progress { get; }
    public int MinQuality { get; }
    public string LocationName { get; }
    public int MinSize { get; }
    public string Weather { get; }
    public string Description { get; }

    internal CustomStepContext(Quest quest, AdventureStepState step)
    {
        Quest = quest;
        StepName = step.Name;
        Targets = step.Targets.AsReadOnly();
        Items = step.Items.AsReadOnly();
        Count = step.Count;
        Progress = step.Progress;
        MinQuality = step.MinQuality;
        LocationName = step.LocationName;
        MinSize = step.MinSize;
        Weather = step.Weather;
        Description = step.Description;
    }
}
