using System;
using System.Collections.Generic;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Content;

/// `IQuestDefinition` adapter that feeds the existing pipeline from a JSON
/// `QuestDef`. Two modes:
///
/// 1. Generator-referenced: `def.Generator` is set. Build() resolves the
///    generator at quest-creation time and calls it. The C# side owns Title /
///    Description / Objective / Rewards.
/// 2. Declarative single-step: `def.Objective` is set. Build() constructs a
///    `QuestPosting` directly from the JSON fields, with `{i18n:...}` tokens
///    resolved against the owning pack's translation helper.
internal sealed class JsonQuestDefinition : IQuestDefinition
{
    private readonly QuestDef _def;
    private readonly string _ownerUniqueId;
    private readonly ITranslationHelper _translation;
    private readonly GeneratorRegistry _generators;
    private readonly IMonitor _monitor;

    public JsonQuestDefinition(
        QuestDef def,
        string ownerUniqueId,
        ITranslationHelper translation,
        GeneratorRegistry generators,
        IMonitor monitor)
    {
        _def = def;
        _ownerUniqueId = ownerUniqueId;
        _translation = translation;
        _generators = generators;
        _monitor = monitor;

        // Use the JSON Name verbatim as the registry ID. Authors must pick
        // names that won't collide with other packs (e.g. "MyMod.QuestX").
        // Auto-prefixing by ownerUniqueId was originally slated for Phase 5
        // but deferred — Phase 5 namespaces only generator names, not quest
        // ids. Keeping the raw name preserves the existing `QuestWeights`
        // config keys ("Mining.BarDelivery" etc.) for in-flight saves and
        // for the framework's own GMCM page. Auto-prefixing will land at
        // Phase 10 alongside the v1.0 API freeze; collisions until then are
        // logged + rejected by `QuestRegistry.Register`.
        Id = def.Name!;
        Category = ParseEnum(def.Category, QuestCategory.Social);
        Source = ParseSource(def.Trigger?.Source);
        Kind = ResolveDelivery(Source, def.Trigger?.Delivery);
        Trigger = BuildTriggerInfo(def.Trigger);
        DefaultWeight = def.Trigger?.Weight ?? 0;
        MaxPerDay = def.Trigger?.MaxPerDay ?? 1;
        CooldownDays = def.Trigger?.CooldownDays ?? 0;
    }

    public string Id { get; }
    public QuestCategory Category { get; }
    public PostingKind Kind { get; }
    public TriggerSource Source { get; }
    public TriggerInfo Trigger { get; }
    public int DefaultWeight { get; }
    public int MaxPerDay { get; }
    public int CooldownDays { get; }
    public string OwnerUniqueId => _ownerUniqueId;

    public bool IsAvailable(QuestContext ctx)
    {
        var available = _def.Trigger?.Available;
        if (available == null || available.Count == 0)
            return true;
        return ConditionEvaluator.Evaluate(available, ctx.Helper.ModRegistry);
    }

    public QuestPosting? Build(QuestContext ctx)
    {
        if (!string.IsNullOrEmpty(_def.Generator))
            return BuildFromGenerator(ctx);
        if (_def.Steps != null && _def.Steps.Count > 0)
            return BuildAdventure(ctx);
        if (_def.Objective != null)
            return BuildDeclarative(ctx);

        _monitor.Log(
            $"Quest '{Id}' has neither a Generator, Steps, nor an Objective; nothing to build.",
            LogLevel.Warn);
        return null;
    }

    private QuestPosting? BuildFromGenerator(QuestContext ctx)
    {
        var gen = _generators.Resolve(_ownerUniqueId, _def.Generator!);
        if (gen == null)
        {
            _monitor.Log(
                $"Quest '{Id}' references unknown generator '{_def.Generator}'. " +
                $"Owner '{_ownerUniqueId}' did not call RegisterGenerator for it.",
                LogLevel.Warn);
            return null;
        }
        var posting = gen(ctx);
        if (posting != null && string.IsNullOrEmpty(posting.DefinitionId))
            posting.DefinitionId = Id;
        return posting;
    }

