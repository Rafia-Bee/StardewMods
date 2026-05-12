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

/// Temporarily reduces every `Data/FarmAnimals` entry's `PurchasePrice` by `PercentOff`
/// for `DurationDays` in-game days starting the day the quest completes. Routes through
/// the same asset-edit path as `ShopDiscountReward`, just on a different asset (so the
/// vanilla `PurchaseAnimalsMenu` and any third-party menu rewrite, e.g. Livestock Bazaar,
/// pick up the discounted price without a per-menu patch). Persisted to
/// `FrameworkState.ActiveAnimalPurchaseDiscounts` and re-applied each time
/// `Data/FarmAnimals` is requested. Discount is global across animal types — no
/// per-species filter today.
public sealed record AnimalPurchaseDiscountReward(
    int PercentOff,
    int DurationDays) : RewardSpec;

/// One-shot judging-time bias on a vanilla festival outcome. Currently supports the Luau
/// (bumps the governor's reaction tier up by `Magnitude`, capped at the "loved it" tier
/// shy of the Mayor's Shorts gag) and the Stardew Valley Fair (adds `Magnitude` flat
/// points to the player's grange display score before Lewis judges it). The bias is
/// stored on per-save state and consumed at the festival's judging hook.
public sealed record FestivalBiasReward(FestivalKind Festival, int Magnitude) : RewardSpec;

/// Adds `Amount` to the player's `festivalScore` (Stardew Valley Fair star tokens) at
/// the start of the Fair event. Persisted to per-save state so a quest completed days
/// before the Fair still injects the bonus on Fall 16. Consumed at the festival's
/// start hook so a re-judging in the same session doesn't re-grant.
public sealed record FairStarTokensReward(int Amount) : RewardSpec;

public enum RecipeKind { Cooking, Crafting }

public enum MailWhen { Today, Tomorrow }

public enum FestivalKind { Luau, Fair }
