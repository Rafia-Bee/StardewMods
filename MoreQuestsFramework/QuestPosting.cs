using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Rewards;
using StardewValley.Quests;

namespace MoreQuestsFramework;

public enum PostingKind
{
    DailyBoard,
    SpecialOrder,
    Mail,
    NpcDialogue
}

public enum BoardQuestType
{
    ItemDelivery,
    ResourceCollection,
    Fishing,
    SlayMonster,
    Socialize,
    // Ship: counted at DayEnding by scanning the shipping bin. Items are NOT
    // removed by the framework, vanilla still sells them as normal.
    Ship,
    Adventure,
    Custom
}

public sealed class QuestPosting
{
    public string DefinitionId { get; set; } = "";
    // UniqueID of the owning mod, used to attribute framework events.
    public string OwnerUniqueId { get; set; } = "";
    public QuestCategory Category { get; set; }
    public DifficultyTier Tier { get; set; }
    public PostingKind Kind { get; set; } = PostingKind.DailyBoard;
    public BoardQuestType QuestType { get; set; }

    // Set when QuestType == BoardQuestType.Custom. Handler id registered via
    // IMoreQuestsModApi.RegisterCustomBoardQuestType. Bare names resolve under the
    // owning consumer mod's UniqueID, "OtherMod/Name" works for cross-mod references.
    public string CustomQuestType { get; set; } = "";

    public string QuestGiver { get; set; } = "";

    // Used by GiftDelivery where the requester is anonymous and the recipient is a
    // third NPC. Empty falls back to QuestGiver.
    public string DeliveryTarget { get; set; } = "";

    public string ObjectiveItemId { get; set; } = "";
    public string ObjectiveItemName { get; set; } = "";
    // OR-alternatives accepted in place of ObjectiveItemId (e.g. Battery Pack OR Coal).
    public List<string> AlternativeObjectiveItemIds { get; set; } = new();
    // Per-stack credit toward ObjectiveQuantity. Higher values let one item count as N
    // (e.g. Submarine Fuel uses weight 15 on Battery Pack so 1 battery = 15 coal).
    public int ObjectiveItemWeight { get; set; } = 1;
    public List<int> AlternativeObjectiveItemWeights { get; set; } = new();
    // ItemDelivery only. Per-alternative required stack size (e.g. Robin's Silo:
    // 100 Stone OR 10 Clay OR 5 Copper Bars). Missing entries fall back to ObjectiveQuantity.
    public List<int> AlternativeObjectiveItemQuantities { get; set; } = new();
    public int ObjectiveQuantity { get; set; } = 1;
    public string? TargetMonster { get; set; }
    public string? TargetLocation { get; set; }
    public int MinQuality { get; set; }

    public string CatchLocationName { get; set; } = string.Empty;

    // Fishing size filter (inches, from OnFishCaught). Squid/Octopus and pond returns
    // report -1 and always fail the min gate.
    public int CatchMinSize { get; set; }

    public int CatchMaxSize { get; set; }

    // True = counter-only quest (any catch passing the filters counts, no specific stack
    // needed at turn-in, no fish consumed). Used by Size Overpopulation.
    public bool CatchAnyFish { get; set; }

    // Sun/Rain/Storm/Snow/Wind (plus sunny/rainy aliases). "Rain" matches both Rain and Storm.
    public string CatchWeather { get; set; } = string.Empty;

    // Overrides vanilla FishingQuest.reloadObjective for catchAnyFish quests (vanilla
    // would render "0/5 Frog caught" from the placeholder). {0} = counter, {1} = quota.
    public string CatchProgressTemplate { get; set; } = string.Empty;

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CurrentObjective { get; set; } = "";
    public string TargetMessage { get; set; } = "";
    public string? MailBody { get; set; }

    public int DeadlineDays { get; set; } = 5;

    // Bypass vanilla's decor/furniture shipping ban while the quest is active. Used by
    // festival-supply quests that ask for Hay Bales, Wood Lamp-posts, Tubs of Flowers, etc.
    public bool AllowDecorShipping { get; set; }

    public List<RewardSpec> Rewards { get; set; } = new();

    public ConsequenceSpec? Consequence { get; set; }

    public SpecialOrderSpec? SpecialOrder { get; set; }

    // If set, used directly instead of building from posting fields. Vanilla-quest
    // definitions populate this so vanilla random logic stays intact.
    public Quest? PreBuiltQuest { get; set; }

    public int TotalMoney => RewardApplier.SumMoney(Rewards);

    public FriendshipReward? GiverFriendshipReward =>
        Rewards.OfType<FriendshipReward>().FirstOrDefault(r => string.Equals(r.Npc, QuestGiver, System.StringComparison.OrdinalIgnoreCase));
}