    private QuestPosting BuildAdventure(QuestContext ctx)
    {
        string giver = _def.Giver ?? string.Empty;
        var steps = new List<AdventureStepState>(_def.Steps.Count);
        foreach (var sd in _def.Steps)
        {
            if (!Enum.TryParse<AdventureStepKind>(sd.Kind, ignoreCase: true, out var kind))
            {
                _monitor.Log($"Quest '{Id}': step '{sd.Name}' has unknown Kind '{sd.Kind}'; skipping.", LogLevel.Warn);
                continue;
            }
            steps.Add(new AdventureStepState
            {
                Name = sd.Name ?? string.Empty,
                Kind = kind,
                Description = Resolve(sd.Description),
                Requires = new List<string>(sd.Requires),
                Targets = ResolveTargets(sd.Targets, giver),
                Items = new List<string>(sd.Items),
                Count = Math.Max(1, sd.Count),
                MinQuality = Math.Max(0, sd.MinQuality)
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver, completionDialogue: Resolve(_def.TargetMessage));

        var posting = new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = ParseEnum(_def.Tier, DifficultyTier.Beginner),
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(ParseEnum(_def.DeadlineDays, DeadlineKind.Short), ctx.Config),
            Title = Resolve(_def.Title),
            Description = Resolve(_def.Description),
            CurrentObjective = Resolve(_def.CurrentObjective),
            TargetMessage = Resolve(_def.TargetMessage),
            PreBuiltQuest = quest
        };
        BuildRewards(posting.Rewards, _def.Rewards);
        return posting;
    }

    /// `$giver` rewrites to the giver name; everything else passes through. Dispatcher
    /// tokens (`$dispatcher.<role>`) land in 7c — until then they pass through verbatim
    /// and the step won't match any NPC, which is the safe default.
    private static List<string> ResolveTargets(List<string> targets, string giver)
    {
        var resolved = new List<string>(targets.Count);
        foreach (var t in targets)
        {
            if (string.Equals(t, "$giver", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(giver))
                    resolved.Add(giver);
            }
            else
            {
                resolved.Add(t);
            }
        }
        return resolved;
    }

    private QuestPosting BuildDeclarative(QuestContext ctx)
    {
        var obj = _def.Objective!;
        string primary = obj.Item.Count > 0 ? obj.Item[0] : string.Empty;
        var posting = new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = ParseEnum(_def.Tier, DifficultyTier.Beginner),
            QuestType = ParseObjectiveKind(obj.Kind),
            QuestGiver = _def.Giver ?? string.Empty,
            ObjectiveItemId = primary,
            ObjectiveItemName = primary,
            ObjectiveQuantity = Math.Max(1, obj.Count),
            TargetMonster = obj.TargetMonster,
            DeadlineDays = Difficulty.Deadline(ParseEnum(_def.DeadlineDays, DeadlineKind.Short), ctx.Config),
            Title = Resolve(_def.Title),
            Description = Resolve(_def.Description),
            CurrentObjective = Resolve(_def.CurrentObjective),
            TargetMessage = Resolve(_def.TargetMessage)
        };
        for (int i = 1; i < obj.Item.Count; i++)
            posting.AlternativeObjectiveItemIds.Add(obj.Item[i]);
        BuildRewards(posting.Rewards, _def.Rewards);
        return posting;
    }

    private void BuildRewards(List<RewardSpec> sink, List<RewardDef> defs)
    {
        foreach (var r in defs)
        {
            switch (r.Kind?.ToLowerInvariant())
            {
                case "money":
                    sink.Add(new MoneyReward(r.Amount));
                    break;
                case "friendship":
                    if (!string.IsNullOrEmpty(r.Npc))
                        sink.Add(new FriendshipReward(r.Npc, r.Points));
                    break;
                case "object":
                    if (!string.IsNullOrEmpty(r.Item))
                        sink.Add(new ObjectReward(r.Item, Math.Max(1, r.Count)));
                    break;
                case "recipe":
                    if (!string.IsNullOrEmpty(r.Recipe))
                    {
                        var rk = ParseEnum(r.RecipeKind, MoreQuestsFramework.Rewards.RecipeKind.Cooking);
                        sink.Add(new RecipeReward(r.Recipe, rk));
                    }
                    break;
                case "mail":
                    if (!string.IsNullOrEmpty(r.Letter))
                    {
                        var when = ParseMailWhen(r.When);
                        sink.Add(new MailReward(r.Letter, when));
                    }
                    break;
                default:
                    _monitor.Log($"Quest '{Id}': unknown reward kind '{r.Kind}'.", LogLevel.Warn);
                    break;
            }
        }
    }

