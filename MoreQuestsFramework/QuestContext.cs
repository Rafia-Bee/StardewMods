using MoreQuestsFramework.Cache;
using MoreQuestsFramework.Config;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework;

/// Shared context passed to every board-quest definition. Holds all the helpers a quest
/// generator might need to resolve items, check mod presence, and look up tunables.
public sealed class QuestContext
{
    public IModHelper Helper { get; }
    public IMonitor Monitor { get; }
    public MoreQuestsFrameworkConfig Config { get; }
    public ItemResolver Items { get; }
    public GameDataCache Data { get; }

    public string Season => Game1.currentSeason;
    public int DayOfMonth => Game1.dayOfMonth;
    public int Year => Game1.year;

    public QuestContext(IModHelper helper, IMonitor monitor, MoreQuestsFrameworkConfig config, ItemResolver items, GameDataCache data)
    {
        Helper = helper;
        Monitor = monitor;
        Config = config;
        Items = items;
        Data = data;
    }
}
