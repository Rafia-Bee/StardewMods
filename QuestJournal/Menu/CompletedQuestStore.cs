using System.Collections.Generic;
using Newtonsoft.Json;
using StardewValley;

namespace QuestJournal.Menu;

// Per-save snapshot of every quest the player has completed via the journal's
// Complete button. Vanilla yanks completed quests out of player.questLog as
// soon as the reward is claimed (or immediately, for no-reward quests), so we
// need our own store if the Completed tab is to keep history. Lives in
// Game1.player.modData under a stable key so it round-trips with the save
// automatically.
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
    // Aggregate text summary kept for backwards-compat with pre Step 5 saves.
    // New completions write the itemised list into RewardLines below as well,
    // so this field is just a fallback for old history rows.
    public string RewardSummary { get; set; } = string.Empty;
    public List<StoredRewardLine> RewardLines { get; set; } = new();
    public string Giver { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    // MoreQuests category / posting kind captured at completion so custom-tab
    // filters still match a quest after it moves into the Completed history.
    // Blank for vanilla quests and pre-existing records.
    public string Category { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    // Total in-game days at the time of completion. Lets us display
    // "completed Spring 14, Y2" later without storing a string.
    public int CompletedOnTotalDays { get; set; }
    // "Completed" for natural / button completions, "Failed" when the
    // quest disappeared without finishing (e.g. deadline expired
    // overnight). Default keeps pre-existing records readable as
    // completions since that's all we recorded before.
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
