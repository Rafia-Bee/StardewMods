using System;
using System.Collections.Generic;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using MoreQuestsFramework.Triggers;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Content;

// Modes: Generator-referenced (C# owns Build), Declarative single-step (built from
// Objective), or Adventure (built from Steps[]).
internal sealed class JsonQuestDefinition : IQuestDefinition
{
    private readonly QuestDef _def;
    private readonly string _ownerUniqueId;
    private readonly ITranslationHelper _translation;
    private readonly GeneratorRegistry _generators;
    private readonly IMonitor _monitor;
    private readonly Func<string, int?>? _cooldownTierResolver;
    private readonly int _cooldownDaysFallback;

    public JsonQuestDefinition(
        QuestDef def,
        string ownerUniqueId,
        ITranslationHelper translation,
        GeneratorRegistry generators,
        IMonitor monitor,
        Func<string, int?>? cooldownTierResolver = null)
    {
        _def = def;
        _ownerUniqueId = ownerUniqueId;
        _translation = translation;
        _generators = generators;
        _monitor = monitor;
        _cooldownTierResolver = cooldownTierResolver;

        // Raw JSON Name preserves existing QuestWeights config keys for in-flight
        // saves. Collisions are logged + rejected by QuestRegistry.Register.
        Id = def.Name!;
        Category = ParseEnum(def.Category, QuestCategory.Social);
        Source = ParseSource(def.Trigger?.Source);
        Kind = ResolveDelivery(Source, def.Trigger?.Delivery);
        Trigger = BuildTriggerInfo(def.Trigger);
        DefaultWeight = def.Trigger?.Weight ?? 0;
        MaxPerDay = def.Trigger?.MaxPerDay ?? 1;
        _cooldownDaysFallback = def.Trigger?.CooldownDays ?? 0;
    }

    public string Id { get; }
    public QuestCategory Category { get; }
    public PostingKind Kind { get; }
    public TriggerSource Source { get; }
    public TriggerInfo Trigger { get; }
    public int DefaultWeight { get; }
    public int MaxPerDay { get; }

