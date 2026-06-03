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

    // Plain rows are already-formatted objective lines (e.g. "Lights 0/2",
    // "Budget remaining: 4000g"). They render the description verbatim, no checkbox
    // marker, no index, no auto progress suffix.
    public bool Plain { get; }

    public string IndexLabel => $"{Index + 1}.";
    public string DoneMarker => Done ? "[x]" : (Active ? "[>]" : "[ ]");
    public bool ShowProgress => !Plain && Count > 1 && !Done;
    public string ProgressLabel => ShowProgress ? $"{Progress}/{Count}" : string.Empty;
    public string DisplayText => string.IsNullOrEmpty(Description) ? Kind : Description;

    // Combined "[x] 1. description 0/2" string. Long descriptions cause
    // StardewUI's layout to loop infinitely when split across a horizontal
    // lane of multiple labels (seen on Lumisteria's Fancy Goods SO with
    // 60-char step descriptions). Rendering as a single label inside a
    // vertical lane lets StardewUI wrap naturally with the parent's width
    // constraint.
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
