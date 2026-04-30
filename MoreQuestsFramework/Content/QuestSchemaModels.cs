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

    /// Optional override of the delivery channel. Defaults vary by source: DailyBoard
    /// posts to the help-wanted board, NpcDialogue queues for the next chat, every
    /// other source defaults to `Mail`. Accepts `Mail`, `NpcDialogue`, or `DailyBoard`.
    public string? Delivery { get; set; }

    // Phase 6 trigger-specific options. See plan.md §5.4 for the full grammar.

    /// Periodic: in-game days between fires. Required for `Source: Periodic`.
    public int? EveryDays { get; set; }

    /// DateLocked: in-game date as `"<season> <day>"`, e.g. `"spring 8"`.
    public string? Date { get; set; }

    /// DateLocked: re-arms the trigger every in-game year. Default false (one-shot per save).
    public bool RepeatYearly { get; set; }

    /// DateRange: inclusive window endpoints, both `"<season> <day>"`.
    public string? From { get; set; }
    public string? To { get; set; }

    /// OneShot: predicate that must be true for the trigger to fire (once per save).
    /// Recognised forms: `FirstStat <name> >= <n>`, `FirstShipped <itemId>`,
    /// `FirstItemOwned <itemId>`.
    public string? When { get; set; }

    /// BuildingBuilt: farm building type to watch for, e.g. `"Coop"` or `"Slime Hutch"`.
    public string? Building { get; set; }

    /// BuildingBuilt / MailReceived: number of in-game days to wait before firing
    /// after the trigger event. 0 = same day.
    public int? DayDelay { get; set; }

    /// MailReceived: mail flag whose appearance in `mailReceived` triggers the quest.
    public string? Flag { get; set; }

    /// WeatherForecast: weather id (`Sun`, `Rain`, `Storm`, `Snow`, `Wind`) or the
    /// adjective alias (`sunny`, `rainy`, ...). Matches `Game1.weatherForTomorrow`.
    public string? Weather { get; set; }

    /// NpcDialogue: target NPC. Quest is queued and pushed into the journal the next
    /// time the player speaks with this NPC.
    public string? Npc { get; set; }
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