    // Evaluated per access so live GMCM edits to a tiered cooldown apply without reload.
    public int CooldownDays
    {
        get
        {
            string? tier = _def.Trigger?.CooldownTier;
            if (!string.IsNullOrEmpty(tier) && _cooldownTierResolver != null)
            {
                int? resolved = _cooldownTierResolver(tier);
                if (resolved.HasValue)
                    return resolved.Value;
            }
            return _cooldownDaysFallback;
        }
    }
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
                Targets = ResolveTargets(sd.Targets, giver, ctx.Dispatch),
                Items = new List<string>(sd.Items),
                Count = Math.Max(1, sd.Count),
                MinQuality = Math.Max(0, sd.MinQuality),
                AllowDecorShipping = sd.AllowDecorShipping,
                LocationName = sd.LocationName ?? string.Empty,
                MinSize = Math.Max(0, sd.MinSize),
                Weather = sd.Weather ?? string.Empty
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
        posting.Consequence = BuildConsequence(_def.Consequence);
        return posting;
    }

    // $giver rewrites to the giver name. $dispatcher.<role> or $dispatcher.<role>[N]
    // resolves to N distinct NPCs from the dispatch role. Resolved once at creation
    // time so picks are stable across save/load.
    private static List<string> ResolveTargets(List<string> targets, string giver, Dispatch.DispatchRegistry dispatch)
    {
        var resolved = new List<string>(targets.Count);
        foreach (var t in targets)
        {
            if (string.Equals(t, "$giver", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(giver))
                    resolved.Add(giver);
            }
            else if (t.StartsWith("$dispatcher.", StringComparison.OrdinalIgnoreCase))
            {
                ResolveDispatcherToken(t, dispatch, resolved);
            }
            else
            {
                resolved.Add(t);
            }
        }
        return resolved;
    }

    private static void ResolveDispatcherToken(string token, Dispatch.DispatchRegistry dispatch, List<string> sink)
    {
        string body = token.Substring("$dispatcher.".Length);
        int n = 1;
        string role = body;
        int bracketAt = body.IndexOf('[');
        if (bracketAt > 0 && body.EndsWith("]"))
        {
            role = body.Substring(0, bracketAt);
            string countStr = body.Substring(bracketAt + 1, body.Length - bracketAt - 2);
            if (!int.TryParse(countStr, out n) || n < 1)
                n = 1;
        }

        var pool = dispatch.ResolvePool(role);
        if (pool.Count == 0)
            return;

        // Sample without replacement so "talk to 3 different villagers" can't be
        // satisfied by the same NPC three times.
        var indices = new List<int>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
            indices.Add(i);
        int take = Math.Min(n, pool.Count);
        for (int i = 0; i < take; i++)
        {
            int pick = StardewValley.Game1.random.Next(indices.Count);
            sink.Add(pool[indices[pick]]);
            indices.RemoveAt(pick);
        }
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
            CustomQuestType = obj.Custom ?? string.Empty,
            QuestGiver = _def.Giver ?? string.Empty,
            ObjectiveItemId = primary,
            ObjectiveItemName = primary,
            ObjectiveQuantity = Math.Max(1, obj.Count),
            TargetMonster = obj.TargetMonster,
            CatchLocationName = obj.LocationName ?? string.Empty,
            CatchMinSize = Math.Max(0, obj.MinSize),
            CatchWeather = obj.Weather ?? string.Empty,
            DeadlineDays = Difficulty.Deadline(ParseEnum(_def.DeadlineDays, DeadlineKind.Short), ctx.Config),
            Title = Resolve(_def.Title),
            Description = Resolve(_def.Description),
            CurrentObjective = Resolve(_def.CurrentObjective),
            TargetMessage = Resolve(_def.TargetMessage)
        };
        for (int i = 1; i < obj.Item.Count; i++)
            posting.AlternativeObjectiveItemIds.Add(obj.Item[i]);
        BuildRewards(posting.Rewards, _def.Rewards);
        posting.Consequence = BuildConsequence(_def.Consequence);
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
                case "shopdiscount":
                    if (string.IsNullOrEmpty(r.ShopId))
                        _monitor.Log($"Quest '{Id}': ShopDiscount reward needs a ShopId.", LogLevel.Warn);
                    else
                        sink.Add(new ShopDiscountReward(
                            r.ShopId,
                            r.PercentOff,
                            Math.Max(1, r.DurationDays),
                            r.AppliesTo.Count > 0 ? new List<string>(r.AppliesTo) : null,
                            r.GuaranteedStock));
                    break;
                case "animalpurchasediscount":
                    sink.Add(new AnimalPurchaseDiscountReward(r.PercentOff, Math.Max(1, r.DurationDays)));
                    break;
                case "festivalbias":
                    if (!TryParseEnum(r.Festival, out FestivalKind fk))
                        _monitor.Log($"Quest '{Id}': FestivalBias reward needs Festival = Luau or Fair.", LogLevel.Warn);
                    else
                        sink.Add(new FestivalBiasReward(fk, r.Magnitude));
                    break;
                case "fairstartokens":
                    sink.Add(new FairStarTokensReward(r.Amount));
                    break;
                case "custom":
                    if (string.IsNullOrEmpty(r.Custom))
                        _monitor.Log($"Quest '{Id}': Custom reward needs a Custom = handler id.", LogLevel.Warn);
                    else
                    {
                        string handlerId = r.Custom.Contains('/') ? r.Custom : $"{_ownerUniqueId}/{r.Custom}";
                        sink.Add(new CustomReward(handlerId, r.Payload ?? string.Empty));
                    }
                    break;
                default:
                    _monitor.Log($"Quest '{Id}': unknown reward kind '{r.Kind}'.", LogLevel.Warn);
                    break;
            }
        }
    }

    private ConsequenceSpec? BuildConsequence(ConsequenceDef? def)
    {
        if (def == null)
            return null;
        if (!Enum.TryParse<ConsequenceTier>(def.Tier, ignoreCase: true, out var tier) || tier == ConsequenceTier.Tier0)
        {
            if (!string.IsNullOrEmpty(def.Tier) && !string.Equals(def.Tier, "Tier0", StringComparison.OrdinalIgnoreCase))
                _monitor.Log($"Quest '{Id}': unknown Consequence Tier '{def.Tier}'; ignoring.", LogLevel.Warn);
            return null;
        }
        if (!Enum.TryParse<ConsequenceSource>(def.Source, ignoreCase: true, out var source))
        {
            _monitor.Log($"Quest '{Id}': unknown Consequence Source '{def.Source}'; defaulting to GiftTastes.", LogLevel.Warn);
            source = ConsequenceSource.GiftTastes;
        }

        var spec = new ConsequenceSpec
        {
            Tier = tier,
            Source = source,
            Subject = def.Subject ?? string.Empty,
            Targets = new List<string>(def.Targets),
            GoldDelta = def.GoldDelta,
            FriendshipOverride = def.FriendshipOverride,
            FriendshipPerDay = def.FriendshipPerDay,
            ChainDays = def.ChainDays,
            LovedLine = Resolve(def.LovedLine),
            HatedLine = Resolve(def.HatedLine)
        };
        foreach (var line in def.ChainLines)
            spec.ChainLines.Add(Resolve(line));
        return spec;
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
            "custom" => TriggerSource.Custom,
            _ => TriggerSource.DailyBoard
        };

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
            Npc: t.Npc,
            StartDate: t.StartDate,
            Duration: t.Duration,
            Weight: t.Weight,
            Custom: t.Custom);
    }

    private static BoardQuestType ParseObjectiveKind(string kind)
        => kind?.ToLowerInvariant() switch
        {
            "fish" or "fishing" or "catch" => BoardQuestType.Fishing,
            "slay" or "slaymonster" => BoardQuestType.SlayMonster,
            "resource" or "collect" or "resourcecollection" => BoardQuestType.ResourceCollection,
            "socialize" => BoardQuestType.Socialize,
            "ship" => BoardQuestType.Ship,
            "custom" => BoardQuestType.Custom,
            _ => BoardQuestType.ItemDelivery
        };

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static bool TryParseEnum<T>(string? value, out T parsed) where T : struct, Enum
    {
        if (!string.IsNullOrEmpty(value) && Enum.TryParse<T>(value, ignoreCase: true, out parsed))
            return true;
        parsed = default;
        return false;
    }

    // Accepts Today (default) or Tomorrow / NextDay.
    private static MailWhen ParseMailWhen(string? value)
        => value?.ToLowerInvariant() switch
        {
            "tomorrow" or "nextday" => MailWhen.Tomorrow,
            _ => MailWhen.Today
        };
}
