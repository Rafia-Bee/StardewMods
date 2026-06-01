using System;
using System.ComponentModel;

namespace QuestJournal.Menu;

// Backing context for custom_tab_editor.sml. The text fields are two-way bound
// so they fill in as the player types; Save/Delete/Cancel route back to the
// JournalContext through the actions it wires up after creating the popup.
public sealed class CustomTabEditorContext : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public CustomTabEditorContext(string categoryHint, string kindHint, bool isEdit)
    {
        CategoryHint = categoryHint;
        KindHint = kindHint;
        ShowDelete = isEdit;
        var t = ModEntry.Instance?.Helper?.Translation;
        HeaderText = isEdit
            ? t?.Get("tabeditor.header.edit").Default("Edit tab").ToString() ?? "Edit tab"
            : t?.Get("tabeditor.header.new").Default("New tab").ToString() ?? "New tab";
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

    // Read-only hints showing which category/kind values exist in the player's
    // current quests, so they know what's worth typing. Fixed for the popup's
    // life, so no change notification needed. The Has* flags drive the *if so
    // the hint label is hidden when nothing in the journal is classified.
    public string CategoryHint { get; }
    public string KindHint { get; }
    public bool HasCategoryHint => !string.IsNullOrEmpty(CategoryHint);
    public bool HasKindHint => !string.IsNullOrEmpty(KindHint);

    // True when editing an existing tab, so the SML shows the Delete button and
    // an "Edit tab" header instead of "New tab". Fixed for the popup's life.
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
