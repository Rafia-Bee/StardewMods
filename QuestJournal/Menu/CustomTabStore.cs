using System.Collections.Generic;
using Newtonsoft.Json;
using StardewValley;

namespace QuestJournal.Menu;

// Saves and loads the player's custom journal tabs in modData.
// Each tab is just a set of filters (title, source, category, etc) defined below.
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
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TitleFilter { get; set; } = string.Empty;
    public string SourceFilter { get; set; } = string.Empty;
    public string CategoryFilter { get; set; } = string.Empty;
    public string KindFilter { get; set; } = string.Empty;
    public string DeadlineFilter { get; set; } = string.Empty;
}
