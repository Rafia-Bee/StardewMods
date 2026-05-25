namespace QuestJournal.Menu;

// One row in the journal's step list for a multi-step Adventure quest.
// Mirrors the bits of MQF's AdventureStepInfo the UI actually renders.
// Built fresh on every selection change; not observable.
public sealed class AdventureStepRow
{
    public int Index { get; }
    public string Description { get; }
    public int Progress { get; }
    public int Count { get; }
    public bool Done { get; }
    public bool Active { get; }
    public string Kind { get; }

    public string IndexLabel => $"{Index + 1}.";
    public string DoneMarker => Done ? "[x]" : (Active ? "[>]" : "[ ]");
    public bool ShowProgress => Count > 1 && !Done;
    public string ProgressLabel => ShowProgress ? $"{Progress}/{Count}" : string.Empty;
    public string DisplayText => string.IsNullOrEmpty(Description) ? Kind : Description;

    public AdventureStepRow(
        int index,
        string description,
        int progress,
        int count,
        bool done,
        bool active,
        string kind)
    {
        Index = index;
        Description = description ?? string.Empty;
        Progress = progress;
        Count = count;
        Done = done;
        Active = active;
        Kind = kind ?? string.Empty;
    }
}
