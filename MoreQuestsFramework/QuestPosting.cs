using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Rewards;
using StardewValley.Quests;

namespace MoreQuestsFramework;

/// How a quest reaches the player.
public enum PostingKind
{
    /// Posted on the help-wanted board. Multiple per day.
    DailyBoard,
    /// Posted on the special orders board (the second tab). Multi-objective, longer windows.
    SpecialOrder,
    /// Sent as a mail letter; accepting auto-adds the quest to the journal.
    Mail,
    /// Triggered when the farmer next speaks with the quest giver.
    NpcDialogue
}

public enum BoardQuestType
{
    ItemDelivery,
    ResourceCollection,
    Fishing,
    SlayMonster,
    Socialize,
    /// Single-objective shipping quest, player ships N of an item (or any of a set of
    /// alternatives) into the farm shipping bin. Counted at `DayEnding` by scanning
    /// `Game1.getFarm().getShippingBin(player)` and matching item ids; the items are not
    /// removed by the framework, vanilla still sells them as normal.
    Ship,
    Adventure,
    Custom
}

/// Single concrete quest ready to be delivered to the player via the chosen PostingKind.
public sealed class QuestPosting
{
    public string DefinitionId { get; set; } = "";
    /// UniqueID of the mod that owns the source `IQuestDefinition`. Set by the
    /// pipeline at posting time so framework events can attribute the quest back
    /// to its owner. Empty string for postings produced by definitions registered
    /// before Phase 5's owner-tracking landed.
    public string OwnerUniqueId { get; set; } = "";
    public QuestCategory Category { get; set; }
    public DifficultyTier Tier { get; set; }
    public PostingKind Kind { get; set; } = PostingKind.DailyBoard;
    public BoardQuestType QuestType { get; set; }
    public string QuestGiver { get; set; } = "";

    /// `BoardQuestType.ItemDelivery` / `ResourceCollection` only. Internal NPC name of the
    /// villager who actually receives the item, when that differs from `QuestGiver` (e.g.
    /// `GiftDelivery`: the giver posts the request anonymously, but the player hands the
    /// gift to a third villager). Empty string falls back to `QuestGiver`, which is the
    /// vanilla behaviour where the requester and the delivery target are the same NPC.
    public string DeliveryTarget { get; set; } = "";

    public string ObjectiveItemId { get; set; } = "";
    public string ObjectiveItemName { get; set; } = "";
    /// Optional OR-alternative item ids accepted in place of `ObjectiveItemId`. Used by the
    /// declarative `Item: [...]` JSON form so a single posting can satisfy on any of several
    /// items (e.g. Submarine Fuel accepts a Battery Pack OR Coal). Empty means single-item.
    public List<string> AlternativeObjectiveItemIds { get; set; } = new();
    /// Per-stack credit toward `ObjectiveQuantity` when the primary item is matched. Defaults
    /// to 1 (count items 1:1). Higher values let one item count as N units of progress,
    /// Submarine Fuel uses weight 15 on Battery Pack so 1 battery = 15 coal of fuel toward
    /// the same shipping bar.
    public int ObjectiveItemWeight { get; set; } = 1;
    /// Parallel to `AlternativeObjectiveItemIds`. Missing entries default to 1.
    public List<int> AlternativeObjectiveItemWeights { get; set; } = new();
    /// `BoardQuestType.ItemDelivery` only. Parallel to `AlternativeObjectiveItemIds`: the
    /// required stack size when that alternative is offered to the NPC. Lets one posting
    /// accept a different quantity per material (e.g. Robin's Silo: 100 Stone OR 10 Clay
    /// OR 5 Copper Bars). Missing or non-positive entries fall back to `ObjectiveQuantity`.
    public List<int> AlternativeObjectiveItemQuantities { get; set; } = new();
    public int ObjectiveQuantity { get; set; } = 1;
    public string? TargetMonster { get; set; }
    public string? TargetLocation { get; set; }
    public int MinQuality { get; set; }

    /// `BoardQuestType.Fishing` filter: when set, the catch only counts when the player's
    /// current location name matches (case-insensitive). Routed into `MoreQuestsFishingQuest`
    /// at build time so the runtime gate sits next to the existing item-id check.
    public string CatchLocationName { get; set; } = string.Empty;

    /// `BoardQuestType.Fishing` filter: when > 0, the catch only counts when its size (in
    /// inches, the value vanilla passes through `OnFishCaught`) is ≥ the threshold. Used by
    /// the small/medium/large overpopulation row to bucket fish by size. Squid / Octopus and
    /// pond catches that report size -1 always fail this gate.
    public int CatchMinSize { get; set; }

