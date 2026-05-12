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

    /// Same shape as `OnShopDiscountGranted` for `FestivalBiasReward`. Wired by the
    /// `FestivalBiasWriter` at save-load so the static `Apply` path reaches per-save state
    /// without each reward record knowing about it. Null when no save is loaded.
    public static System.Action<FestivalBiasReward>? OnFestivalBiasGranted { get; set; }

    /// Same shape as `OnShopDiscountGranted` for `AnimalPurchaseDiscountReward`. Wired by
    /// `AnimalPurchaseDiscountWriter` at save-load.
    public static System.Action<AnimalPurchaseDiscountReward>? OnAnimalPurchaseDiscountGranted { get; set; }

    /// Same shape as `OnFestivalBiasGranted` for `FairStarTokensReward`. Wired by the
    /// `FairStarTokensWriter` at save-load.
    public static System.Action<FairStarTokensReward>? OnFairStarTokensGranted { get; set; }

    /// Applies every reward in the encoded list to the active player. Designed for
    /// `Quest.questComplete()` overrides on our custom subclasses. Consequence-spec
    /// lines (`Consequence|...`) are ignored here — they're forwarded to the
    /// `ConsequenceEngine` separately via `FireEncodedConsequence`.
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

    /// Decodes the consequence line (if any) from the encoded list and forwards it to
    /// the active `ConsequenceEngine`. Called from custom Quest subclasses' `questComplete()`
    /// overrides alongside `ApplyEncoded` so the consequence fires on the same event as
    /// reward payout. No-ops gracefully when the engine isn't wired (e.g. authoring tests).
    public static void FireEncodedConsequence(IEnumerable<string> encoded)
    {
        var spec = RewardCodec.DecodeConsequence(encoded);
        if (spec == null)
            return;
        Consequences.ConsequenceEngine.Active?.Apply(spec);
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
    /// `Quest.moneyReward` instead. When `consequence` is non-null, its encoded form
    /// is appended as a `Consequence|...` line so the same NetStringList carries both
    /// reward + consequence state through save/reload.
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

        // Collapse the friendship lines when there are 3+ named NPCs — listing every
        // loved-by villager spoils the consequence pool (the summary doubles as a tip
        // sheet for who's about to react). For 1-2 NPCs we keep the named lines so
        // small-cast rewards (single FriendshipReward, e.g. Forage with Linus) read
        // naturally.
        var friendshipRewards = rewards.OfType<FriendshipReward>()
            .Where(f => f.Points > 0 && !string.IsNullOrEmpty(f.Npc))
            .ToList();
        if (friendshipRewards.Count >= 3)
        {
            lines.Add(translation.Get("quest.reward.line.friendship.collapsed")
                .Default("Word will get around — a few villagers will warm up to you").ToString());
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
