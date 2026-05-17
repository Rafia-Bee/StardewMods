using Newtonsoft.Json;

namespace MoreQuestsFramework.Rewards;

// Records stay tiny so RewardCodec can encode them into single text lines and
// net-sync them as NetStringList entries without a polymorphic serializer.
// The JsonConverter routes JSON persistence through the same codec, so save data
// stores each reward as a string instead of an object that needs a $type tag.
[JsonConverter(typeof(RewardSpecJsonConverter))]
public abstract record RewardSpec;

public sealed record MoneyReward(int Amount) : RewardSpec;

public sealed record FriendshipReward(string Npc, int Points) : RewardSpec;

public sealed record ObjectReward(string ItemId, int Count = 1) : RewardSpec;

public sealed record RecipeReward(string RecipeName, RecipeKind Kind = RecipeKind.Cooking) : RewardSpec;

public sealed record MailReward(string LetterKey, MailWhen When = MailWhen.Today) : RewardSpec;

// Empty AppliesTo discounts the entire shop. GuaranteedStock>0 force-adds any missing
// AppliesTo item as a temporary entry (for content quests asking for modded crops the
// vanilla shop wouldn't normally carry).
public sealed record ShopDiscountReward(
    string ShopId,
    int PercentOff,
    int DurationDays,
    System.Collections.Generic.List<string>? AppliesTo = null,
    int GuaranteedStock = 0) : RewardSpec;

// Global across animal types, no per-species filter.
public sealed record AnimalPurchaseDiscountReward(
    int PercentOff,
    int DurationDays) : RewardSpec;

// Luau: bumps governor reaction tier (clamped shy of the Mayor's Shorts gag).
// Fair: adds flat points to grange score before Lewis judges.
public sealed record FestivalBiasReward(FestivalKind Festival, int Magnitude) : RewardSpec;

public sealed record FairStarTokensReward(int Amount) : RewardSpec;

public enum RecipeKind { Cooking, Crafting }

public enum MailWhen { Today, Tomorrow }

public enum FestivalKind { Luau, Fair }
