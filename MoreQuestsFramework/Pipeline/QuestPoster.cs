using System;
using System.Collections.Generic;
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

    /// Wires the mail-quest registry + persisted state. Called from `ModEntry.OnSaveLoaded`
    /// once the per-save state is available. Safe to call multiple times — the latest
    /// references win.
    public void WireMailDelivery(MailQuestRegistry registry, FrameworkState state)
    {
        _mailRegistry = registry;
        _state = state;
    }

    /// Wires the SpecialOrder writer so PostingKind.SpecialOrder routes through it. Called
    /// once at startup from `ModEntry`.
    public void WireSpecialOrders(SpecialOrderWriter writer)
    {
        _specialOrders = writer;
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

    /// Builds + stamps a Quest from a CustomBoard-source posting and registers it with the
    /// API (so QuestAccepted/QuestCompleted events attribute back to the owning mod). The
    /// caller then stuffs the Quest into the per-board `CustomBoardSlots` for display —
    /// no help-wanted billboard plumbing involved. Returns null when the QuestType isn't
    /// constructable from posting fields.
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

    /// Builds and stamps a Quest from the posting without delivering it. Used by the
    /// `NpcDialogue` watcher, which needs to push the prepared Quest into `questLog`
    /// at chat time rather than at posting time.
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

        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: Math.Max(1, posting.DeadlineDays));

        string mailKey = MailPrefix + posting.DefinitionId.Replace('.', '_') + "_" + Game1.Date.TotalDays;
        // Use the mailKey as the Quest's id so the `%item quest <mailKey> 1 %%`
        // token can find it via our Harmony prefix on `Quest.getQuestFromId`.
        quest.id.Value = mailKey;

        // Embed the standard vanilla mail-quest token. The trailing `1` is the
        // "addImmediately" flag — vanilla `LetterViewerMenu` calls
        // `Game1.player.addQuest(mailKey)` at letter-open time, which routes
        // through `getQuestFromId` (and our prefix), pushes the prepared Quest
        // into `questLog`, and trips Quest Helper / similar tracker hooks via
        // the standard NetCollection-change event.
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

    /// Appends `%item quest <id> 1 %%` to the letter body unless one is already
    /// present. The trailing `1` is vanilla's `addImmediately` flag — quest enters
    /// the journal at letter-open with no Accept button.
    private static string AppendQuestToken(string body, string mailKey)
    {
        if (body.Contains("%item quest ", StringComparison.Ordinal))
            return body;
        // `^^` is vanilla's hard-line-break in mail bodies; one creates the gap
        // before the auto-added quest's HUD message.
        return body + "^^%item quest " + mailKey + " 1 %%";
    }

    /// Mirrors the just-posted Quest into framework save state so a letter sitting
    /// unread across save/load still resolves the same quest. Rebuilt at SaveLoaded.
    /// PreBuilt postings (custom Quest subclasses with their own NetFields) can't
    /// round-trip through this DTO and will skip the stash; their owners must
    /// re-post on the next trigger fire if the player reloaded before reading.
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

        // Replace any prior stash with the same mailKey (re-fire on the same day).
        _state.PendingMailDeliveries.RemoveAll(s => s.MailKey == mailKey);
        _state.PendingMailDeliveries.Add(stash);
    }

    /// Rehydrates a stashed mail quest after save load. Called by ModEntry while
    /// rebuilding the registry. Returns the prepared Quest (added to the registry
    /// with its mailKey) and re-injects the body into `_pendingMail` so the
    /// `Data/mail` asset edit still applies for letters in the player's mailbox.
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
        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: Math.Max(1, posting.DeadlineDays));
        quest.id.Value = stash.MailKey;
        _pendingMail[stash.MailKey] = stash.MailBody;
        return quest;
    }

    /// Drop a stash entry when the corresponding mail letter is no longer in flight
    /// (player opened it, or expired off the mailbox). Called from ModEntry's
    /// `QuestAccepted` / `QuestCompleted` / `QuestRemoved` handlers.
    public void DropStash(string mailKey)
    {
        _pendingMail.Remove(mailKey);
        _state?.PendingMailDeliveries.RemoveAll(s => s.MailKey == mailKey);
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
            RewardApplier.EncodeInto(rewarded.SerializedRewards, posting.Rewards, posting.Consequence);
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
