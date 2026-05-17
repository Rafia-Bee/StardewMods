using System;
using System.Collections.Generic;
using System.Reflection;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.State;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Pipeline;

public sealed class QuestPoster
{
    private const string MailPrefix = "RafiaBee.MoreQuestsFramework.";

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly MoreQuestsApi _api;
    private readonly Dictionary<string, string> _pendingMail = new();
    private readonly List<(Quest q, QuestPosting p)> _pendingBoard = new();
    private MailQuestRegistry? _mailRegistry;
    private FrameworkState? _state;
    private SpecialOrderWriter? _specialOrders;

    public QuestPoster(IModHelper helper, IMonitor monitor, MoreQuestsApi api)
    {
        _helper = helper;
        _monitor = monitor;
        _api = api;
    }

    public void WireMailDelivery(MailQuestRegistry registry, FrameworkState state)
    {
        _mailRegistry = registry;
        _state = state;
    }

    public void WireSpecialOrders(SpecialOrderWriter writer)
    {
        _specialOrders = writer;
    }

    public void Register()
    {
        _helper.Events.Content.AssetRequested += OnAssetRequested;
    }

    // Preserves the mail buffer across days because letters can target a future day.
    public void BeginDay()
    {
        _pendingBoard.Clear();
        BillboardSlots.Clear();
    }

    public void CommitBoard()
    {
        BillboardSlots.Replace(_pendingBoard, _monitor);
    }

    public void Post(QuestPosting posting)
    {
        // Non-human NPCs (friendable animals like East Scarp's Duck2NPC, LexiMonster,
        // etc.) shouldn't appear on the daily board or special-orders board. Drop the
        // posting instead of rerouting it: mail is never an implicit fallback, only an
        // explicit Trigger.Source.
        if ((posting.Kind == PostingKind.DailyBoard || posting.Kind == PostingKind.SpecialOrder)
            && !string.IsNullOrEmpty(posting.QuestGiver)
            && !NpcDisplay.IsBoardEligible(posting.QuestGiver))
        {
            _monitor.Log($"Dropped {posting.Kind} posting {posting.DefinitionId}: giver '{posting.QuestGiver}' is not board-eligible.", LogLevel.Debug);
            return;
        }

        switch (posting.Kind)
        {
            case PostingKind.DailyBoard:
                PostToBoard(posting);
                break;
            case PostingKind.Mail:
                PostViaMail(posting);
                break;
            case PostingKind.SpecialOrder:
                if (_specialOrders == null)
                    _monitor.Log($"SpecialOrder posting for {posting.DefinitionId} dropped (writer not wired).", LogLevel.Warn);
                else
                    _specialOrders.Emit(posting);
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

    public Quest? PrepareCustomBoardQuest(QuestPosting posting)
    {
        Quest? quest = posting.PreBuiltQuest ?? QuestFactory.Build(posting);
        if (quest == null)
        {
            _monitor.Log($"Could not build Quest for {posting.DefinitionId} ({posting.QuestType}).", LogLevel.Warn);
            return null;
        }
        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: 0);
        _api.TrackPosted(quest, posting.OwnerUniqueId, posting.DefinitionId);
        return quest;
    }

    // Used by the NpcDialogue watcher: it pushes the prepared Quest into questLog at
    // chat time, not posting time.
    public Quest? PrepareQuest(QuestPosting posting, int daysLeft = 0)
    {
        Quest? quest = posting.PreBuiltQuest ?? QuestFactory.Build(posting);
        if (quest == null)
        {
            _monitor.Log($"Could not build Quest for {posting.DefinitionId} ({posting.QuestType}).", LogLevel.Warn);
            return null;
        }
        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: daysLeft);
        return quest;
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
        _api.TrackPosted(quest, posting.OwnerUniqueId, posting.DefinitionId);
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

        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: ResolveMailDaysLeft(posting));

        string mailKey = MailPrefix + posting.DefinitionId.Replace('.', '_') + "_" + Game1.Date.TotalDays;
        // mailKey doubles as the Quest's id so the "%item quest <mailKey> 1 %%" token
        // resolves via our Harmony prefix on Quest.getQuestFromId.
        quest.id.Value = mailKey;

