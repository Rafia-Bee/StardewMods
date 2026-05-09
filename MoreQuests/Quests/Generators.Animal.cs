using System;
using System.Collections.Generic;
using System.Linq;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Conditions;
using MoreQuestsFramework.Consequences;
using MoreQuestsFramework.Dispatch;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewValley;

namespace MoreQuests.Quests;

internal static partial class Generators
{
    private static QuestPosting? HaySupplyRun(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        int animals = CountAnimals();
        if (animals < 4)
            return null;

        int qty = Math.Max(ModEntry.Config.HaySupplyBaseQty, animals * 3);
        int gold = (int)(qty * 50 * 0.8);

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = "(O)178",
            ObjectiveItemName = "Hay",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.animal.hay.title"),
            Description = ModEntry.I18n.Get("quest.animal.hay.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.hay.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.hay.targetMessage")
        };
    }

    private static int CountAnimals()
    {
        int total = 0;
        foreach (var location in Game1.locations)
        {
            total += location.animals.Count();
            foreach (var building in location.buildings)
            {
                var indoor = building.GetIndoors();
                if (indoor != null)
                    total += indoor.animals.Count();
            }
        }
        return total;
    }

    /// Counts farm animals whose `displayType` / animal type name contains the given
    /// substring (case-insensitive). Used by Alex's Protein Shakes to scale the egg ask
    /// by chicken count, where `kind = "Chicken"` matches White / Brown / Blue / Void
    /// chickens and any modded chicken type. Walks both `location.animals` and the
    /// indoor animals of every farm building in case the save spreads animals across
    /// multiple coops/barns.
    private static int CountAnimalsByType(string kind)
    {
        if (string.IsNullOrEmpty(kind))
            return 0;
        int total = 0;
        foreach (var location in Game1.locations)
        {
            foreach (var a in location.animals.Values)
                if (a?.type?.Value != null && a.type.Value.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0)
                    total++;
            foreach (var building in location.buildings)
            {
                var indoor = building.GetIndoors();
                if (indoor == null) continue;
                foreach (var a in indoor.animals.Values)
                    if (a?.type?.Value != null && a.type.Value.IndexOf(kind, StringComparison.OrdinalIgnoreCase) >= 0)
                        total++;
            }
        }
        return total;
    }

    /// CSV row 14. Periodic ItemDelivery to Alex, scaled by chicken count. Asks for eggs
    /// (vanilla `(O)176` white egg accepted; the framework's ItemDelivery quest matches
    /// against either the requested id or any modded-egg alternative the resolver
    /// surfaces). Reward = a random pick from the curated stamina/health pool — Energy
    /// Tonic / Muscle Remedy / Life Elixir. The custom Protein Bar reward referenced in
    /// the CSV is deferred to the asset drop (no spritework yet).

    /// CSV row 14. Periodic ItemDelivery to Alex, scaled by chicken count. Asks for eggs
    /// (vanilla `(O)176` white egg accepted; the framework's ItemDelivery quest matches
    /// against either the requested id or any modded-egg alternative the resolver
    /// surfaces). Reward = a random pick from the curated stamina/health pool — Energy
    /// Tonic / Muscle Remedy / Life Elixir. The custom Protein Bar reward referenced in
    /// the CSV is deferred to the asset drop (no spritework yet).
    private static readonly string[] AlexProteinRewardPool =
    {
        "(O)349", // Energy Tonic
        "(O)351", // Muscle Remedy
        "(O)773"  // Life Elixir
    };

    private static QuestPosting? AlexProteinShakes(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Alex") == null)
            return null;

        int chickens = CountAnimalsByType("Chicken");
        if (chickens <= 0)
            return null;

        int qty = Math.Clamp(
            ModEntry.Config.AlexProteinShakesBaseQty + chickens * Math.Max(0, ModEntry.Config.AlexProteinShakesPerChicken),
            Math.Max(1, ModEntry.Config.AlexProteinShakesBaseQty),
            30);

        var rewardItem = ctx.Items.TryResolveItem(AlexProteinRewardPool[Game1.random.Next(AlexProteinRewardPool.Length)]);
        var rewards = new List<RewardSpec>
        {
            new FriendshipReward("Alex", ctx.Config.FriendshipBasic)
        };
        if (rewardItem != null)
            rewards.Add(new ObjectReward(rewardItem.QualifiedItemId));

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Alex",
            ObjectiveItemId = "(O)176",
            ObjectiveItemName = "Egg",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.animal.alexProtein.title"),
            Description = ModEntry.I18n.Get("quest.animal.alexProtein.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.alexProtein.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.alexProtein.targetMessage")
        };
    }

    /// CSV row 29. OneShot triggered when the player first holds a Dinosaur Egg `(O)107`
    /// (framework `FirstHeldItem` predicate). Mail+ItemDelivery to Gunther for one
    /// Dinosaur Egg. Reward = `GoldAdvancedBase` plus one Dinosaur Egg returned via
    /// `ObjectReward`. The "one quality tier higher" gimmick from the CSV (silver in →
    /// gold out) is deferred — the framework's `ObjectReward` doesn't carry a quality
    /// field, and adding one is more invasive than the row warrants on its own.

    /// CSV row 29. OneShot triggered when the player first holds a Dinosaur Egg `(O)107`
    /// (framework `FirstHeldItem` predicate). Mail+ItemDelivery to Gunther for one
    /// Dinosaur Egg. Reward = `GoldAdvancedBase` plus one Dinosaur Egg returned via
    /// `ObjectReward`. The "one quality tier higher" gimmick from the CSV (silver in →
    /// gold out) is deferred — the framework's `ObjectReward` doesn't carry a quality
    /// field, and adding one is more invasive than the row warrants on its own.
    private static QuestPosting? GuntherDinosaurStudy(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Gunther") == null)
            return null;

        const string giver = "Gunther";
        const string dinosaurEggId = "(O)107";
        int gold = ctx.Config.GoldAdvancedBase;

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = dinosaurEggId,
            ObjectiveItemName = "Dinosaur Egg",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Extended, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new ObjectReward(dinosaurEggId)
            },
            Title = ModEntry.I18n.Get("quest.animal.guntherDinosaur.title"),
            Description = ModEntry.I18n.Get("quest.animal.guntherDinosaur.description"),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.guntherDinosaur.objective"),
            TargetMessage = ModEntry.I18n.Get("quest.animal.guntherDinosaur.targetMessage")
        };
    }

    /// CSV row 43. BuildingBuilt(Coop)+1 day. Mail+ItemDelivery for a stack of one
    /// current-season seed type to Marnie. Reward = a gold rebate (`MarnieChickenOfferRebate`,
    /// default 800g — vanilla white chicken price) as a proxy for the "deal on a chicken"
    /// the CSV calls out. A real shop discount would need a `PurchaseAnimalsMenu` patch
    /// (deferred — the framework doesn't currently hook the animal-shop menu).

    /// CSV row 43. BuildingBuilt(Coop)+1 day. Mail+ItemDelivery for a stack of one
    /// current-season seed type to Marnie. Reward = a gold rebate (`MarnieChickenOfferRebate`,
    /// default 800g — vanilla white chicken price) as a proxy for the "deal on a chicken"
    /// the CSV calls out. A real shop discount would need a `PurchaseAnimalsMenu` patch
    /// (deferred — the framework doesn't currently hook the animal-shop menu).
    private static QuestPosting? MarnieChickenOffer(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        var seed = PickSeasonalSeed(ctx);
        if (seed == null)
            return null;

        int qty = Math.Max(1, ModEntry.Config.MarnieChickenOfferSeedQty);
        int rebate = Math.Max(0, ModEntry.Config.MarnieChickenOfferRebate);

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = seed.QualifiedItemId,
            ObjectiveItemName = seed.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new MoneyReward(rebate),
                new FriendshipReward("Marnie", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieChickenOffer.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieChickenOffer.description", new { qty, item = seed.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieChickenOffer.objective", new { qty, item = seed.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieChickenOffer.targetMessage")
        };
    }

    /// CSV row 44. BuildingBuilt(Barn)+1 day. Mail+ItemDelivery for hay to Marnie. Reward
    /// = a gold rebate (`MarnieCowOfferRebate`, default 1500g — vanilla cow price) as a
    /// proxy for the "deal on a cow" the CSV calls out. Same rebate-as-proxy approach as
    /// the Chicken Offer; the Grazing Bell variant in the CSV (LFY-gated) is deferred —
    /// LFY doesn't currently expose its Grazing Bell item id through a stable API, and
    /// the framework can't grant a real animal-shop discount without patching the menu.

    /// CSV row 44. BuildingBuilt(Barn)+1 day. Mail+ItemDelivery for hay to Marnie. Reward
    /// = a gold rebate (`MarnieCowOfferRebate`, default 1500g — vanilla cow price) as a
    /// proxy for the "deal on a cow" the CSV calls out. Same rebate-as-proxy approach as
    /// the Chicken Offer; the Grazing Bell variant in the CSV (LFY-gated) is deferred —
    /// LFY doesn't currently expose its Grazing Bell item id through a stable API, and
    /// the framework can't grant a real animal-shop discount without patching the menu.
    private static QuestPosting? MarnieCowOffer(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        int qty = Math.Max(1, ModEntry.Config.MarnieCowOfferHayQty);
        int rebate = Math.Max(0, ModEntry.Config.MarnieCowOfferRebate);

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = "(O)178",
            ObjectiveItemName = "Hay",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new MoneyReward(rebate),
                new FriendshipReward("Marnie", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieCowOffer.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieCowOffer.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieCowOffer.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieCowOffer.targetMessage")
        };
    }

    /// CSV row 45. OneShot on `chickenEggsLayed >= 1`. Mail+Ship 10 eggs through the bin.
    /// Reward = `GoldBasicBase` plus the Mayonnaise Machine crafting recipe (granted only
    /// if the player doesn't already know it; `RewardApplier` no-ops on duplicate
    /// recipes).

    /// CSV row 45. OneShot on `chickenEggsLayed >= 1`. Mail+Ship 10 eggs through the bin.
    /// Reward = `GoldBasicBase` plus the Mayonnaise Machine crafting recipe (granted only
    /// if the player doesn't already know it; `RewardApplier` no-ops on duplicate
    /// recipes).
    private static QuestPosting? MarnieEggRequest(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        int qty = Math.Max(1, ModEntry.Config.MarnieEggRequestQty);
        int gold = ctx.Config.GoldBasicBase;

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Ship,
            QuestGiver = "Marnie",
            ObjectiveItemId = "(O)176",
            ObjectiveItemName = "Egg",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new RecipeReward("Mayonnaise Machine", RecipeKind.Crafting),
                new FriendshipReward("Marnie", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieEgg.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieEgg.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieEgg.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieEgg.targetMessage")
        };
    }

    /// CSV row 47. Same shape as the Egg Request but for milk. Triggered on
    /// `cowMilkProduced >= 1` (covers vanilla white + brown cows; `goatMilkProduced` is
    /// a separate stat and intentionally not unioned in — goat milk is a separate
    /// progression beat). Reward = `GoldBasicBase` plus the Cheese Press crafting recipe.

    /// CSV row 47. Same shape as the Egg Request but for milk. Triggered on
    /// `cowMilkProduced >= 1` (covers vanilla white + brown cows; `goatMilkProduced` is
    /// a separate stat and intentionally not unioned in — goat milk is a separate
    /// progression beat). Reward = `GoldBasicBase` plus the Cheese Press crafting recipe.
    private static QuestPosting? MarnieMilkRequest(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        int qty = Math.Max(1, ModEntry.Config.MarnieMilkRequestQty);
        int gold = ctx.Config.GoldBasicBase;

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Ship,
            QuestGiver = "Marnie",
            ObjectiveItemId = "(O)184",
            ObjectiveItemName = "Milk",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new RecipeReward("Cheese Press", RecipeKind.Crafting),
                new FriendshipReward("Marnie", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieMilk.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieMilk.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieMilk.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieMilk.targetMessage")
        };
    }

    /// CSV row 64. Two JSON entries (one for Coop, one for Barn) gate this generator on
    /// `BuildingBuilt` + `not:BuildingExists Silo`, so the offer fires the first time the
    /// player builds either an animal house without already owning a silo. Mail+ItemDelivery
    /// for one randomly-picked silo material — Stone, Clay, or a Copper Bar in the
    /// quantities the silo recipe actually consumes (100 / 10 / 5 respectively). Reward =
    /// a `RobinSiloOfferRebate` gold rebate to cover the silo's gold cost plus a chunk of
    /// the leftover materials. The "free silo build" flavor in the CSV is approximated
    /// by the rebate; a real Robin-build-menu hook is deferred (the framework would need
    /// to patch `CarpenterMenu` to grant a discounted/free silo, which is out of scope
    /// for the 9.5 sweep).

    /// CSV row 64. Two JSON entries (one for Coop, one for Barn) gate this generator on
    /// `BuildingBuilt` + `not:BuildingExists Silo`, so the offer fires the first time the
    /// player builds either an animal house without already owning a silo. Mail+ItemDelivery
    /// for one randomly-picked silo material — Stone, Clay, or a Copper Bar in the
    /// quantities the silo recipe actually consumes (100 / 10 / 5 respectively). Reward =
    /// a `RobinSiloOfferRebate` gold rebate to cover the silo's gold cost plus a chunk of
    /// the leftover materials. The "free silo build" flavor in the CSV is approximated
    /// by the rebate; a real Robin-build-menu hook is deferred (the framework would need
    /// to patch `CarpenterMenu` to grant a discounted/free silo, which is out of scope
    /// for the 9.5 sweep).
    private static QuestPosting? RobinSiloOffer(QuestContext ctx)
    {
        if (!ModEntry.Config.AnimalQuestsEnabled)
            return null;
        if (Game1.getCharacterFromName("Robin") == null)
            return null;

        // Bail if the player has somehow already built a silo between trigger eval and
        // this generator run (e.g. they bought a silo same day as the coop). The trigger's
        // Available block already gates on `not:BuildingExists Silo` but generators are
        // robust to this edge case for safety.
        var farm = Game1.getFarm();
        if (farm != null)
        {
            foreach (var b in farm.buildings)
                if (string.Equals(b.buildingType.Value, "Silo", StringComparison.OrdinalIgnoreCase))
                    return null;
        }

        // Roll one of the three silo materials. Stone / Clay / Copper Bar are the actual
        // build inputs vanilla consumes from `Data/Buildings`'s Silo entry, so picking from
        // them keeps the request grounded in the recipe.
        var materials = new (string Id, string Name, int Qty)[]
        {
            ("(O)390", "Stone", Math.Max(1, ModEntry.Config.RobinSiloOfferStoneQty)),
            ("(O)330", "Clay", Math.Max(1, ModEntry.Config.RobinSiloOfferClayQty)),
            ("(O)334", "Copper Bar", Math.Max(1, ModEntry.Config.RobinSiloOfferCopperBarQty))
        };
        var pick = materials[Game1.random.Next(materials.Length)];

        int rebate = Math.Max(0, ModEntry.Config.RobinSiloOfferRebate);

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Robin",
            ObjectiveItemId = pick.Id,
            ObjectiveItemName = pick.Name,
            ObjectiveQuantity = pick.Qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(rebate),
                new FriendshipReward("Robin", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.robinSilo.title"),
            Description = ModEntry.I18n.Get("quest.animal.robinSilo.description", new { qty = pick.Qty, item = pick.Name }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.robinSilo.objective", new { qty = pick.Qty, item = pick.Name }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.robinSilo.targetMessage")
        };
    }

    // -------------------- Phase 9.5g: Multi-step / misc --------------------

    /// CSV row 15. Daily-board ItemDeliveryQuest. Caroline asks for off-season edible
    /// forage or flowers she loves or likes (no herbs) so she can brew a new batch of
    /// tea. Off-season pool: Y1 = seasons that have already passed in the current year
    /// (so Y1 spring skips), Y2+ = every season except the current one. Quantity scales
    /// with foraging level when DifficultyScaling is on; flat 5 when off. Reward =
    /// `FriendshipMid` to Caroline + Tea Leaves equal to twice the requested quantity.
}
