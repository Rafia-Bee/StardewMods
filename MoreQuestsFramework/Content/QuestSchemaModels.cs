using System.Collections.Generic;

namespace MoreQuestsFramework.Content;

/// Plain DTO shapes for `quests.json`. Deserialised directly from JSON via
/// SMAPI's `IDataHelper.ReadJsonFile` / `IContentPack.ReadJsonFile`.
///
/// This is the Phase 4 subset of plan.md §5: top-level `Schema` + `Quests`,
/// and a `QuestDef` shape that supports both generator-referenced quests
/// (Phase 4's primary migration target) and fully-declarative single-step
/// quests with fixed item + giver. Multi-step (`Steps`) and `Boards` are
/// deferred to later phases.
public sealed class QuestPackDocument
{
    public string Schema { get; set; } = "1.0";
    public List<QuestDef> Quests { get; set; } = new();
}

public sealed class QuestDef
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Tier { get; set; }

    public TriggerDef? Trigger { get; set; }

    /// If present, this name resolves to a generator registered via
    /// `RegisterGenerator(...)`. The generator owns full Build() control.
    /// All other fields below (Title/Description/Objective/Rewards) are
    /// ignored when Generator is set.
    public string? Generator { get; set; }

    public string? Giver { get; set; }
    public ObjectiveDef? Objective { get; set; }

    public string? DeadlineDays { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CurrentObjective { get; set; }
    public string? TargetMessage { get; set; }

    public List<RewardDef> Rewards { get; set; } = new();
}

public sealed class TriggerDef
{
    public string Source { get; set; } = "DailyBoard";
    public int? Weight { get; set; }
    public int? MaxPerDay { get; set; }
    public int? CooldownDays { get; set; }
    public Dictionary<string, string>? Available { get; set; }
}

public sealed class ObjectiveDef
{
    /// Deliver | Resource | Fish | Slay
    public string Kind { get; set; } = "Deliver";
    public string? Item { get; set; }
    public int Count { get; set; } = 1;
    public string? TargetMonster { get; set; }
}

public sealed class RewardDef
{
    /// Money | Friendship | Object | Recipe | Mail
    public string Kind { get; set; } = "Money";

    public int Amount { get; set; }
    public string? Npc { get; set; }
    public int Points { get; set; }
    public string? Item { get; set; }
    public int Count { get; set; } = 1;
    public string? Recipe { get; set; }
    public string? RecipeKind { get; set; }
    public string? Letter { get; set; }
    public string? When { get; set; }
}