        string flavour = posting.MailBody ?? BuildDefaultMailBody(posting);
        string body = AppendQuestToken(flavour, mailKey);
        _pendingMail[mailKey] = body;

        if (_mailRegistry != null)
            _mailRegistry.Register(mailKey, quest, posting.OwnerUniqueId, posting.DefinitionId);
        else
            _monitor.Log("Mail-quest registry not wired; %item quest token will not resolve.", LogLevel.Warn);

        StashForReload(posting, mailKey, body);

        _helper.GameContent.InvalidateCache("Data/mail");

        if (!Game1.player.mailReceived.Contains(mailKey) && !Game1.player.mailbox.Contains(mailKey))
            Game1.player.mailbox.Add(mailKey);

        _monitor.Log($"Posted {posting.DefinitionId} via mail. Days left: {quest.daysLeft.Value}.", LogLevel.Trace);
    }

    // Trailing 1 is vanilla's addImmediately flag (quest enters the journal at
    // letter-open, no Accept button). "^^" is the hard-line-break in mail bodies.
    private static string AppendQuestToken(string body, string mailKey)
    {
        if (body.Contains("%item quest ", StringComparison.Ordinal))
            return body;
        return body + "^^%item quest " + mailKey + " 1 %%";
    }

    // PreBuilt postings (custom Quest subclasses with their own NetFields) can't
    // round-trip through this DTO, so they skip the stash; the owner re-posts on
    // the next trigger fire if the player reloaded before reading.
    private void StashForReload(QuestPosting posting, string mailKey, string body)
    {
        if (_state == null || posting.PreBuiltQuest != null)
            return;

        var stash = new StashedMailQuest
        {
            MailKey = mailKey,
            OwnerUniqueId = posting.OwnerUniqueId,
            DefinitionId = posting.DefinitionId,
            MailBody = body,
            QuestType = (int)posting.QuestType,
            Category = (int)posting.Category,
            Tier = (int)posting.Tier,
            QuestGiver = posting.QuestGiver,
            ObjectiveItemId = posting.ObjectiveItemId,
            ObjectiveItemName = posting.ObjectiveItemName,
            ObjectiveQuantity = posting.ObjectiveQuantity,
            TargetMonster = posting.TargetMonster,
            DeadlineDays = posting.DeadlineDays,
            Title = posting.Title,
            Description = posting.Description,
            CurrentObjective = posting.CurrentObjective,
            TargetMessage = posting.TargetMessage
        };
        foreach (var alt in posting.AlternativeObjectiveItemIds)
            stash.AlternativeObjectiveItemIds.Add(alt);
        stash.ObjectiveItemWeight = posting.ObjectiveItemWeight;
        foreach (var w in posting.AlternativeObjectiveItemWeights)
            stash.AlternativeObjectiveItemWeights.Add(w);
        foreach (var r in posting.Rewards)
            stash.EncodedRewards.Add(RewardCodec.Encode(r));
        if (posting.Consequence != null && posting.Consequence.Tier != Consequences.ConsequenceTier.Tier0)
            stash.EncodedConsequence = RewardCodec.EncodeConsequence(posting.Consequence);

        _state.PendingMailDeliveries.RemoveAll(s => s.MailKey == mailKey);
        _state.PendingMailDeliveries.Add(stash);
    }

    public Quest? RehydrateStash(StashedMailQuest stash)
    {
        var posting = new QuestPosting
        {
            DefinitionId = stash.DefinitionId,
            OwnerUniqueId = stash.OwnerUniqueId,
            Category = (QuestCategory)stash.Category,
            Tier = (DifficultyTier)stash.Tier,
            QuestType = (BoardQuestType)stash.QuestType,
            QuestGiver = stash.QuestGiver,
            ObjectiveItemId = stash.ObjectiveItemId,
            ObjectiveItemName = stash.ObjectiveItemName,
            ObjectiveQuantity = stash.ObjectiveQuantity,
            AlternativeObjectiveItemIds = new List<string>(stash.AlternativeObjectiveItemIds),
            ObjectiveItemWeight = stash.ObjectiveItemWeight,
            AlternativeObjectiveItemWeights = new List<int>(stash.AlternativeObjectiveItemWeights),
            TargetMonster = stash.TargetMonster,
            DeadlineDays = stash.DeadlineDays,
            Title = stash.Title,
            Description = stash.Description,
            CurrentObjective = stash.CurrentObjective,
            TargetMessage = stash.TargetMessage
        };
        foreach (var line in stash.EncodedRewards)
        {
            var spec = RewardCodec.Decode(line);
            if (spec != null)
                posting.Rewards.Add(spec);
        }
        if (!string.IsNullOrEmpty(stash.EncodedConsequence))
            posting.Consequence = RewardCodec.DecodeConsequence(new[] { stash.EncodedConsequence });

        var quest = QuestFactory.Build(posting);
        if (quest == null)
            return null;
        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: ResolveMailDaysLeft(posting));
        quest.id.Value = stash.MailKey;
        _pendingMail[stash.MailKey] = stash.MailBody;
        return quest;
    }

    public void DropStash(string mailKey)
    {
        _pendingMail.Remove(mailKey);
        _state?.PendingMailDeliveries.RemoveAll(s => s.MailKey == mailKey);
    }

    // Quest.questTitle/questDescription getters are first-read-builds-from-fields gates:
    // until _loadedTitle/_loadedDescription flip true, the getter overwrites what we set
    // with a vanilla template built from target.Value/ItemId.Value. Setters don't flip
    // these flags, so we do it ourselves (vanilla subclass ctors do the same).
    private static readonly FieldInfo? LoadedTitleField = typeof(Quest)
        .GetField("_loadedTitle", BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo? LoadedDescriptionField = typeof(Quest)
        .GetField("_loadedDescription", BindingFlags.Instance | BindingFlags.NonPublic);

    // Reward routing: Money collapses into Quest.moneyReward (paid by base.questComplete).
    // Friendship/Object/Recipe/Mail are encoded into the quest's NetStringList if it
    // implements IRewardedQuest; subclass questComplete decodes via ApplyEncoded.
    private void ApplyPostingFields(Quest quest, QuestPosting posting, bool dailyQuestDefault, int daysLeft)
    {
        quest.dailyQuest.Value = dailyQuestDefault;
        quest.daysLeft.Value = daysLeft;
        if (!dailyQuestDefault)
            quest.accepted.Value = false;
        if (!string.IsNullOrEmpty(posting.Title))
        {
            quest.questTitle = NpcDisplay.SubstituteIn(posting.Title);
            LoadedTitleField?.SetValue(quest, true);
        }
        if (!string.IsNullOrEmpty(posting.Description))
        {
            quest.questDescription = NpcDisplay.SubstituteIn(AppendRewardLine(posting.Description, posting));
            LoadedDescriptionField?.SetValue(quest, true);
        }
        if (!string.IsNullOrEmpty(posting.CurrentObjective))
            quest.currentObjective = NpcDisplay.SubstituteIn(posting.CurrentObjective);

        int money = posting.TotalMoney;
        if (money > 0)
            quest.moneyReward.Value = money;

        if (quest is IRewardedQuest rewarded)
            RewardApplier.EncodeInto(rewarded.SerializedRewards, posting.Rewards, posting.Consequence);
    }

    private string AppendRewardLine(string description, QuestPosting posting)
    {
        string summary = RewardApplier.BuildRewardSummary(posting.Rewards, posting.QuestGiver, _helper.Translation);
        if (string.IsNullOrEmpty(summary))
            return description;
        // Quest descriptions use \n (Game1.parseText). "^" is mail-only and renders as ~~.
        return $"{description}\n\n{summary}";
    }

    // 0 = explicit "no deadline" (quest enters with daysLeft=0 AND dailyQuest=false, so
    // IsTimedQuest()==false and dayUpdate never decrements). Negatives clamp to 1.
    private static int ResolveMailDaysLeft(QuestPosting posting)
    {
        if (posting.DeadlineDays == 0)
            return 0;
        return Math.Max(1, posting.DeadlineDays);
    }

    private static string BuildDefaultMailBody(QuestPosting p)
    {
        string greeting = $"Dear @,^";
        string body = string.IsNullOrEmpty(p.Description) ? p.CurrentObjective : p.Description;
        string signoff = $"^  -{NpcDisplay.Resolve(p.QuestGiver)}";
        return NpcDisplay.SubstituteIn(greeting + body + signoff);
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
