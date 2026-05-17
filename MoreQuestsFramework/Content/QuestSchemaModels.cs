using System.Collections.Generic;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Content;

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

    // When set, references a generator registered via RegisterGenerator(...);
    // the generator owns Build() and every other field is ignored.
    public string? Generator { get; set; }

    public string? Giver { get; set; }
    public ObjectiveDef? Objective { get; set; }

    // Multi-step "Adventure" quests. Overrides Objective when non-empty.
    public List<StepDef> Steps { get; set; } = new();

    public string? DeadlineDays { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CurrentObjective { get; set; }
    public string? TargetMessage { get; set; }

    public List<RewardDef> Rewards { get; set; } = new();

    public ConsequenceDef? Consequence { get; set; }
}

public sealed class TriggerDef
{
    public string Source { get; set; } = "DailyBoard";
    public int? Weight { get; set; }
    public int? MaxPerDay { get; set; }
    public int? CooldownDays { get; set; }

    // Named cooldown bucket resolved by the consumer mod's cooldown-tier resolver
    // (see IMoreQuestsModApi.LoadQuestsFromMod overload). Null/unknown falls back
    // to CooldownDays.
    public string? CooldownTier { get; set; }
    public Dictionary<string, string>? Available { get; set; }

    // Mail | NpcDialogue | DailyBoard.
    public string? Delivery { get; set; }

    // Periodic.
    public int? EveryDays { get; set; }

    // DateLocked: "<season> <day>".
    public string? Date { get; set; }

    // DateLocked.
    public bool RepeatYearly { get; set; }

    // DateRange.
    public string? From { get; set; }
    public string? To { get; set; }

    // OneShot. Forms: "FirstStat <name> >= <n>", "FirstShipped <itemId>",
    // "FirstItemOwned <itemId>".
    public string? When { get; set; }

    // BuildingBuilt.
    public string? Building { get; set; }

    // BuildingBuilt / MailReceived: days to wait after the event. 0 = same day.
    public int? DayDelay { get; set; }

    // MailReceived.
    public string? Flag { get; set; }

    // WeatherForecast: Sun/Rain/Storm/Snow/Wind or sunny/rainy aliases.
    public string? Weather { get; set; }

    // NpcDialogue.
    public string? Npc { get; set; }

    // SpecialOrder.
    public string? StartDate { get; set; }

    // SpecialOrder. Vanilla QuestDuration names (OneDay/TwoDays/Week/TwoWeeks/Month).
    public string? Duration { get; set; }

    // Custom. Handler id registered via IMoreQuestsModApi.RegisterCustomTrigger.
    public string? Custom { get; set; }
}

public sealed class ObjectiveDef
{
    // Deliver | Resource | Fish | Slay | Ship | Socialize | Custom.
    public string Kind { get; set; } = "Deliver";

    // Used only when Kind = "Custom". Handler id registered via
    // IMoreQuestsModApi.RegisterCustomBoardQuestType. Bare names resolve under the
    // owning consumer mod, "OtherMod/Name" references another mod's handler.
    public string? Custom { get; set; }

    // First entry is the primary (used for journal text); extras are OR-alternatives.
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string> Item { get; set; } = new();
    public int Count { get; set; } = 1;
    public string? TargetMonster { get; set; }

    // Fishing filter: case-insensitive Game1.currentLocation.Name match.
    public string? LocationName { get; set; }

    // Fishing filter: catch size in inches. Squid/Octopus/pond returns report -1
    // and fail this gate.
    public int MinSize { get; set; }

    // Sun/Rain/Storm/Snow/Wind (or sunny/rainy aliases). "Rain" matches Rain+Storm.
    public string? Weather { get; set; }
}

public sealed class StepDef
{
    public string? Name { get; set; }

    // Deliver | Talk | Gift | Catch | Slay | Ship | Visit | Build | ReachLevel |
    // Plant | Collect | ClearDebris | ClearWeeds | Custom.
    public string Kind { get; set; } = "Talk";

    public string? Description { get; set; }

    public List<string> Requires { get; set; } = new();

    // Supports $giver, rewritten at quest-creation time.
    public List<string> Targets { get; set; } = new();

    public List<string> Items { get; set; } = new();

    public int Count { get; set; } = 1;
    public int MinQuality { get; set; }

    // Lifts vanilla's furniture/decor shipping ban while the parent quest is active.
    public bool AllowDecorShipping { get; set; }

    public string? LocationName { get; set; }

    public int MinSize { get; set; }

    public string? Weather { get; set; }
}

public sealed class RewardDef
{
    // Money | Friendship | Object | Recipe | Mail | ShopDiscount | AnimalPurchaseDiscount | FestivalBias | FairStarTokens | Custom.
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

    public string? ShopId { get; set; }
    public int PercentOff { get; set; }
    public int DurationDays { get; set; }

    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string> AppliesTo { get; set; } = new();

    public int GuaranteedStock { get; set; }

    public string? Festival { get; set; }
    public int Magnitude { get; set; }

    public string? Custom { get; set; }
    public string? Payload { get; set; }
}

public sealed class ConsequenceDef
{
    // Tier1 | Tier2 | Tier3 | Special. Default Tier0 (off).
    public string Tier { get; set; } = "Tier0";

    // GiftTastes | Static.
    public string Source { get; set; } = "GiftTastes";

    public string Subject { get; set; } = "";

    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string> Targets { get; set; } = new();

    public int GoldDelta { get; set; }
    public int FriendshipOverride { get; set; }
    public int FriendshipPerDay { get; set; }
    public int ChainDays { get; set; }

    public string? LovedLine { get; set; }
    public string? HatedLine { get; set; }
    public List<string> ChainLines { get; set; } = new();
}
