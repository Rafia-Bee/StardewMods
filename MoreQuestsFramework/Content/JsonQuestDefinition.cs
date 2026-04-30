using System;
using System.Collections.Generic;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Rewards;
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

        // Use the JSON Name verbatim as the registry ID. Authors should pick
        // names that won't collide with other packs (e.g. "MyMod.QuestX").
        // Phase 5 will introduce automatic owner-prefix namespacing on the
        // public API; until then keeping the raw name preserves the existing
        // config keys (`QuestWeights["Mining.BarDelivery"]`) for in-flight
        // saves migrated from the C# version.
        Id = def.Name!;
        Category = ParseEnum(def.Category, QuestCategory.Social);
        Kind = ParseKind(def.Trigger?.Source);
        DefaultWeight = def.Trigger?.Weight ?? 0;
        MaxPerDay = def.Trigger?.MaxPerDay ?? 1;
        CooldownDays = def.Trigger?.CooldownDays ?? 0;
    }

    public string Id { get; }
    public QuestCategory Category { get; }
    public PostingKind Kind { get; }
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
        if (_def.Objective != null)
            return BuildDeclarative(ctx);

        _monitor.Log(
            $"Quest '{Id}' has neither a Generator nor an Objective; nothing to build.",
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

    private QuestPosting BuildDeclarative(QuestContext ctx)
    {
        var obj = _def.Objective!;
        var posting = new QuestPosting
        {
            DefinitionId = Id,
            Category = Category,
            Tier = ParseEnum(_def.Tier, DifficultyTier.Beginner),
            QuestType = ParseObjectiveKind(obj.Kind),
            QuestGiver = _def.Giver ?? string.Empty,
            ObjectiveItemId = obj.Item ?? string.Empty,
            ObjectiveItemName = obj.Item ?? string.Empty,
            ObjectiveQuantity = Math.Max(1, obj.Count),
            TargetMonster = obj.TargetMonster,
            DeadlineDays = Difficulty.Deadline(ParseEnum(_def.DeadlineDays, DeadlineKind.Short), ctx.Config),
            Title = Resolve(_def.Title),
            Description = Resolve(_def.Description),
            CurrentObjective = Resolve(_def.CurrentObjective),
            TargetMessage = Resolve(_def.TargetMessage)
        };
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
                        var when = ParseEnum(r.When, MailWhen.Today);
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

    private static PostingKind ParseKind(string? source)
        => source?.ToLowerInvariant() switch
        {
            "mail" => PostingKind.Mail,
            "specialorder" => PostingKind.SpecialOrder,
            "npcdialogue" => PostingKind.NpcDialogue,
            _ => PostingKind.DailyBoard
        };

    private static BoardQuestType ParseObjectiveKind(string kind)
        => kind?.ToLowerInvariant() switch
        {
            "fish" or "fishing" or "catch" => BoardQuestType.Fishing,
            "slay" or "slaymonster" => BoardQuestType.SlayMonster,
            "resource" or "collect" or "resourcecollection" => BoardQuestType.ResourceCollection,
            "socialize" => BoardQuestType.Socialize,
            _ => BoardQuestType.ItemDelivery
        };

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
