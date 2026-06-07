using System.Collections.Generic;
using Newtonsoft.Json;
using StardewValley;

namespace QuestJournal.Menu;

// Keeps a per-save history of finished quests in the player's modData.
// Load reads them back, Add tacks on a new one. The records below are the saved shape.
internal static class CompletedQuestStore
{
    private const string ModDataKey = "RafiaBee.QuestJournal/CompletedQuests";

    public static List<CompletedQuestRecord> Load()
    {
        if (Game1.player == null) return new List<CompletedQuestRecord>();
        if (!Game1.player.modData.TryGetValue(ModDataKey, out string? json) || string.IsNullOrEmpty(json))
            return new List<CompletedQuestRecord>();
        try
        {
            return JsonConvert.DeserializeObject<List<CompletedQuestRecord>>(json) ?? new List<CompletedQuestRecord>();
        }
        catch
        {
            return new List<CompletedQuestRecord>();
        }
    }

    public static void Add(CompletedQuestRecord record)
    {
        if (Game1.player == null) return;
        var list = Load();
        list.Add(record);
        Game1.player.modData[ModDataKey] = JsonConvert.SerializeObject(list);
    }
}

public sealed class CompletedQuestRecord
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string RewardSummary { get; set; } = string.Empty;
    public List<StoredRewardLine> RewardLines { get; set; } = new();
    public string Giver { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int CompletedOnTotalDays { get; set; }
    public string Status { get; set; } = "Completed";
}

public sealed class StoredRewardLine
{
    public string Kind { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? ItemId { get; set; }
    public string? NpcName { get; set; }
    public int Amount { get; set; }
    public int DurationDays { get; set; }
}
