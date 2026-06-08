using System;
using System.Collections.Generic;

namespace QuestJournal.Api;

// A plain settable implementation of IJournalEntry. The journal uses it for its own test command, and any
// consumer is welcome to copy it. Defaults are safe so you only set the fields you care about.
public sealed class JournalEntry : IJournalEntry
{
    public string OwnerId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string BannerTitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public IReadOnlyList<string> Steps { get; set; } = new List<string>();
    public int Progress { get; set; }
    public int MaxProgress { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int? DeadlineDays { get; set; }
    public bool Completed { get; set; }
    public string Placement { get; set; } = string.Empty;
    public Action? OnComplete { get; set; }
    public Action? OnCancel { get; set; }
}
