using System;
using System.Collections.Generic;
using System.Reflection;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.State;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Quests;

namespace MoreQuestsFramework.Pipeline;

internal sealed class QuestPoster
{
    // Fallback prefix when a posting somehow lacks an OwnerUniqueId.
    private const string MailFallbackPrefix = "RafiaBee.MoreQuestsFramework";

    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private readonly MoreQuestsApi _api;
    private readonly Dictionary<string, string> _pendingMail = new();
    private readonly List<(Quest q, QuestPosting p)> _pendingBoard = new();
    private bool _mailDirty;
    private MailQuestRegistry? _mailRegistry;
    private FrameworkState? _state;
    private SpecialOrderWriter? _specialOrders;
    private DialogueWatcher? _dialogueWatcher;
    private MailStashCodecRegistry? _stashCodecs;

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

    public void WireDialogueWatcher(DialogueWatcher watcher)
    {
        _dialogueWatcher = watcher;
    }

    public void WireMailStashCodecs(MailStashCodecRegistry codecs)
    {
        _stashCodecs = codecs;
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
        // Item-delivery / shipping quests whose requested item is on the player's
        // exclusion list are dropped outright, so they're never asked for it. Other
        // quest types and the reward side are untouched. Opt-in, off unless the content
        // mod arms ItemResolver.RequestItemExclusion.
        if (IsRequestedItemExcluded(posting))
        {
            ModEntry.LogDebug($"Dropped {posting.DefinitionId}: requested item '{posting.ObjectiveItemId}' is on the exclusion list.");
            return;
        }

        // Excluded givers can't go on the daily board or special-orders board. The
        // heuristic side of IsBoardEligible (Age=Child, !CanSocialize, !PerfectionScore)
        // catches non-human NPCs like East Scarp's Duck2NPC, LexiMonster, etc. The
        // IneligibleGivers config list is the manual override the player edits for
        // anyone the heuristic can't catch (friendable monsters built like real NPCs)
        // or for any villager they personally don't want quests from.
        if (!string.IsNullOrEmpty(posting.QuestGiver)
            && !NpcDisplay.IsBoardEligible(posting.QuestGiver, posting.AllowChildGiver))
        {
            if (posting.Kind == PostingKind.DailyBoard)
            {
                if (ModEntry.Config?.MailFallbackForExcludedGivers ?? true)
                {
                    _monitor.Log(
                        $"Daily-board posting {posting.DefinitionId} has a hardcoded giver '{posting.QuestGiver}' who is on the exclusion list. "
                        + "Redirecting to mail. "
                        + $"Remove {posting.QuestGiver} from your exclusion list for the quest to post normally on the billboard, as the mod author intended.",
                        LogLevel.Warn);
                    posting.Kind = PostingKind.Mail;
                    PostViaMail(posting);
                    // Mail has no accept step, so start the definition cooldown now.
                    // Without this the redirected posting re-rolls into the pool every
                    // morning (it never got "accepted" off the board).
                    ModEntry.Instance?.Anti?.RecordDefinitionAccepted(posting.DefinitionId);
                }
                else
                {
                    _monitor.Log(
                        $"Dropped daily-board posting {posting.DefinitionId}: hardcoded giver '{posting.QuestGiver}' is on the exclusion list "
                        + "and MailFallbackForExcludedGivers is off. "
                        + $"Remove {posting.QuestGiver} from your exclusion list for the quest to post normally on the billboard, as the mod author intended.",
                        LogLevel.Warn);
                }
                return;
            }
            if (posting.Kind == PostingKind.SpecialOrder)
            {
                _monitor.Log(
                    $"Dropped special-order posting {posting.DefinitionId}: hardcoded giver '{posting.QuestGiver}' is on the exclusion list. "
                    + "Special orders can't fall back to mail. "
                    + $"Remove {posting.QuestGiver} from your exclusion list for the quest to post normally on the billboard, as the mod author intended.",
                    LogLevel.Warn);
                return;
            }
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
                PostViaDialogue(posting);
                break;
        }
    }

    // Item-delivery and shipping quests only. True when the content mod's request
    // exclusion is armed and the requested item is on the list. Alternatives are left
    // alone: they're items the player MAY substitute, not what the quest asks for.
    private static bool IsRequestedItemExcluded(QuestPosting posting)
    {
        var filter = ItemResolver.RequestItemExclusion;
        if (filter == null)
            return false;
        if (posting.QuestType is not (BoardQuestType.ItemDelivery
            or BoardQuestType.ResourceCollection
            or BoardQuestType.Ship))
            return false;
        if (string.IsNullOrEmpty(posting.ObjectiveItemId))
            return false;
        string qualified = ItemRegistry.QualifyItemId(posting.ObjectiveItemId) ?? posting.ObjectiveItemId;
        return filter(qualified);
    }

    public void PostBatch(IReadOnlyList<QuestPosting> postings)
    {
        foreach (var p in postings)
            Post(p);
        FlushMailCacheIfDirty();
    }

    private void FlushMailCacheIfDirty()
    {
        if (!_mailDirty)
            return;
        _mailDirty = false;
        _helper.GameContent.InvalidateCache("Data/mail");
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
        ModEntry.LogDebug($"Buffered {posting.DefinitionId} for billboard ({posting.QuestType}).");
    }

    // Builds a daily-board quest and drops it straight onto the live billboard, skipping
    // the morning buffer + CommitBoard replace. mq_trigger uses this so a forced quest
    // appears immediately, even when it's on cooldown or out of its day range.
    public Quest? PostToBoardImmediate(QuestPosting posting)
    {
        Quest? quest = posting.PreBuiltQuest ?? QuestFactory.Build(posting);
        if (quest == null)
        {
            _monitor.Log($"Could not build Quest for {posting.DefinitionId} ({posting.QuestType}).", LogLevel.Warn);
            return null;
        }

        ApplyPostingFields(quest, posting, dailyQuestDefault: false, daysLeft: 0);
        _api.TrackPosted(quest, posting.OwnerUniqueId, posting.DefinitionId);
        BillboardSlots.Append(quest, posting);
        return quest;
    }

