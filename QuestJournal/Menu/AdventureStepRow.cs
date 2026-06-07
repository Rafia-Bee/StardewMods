namespace QuestJournal.Menu;

// One row in a multi-step quest's checklist shown in the journal.
// Holds the step's progress and builds the display text (checkbox, number, count).
public sealed class AdventureStepRow
{
    public int Index { get; }
    public string Description { get; }
    public int Progress { get; }
    public int Count { get; }
    public bool Done { get; }
    public bool Active { get; }
    public string Kind { get; }

    public bool Plain { get; }

    public string IndexLabel => $"{Index + 1}.";
    public string DoneMarker => Done ? "[x]" : (Active ? "[>]" : "[ ]");
    public bool ShowProgress => !Plain && Count > 1 && !Done;
    public string ProgressLabel => ShowProgress ? $"{Progress}/{Count}" : string.Empty;
    public string DisplayText => string.IsNullOrEmpty(Description) ? Kind : Description;

    public string RowText
    {
        get
        {
            if (Plain)
                return DisplayText;
            var sb = new System.Text.StringBuilder(64);
            sb.Append(DoneMarker).Append(' ').Append(IndexLabel).Append(' ').Append(DisplayText);
            if (ShowProgress) sb.Append("  ").Append(ProgressLabel);
            return sb.ToString();
        }
    }

    public AdventureStepRow(
        int index,
        string description,
        int progress,
        int count,
        bool done,
        bool active,
        string kind,
        bool plain = false)
    {
        Index = index;
        Description = description ?? string.Empty;
        Progress = progress;
        Count = count;
        Done = done;
        Active = active;
        Kind = kind ?? string.Empty;
        Plain = plain;
    }
}
