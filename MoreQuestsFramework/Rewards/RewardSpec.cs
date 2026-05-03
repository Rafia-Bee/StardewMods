namespace MoreQuestsFramework.Rewards;

/// Declarative reward kinds. A `QuestPosting` carries a `List<RewardSpec>` that
/// gets applied at quest completion. The set here mirrors the §5.2 schema:
/// Money, Friendship, Object, Recipe, Mail.
///
/// Records are intentionally tiny so they can be encoded into a single line of
/// text (see `RewardCodec`) and net-synced as `NetStringList` entries on the
/// custom Quest subclasses without a polymorphic serializer.
public abstract record RewardSpec;

public sealed record MoneyReward(int Amount) : RewardSpec;

public sealed record FriendshipReward(string Npc, int Points) : RewardSpec;

public sealed record ObjectReward(string ItemId, int Count = 1) : RewardSpec;

public sealed record RecipeReward(string RecipeName, RecipeKind Kind = RecipeKind.Cooking) : RewardSpec;

public sealed record MailReward(string LetterKey, MailWhen When = MailWhen.Today) : RewardSpec;

/// Temporarily reduces prices in `Data/Shops/<ShopId>` by `PercentOff` for `DurationDays`
/// in-game days starting the day the quest completes. `AppliesTo` (when non-empty)
/// restricts the discount to entries whose `ItemId` matches one of the listed ids; an
/// empty list discounts every entry in the shop. `GuaranteedStock` (when > 0) force-adds
/// any `AppliesTo` item the shop doesn't already sell as a temporary stocked entry with
/// that per-visit limit at the discounted price — handy for content quests that ask for
/// modded crops the vanilla shop wouldn't normally carry. Persisted to
/// `FrameworkState.ActiveShopDiscounts` and re-applied via an asset-edit handler each
/// time `Data/Shops` is requested.
public sealed record ShopDiscountReward(
    string ShopId,
    int PercentOff,
    int DurationDays,
    System.Collections.Generic.List<string>? AppliesTo = null,
    int GuaranteedStock = 0) : RewardSpec;

public enum RecipeKind { Cooking, Crafting }

public enum MailWhen { Today, Tomorrow }
