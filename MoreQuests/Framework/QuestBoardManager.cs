using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuests.Framework;

/// <summary>
/// Manages the quest board, generating multiple daily quests with difficulty scaling
/// and modded item support.
/// </summary>
internal sealed class QuestBoardManager
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly ItemResolver _itemResolver;
    private readonly DifficultyScaler _difficultyScaler;
    private readonly AntiRepetitionTracker _antiRepetition;
    private readonly QuestGenerator _questGenerator;

    private readonly List<GeneratedQuest> _activeQuests = new();

    public IReadOnlyList<GeneratedQuest> ActiveQuests => _activeQuests;

    public QuestBoardManager(
        IModHelper helper,
        IMonitor monitor,
        ItemResolver itemResolver,
        DifficultyScaler difficultyScaler,
        AntiRepetitionTracker antiRepetition)
    {
        _helper = helper;
        _monitor = monitor;
        _itemResolver = itemResolver;
        _difficultyScaler = difficultyScaler;
        _antiRepetition = antiRepetition;
        _questGenerator = new QuestGenerator(monitor, itemResolver, difficultyScaler, antiRepetition);
    }

    public void GenerateDailyQuests()
    {
        _activeQuests.Clear();
        int questCount = ModEntry.Config.QuestsPerDay;
        string season = Game1.currentSeason;

        // Pick a mix of categories, avoiding repeats
        var categories = PickCategories(questCount);

        foreach (var category in categories)
        {
            var quest = _questGenerator.Generate(category, season);
            if (quest != null)
            {
                quest.DeadlineDays = ModEntry.Config.QuestDeadlineDays;
                _activeQuests.Add(quest);
                _antiRepetition.RecordQuest(quest);
            }
        }

        _monitor.Log($"Generated {_activeQuests.Count} daily quests.", LogLevel.Trace);
    }

    public void SaveState()
    {
        // TODO: persist active quests to save data
    }

    private List<QuestCategory> PickCategories(int count)
    {
        var available = new List<QuestCategory>
        {
            QuestCategory.Farming,
            QuestCategory.Fishing,
            QuestCategory.Mining,
            QuestCategory.Foraging,
            QuestCategory.Cooking,
            QuestCategory.Social
        };

        // Combat requires mine access
        if (Game1.player.deepestMineLevel > 0)
            available.Add(QuestCategory.Combat);

        var rng = Game1.random;
        var picked = new List<QuestCategory>();

        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int idx = rng.Next(available.Count);
            picked.Add(available[idx]);
            available.RemoveAt(idx); // no duplicate categories per day
        }

        return picked;
    }
}
