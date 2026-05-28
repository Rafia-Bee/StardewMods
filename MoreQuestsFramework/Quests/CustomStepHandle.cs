using System;
using System.Collections.Generic;
using StardewValley.Quests;

namespace MoreQuestsFramework.Quests;

internal sealed class CustomStepHandle : ICustomStepHandle
{
    private static readonly IReadOnlyList<string> Empty = Array.Empty<string>();

    private readonly AdventureQuest _quest;
    private readonly int _index;

    public CustomStepHandle(AdventureQuest quest, int index, string stepName, string handlerName)
    {
        _quest = quest;
        _index = index;
        StepName = stepName ?? string.Empty;
        HandlerName = handlerName ?? string.Empty;
    }

    public Quest Quest => _quest;
    public string StepName { get; }
    public string HandlerName { get; }

    public IReadOnlyList<string> Targets => _quest.PeekCustomStep(_index)?.Targets.AsReadOnly() ?? Empty;
    public IReadOnlyList<string> Items => _quest.PeekCustomStep(_index)?.Items.AsReadOnly() ?? Empty;
    public int Count => _quest.PeekCustomStep(_index)?.Count ?? 0;
    public int Progress => _quest.PeekCustomStep(_index)?.Progress ?? 0;
    public bool IsActive => _quest.IsCustomStepActive(_index);

    public int AddProgress(int delta) => _quest.TryAddCustomStepProgress(_index, delta);
    public int AddProgressOnceForKey(string key, int delta = 1) => _quest.TryAddCustomStepProgressOnceForKey(_index, key, delta);
    public bool MarkDone() => _quest.TryMarkCustomStepDone(_index);
}
