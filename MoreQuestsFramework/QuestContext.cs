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

    public QuestContext(IModHelper helper, IMonitor monitor, MoreQuestsFrameworkConfig config, ItemResolver items, GameDataCache data, DispatchRegistry dispatch)
    {
        Helper = helper;
        Monitor = monitor;
        Config = config;
        Items = items;
        Data = data;
        Dispatch = dispatch;
    }
}