    private void PostViaDialogue(QuestPosting posting)
    {
        if (_dialogueWatcher == null)
        {
            _monitor.Log($"NpcDialogue posting for {posting.DefinitionId} dropped (watcher not wired).", LogLevel.Warn);
            return;
        }
        string npc = posting.QuestGiver;
        if (string.IsNullOrEmpty(npc))
        {
            _monitor.Log($"NpcDialogue posting for {posting.DefinitionId} dropped (no QuestGiver to wait for).", LogLevel.Warn);
            return;
        }
        _dialogueWatcher.Enqueue(posting.DefinitionId, npc, posting.DialogueText, posting);
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

        string mailOwner = string.IsNullOrEmpty(posting.OwnerUniqueId) ? MailFallbackPrefix : posting.OwnerUniqueId;
        string mailKey = mailOwner + "." + posting.DefinitionId.Replace('.', '_') + "_" + Game1.Date.TotalDays;
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
        _mailDirty = true;

        if (!Game1.player.mailReceived.Contains(mailKey) && !Game1.player.mailbox.Contains(mailKey))
            Game1.player.mailbox.Add(mailKey);

        ModEntry.LogDebug($"Posted {posting.DefinitionId} via mail. Days left: {quest.daysLeft.Value}.");
    }

    // Trailing 1 is vanilla's addImmediately flag (quest enters the journal at
    // letter-open, no Accept button). "^^" is the hard-line-break in mail bodies.
    private static string AppendQuestToken(string body, string mailKey)
    {
        if (body.Contains("%item quest ", StringComparison.Ordinal))
            return body;
        return body + "^^%item quest " + mailKey + " 1 %%";
    }

    // PreBuilt postings carry extra NetFields the plain DTO can't see, so they go
    // through MailStashCodecRegistry. The framework's two built-in subclass codecs
    // are registered at startup; consumer mods register their own via the API. A
    // PreBuilt subclass with no codec falls out with a Warn so the loss is visible.
    private void StashForReload(QuestPosting posting, string mailKey, string body)
    {
        if (_state == null)
            return;

        string subclassKind = string.Empty;
        List<string> subclassPayload = new();
        if (posting.PreBuiltQuest != null)
        {
            if (_stashCodecs == null || !_stashCodecs.TryEncode(posting.PreBuiltQuest, out subclassKind, out subclassPayload))
            {
                _monitor.Log(
                    $"Mail-delivered '{posting.DefinitionId}' uses Quest subclass '{posting.PreBuiltQuest.GetType().Name}' with no registered mail-stash codec. "
                    + "If the player reloads before opening the letter the quest will be lost. "
                    + "Register a codec via IMoreQuestsModApi.RegisterMailStashCodec.",
                    LogLevel.Warn);
                return;
            }
        }

        var stash = new StashedMailQuest
        {
            MailKey = mailKey,
            OwnerUniqueId = posting.OwnerUniqueId,
            DefinitionId = posting.DefinitionId,
            MailBody = body,
            QuestType = (int)posting.QuestType,
            CustomQuestType = posting.CustomQuestType,
            Category = posting.Category,
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
            TargetMessage = posting.TargetMessage,
            CanBeCancelled = posting.CanBeCancelled
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
        stash.SubclassKind = subclassKind;
        stash.SubclassPayload = subclassPayload;

        _state.PendingMailDeliveries.RemoveAll(s => s.MailKey == mailKey);
        _state.PendingMailDeliveries.Add(stash);
    }

    public Quest? RehydrateStash(StashedMailQuest stash)
    {
        var posting = new QuestPosting
        {
            DefinitionId = stash.DefinitionId,
            OwnerUniqueId = stash.OwnerUniqueId,
            Category = stash.Category,
            Tier = (DifficultyTier)stash.Tier,
            QuestType = (BoardQuestType)stash.QuestType,
            CustomQuestType = stash.CustomQuestType,
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
            TargetMessage = stash.TargetMessage,
            CanBeCancelled = stash.CanBeCancelled
        };
        foreach (var line in stash.EncodedRewards)
        {
            var spec = RewardCodec.Decode(line);
            if (spec != null)
                posting.Rewards.Add(spec);
        }
        if (!string.IsNullOrEmpty(stash.EncodedConsequence))
            posting.Consequence = RewardCodec.DecodeConsequence(new[] { stash.EncodedConsequence });

        Quest? quest;
        if (!string.IsNullOrEmpty(stash.SubclassKind))
        {
            if (_stashCodecs == null || !_stashCodecs.TryDecode(stash.SubclassKind, stash.SubclassPayload, out quest) || quest == null)
            {
                _monitor.Log(
                    $"Mail-stash codec '{stash.SubclassKind}' is not registered (or returned null); "
                    + $"'{stash.DefinitionId}' could not be rehydrated.",
                    LogLevel.Warn);
                return null;
            }
        }
        else
        {
            quest = QuestFactory.Build(posting);
            if (quest == null)
                return null;
        }
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
        quest.canBeCancelled.Value = posting.CanBeCancelled;
        // Marker survives save/reload so the backfill can leave an opt-out quest alone.
        if (quest.modData != null)
            quest.modData[Api.MoreQuestsApi.ModDataCancellableKey] = posting.CanBeCancelled ? "true" : "false";
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
        string summary = RewardApplier.BuildRewardSummary(posting.Rewards, posting.QuestGiver, _helper.Translation, posting.FriendshipSummaryOverride);
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
