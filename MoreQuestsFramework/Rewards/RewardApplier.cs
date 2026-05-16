using System.Collections.Generic;
using System.Linq;
using Netcode;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuestsFramework.Rewards;

// Money is a special case: vanilla Quest.questComplete already pays Quest.moneyReward,
// so MoneyReward routes there at posting time rather than through the encoded list.
public static class RewardApplier
{
    // Set by ModEntry.OnSaveLoaded so the static Apply path reaches per-save state
    // without each reward record knowing about it. Null when no save is loaded.
    public static System.Action<ShopDiscountReward>? OnShopDiscountGranted { get; set; }

    public static System.Action<FestivalBiasReward>? OnFestivalBiasGranted { get; set; }

    public static System.Action<AnimalPurchaseDiscountReward>? OnAnimalPurchaseDiscountGranted { get; set; }

    public static System.Action<FairStarTokensReward>? OnFairStarTokensGranted { get; set; }

    public static void ApplyEncoded(IEnumerable<string> encoded)
    {
        foreach (var line in encoded)
        {
            if (RewardCodec.IsConsequenceLine(line))
                continue;
            var spec = RewardCodec.Decode(line);
            if (spec != null)
                ApplyOne(spec);
        }
    }

    public static void FireEncodedConsequence(IEnumerable<string> encoded)
    {
        var spec = RewardCodec.DecodeConsequence(encoded);
        if (spec == null)
            return;
        Consequences.ConsequenceEngine.Active?.Apply(spec);
    }

    public static void Apply(IEnumerable<RewardSpec> rewards)
    {
        foreach (var r in rewards)
            ApplyOne(r);
    }

    public static void EncodeInto(NetStringList target, IEnumerable<RewardSpec> rewards, Consequences.ConsequenceSpec? consequence = null)
    {
        target.Clear();
        foreach (var r in rewards)
        {
            if (r is MoneyReward)
                continue;
            target.Add(RewardCodec.Encode(r));
        }
        if (consequence != null && consequence.Tier != Consequences.ConsequenceTier.Tier0)
            target.Add(RewardCodec.EncodeConsequence(consequence));
    }

    public static int SumMoney(IEnumerable<RewardSpec> rewards)
    {
        int total = 0;
        foreach (var r in rewards)
            if (r is MoneyReward m)
                total += m.Amount;
        return total;
    }

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

        // 3+ named NPCs collapse to one line: the summary doubles as a tip sheet for
        // who's about to react, so listing every loved-by villager spoils the consequence pool.
        var friendshipRewards = rewards.OfType<FriendshipReward>()
            .Where(f => f.Points > 0 && !string.IsNullOrEmpty(f.Npc))
            .ToList();
        if (friendshipRewards.Count >= 3)
        {
            lines.Add(translation.Get("quest.reward.line.friendship.collapsed")
                .Default("Word will get around, a few villagers will warm up to you").ToString());
        }
        else
        {
            foreach (var r in friendshipRewards)
            {
                lines.Add(translation.Get("quest.reward.line.friendship", new { npc = r.Npc })
                    .Default($"{r.Npc} will like you more").ToString());
            }
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

        foreach (var r in rewards.OfType<AnimalPurchaseDiscountReward>())
        {
            if (r.PercentOff <= 0 || r.DurationDays <= 0)
                continue;
            lines.Add(translation.Get("quest.reward.line.animalPurchaseDiscount", new { percent = r.PercentOff, days = r.DurationDays, npc = giver })
                .Default($"{giver} will mark down livestock {r.PercentOff}% for {r.DurationDays} day(s)").ToString());
        }

        foreach (var r in rewards.OfType<FestivalBiasReward>())
        {
            if (r.Magnitude <= 0)
                continue;
            string festivalKey = r.Festival == FestivalKind.Luau ? "luau" : "fair";
            lines.Add(translation.Get($"quest.reward.line.festivalBias.{festivalKey}", new { npc = giver })
                .Default($"{giver}'s help will tilt the {festivalKey} judging in your favour").ToString());
        }

        foreach (var r in rewards.OfType<FairStarTokensReward>())
        {
            if (r.Amount <= 0)
                continue;
            lines.Add(translation.Get("quest.reward.line.fairStarTokens", new { amount = r.Amount, npc = giver })
                .Default($"{giver} will tip you {r.Amount} extra star tokens on Fair day").ToString());
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
                // Covers the case where a generator calls Apply directly outside a Quest
                // completion path; otherwise vanilla pays via Quest.moneyReward.
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

            case AnimalPurchaseDiscountReward apd:
                if (apd.PercentOff <= 0 || apd.DurationDays <= 0)
                    return;
                OnAnimalPurchaseDiscountGranted?.Invoke(apd);
                break;

            case FestivalBiasReward fb:
                if (fb.Magnitude <= 0)
                    return;
                OnFestivalBiasGranted?.Invoke(fb);
                break;

            case FairStarTokensReward fst:
                if (fst.Amount <= 0)
                    return;
                OnFairStarTokensGranted?.Invoke(fst);
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
