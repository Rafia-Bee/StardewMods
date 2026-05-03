using System.Collections.Generic;
using System.Linq;
using Netcode;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Rewards;

/// Centralized reward application. Custom Quest subclasses delegate their on-complete
/// payouts here so any completion path - in-person delivery, Mail Services Mod,
/// future custom quest types - produces the same result without duplicating the
/// inventory/friendship/recipe/mail dance per subclass.
///
/// Phase 3 introduces a declarative `RewardSpec` list. Quests author rewards as
/// records (`MoneyReward`, `FriendshipReward`, `ObjectReward`, `RecipeReward`,
/// `MailReward`); the framework encodes them into a `NetStringList` on the
/// quest, and `Apply` decodes + dispatches at completion time.
///
/// Money is a special case: vanilla `Quest.questComplete()` already pays
/// `Quest.moneyReward.Value`, so `MoneyReward` entries route into that field at
/// posting time rather than going through the encoded list.
public static class RewardApplier
{
    /// Set by `ModEntry.OnSaveLoaded` to a callback that writes the granted ShopDiscount
    /// into per-save framework state. Static so generators / tests can fire individual
    /// rewards via `Apply(...)` without threading a save-state reference through every
    /// completion path. Null when no save is loaded — `ApplyOne` no-ops the discount in
    /// that case (test / generator authoring scenarios won't have a save to write to).
    public static System.Action<ShopDiscountReward>? OnShopDiscountGranted { get; set; }

    /// Applies every reward in the encoded list to the active player. Designed for
    /// `Quest.questComplete()` overrides on our custom subclasses.
    public static void ApplyEncoded(IEnumerable<string> encoded)
    {
        foreach (var line in encoded)
        {
            var spec = RewardCodec.Decode(line);
            if (spec != null)
                ApplyOne(spec);
        }
    }

    /// Applies a single reward spec. Public so generators / tests can fire individual
    /// rewards without going through the encoded list.
    public static void Apply(IEnumerable<RewardSpec> rewards)
    {
        foreach (var r in rewards)
            ApplyOne(r);
    }

    /// Encodes the non-Money rewards in `rewards` into `target`, replacing existing
    /// entries. Money rewards are skipped because they're paid by vanilla via
    /// `Quest.moneyReward` instead.
    public static void EncodeInto(NetStringList target, IEnumerable<RewardSpec> rewards)
    {
        target.Clear();
        foreach (var r in rewards)
        {
            if (r is MoneyReward)
                continue;
            target.Add(RewardCodec.Encode(r));
        }
    }

    /// Total of all `MoneyReward` entries, summed. Routed into `Quest.moneyReward` at
    /// posting time.
    public static int SumMoney(IEnumerable<RewardSpec> rewards)
    {
        int total = 0;
        foreach (var r in rewards)
            if (r is MoneyReward m)
                total += m.Amount;
        return total;
    }

