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

    // Mod-scoped lookups (generators, custom triggers/steps/rewards/conditions/board types)
    // resolve against this UniqueID. When unset, the loader infers it from the dict key
    // (everything before the first '_', if that prefix contains '.'). CP authors typically
    // set "Owner": "{{ModId}}" explicitly; DLL mods set it in their IAssetRequested.Edit handler.
    public string? Owner { get; set; }

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

    // Mail-delivered quests only. Overrides the auto-generated letter body. Supports
    // {i18n:key} tokens. Null/empty falls back to the framework's default body.
    public string? MailBody { get; set; }

    // Deliver-only. Recipient NPC when the requester (Giver) is not who actually
    // accepts the item (anonymous gift orders). Empty falls back to Giver.
    public string? DeliveryTarget { get; set; }

    // Single-step Ship quests only. Lifts vanilla's furniture/decor shipping ban
    // while the parent quest is active. (For Adventure quests, set this on the
    // individual Ship step instead.)
    public bool AllowDecorShipping { get; set; }

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

    [JsonConverter(typeof(ScalarStringDictionaryConverter))]
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

    // BuildingBuilt. Fires the morning AFTER the building finishes construction
    // (the diff is taken between yesterday's and today's farm snapshot at DayStarted).
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

    // Fishing filter: max catch size in inches. 0 = no upper bound.
    public int MaxSize { get; set; }

    // Fishing counter-only mode: any catch passing the filters counts, no specific
    // stack needed at turn-in. Used by quests like Size Overpopulation.
    public bool AnyFish { get; set; }

    // Overrides vanilla's "0/5 Frog caught" progress label for AnyFish quests.
    // {0} = current count, {1} = quota. Supports {i18n:key} tokens.
    public string? ProgressTemplate { get; set; }

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

    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string> Requires { get; set; } = new();

    // Supports $giver (resolved giver name), $dispatcher.<role> (one NPC from the
    // dispatch role), and $dispatcher.<role>[N] (N distinct NPCs from the role).
    // All rewritten once at quest-creation time so picks stay stable across reloads.
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string> Targets { get; set; } = new();

    [JsonConverter(typeof(StringOrArrayConverter))]
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
    // One line per chain day, in order. JSON name spells out the per-day intent
    // so authors don't have to read the schema doc to find that out.
    [JsonConverter(typeof(StringOrArrayConverter))]
    public List<string> ChainLinesByDay { get; set; } = new();
}