    /// `BoardQuestType.Fishing` filter: when > 0, the catch only counts when its size (in
    /// inches) is ≤ the threshold. Paired with `CatchMinSize` to bound a size bucket. 0
    /// disables the upper bound. Note -1 catches (Squid / Octopus / pond returns) fail
    /// the min gate before they reach this one.
    public int CatchMaxSize { get; set; }

    /// `BoardQuestType.Fishing` flag: when true, the quest counts any caught fish that
    /// passes the size / location / weather filters, regardless of `ObjectiveItemId`.
    /// Turn-in only requires the catch counter to be full, no specific stack is needed
    /// in inventory and no fish are consumed. Used by Size Overpopulation. Default false
    /// preserves single-species quest semantics.
    public bool CatchAnyFish { get; set; }

    /// `BoardQuestType.Fishing` filter: when set, the catch only counts when the runtime
    /// weather at the player's current location matches. Accepts `Sun` / `Rain` / `Storm` /
    /// `Snow` / `Wind` plus the `sunny` / `rainy` / ... aliases. `Rain` matches both Rain
    /// and Storm.
    public string CatchWeather { get; set; } = string.Empty;

    /// `BoardQuestType.Fishing` progress template for `catchAnyFish` quests. Vanilla
    /// `FishingQuest.reloadObjective` always rebuilds the objective line from `ItemId.Value`
    /// (the placeholder fish), so a Size Overpopulation quest would show "0/5 Frog caught"
    /// instead of "0/5 medium-sized fish caught". When this is set on a `catchAnyFish`
    /// posting, `MoreQuestsFishingQuest.reloadObjective` formats it with the catch counter
    /// and quota in place of `{0}` and `{1}`. Empty string falls back to vanilla behaviour.
    public string CatchProgressTemplate { get; set; } = string.Empty;

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CurrentObjective { get; set; } = "";
    public string TargetMessage { get; set; } = "";
    public string? MailBody { get; set; }

    public int DeadlineDays { get; set; } = 5;

    /// When true, vanilla's furniture / decor shipping ban is bypassed for the duration
    /// the quest sits in the player's active log. Implemented via a gated Harmony postfix
    /// on `Object.canBeShipped` (see `DecorShippingPatches`). Used by festival-supply quests
    /// (Moonlight Jellies, Luau, Spirit's Eve, etc.) that ask the player to ship items
    /// vanilla otherwise wouldn't accept (Hay Bales, Wood Lamp-posts, Tubs of Flowers).
    public bool AllowDecorShipping { get; set; }

    /// Declarative reward block. Phase 3 entry point - quests author rewards as
    /// `RewardSpec` records (Money / Friendship / Object / Recipe / Mail) and the
    /// poster routes them into the right fields at delivery time.
    public List<RewardSpec> Rewards { get; set; } = new();

    /// Optional Phase 9 consequence block. Fired by `ConsequenceEngine.Apply` from every
    /// `IRewardedQuest.questComplete()` and `AdventureQuest.questComplete()` override.
    /// Null = no consequence (the engine no-ops on a null spec or `Tier0`).
    public ConsequenceSpec? Consequence { get; set; }

    /// Populated only when `Kind == PostingKind.SpecialOrder`. Carries the full
    /// `Data/SpecialOrders` shape for one entry (objectives + rewards + duration +
    /// requester). Read by `SpecialOrderWriter` at emit time. Null for every other
    /// posting kind.
    public SpecialOrderSpec? SpecialOrder { get; set; }

    /// If set, this Quest object is used directly instead of building one from the posting fields.
    /// Vanilla-quest definitions populate this so the vanilla random logic stays intact.
    public Quest? PreBuiltQuest { get; set; }

    /// Total gold across all `MoneyReward` entries. Routed into `Quest.moneyReward` at
    /// posting time so vanilla pays it on completion.
    public int TotalMoney => RewardApplier.SumMoney(Rewards);

    /// Friendship reward to the quest giver (if any). Used by anti-spam pipeline filters
    /// like `SkipFriendshipQuestsAtMaxHeart`.
    public FriendshipReward? GiverFriendshipReward =>
        Rewards.OfType<FriendshipReward>().FirstOrDefault(r => string.Equals(r.Npc, QuestGiver, System.StringComparison.OrdinalIgnoreCase));
}

