using System;
using System.Collections.Generic;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Pipeline;

/// Routes generated postings to their delivery channel. DailyBoard postings are pushed into
/// `BillboardSlots` for display on the help-wanted billboard. Mail postings still ship a
/// flavour letter via a Data/mail asset edit and are added to the journal directly.
///
/// Quest construction is delegated to `QuestFactory`; reward summary text comes from
/// `RewardApplier`. This class is now pure routing + asset-edit plumbing.
public sealed class QuestPoster
{
    private const string MailPrefix = "RafiaBee.MoreQuestsFramework.";

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly Dictionary<string, string> _pendingMail = new();
    private readonly List<(Quest q, QuestPosting p)> _pendingBoard = new();

    public QuestPoster(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
    }

    public void Register()
    {
        _helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    /// Only wipes the in-flight board buffer; mail buffer is preserved
    /// across days because letters can target a future day.
    public void BeginDay()
    {
        _pendingBoard.Clear();
        BillboardSlots.Clear();
    }

    /// Commits the buffered daily-board postings to BillboardSlots so the custom Billboard
    /// menu can render them when opened.
    public void CommitBoard()
    {
        BillboardSlots.Replace(_pendingBoard, _monitor);
    }

    public void Post(QuestPosting posting)
    {
        switch (posting.Kind)
        {
            case PostingKind.DailyBoard:
                PostToBoard(posting);
                break;
            case PostingKind.Mail:
                PostViaMail(posting);
                break;
            case PostingKind.SpecialOrder:
                _monitor.Log($"SpecialOrder posting for {posting.DefinitionId} skipped (not yet implemented).", LogLevel.Trace);
                break;
            case PostingKind.NpcDialogue:
                _monitor.Log($"NpcDialogue posting for {posting.DefinitionId} skipped (not yet implemented).", LogLevel.Trace);
                break;
        }
    }

    public void PostBatch(IReadOnlyList<QuestPosting> postings)
    {
        foreach (var p in postings)
            Post(p);
    }

    private void PostToBoard(QuestPosting posting)
    {
        Quest? quest = posting.PreBuiltQuest ?? QuestFactory.Build(posting);
        if (quest == null)
        {
            _monitor.Log($"Could not build Quest for {posting.DefinitionId} ({posting.QuestType}).", LogLevel.Warn);
            return;
        }

        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: 0);
        _pendingBoard.Add((quest, posting));
        _monitor.Log($"Buffered {posting.DefinitionId} for billboard ({posting.QuestType}).", LogLevel.Trace);
    }

    private void PostViaMail(QuestPosting posting)
    {
        var quest = posting.PreBuiltQuest ?? QuestFactory.Build(posting);
        if (quest == null)
        {
            _monitor.Log($"Could not build vanilla quest for {posting.DefinitionId} ({posting.QuestType}).", LogLevel.Warn);
            return;
        }

        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: Math.Max(1, posting.DeadlineDays));
        Game1.player.questLog.Add(quest);

        string mailKey = MailPrefix + posting.DefinitionId.Replace('.', '_') + "_" + Game1.Date.TotalDays;
        string body = posting.MailBody ?? BuildDefaultMailBody(posting);
        _pendingMail[mailKey] = body;

        _helper.GameContent.InvalidateCache("Data/mail");

        if (!Game1.player.mailReceived.Contains(mailKey) && !Game1.player.mailbox.Contains(mailKey))
            Game1.player.mailbox.Add(mailKey);

        _monitor.Log($"Posted {posting.DefinitionId} via mail. Days left: {quest.daysLeft.Value}.", LogLevel.Trace);
    }

    /// Stamps title/description/objective + reward fields onto the quest. Reward routing:
    ///   - Money rewards collapse into vanilla `Quest.moneyReward` (paid by base.questComplete).
    ///   - Friendship/Object/Recipe/Mail rewards are encoded into the quest's NetStringList
    ///     if it implements `IRewardedQuest`; the subclass's `questComplete` override decodes
    ///     them back via `RewardApplier.ApplyEncoded`.
    private void ApplyPostingFields(Quest quest, QuestPosting posting, bool dailyQuestDefault, int daysLeft)
    {
        quest.dailyQuest.Value = dailyQuestDefault;
        quest.daysLeft.Value = daysLeft;
        if (!dailyQuestDefault)
            quest.accepted.Value = false;
        if (!string.IsNullOrEmpty(posting.Title))
            quest.questTitle = posting.Title;
        if (!string.IsNullOrEmpty(posting.Description))
            quest.questDescription = AppendRewardLine(posting.Description, posting);
        if (!string.IsNullOrEmpty(posting.CurrentObjective))
            quest.currentObjective = posting.CurrentObjective;

        int money = posting.TotalMoney;
        if (money > 0)
            quest.moneyReward.Value = money;

        if (quest is IRewardedQuest rewarded)
            RewardApplier.EncodeInto(rewarded.SerializedRewards, posting.Rewards);
    }

    /// Appends a "Reward: ..." footer to the description so the quest log shows it inline,
    /// matching how vanilla bakes the reward into the description text.
    private string AppendRewardLine(string description, QuestPosting posting)
    {
        string summary = RewardApplier.BuildRewardSummary(posting.Rewards, posting.QuestGiver, _helper.Translation);
        if (string.IsNullOrEmpty(summary))
            return description;
        // `Game1.parseText` (used by the quest log) treats `\n` as a hard line break;
        // `^` doesn't work for quest descriptions (it's a mail-asset convention), and
        // showed up rendered as `~~` glyphs.
        return $"{description}\n\n{summary}";
    }

    private static string BuildDefaultMailBody(QuestPosting p)
    {
        string greeting = $"Dear @,^";
        string body = string.IsNullOrEmpty(p.Description) ? p.CurrentObjective : p.Description;
        string signoff = $"^  -{p.QuestGiver}";
        return greeting + body + signoff;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (!e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
            return;
        if (_pendingMail.Count == 0)
            return;

        e.Edit(asset =>
        {
            var dict = asset.AsDictionary<string, string>().Data;
            foreach (var (key, body) in _pendingMail)
                dict[key] = body;
        });
    }
}
