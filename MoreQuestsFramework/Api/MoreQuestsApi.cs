using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Content;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Posting;
using MoreQuestsFramework.Posting.Boards;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Registry;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.State;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley.Quests;

namespace MoreQuestsFramework.Api;

// Must stay top-level: SMAPI's GetApi rejects API types where Type.IsPublic is false,
// which is the case for nested types regardless of declared accessibility.
public sealed class MoreQuestsApi : IMoreQuestsApi
{
    private readonly QuestRegistry _registry;
    private readonly GeneratorRegistry _generators;
    private readonly CustomStepRegistry _customSteps;
    private readonly ReportBackRegistry _reportBack;
    private readonly CustomTriggerRegistry _customTriggers;
    private readonly CustomRewardRegistry _customRewards;
    private readonly CustomConditionRegistry _customConditions;
    private readonly CustomBoardQuestRegistry _customBoardQuests;
    private readonly DispatchRegistry _dispatch;
    private readonly BoardRegistry _boards;
    private readonly NoticeRegistry _notices;
    private readonly CombatFoodRegistry _combatFood;
    private readonly MailStashCodecRegistry _mailStashCodecs;
    private readonly IMonitor _monitor;
    private readonly Func<ISpaceCoreApi?> _spaceCore;
    private readonly Action _refreshOffers;
    private readonly Func<QuestContext?> _ctxProvider;

    private readonly ConditionalWeakTable<Quest, ManagedQuest> _managed = new();
    private readonly Dictionary<string, IMoreQuestsModApi> _modScopes
        = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? RegistrationOpen;
    public event EventHandler? RegistrationClosed;
    public event EventHandler<QuestAcceptedArgs>? QuestAccepted;
    public event EventHandler<QuestCompletedArgs>? QuestCompleted;
    public event EventHandler<QuestRemovedArgs>? QuestRemoved;
    public event EventHandler<DayRefreshedArgs>? DayRefreshed;
    public event EventHandler<QuestSkippedArgs>? QuestSkippedToday;
    public event EventHandler? FrameworkShuttingDown;
    public event EventHandler<CropHarvestInfo>? CropHarvested;

    private FrameworkState? _state;

    internal MoreQuestsApi(
        QuestRegistry registry,
        GeneratorRegistry generators,
        CustomStepRegistry customSteps,
        ReportBackRegistry reportBack,
        CustomTriggerRegistry customTriggers,
        CustomRewardRegistry customRewards,
        CustomConditionRegistry customConditions,
        CustomBoardQuestRegistry customBoardQuests,
        DispatchRegistry dispatch,
        BoardRegistry boards,
        NoticeRegistry notices,
        CombatFoodRegistry combatFood,
        MailStashCodecRegistry mailStashCodecs,
        IMonitor monitor,
        Func<ISpaceCoreApi?> spaceCore,
        Action refreshOffers,
        Func<QuestContext?> ctxProvider)
    {
        _registry = registry;
        _generators = generators;
        _customSteps = customSteps;
        _reportBack = reportBack;
        _customTriggers = customTriggers;
        _customRewards = customRewards;
        _customConditions = customConditions;
        _customBoardQuests = customBoardQuests;
        _dispatch = dispatch;
        _boards = boards;
        _notices = notices;
        _combatFood = combatFood;
        _mailStashCodecs = mailStashCodecs;
        _monitor = monitor;
        _spaceCore = spaceCore;
        _refreshOffers = refreshOffers;
        _ctxProvider = ctxProvider;
    }

    public IMoreQuestsModApi GetModApi(IManifest mod)
    {
        if (mod == null)
            throw new ArgumentNullException(nameof(mod));
        if (_modScopes.TryGetValue(mod.UniqueID, out var existing))
            return existing;
        var scope = new MoreQuestsModApi(mod, _registry, _generators, _customSteps, _reportBack, _customTriggers, _customRewards, _customConditions, _customBoardQuests, _dispatch, _boards, _notices, _mailStashCodecs, _monitor, _spaceCore, ResolveQuestOwner);
        _modScopes[mod.UniqueID] = scope;
        return scope;
    }

    public bool IsManagedQuest(Quest quest) =>
        quest != null && _managed.TryGetValue(quest, out _);

