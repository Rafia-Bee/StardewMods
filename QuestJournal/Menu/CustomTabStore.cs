using System.Collections.Generic;
using Newtonsoft.Json;
using StardewValley;

namespace QuestJournal.Menu;

// Per-save list of player-defined journal tabs. Each tab is a saved filter
// (title / source / category / kind substrings) the journal applies across the
// active, special-order and completed buckets. Lives in Game1.player.modData
// under a stable key so it round-trips with the save like the completed history.
internal static class CustomTabStore
{
    private const string ModDataKey = "RafiaBee.QuestJournal/CustomTabs";

    public static List<CustomTabDef> Load()
    {
        if (Game1.player == null) return new List<CustomTabDef>();
        if (!Game1.player.modData.TryGetValue(ModDataKey, out string? json) || string.IsNullOrEmpty(json))
            return new List<CustomTabDef>();
        try
        {
            return JsonConvert.DeserializeObject<List<CustomTabDef>>(json) ?? new List<CustomTabDef>();
        }
        catch
        {
            return new List<CustomTabDef>();
        }
    }

    public static void Save(List<CustomTabDef> tabs)
    {
        if (Game1.player == null) return;
        Game1.player.modData[ModDataKey] = JsonConvert.SerializeObject(tabs);
    }

    public static void Add(CustomTabDef def)
    {
        var list = Load();
        list.Add(def);
        Save(list);
    }

    public static void Remove(string id)
    {
        var list = Load();
        list.RemoveAll(t => t.Id == id);
        Save(list);
    }
}

public sealed class CustomTabDef
{
    // Stable id so a tab can be selected/deleted even after the list reorders.
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // All four filters are case-insensitive substring matches, AND-ed together.
    // Empty means "match anything", so a tab with everything blank shows every
    // quest. Category/Kind match the quest's MoreQuests category and posting
    // kind (e.g. "Cooking", "DailyBoard"); they stay blank for vanilla quests.
    public string TitleFilter { get; set; } = string.Empty;
    public string SourceFilter { get; set; } = string.Empty;
    public string CategoryFilter { get; set; } = string.Empty;
    public string KindFilter { get; set; } = string.Empty;
    // A number keeps quests due within that many days; "None" keeps quests with
    // no deadline. Blank ignores deadlines. Empty on tabs saved before this
    // field existed, which reads the same as "ignore".
    public string DeadlineFilter { get; set; } = string.Empty;
}
