using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using StardewValley;
using StardewValley.Quests;

namespace QuestJournal.Menu;

// Bound model object for journal.sml. StardewUI uses INotifyPropertyChanged to
// react to changes, so the surface is dumb get/set with PropertyChanged firings
// behind a helper. Skeleton-only: just a title + a list of quest titles. The
// real journal model (tabs, filters, selection, reward lines, etc.) lands in
// step 2 of the build order.
public sealed class JournalContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _title = "Quest Journal";
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    private string _summary = string.Empty;
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    public List<QuestRow> Quests { get; } = new();

    public void Refresh()
    {
        Quests.Clear();
        foreach (var quest in Game1.player.questLog)
        {
            if (quest == null) continue;
            Quests.Add(new QuestRow(quest));
        }
        Summary = Quests.Count switch
        {
            0 => "No active quests.",
            1 => "1 active quest.",
            _ => $"{Quests.Count} active quests."
        };
        // Bare list mutation doesn't fire a binding update on its own; nudge
        // PropertyChanged so StardewUI re-walks the *repeat source.
        Raise(nameof(Quests));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Raise(name);
    }

    private void Raise(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class QuestRow
{
    public string Title { get; }
    public string Objective { get; }
    public int DaysLeft { get; }

    public QuestRow(Quest quest)
    {
        Title = quest.questTitle ?? string.Empty;
        Objective = quest.currentObjective ?? string.Empty;
        DaysLeft = quest.daysLeft.Value;
    }
}
