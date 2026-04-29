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

public enum RecipeKind { Cooking, Crafting }

public enum MailWhen { Today, Tomorrow }
