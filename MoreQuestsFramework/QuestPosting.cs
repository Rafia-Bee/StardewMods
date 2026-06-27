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
    // EarnMoney: no item or turn-in. Snapshots the player's lifetime earnings when the
    // quest starts and completes once they've earned ObjectiveQuantity gold MORE from
    // that point. Progress is polled once a second off the quest log.
    EarnMoney,
    // Sell: counts items the player sells INTO a named shop (not turned in to the giver).
    // Watches inventory drops while that shop's menu is open. Items leave normally, the
    // framework just counts them. Filtered by id/category, max store price, and quality.
    Sell,
    Adventure,
    Custom
}

public sealed class QuestPosting
{
    public string DefinitionId { get; set; } = "";
    // UniqueID of the owning mod, used to attribute framework events.
    public string OwnerUniqueId { get; set; } = "";
    public string Category { get; set; } = QuestCategory.Social;

    // Per-quest override for the board note's corner icon. Empty defers to the category's
    // icon. "Portrait" draws the giver portrait, "None" draws nothing, anything else is an
    // asset name. See CategoryDefinition.Icon.
    public string Icon { get; set; } = "";

    public DifficultyTier Tier { get; set; }
    public PostingKind Kind { get; set; } = PostingKind.DailyBoard;
    public BoardQuestType QuestType { get; set; }

    // Set when QuestType == BoardQuestType.Custom. Handler id registered via
    // IMoreQuestsModApi.RegisterCustomBoardQuestType. Bare names resolve under the
    // owning consumer mod's UniqueID, "OtherMod/Name" works for cross-mod references.
    public string CustomQuestType { get; set; } = "";

    public string QuestGiver { get; set; } = "";

    // Opt-in for child givers. NpcDisplay.IsBoardEligible rejects Age=Child by default
    // so the help-wanted board doesn't get cluttered with kids asking for chores; the
    // few quests that are written specifically for child givers (Feed Wild Critters)
    // set this true so the posting skips the Age=Child gate. The IneligibleGivers
    // denylist and CanSocialize / PerfectionScore checks still apply.
    public bool AllowChildGiver { get; set; }

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

    // Sell quests only. The shop the player has to sell into, matched on ShopMenu.ShopId
    // (Pierre's is "SeedShop", JojaMart's is "Joja"). A sale anywhere else doesn't count.
    public string SellShopId { get; set; } = string.Empty;
    // Object categories that count toward a Sell quest (negative ids, e.g. -75 Vegetable,
    // -79 Fruit, -80 Flower). Empty means category isn't checked. Combined with the item
    // id list as an OR: a sold item counts if it matches an id OR sits in one of these.
    public List<int> SellCategories { get; set; } = new();
    // Sell quests only. Highest single-item store price that still counts (exclusive).
    // 0 means no price cap. Lets a quest target cheap goods only.
    public int SellMaxValue { get; set; }
    // Sell quests only. Highest item quality that counts (0 base, 1 silver, 2 gold,
    // 4 iridium). Default 0 so only base-quality items count.
    public int SellMaxQuality { get; set; }

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

    // True = "show the catch and report back", not a delivery. Legendary fish quests
    // and similar use this to keep the journal on vanilla "Return to <npc>" instead of
    // the "Deliver X to Y" override that real delivery fishing quests get.
    public bool IsReportBack { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CurrentObjective { get; set; } = "";
    public string TargetMessage { get; set; } = "";
    public string? MailBody { get; set; }

    // NpcDialogue postings only. When set, the NPC says this line the second time the
    // player chats with them today; the quest is added to the journal at that moment.
    // When empty, the quest is added silently on the first chat (the legacy behavior).
    public string DialogueText { get; set; } = "";

    // Optional replacement for the per-NPC / collapsed friendship lines in the journal
    // reward summary. When non-empty, BuildRewardSummary drops the auto-built friendship
    // lines and uses this string instead. Useful when a quest's friendship pool is a
    // themed group (the kids, the saloon crowd, the wizard tower) and the generic
    // "word will get around" line reads wrong.
    public string FriendshipSummaryOverride { get; set; } = "";

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