    public int? GetDeliveredQuality(Quest quest)
    {
        return quest is MoreQuestsItemDeliveryQuest idq
            ? idq.deliveredQuality.Value
            : null;
    }

    public QuestItemRequirement? GetItemRequirement(Quest quest)
    {
        switch (quest)
        {
            // MoreQuests delivery: ItemId may be a "$any" placeholder, so resolve
            // through the concrete alternatives. minQuality carries the required tier.
            case MoreQuestsItemDeliveryQuest mqd:
            {
                string id = ResolveConcreteId(mqd.ItemId.Value, mqd.alternativeItemIds);
                if (string.IsNullOrEmpty(id)) return null;
                int total = mqd.lockedRequiredQty.Value > 0 ? mqd.lockedRequiredQty.Value : mqd.number.Value;
                int remaining = System.Math.Max(1, total - mqd.delivered.Value);
                return new QuestItemRequirement(id, mqd.minQuality.Value, remaining);
            }
            case MoreQuestsShipQuest sq:
            {
                string id = ResolveConcreteId(sq.itemId.Value, sq.alternativeItemIds);
                if (string.IsNullOrEmpty(id)) return null;
                int remaining = System.Math.Max(1, sq.numberToShip.Value - sq.numberShipped.Value);
                return new QuestItemRequirement(id, 0, remaining);
            }
            case ItemDeliveryQuest vd:
            {
                if (!IsRealItem(vd.ItemId.Value)) return null;
                return new QuestItemRequirement(vd.ItemId.Value, 0, System.Math.Max(1, vd.number.Value));
            }
            case ResourceCollectionQuest rc:
            {
                if (!IsRealItem(rc.ItemId.Value)) return null;
                return new QuestItemRequirement(rc.ItemId.Value, 0, System.Math.Max(1, rc.number.Value - rc.numberCollected.Value));
            }
        }
        return null;
    }

    private static bool IsRealItem(string? id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        try { return StardewValley.ItemRegistry.GetData(id) != null; }
        catch { return false; }
    }

    private static string ResolveConcreteId(string? primary, Netcode.NetStringList alts)
    {
        if (IsRealItem(primary)) return primary!;
        if (alts != null)
            for (int i = 0; i < alts.Count; i++)
                if (IsRealItem(alts[i])) return alts[i];
        return string.Empty;
    }

    public string? GetDefinitionId(Quest quest)
    {
        return TryGetManaged(quest, out var info) ? info.DefinitionId : null;
    }

    public IReadOnlyList<string> RegisteredQuestIds() => _registry.RegisteredIds();

    public bool? IsQuestAvailable(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return null;
        if (!_registry.TryGet(definitionId, out var def) || def == null)
            return null;
        var ctx = _ctxProvider();
        if (ctx == null)
            return null;
        return def.IsAvailable(ctx);
    }

