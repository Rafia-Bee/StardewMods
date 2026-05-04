using System.Collections.Generic;
using Newtonsoft.Json;

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

    /// Multi-step ("Adventure") quests. When set, the quest is built as an `AdventureQuest`
    /// instead of a single-objective vanilla wrapper. `Objective` is ignored if `Steps` is
    /// non-empty. Step `Requires[]` enforces ordering; `$giver` in `Targets[]` is rewritten
    /// to the resolved giver name at quest-creation time.
    public List<StepDef> Steps { get; set; } = new();

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

    /// SpecialOrder: in-game date as `"<season> <day>"` when the order is offered on the
    /// SpecialOrders board. Cooldown gates re-fires across years.
    public string? StartDate { get; set; }

    /// SpecialOrder: window length the order stays on the board. Accepts vanilla's
    /// QuestDuration enum names — `OneDay`, `TwoDays`, `ThreeDays`, `Week`, `TwoWeeks`,
    /// `Month`. Defaults to `Week` when omitted. Maps to vanilla's `SetDuration`.
    public string? Duration { get; set; }
}

public sealed class ObjectiveDef
{
    /// Deliver | Resource | Fish | Slay | Ship
    public string Kind { get; set; } = "Deliver";

    /// Item id(s) accepted by the objective. Authors may write either a single string
    /// (`"Item": "(O)787"`) or a string array (`"Item": ["(O)787", "(O)382"]`); the
    /// converter normalises both shapes to a `List<string>`. The first entry is the
    /// "primary" id used for journal text + display; any extra entries are OR-alternatives
    /// matched at completion time.
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string> Item { get; set; } = new();
    public int Count { get; set; } = 1;
    public string? TargetMonster { get; set; }
}

/// One step within an Adventure quest's `Steps[]` list. Mirrors `AdventureStepState` but
/// only carries the authoring-time fields (no `Progress` / `Done` — those start at zero).
public sealed class StepDef
{
    public string? Name { get; set; }

    /// `Deliver` | `Talk` | `Gift` | `Catch` | `Slay` | `Ship` | `Visit` | `Build` |
    /// `ReachLevel` | `Plant` | `Collect` | `ClearDebris` | `ClearWeeds` | `Custom`. 7a
    /// implements `Deliver`, `Talk`, and `Gift`; the rest land in 7b/7c.
    public string Kind { get; set; } = "Talk";

    public string? Description { get; set; }

    /// Other step `Name`s that must be done before this one becomes active.
    public List<string> Requires { get; set; } = new();

    /// NPC names / location names / monster types depending on Kind. Supports `$giver` —
    /// rewritten to the resolved giver at quest-creation time.
    public List<string> Targets { get; set; } = new();

    /// Item ids accepted for `Deliver` / `Gift` / `Ship` / `Collect`. May be set as a
    /// single string OR a string array in JSON; the parser normalises to a list.
    public List<string> Items { get; set; } = new();

    public int Count { get; set; } = 1;
    public int MinQuality { get; set; }

    /// `Ship`-step opt-in to the framework's furniture / decor shipping bypass. While the
    /// parent quest is in the active log and any step has this flag set, vanilla's
    /// shipping ban is lifted via a gated Harmony postfix on `Object.canBeShipped`.
    public bool AllowDecorShipping { get; set; }
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
