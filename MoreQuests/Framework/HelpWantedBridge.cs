using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace MoreQuests.Framework;

/// <summary>
/// Injects MoreQuests-generated quests into the HelpWanted mod's board
/// via its <c>semper1dem.HelpWanted/dictionary</c> data asset.
/// Only active when HelpWanted is loaded.
/// </summary>
internal sealed class HelpWantedBridge
{
    private const string DictionaryAssetPath = "semper1dem.HelpWanted/dictionary";
    private const string HelpWantedUniqueId = "semper1dem.HelpWanted";

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;

    private bool _helpWantedPresent;
    private List<HwQuestNoteTemplate> _pendingTemplates = new();

    public HelpWantedBridge(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
    }

    public void Register()
    {
        _helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        _helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    public void SetDailyQuests(IReadOnlyList<GeneratedQuest> quests)
    {
        if (!_helpWantedPresent)
            return;

        _pendingTemplates.Clear();

        foreach (var quest in quests)
        {
            var template = BuildTemplate(quest);
            if (template != null)
                _pendingTemplates.Add(template);
        }

        if (_pendingTemplates.Count > 0)
        {
            _helper.GameContent.InvalidateCache(DictionaryAssetPath);
            _monitor.Log($"HelpWantedBridge: queued {_pendingTemplates.Count} quests for injection.", LogLevel.Trace);
        }
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        _helpWantedPresent = _helper.ModRegistry.IsLoaded(HelpWantedUniqueId);
        if (_helpWantedPresent)
            _monitor.Log("HelpWanted detected — MoreQuests will inject quests via HelpWanted's board.", LogLevel.Info);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (!_helpWantedPresent)
            return;

        if (!e.NameWithoutLocale.IsEquivalentTo(DictionaryAssetPath))
            return;

        if (_pendingTemplates.Count == 0)
            return;

        e.Edit(asset =>
        {
            var dict = asset.AsDictionary<string, HwQuestNoteTemplate>().Data;
            for (int i = 0; i < _pendingTemplates.Count; i++)
            {
                string key = $"RafiaBee.MoreQuests.Daily.{i}";
                dict[key] = _pendingTemplates[i];
            }
        });
    }

    private static HwQuestNoteTemplate? BuildTemplate(GeneratedQuest quest)
    {
        HwQuestType? questType = MapQuestType(quest.Category);
        if (questType == null)
            return null;

        string npc = string.IsNullOrWhiteSpace(quest.QuestGiverNpc) ? "Robin" : quest.QuestGiverNpc;
        string itemId = quest.ObjectiveItemId;

        // Strip the qualified item prefix (e.g. "(O)254" -> "254") for HelpWanted compatibility
        if (itemId.StartsWith("(") && itemId.Contains(")"))
            itemId = itemId[(itemId.IndexOf(')') + 1)..];

        string objectiveName = quest.ObjectiveItemName;
        int quantity = Math.Max(1, quest.ObjectiveQuantity);
        int reward = Math.Max(100, quest.GoldReward);

        var (title, description, objective, targetMessage) = BuildQuestText(quest, questType.Value, npc, objectiveName, quantity);

        return new HwQuestNoteTemplate
        {
            weight = 100,
            quest = new HwQuestTemplate
            {
                questType = questType.Value,
                questTitle = title,
                questDescription = description,
                currentObjective = objective,
                target = npc,
                targetMessage = targetMessage,
                itemId = itemId,
                number = quantity,
                reward = reward
            }
        };
    }

    private static (string title, string description, string objective, string targetMessage)
        BuildQuestText(GeneratedQuest quest, HwQuestType questType, string npc, string itemName, int quantity)
    {
        return questType switch
        {
            HwQuestType.ItemDelivery => (
                $"{npc}'s Request",
                $"{npc} has put out a request for {itemName}. Deliver it before the deadline.",
                $"Bring {itemName} to {npc}.",
                "Thanks, I appreciate it!"
            ),
            HwQuestType.ResourceCollection => (
                $"{npc} Needs Resources",
                $"{npc} is looking for {quantity} {itemName}. Bring them by before the deadline.",
                $"Bring 0/{quantity} {itemName} to {npc}.",
                "That's just what I needed, thank you."
            ),
            HwQuestType.Fishing => (
                "Fresh Catch Needed",
                $"{npc} is asking for {quantity} {itemName}. Catch them before the deadline.",
                $"Catch 0/{quantity} {itemName} for {npc}.",
                "Perfect, these are exactly what I was after!"
            ),
            HwQuestType.SlayMonster => (
                "Monster Trouble",
                $"{npc} wants someone to deal with {quantity} {itemName}s nearby. Help out before the deadline.",
                $"Slay 0/{quantity} {itemName}.",
                "That should keep us safe for a while. Thank you."
            ),
            _ => (
                $"{npc}'s Request",
                $"{npc} has posted a request on the board.",
                $"Complete {npc}'s request.",
                "Thank you so much!"
            )
        };
    }

    private static HwQuestType? MapQuestType(QuestCategory category)
    {
        return category switch
        {
            QuestCategory.Farming => HwQuestType.ItemDelivery,
            QuestCategory.Fishing => HwQuestType.Fishing,
            QuestCategory.Foraging => HwQuestType.ResourceCollection,
            QuestCategory.Mining => HwQuestType.ResourceCollection,
            QuestCategory.Combat => HwQuestType.SlayMonster,
            QuestCategory.Cooking => HwQuestType.ItemDelivery,
            _ => null
        };
    }

    // Mirror types matching HelpWanted's QuestNoteTemplate/QuestTemplate shapes.
    // SMAPI serializes through JSON so enum string values must match HelpWanted's enum names.

    private sealed class HwQuestNoteTemplate
    {
        public int weight { get; set; } = 100;
        public HwQuestTemplate quest { get; set; } = new();
    }

    private sealed class HwQuestTemplate
    {
        public HwQuestType questType { get; set; }
        public string? itemId { get; set; }
        public int number { get; set; }
        public string? questTitle { get; set; }
        public string? questDescription { get; set; }
        public string? target { get; set; }
        public string? targetMessage { get; set; }
        public string? currentObjective { get; set; }
        public int reward { get; set; }
    }

    private enum HwQuestType
    {
        ItemDelivery,
        ResourceCollection,
        SlayMonster,
        Fishing
    }
}
