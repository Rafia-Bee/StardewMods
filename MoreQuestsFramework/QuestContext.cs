using MoreQuestsFramework.Cache;
using MoreQuestsFramework.Config;
using MoreQuestsFramework.Dispatch;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework;

public sealed class QuestContext
{
    public IModHelper Helper { get; }
    public IMonitor Monitor { get; }
    public MoreQuestsFrameworkConfig Config { get; }
    public ItemResolver Items { get; }
    public GameDataCache Data { get; }
    public DispatchRegistry Dispatch { get; }

    public string Season => Game1.currentSeason;
    public int DayOfMonth => Game1.dayOfMonth;
    public int Year => Game1.year;

    private AntiRepetition? _anti;

    public QuestContext(IModHelper helper, IMonitor monitor, MoreQuestsFrameworkConfig config, ItemResolver items, GameDataCache data, DispatchRegistry dispatch)
    {
        Helper = helper;
        Monitor = monitor;
        Config = config;
        Items = items;
        Data = data;
        Dispatch = dispatch;
    }

    internal void AttachAntiRepetition(AntiRepetition anti) => _anti = anti;

    /// True when the framework recently posted a quest whose objective targeted this
    /// item id. Generators can use this to avoid back-to-back duplicates within the
    /// last ~6 postings (the AntiRepetition window). Returns false if anti-repetition
    /// hasn't been wired yet (e.g. dry-run preview).
    public bool IsItemRecent(string qualifiedItemId)
        => !string.IsNullOrEmpty(qualifiedItemId) && (_anti?.ItemRecent(qualifiedItemId) ?? false);
}