    private string Resolve(string? input)
        => I18nResolver.Resolve(input ?? string.Empty, _translation);

    private static TriggerSource ParseSource(string? source)
        => source?.ToLowerInvariant() switch
        {
            "mail" => TriggerSource.Mail,
            "periodic" => TriggerSource.Periodic,
            "datelocked" => TriggerSource.DateLocked,
            "daterange" => TriggerSource.DateRange,
            "oneshot" => TriggerSource.OneShot,
            "buildingbuilt" => TriggerSource.BuildingBuilt,
            "mailreceived" => TriggerSource.MailReceived,
            "weatherforecast" => TriggerSource.WeatherForecast,
            "npcdialogue" => TriggerSource.NpcDialogue,
            "specialorder" => TriggerSource.SpecialOrder,
            "customboard" => TriggerSource.CustomBoard,
            _ => TriggerSource.DailyBoard
        };

    /// Picks the delivery channel (PostingKind) based on the trigger source. Authors
    /// can override via `Trigger.Delivery` for unusual combinations like a DateLocked
    /// quest delivered via NPC dialogue.
    private static PostingKind ResolveDelivery(TriggerSource source, string? deliveryOverride)
    {
        if (!string.IsNullOrEmpty(deliveryOverride))
        {
            return deliveryOverride.ToLowerInvariant() switch
            {
                "mail" => PostingKind.Mail,
                "npcdialogue" => PostingKind.NpcDialogue,
                "dailyboard" => PostingKind.DailyBoard,
                "specialorder" => PostingKind.SpecialOrder,
                _ => PostingKind.Mail
            };
        }
        return source switch
        {
            TriggerSource.DailyBoard => PostingKind.DailyBoard,
            TriggerSource.NpcDialogue => PostingKind.NpcDialogue,
            TriggerSource.SpecialOrder => PostingKind.SpecialOrder,
            TriggerSource.CustomBoard => PostingKind.DailyBoard,
            _ => PostingKind.Mail
        };
    }

    private static TriggerInfo BuildTriggerInfo(TriggerDef? t)
    {
        if (t == null)
            return TriggerInfo.Default;
        return new TriggerInfo(
            EveryDays: t.EveryDays,
            Date: t.Date,
            RepeatYearly: t.RepeatYearly,
            From: t.From,
            To: t.To,
            When: t.When,
            Building: t.Building,
            DayDelay: t.DayDelay,
            Flag: t.Flag,
            Weather: t.Weather,
            Npc: t.Npc);
    }

    private static BoardQuestType ParseObjectiveKind(string kind)
        => kind?.ToLowerInvariant() switch
        {
            "fish" or "fishing" or "catch" => BoardQuestType.Fishing,
            "slay" or "slaymonster" => BoardQuestType.SlayMonster,
            "resource" or "collect" or "resourcecollection" => BoardQuestType.ResourceCollection,
            "socialize" => BoardQuestType.Socialize,
            "ship" => BoardQuestType.Ship,
            _ => BoardQuestType.ItemDelivery
        };

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    /// `When` field on a mail reward. Accepts `Today` (default — letter arrives in today's
    /// mailbox immediately) or `Tomorrow` / `NextDay` (letter is queued for the next morning
    /// via `mailForTomorrow`). The `NextDay` alias matches plan §7b's authoring convention
    /// without churning the `MailWhen` enum.
    private static MailWhen ParseMailWhen(string? value)
        => value?.ToLowerInvariant() switch
        {
            "tomorrow" or "nextday" => MailWhen.Tomorrow,
            _ => MailWhen.Today
        };
}