    /// Builds the "Reward:" block appended to the quest description in the journal.
    /// Each reward gets its own bullet line phrased in the quest giver's voice
    /// ("Marnie will give you 200g in return", "Abigail will like you more", etc.)
    /// rather than a flat comma-separated list. Vanilla bakes its reward into the
    /// description text; we mirror that look while keeping the wording personal.
    public static string BuildRewardSummary(IReadOnlyList<RewardSpec> rewards, string questGiver, ITranslationHelper translation)
    {
        if (rewards.Count == 0)
            return string.Empty;

        var lines = new List<string>(rewards.Count);
        string giver = string.IsNullOrEmpty(questGiver) ? "They" : questGiver;

        int gold = SumMoney(rewards);
        if (gold > 0)
            lines.Add(translation.Get("quest.reward.line.money", new { npc = giver, gold })
                .Default($"{giver} will give you {gold}g in return").ToString());

        foreach (var r in rewards.OfType<FriendshipReward>())
        {
            if (r.Points <= 0 || string.IsNullOrEmpty(r.Npc))
                continue;
            lines.Add(translation.Get("quest.reward.line.friendship", new { npc = r.Npc })
                .Default($"{r.Npc} will like you more").ToString());
        }

        foreach (var r in rewards.OfType<ObjectReward>())
        {
            if (string.IsNullOrEmpty(r.ItemId) || r.Count <= 0)
                continue;
            var item = ItemRegistry.Create(r.ItemId, r.Count);
            string name = item?.DisplayName ?? r.ItemId;
            string itemPhrase = r.Count > 1 ? $"{r.Count}x {name}" : name;
            lines.Add(translation.Get("quest.reward.line.item", new { item = itemPhrase, count = r.Count, npc = giver })
                .Default($"You will get {itemPhrase} as a thank you").ToString());
        }

        foreach (var r in rewards.OfType<RecipeReward>())
        {
            if (string.IsNullOrEmpty(r.RecipeName))
                continue;
            lines.Add(translation.Get("quest.reward.line.recipe", new { recipe = r.RecipeName, npc = giver })
                .Default($"You will learn the {r.RecipeName} recipe").ToString());
        }

        foreach (var r in rewards.OfType<MailReward>())
        {
            if (string.IsNullOrEmpty(r.LetterKey))
                continue;
            lines.Add(translation.Get("quest.reward.line.mail", new { npc = giver })
                .Default($"{giver} will send you a letter").ToString());
        }

        foreach (var r in rewards.OfType<ShopDiscountReward>())
        {
            if (string.IsNullOrEmpty(r.ShopId) || r.PercentOff <= 0 || r.DurationDays <= 0)
                continue;
            lines.Add(translation.Get("quest.reward.line.shopDiscount", new { percent = r.PercentOff, days = r.DurationDays, npc = giver })
                .Default($"{giver} will mark down their shop {r.PercentOff}% for {r.DurationDays} day(s)").ToString());
        }

        if (lines.Count == 0)
            return string.Empty;

        string label = translation.Get("quest.reward.label").Default("Reward").ToString();
        return $"{label}:\n- {string.Join("\n- ", lines)}";
    }

    private static void ApplyOne(RewardSpec spec)
    {
        switch (spec)
        {
            case MoneyReward m:
                // Money is normally paid by vanilla via `Quest.moneyReward`. This branch
                // covers the case where a generator calls Apply directly outside a Quest
                // completion path, so the player still gets paid.
                if (m.Amount > 0)
                    Game1.player.Money += m.Amount;
                break;

            case FriendshipReward f:
                if (f.Points == 0 || string.IsNullOrEmpty(f.Npc))
                    return;
                var rewardNpc = Game1.getCharacterFromName(f.Npc);
                if (rewardNpc != null)
                    Game1.player.changeFriendship(f.Points, rewardNpc);
                break;

            case ObjectReward o:
                if (string.IsNullOrEmpty(o.ItemId) || o.Count <= 0)
                    return;
                var reward = ItemRegistry.Create(o.ItemId, o.Count);
                if (reward != null && !Game1.player.addItemToInventoryBool(reward))
                    Game1.createItemDebris(reward, Game1.player.getStandingPosition(), 2);
                break;

            case RecipeReward r:
                if (string.IsNullOrEmpty(r.RecipeName))
                    return;
                var recipes = r.Kind == RecipeKind.Cooking
                    ? Game1.player.cookingRecipes
                    : Game1.player.craftingRecipes;
                if (!recipes.ContainsKey(r.RecipeName))
                    recipes.Add(r.RecipeName, 0);
                break;

            case ShopDiscountReward sd:
                if (string.IsNullOrEmpty(sd.ShopId) || sd.PercentOff <= 0 || sd.DurationDays <= 0)
                    return;
                OnShopDiscountGranted?.Invoke(sd);
                break;

            case MailReward ml:
                if (string.IsNullOrEmpty(ml.LetterKey))
                    return;
                if (ml.When == MailWhen.Tomorrow)
                {
                    if (!Game1.player.mailForTomorrow.Contains(ml.LetterKey))
                        Game1.player.mailForTomorrow.Add(ml.LetterKey);
                }
                else
                {
                    if (!Game1.player.mailReceived.Contains(ml.LetterKey)
                        && !Game1.player.mailbox.Contains(ml.LetterKey))
                        Game1.player.mailbox.Add(ml.LetterKey);
                }
                break;
        }
    }
}