    public QuestInfo? GetQuestInfo(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId))
            return null;
        if (!_registry.TryGet(definitionId, out var def) || def == null)
            return null;
        return new QuestInfo(
            def.Id,
            def.OwnerUniqueId,
            def.Category,
            def.Kind,
            def.Source,
            _registry.EffectiveSource(def));
    }

    public int? GetLastFiredDay(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId) || _state == null)
            return null;
        if (!_registry.TryGet(definitionId, out _))
            return null;
        return _state.LastFiredDay.TryGetValue(definitionId, out int day) ? day : (int?)null;
    }

    public bool? GetOneShotFired(string definitionId)
    {
        if (string.IsNullOrEmpty(definitionId) || _state == null)
            return null;
        if (!_registry.TryGet(definitionId, out _))
            return null;
        return _state.OneShotFired.TryGetValue(definitionId, out bool fired) && fired;
    }

    public IReadOnlyList<AdventureStepInfo>? GetAdventureSteps(Quest quest)
    {
        return quest is AdventureQuest aq ? aq.BuildStepInfos() : null;
    }

    public int? GetActiveStepIndex(Quest quest)
    {
        if (quest is not AdventureQuest aq)
            return null;
        int idx = aq.ActiveStepIndex();
        return idx < 0 ? null : idx;
    }

    public IReadOnlyList<string>? GetObjectiveLines(Quest quest)
    {
        return quest is IObjectiveLineSource s ? s.GetObjectiveLines() : null;
    }

    public string? GetGiverNpc(Quest quest)
    {
        if (quest == null)
            return null;
        if (quest is AdventureQuest aq)
        {
            string g = aq.giverNpc.Value;
            return string.IsNullOrEmpty(g) ? null : g;
        }
        switch (quest)
        {
            // MoreQuestsItemDeliveryQuest and MoreQuestsFishingQuest extend the
            // vanilla subclasses, so they hit the ItemDeliveryQuest / FishingQuest
            // arms by inheritance. MoreQuestsShipQuest extends base Quest and
            // carries its own `target` NetString, so it needs its own arm.
            case MoreQuestsShipQuest msq:
                return string.IsNullOrEmpty(msq.target.Value) ? null : msq.target.Value;
            case StardewValley.Quests.ItemDeliveryQuest idq:
                return string.IsNullOrEmpty(idq.target.Value) ? null : idq.target.Value;
            case StardewValley.Quests.FishingQuest fq:
                return string.IsNullOrEmpty(fq.target.Value) ? null : fq.target.Value;
            case StardewValley.Quests.SlayMonsterQuest smq:
                return string.IsNullOrEmpty(smq.target.Value) || smq.target.Value == "null" ? null : smq.target.Value;
            case StardewValley.Quests.ResourceCollectionQuest rcq:
                return string.IsNullOrEmpty(rcq.target.Value) ? null : rcq.target.Value;
        }
        return null;
    }

    public IReadOnlyList<QuestRewardLine> GetRewardLines(Quest quest)
    {
        if (quest is not Rewards.IRewardedQuest rq)
            return System.Array.Empty<QuestRewardLine>();

        var specs = new List<Rewards.RewardSpec>();
        foreach (var line in rq.SerializedRewards)
        {
            if (Rewards.RewardCodec.IsConsequenceLine(line))
                continue;
            var spec = Rewards.RewardCodec.Decode(line);
            if (spec != null)
                specs.Add(spec);
        }

        if (quest.moneyReward.Value > 0)
            specs.Insert(0, new Rewards.MoneyReward(quest.moneyReward.Value));

        string giver = GetGiverNpc(quest) ?? string.Empty;
        var translation = ModEntry.Instance?.Helper.Translation;
        var lines = new List<QuestRewardLine>(specs.Count);
        foreach (var spec in specs)
        {
            var line = ProjectRewardLine(spec, giver, translation);
            if (line != null)
                lines.Add(line);
        }
        return lines;
    }

    private static QuestRewardLine? ProjectRewardLine(Rewards.RewardSpec spec, string giver, StardewModdingAPI.ITranslationHelper? translation)
    {
        string giverDisplay = string.IsNullOrEmpty(giver) ? "They" : NpcDisplay.Resolve(giver);
        switch (spec)
        {
            case Rewards.MoneyReward m when m.Amount > 0:
                return new QuestRewardLine(
                    "Money",
                    translation?.Get("quest.reward.line.money", new { npc = giverDisplay, gold = m.Amount })
                        .Default($"{giverDisplay} will give you {m.Amount}g in return").ToString()
                        ?? $"{m.Amount}g",
                    amount: m.Amount);

            case Rewards.FriendshipReward f when f.Points > 0 && !string.IsNullOrEmpty(f.Npc):
            {
                string npcDisplay = NpcDisplay.Resolve(f.Npc);
                return new QuestRewardLine(
                    "Friendship",
                    translation?.Get("quest.reward.line.friendship", new { npc = npcDisplay })
                        .Default($"{npcDisplay} will like you more").ToString()
                        ?? $"+{f.Points} friendship with {npcDisplay}",
                    npcName: f.Npc,
                    amount: f.Points);
            }

            case Rewards.ObjectReward o when !string.IsNullOrEmpty(o.ItemId) && o.Count > 0:
            {
                var item = StardewValley.ItemRegistry.Create(o.ItemId, o.Count);
                string name = item?.DisplayName ?? o.ItemId;
                string itemPhrase = o.Count > 1 ? $"{o.Count}x {name}" : name;
                return new QuestRewardLine(
                    "Object",
                    translation?.Get("quest.reward.line.item", new { item = itemPhrase, count = o.Count, npc = giverDisplay })
                        .Default($"You will get {itemPhrase} as a thank you").ToString()
                        ?? itemPhrase,
                    itemId: o.ItemId,
                    amount: o.Count);
            }

            case Rewards.RecipeReward r when !string.IsNullOrEmpty(r.RecipeName):
                return new QuestRewardLine(
                    "Recipe",
                    translation?.Get("quest.reward.line.recipe", new { recipe = r.RecipeName, npc = giverDisplay })
                        .Default($"You will learn the {r.RecipeName} recipe").ToString()
                        ?? $"Recipe: {r.RecipeName}",
                    payload: r.RecipeName);

            case Rewards.MailReward mr when !string.IsNullOrEmpty(mr.LetterKey):
                return new QuestRewardLine(
                    "Mail",
                    translation?.Get("quest.reward.line.mail", new { npc = giverDisplay })
                        .Default($"{giverDisplay} will send you a letter").ToString()
                        ?? $"Letter: {mr.LetterKey}",
                    payload: mr.LetterKey);

            case Rewards.ShopDiscountReward sd when !string.IsNullOrEmpty(sd.ShopId) && sd.PercentOff > 0 && sd.DurationDays > 0:
                return new QuestRewardLine(
                    "ShopDiscount",
                    translation?.Get("quest.reward.line.shopDiscount", new { percent = sd.PercentOff, days = sd.DurationDays, npc = giverDisplay })
                        .Default($"{giverDisplay} will mark down their shop {sd.PercentOff}% for {sd.DurationDays} day(s)").ToString()
                        ?? $"{sd.PercentOff}% off shop for {sd.DurationDays}d",
                    payload: sd.ShopId,
                    amount: sd.PercentOff,
                    durationDays: sd.DurationDays);

            case Rewards.AnimalPurchaseDiscountReward ap when ap.PercentOff > 0 && ap.DurationDays > 0:
                return new QuestRewardLine(
                    "AnimalPurchaseDiscount",
                    translation?.Get("quest.reward.line.animalPurchaseDiscount", new { percent = ap.PercentOff, days = ap.DurationDays, npc = giverDisplay })
                        .Default($"{giverDisplay} will mark down livestock {ap.PercentOff}% for {ap.DurationDays} day(s)").ToString()
                        ?? $"{ap.PercentOff}% off livestock for {ap.DurationDays}d",
                    amount: ap.PercentOff,
                    durationDays: ap.DurationDays);

            case Rewards.FestivalBiasReward fb when fb.Magnitude > 0:
            {
                string festivalKey = fb.Festival == Rewards.FestivalKind.Luau ? "luau" : "fair";
                return new QuestRewardLine(
                    "FestivalBias",
                    translation?.Get($"quest.reward.line.festivalBias.{festivalKey}", new { npc = giverDisplay })
                        .Default($"{giverDisplay}'s help will tilt the {festivalKey} judging in your favour").ToString()
                        ?? $"Festival bias: {festivalKey} +{fb.Magnitude}",
                    payload: festivalKey,
                    amount: fb.Magnitude);
            }

            case Rewards.FairStarTokensReward ft when ft.Amount > 0:
                return new QuestRewardLine(
                    "FairStarTokens",
                    translation?.Get("quest.reward.line.fairStarTokens", new { amount = ft.Amount, npc = giverDisplay })
                        .Default($"{giverDisplay} will tip you {ft.Amount} extra star tokens on Fair day").ToString()
                        ?? $"+{ft.Amount} star tokens",
                    amount: ft.Amount);

            case Rewards.CustomReward cr when !string.IsNullOrEmpty(cr.Kind):
            {
                var entry = Rewards.RewardApplier.CustomRewards?.Resolve(cr.Kind);
                string summary = string.Empty;
                if (entry?.Summarize != null && translation != null)
                {
                    try { summary = entry.Summarize(cr.Payload ?? string.Empty, giverDisplay, translation); }
                    catch { summary = string.Empty; }
                }
                if (string.IsNullOrEmpty(summary))
                    return null;
                return new QuestRewardLine("Custom", summary, payload: cr.Payload);
            }
        }
        return null;
    }

    public bool TryAdvanceCustomStep(Quest quest, int stepIndex, int amount)
    {
        if (quest is not AdventureQuest aq)
            return false;
        if (amount < 0)
            return aq.TryMarkCustomStepDone(stepIndex);
        return aq.TryAddCustomStepProgress(stepIndex, amount) > 0;
    }

    public IReadOnlyList<CustomBoardSlotInfo> GetCustomBoardSlots(string? boardOwnerUniqueId = null, string? boardName = null)
    {
        var list = new List<CustomBoardSlotInfo>();
        bool filtered = !string.IsNullOrEmpty(boardOwnerUniqueId) && !string.IsNullOrEmpty(boardName);
        string filterKey = (boardOwnerUniqueId ?? "") + "/" + (boardName ?? "");
        foreach (var (boardKey, slot) in CustomBoardSlots.AllSlots())
        {
            if (filtered && !string.Equals(boardKey, filterKey, StringComparison.OrdinalIgnoreCase))
                continue;
            // This API surfaces quest pins only; notice pins carry no Quest/Posting.
            if (slot.Kind != SlotKind.Quest)
                continue;
            int slash = boardKey.IndexOf('/');
            string owner = slash >= 0 ? boardKey.Substring(0, slash) : "";
            string name = slash >= 0 ? boardKey.Substring(slash + 1) : boardKey;
            list.Add(new CustomBoardSlotInfo(
                slot.SyncId,
                slot.Quest!,
                owner,
                name,
                slot.Posting!.DefinitionId,
                slot.Posting!.OwnerUniqueId,
                slot.Accepted));
        }
        return list;
    }

    public IReadOnlyList<DailyBoardSlotInfo> GetDailyBoardSlots()
    {
        var list = new List<DailyBoardSlotInfo>();
        foreach (var slot in BillboardSlots.Slots)
        {
            list.Add(new DailyBoardSlotInfo(
                slot.SyncId,
                slot.Quest,
                slot.Posting.DefinitionId,
                slot.Posting.OwnerUniqueId,
                slot.Accepted));
        }
        return list;
    }

    public int CountUnacceptedDailyBoardQuests()
    {
        int count = 0;
        foreach (var slot in BillboardSlots.Slots)
        {
            if (!slot.Accepted)
                count++;
        }
        return count;
    }

    // Called from ModEntry once StateStore has finished loading. Before this call,
    // the LastFiredDay / OneShotFired lookups return null (no save loaded yet).
    internal void WireState(FrameworkState state) => _state = state;

    internal void ClearState() => _state = null;

    internal CustomStepRegistry CustomSteps => _customSteps;
    internal ReportBackRegistry ReportBack => _reportBack;
    internal string? ResolveOwner(Quest quest) => ResolveQuestOwner(quest);
    internal CustomTriggerRegistry CustomTriggers => _customTriggers;
    internal CustomRewardRegistry CustomRewards => _customRewards;
    internal CustomConditionRegistry CustomConditions => _customConditions;
    internal CustomBoardQuestRegistry CustomBoardQuests => _customBoardQuests;

    public void RefreshOffers() => _refreshOffers();

    public string? PickDispatchNpc(string role) => _dispatch.Pick(role);

    public IReadOnlyList<string> GetDispatchPool(string role) => _dispatch.ResolvePool(role);

    public IReadOnlyList<string> GetMetHumanNpcs() => DispatchRegistry.MetHumanNpcs();

    public void RegisterCombatFood(string itemId, int? magnitude = null) => _combatFood.Register(itemId, magnitude);

    public IReadOnlyList<string> GetCombatFoodPool() => _combatFood.Pool;

    public int? GetCombatFoodMagnitude(string qualifiedItemId) => _combatFood.GetMagnitude(qualifiedItemId);

    // modData keys that mirror the in-memory _managed entry onto the Quest itself,
    // so the owner / definition id survive a save + reload (the ConditionalWeakTable
    // does not, since the deserialized Quest is a fresh instance).
    internal const string ModDataOwnerKey = "RafiaBee.MoreQuestsFramework/Owner";
    internal const string ModDataDefinitionKey = "RafiaBee.MoreQuestsFramework/Definition";
    // Records the CanBeCancelled intent on the quest so the save-load backfill can tell a
    // quest that predates the field (no marker, bring up to the cancellable default) apart
    // from one an author deliberately opted out of cancelling (marker "false", leave alone).
    internal const string ModDataCancellableKey = "RafiaBee.MoreQuestsFramework/Cancellable";

    internal void TrackPosted(Quest quest, string ownerUniqueId, string definitionId)
    {
        if (quest == null)
            return;
        if (_managed.TryGetValue(quest, out _))
            return;
        _managed.Add(quest, new ManagedQuest(ownerUniqueId, definitionId));
        WriteOwnerToModData(quest, ownerUniqueId, definitionId);
    }

    internal bool TryGetManaged(Quest quest, out ManagedQuest info)
    {
        if (quest == null)
        {
            info = default!;
            return false;
        }
        if (_managed.TryGetValue(quest, out var found))
        {
            info = found;
            return true;
        }
        // Fall back to modData. Save-reloaded quests deserialise as fresh instances
        // that fall out of _managed, but their modData survives the round trip.
        // Re-register them so the next call hits the fast path.
        if (TryReadOwnerFromModData(quest, out string? owner, out string? defId))
        {
            info = new ManagedQuest(owner!, defId!);
            _managed.Add(quest, info);
            return true;
        }
        info = default!;
        return false;
    }

    private static void WriteOwnerToModData(Quest quest, string ownerUniqueId, string definitionId)
    {
        if (quest?.modData == null) return;
        if (!string.IsNullOrEmpty(ownerUniqueId))
            quest.modData[ModDataOwnerKey] = ownerUniqueId;
        if (!string.IsNullOrEmpty(definitionId))
            quest.modData[ModDataDefinitionKey] = definitionId;
    }

    private static bool TryReadOwnerFromModData(Quest quest, out string? ownerUniqueId, out string? definitionId)
    {
        ownerUniqueId = null;
        definitionId = null;
        if (quest?.modData == null) return false;
        if (!quest.modData.TryGetValue(ModDataOwnerKey, out var owner) || string.IsNullOrEmpty(owner))
            return false;
        if (!quest.modData.TryGetValue(ModDataDefinitionKey, out var defId) || string.IsNullOrEmpty(defId))
            return false;
        ownerUniqueId = owner;
        definitionId = defId;
        return true;
    }

    private string? ResolveQuestOwner(Quest quest)
        => TryGetManaged(quest, out var info) ? info.OwnerUniqueId : null;

    internal void FireRegistrationOpen() => RegistrationOpen?.Invoke(this, EventArgs.Empty);
    internal void FireRegistrationClosed() => RegistrationClosed?.Invoke(this, EventArgs.Empty);
    internal void FireQuestAccepted(Quest q, ManagedQuest info)
        => QuestAccepted?.Invoke(this, new QuestAcceptedArgs(q, info.OwnerUniqueId, info.DefinitionId));
    internal void FireQuestCompleted(Quest q, ManagedQuest info)
        => QuestCompleted?.Invoke(this, new QuestCompletedArgs(q, info.OwnerUniqueId, info.DefinitionId));
    internal void FireQuestRemoved(Quest q, ManagedQuest info, QuestRemovalReason reason)
        => QuestRemoved?.Invoke(this, new QuestRemovedArgs(q, info.OwnerUniqueId, info.DefinitionId, reason));
    internal void FireDayRefreshed(int dailyCount, int mailCount)
        => DayRefreshed?.Invoke(this, new DayRefreshedArgs(dailyCount, mailCount));
    internal void FireQuestSkippedToday(string defId, string ownerUniqueId, TriggerSource source, QuestSkipReason reason)
        => QuestSkippedToday?.Invoke(this, new QuestSkippedArgs(defId, ownerUniqueId, source, reason));
    internal void FireFrameworkShuttingDown()
        => FrameworkShuttingDown?.Invoke(this, EventArgs.Empty);
    internal void FireCropHarvested(CropHarvestInfo info)
        => CropHarvested?.Invoke(this, info);

    internal sealed class ManagedQuest
    {
        public string OwnerUniqueId { get; }
        public string DefinitionId { get; }
        public ManagedQuest(string ownerUniqueId, string definitionId)
        {
            OwnerUniqueId = ownerUniqueId;
            DefinitionId = definitionId;
        }
    }
}
