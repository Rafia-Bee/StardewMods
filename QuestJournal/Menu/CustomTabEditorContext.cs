using System;
using System.ComponentModel;

namespace QuestJournal.Menu;

// Backing data for the custom tab editor popup (the new/edit tab form).
// Holds the editable filter fields and help text, raises change notifications so
// the UI updates, and forwards the save, delete, and cancel buttons.
public sealed class CustomTabEditorContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public CustomTabEditorContext(string categoryHint, string kindHint, bool isEdit)
    {
        ShowDelete = isEdit;
        var t = ModEntry.Instance?.Helper?.Translation;
        string Help(string key, string fallback = "") => t?.Get(key).Default(fallback).ToString() ?? fallback;
        string WithHint(string text, string hint) => string.IsNullOrEmpty(hint) ? text : text + " " + hint;

        HeaderText = isEdit ? Help("tabeditor.header.edit", "Edit tab") : Help("tabeditor.header.new", "New tab");

        TitleHelp = Help("tabeditor.help.title");
        SourceHelp = Help("tabeditor.help.source");
        CategoryHelp = WithHint(Help("tabeditor.help.category"), categoryHint);
        KindHelp = WithHint(Help("tabeditor.help.kind"), kindHint);
        DeadlineHelp = Help("tabeditor.help.deadline");
    }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set { if (_name == value) return; _name = value; Raise(nameof(Name)); }
    }

    private string _titleFilter = string.Empty;
    public string TitleFilter
    {
        get => _titleFilter;
        set { if (_titleFilter == value) return; _titleFilter = value; Raise(nameof(TitleFilter)); }
    }

    private string _sourceFilter = string.Empty;
    public string SourceFilter
    {
        get => _sourceFilter;
        set { if (_sourceFilter == value) return; _sourceFilter = value; Raise(nameof(SourceFilter)); }
    }

    private string _categoryFilter = string.Empty;
    public string CategoryFilter
    {
        get => _categoryFilter;
        set { if (_categoryFilter == value) return; _categoryFilter = value; Raise(nameof(CategoryFilter)); }
    }

    private string _kindFilter = string.Empty;
    public string KindFilter
    {
        get => _kindFilter;
        set { if (_kindFilter == value) return; _kindFilter = value; Raise(nameof(KindFilter)); }
    }

    private string _deadlineFilter = string.Empty;
    public string DeadlineFilter
    {
        get => _deadlineFilter;
        set { if (_deadlineFilter == value) return; _deadlineFilter = value; Raise(nameof(DeadlineFilter)); }
    }

    public string TitleHelp { get; }
    public string SourceHelp { get; }
    public string CategoryHelp { get; }
    public string KindHelp { get; }
    public string DeadlineHelp { get; }

    public bool ShowDelete { get; }
    public string HeaderText { get; }

    private Action<CustomTabEditorContext>? _onSave;
    private Action? _onDelete;
    private Action? _onCancel;

    public void Bind(Action<CustomTabEditorContext> onSave, Action onDelete, Action onCancel)
    {
        _onSave = onSave;
        _onDelete = onDelete;
        _onCancel = onCancel;
    }

    public void Save() => _onSave?.Invoke(this);
    public void Delete() => _onDelete?.Invoke();
    public void Cancel() => _onCancel?.Invoke();

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
