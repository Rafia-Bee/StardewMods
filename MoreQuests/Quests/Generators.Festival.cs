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
    /// Single-objective Ship quest. Player ships Battery Pack OR Coal into the farm
    /// shipping bin; the framework's DayEnding observer credits each match by weight,
    /// where one Battery Pack equals 15 Coal of "fuel". The reward Pearl arrives by mail
    /// the next morning. Mining-skill scaling: base = 15 fuel (= 1 battery / 15 coal),
    /// scales 1.5× per mining level when DifficultyScaling is on. With scaling off it's
    /// a fixed 30 fuel target (= 2 batteries / 30 coal).
    private static QuestPosting? SubmarineFuel(QuestContext ctx)
    {
        const int batteryWeight = 15;
        int totalFuel = ctx.Config.DifficultyScaling
            ? Math.Max(15, (int)Math.Floor(15 * 1.5 * Game1.player.MiningLevel))
            : 30;

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Ship,
            QuestGiver = "Captain",
            ObjectiveItemId = "(O)787",
            ObjectiveItemName = "Battery Pack",
            ObjectiveItemWeight = batteryWeight,
            AlternativeObjectiveItemIds = { "(O)382" },
            AlternativeObjectiveItemWeights = { 1 },
            ObjectiveQuantity = totalFuel,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new MailReward("RafiaBee.MoreQuests.SubmarineFuelReward", MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.festival.submarineFuel.title"),
            Description = ModEntry.I18n.Get("quest.festival.submarineFuel.description"),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.submarineFuel.objective", new { batteries = Math.Max(1, (int)Math.Ceiling(totalFuel / (double)batteryWeight)), coal = totalFuel }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.submarineFuel.targetMessage")
        };
    }

    /// Multi-step Ship Adventure: ship Void Essence + Bat Wings + Solar Essence to the
    /// shipping bin, in any order. Combat-skill scaling: count = max(1, 2 × CombatLevel)
    /// per item when DifficultyScaling is on; fixed 3 of each when off. Reward = Book of
    /// Mysteries via inventory add on completion.

    /// Multi-step Ship Adventure: ship Void Essence + Bat Wings + Solar Essence to the
    /// shipping bin, in any order. Combat-skill scaling: count = max(1, 2 × CombatLevel)
    /// per item when DifficultyScaling is on; fixed 3 of each when off. Reward = Book of
    /// Mysteries via inventory add on completion.
    private static QuestPosting? WizardsRitualMaterials(QuestContext ctx)
    {
        int countPer = ctx.Config.DifficultyScaling
            ? Math.Max(1, 2 * Game1.player.CombatLevel)
            : 3;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipVoidEssence",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)769" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.step.voidEssence", new { count = countPer })
            },
            new AdventureStepState
            {
                Name = "ShipBatWing",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)767" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.step.batWing", new { count = countPer })
            },
            new AdventureStepState
            {
                Name = "ShipSolarEssence",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)768" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.step.solarEssence", new { count = countPer })
            }
        }, giver: "M. Rasmodius");

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "M. Rasmodius",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new MailReward("RafiaBee.MoreQuests.WizardsRitualReward", MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.festival.wizardsRitual.title"),
            Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.description", new { count = countPer }),
            PreBuiltQuest = quest
        };
    }

    /// Multi-step Deliver Adventure: hand Flour + Sugar + Egg to Evelyn in any order.
    /// Farming-skill scaling: count = max(3, 6 × FarmingLevel) per ingredient when
    /// DifficultyScaling is on; fixed 3 of each when off. Egg step accepts ANY edible egg
    /// (Object Category -5 with non-inedible Edibility) via the `$edible-egg` token, so
    /// modded eggs (Hootin' & Hollerin' Owl, SVE Goose, VMV Speckled Fowl, etc.) all
    /// count alongside the vanilla white/brown chicken / duck / ostrich / void eggs.
    /// Dinosaur Egg is excluded because vanilla marks it `Edibility = -300` (inedible).

    /// Multi-step Deliver Adventure: hand Flour + Sugar + Egg to Evelyn in any order.
    /// Farming-skill scaling: count = max(3, 6 × FarmingLevel) per ingredient when
    /// DifficultyScaling is on; fixed 3 of each when off. Egg step accepts ANY edible egg
    /// (Object Category -5 with non-inedible Edibility) via the `$edible-egg` token, so
    /// modded eggs (Hootin' & Hollerin' Owl, SVE Goose, VMV Speckled Fowl, etc.) all
    /// count alongside the vanilla white/brown chicken / duck / ostrich / void eggs.
    /// Dinosaur Egg is excluded because vanilla marks it `Edibility = -300` (inedible).
    private static QuestPosting? HolidayCookies(QuestContext ctx)
    {
        int countPer = ctx.Config.DifficultyScaling
            ? Math.Max(3, 6 * Game1.player.FarmingLevel)
            : 3;

        var eggIds = new List<string> { "$edible-egg" };

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DeliverFlour",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Evelyn" },
                Items = new List<string> { "(O)246" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.holidayCookies.step.flour", new { count = countPer })
            },
            new AdventureStepState
            {
                Name = "DeliverSugar",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Evelyn" },
                Items = new List<string> { "(O)245" },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.holidayCookies.step.sugar", new { count = countPer })
            },
            new AdventureStepState
            {
                Name = "DeliverEggs",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Evelyn" },
                Items = eggIds,
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.holidayCookies.step.eggs", new { count = countPer })
            }
        }, giver: "Evelyn", completionDialogue: ModEntry.I18n.Get("quest.festival.holidayCookies.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Evelyn",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new FriendshipReward("Evelyn", ctx.Config.FriendshipLarge),
                new ObjectReward("(O)223", 6)
            },
            Title = ModEntry.I18n.Get("quest.festival.holidayCookies.title"),
            Description = ModEntry.I18n.Get("quest.festival.holidayCookies.description", new { count = countPer }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.holidayCookies.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Phase 7c: Check on Friends --------------------

    /// Two-step Adventure: pick N met villagers + a separate giver, talk to all N (in
    /// any order — the Talk step's CreditedKeys enforces uniqueness so the same NPC
    /// can't be counted twice), then report back to the giver. Requires at least
    /// CheckOnFriendsCount + 1 met villagers (otherwise there are fewer NPCs than the
    /// quest expects). Reward = FriendshipIntermediate to the giver only.

    /// Spring 6 (Egg Festival prep). Gus is taste-testing dishes for the festival;
    /// player delivers spring-themed ingredients, gets a "sample" cooked dish back as
    /// reward. CSV row 30. Reward kind = `Dish` only (no Festival Bonus), so no
    /// dependency on the Phase 9 `FestivalBias` reward kind.
    private static QuestPosting? GusFestivalFeastSpring(QuestContext ctx)
    {

        var ingredient = PickSpringIngredient(ctx);
        if (ingredient == null)
            return null;
        var sampleDish = PickSpringSampleDish(ctx);
        if (sampleDish == null)
            return null;

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(3, 2 * Game1.player.FarmingLevel)
            : 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DeliverIngredient",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Gus" },
                Items = new List<string> { ingredient.QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.festival.gusSpring.step.deliver", new { count = qty, item = ingredient.DisplayName })
            }
        }, giver: "Gus", completionDialogue: ModEntry.I18n.Get("quest.festival.gusSpring.targetMessage", new { dish = sampleDish.DisplayName }));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new ObjectReward(sampleDish.QualifiedItemId) },
            Title = ModEntry.I18n.Get("quest.festival.gusSpring.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusSpring.description", new { count = qty, item = ingredient.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusSpring.targetMessage", new { dish = sampleDish.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    /// Winter 18 (Winter Star prep). Ship winter-themed forageables; reward is
    /// FriendshipMultiSmall to every met villager (CSV row 33: "Only +friendship
    /// with NPCs farmer has already met."). Stacked FriendshipReward entries — one
    /// per met villager — get applied at completion via the existing reward pipeline.

    /// Winter 18 (Winter Star prep). Ship winter-themed forageables; reward is
    /// FriendshipMultiSmall to every met villager (CSV row 33: "Only +friendship
    /// with NPCs farmer has already met."). Stacked FriendshipReward entries — one
    /// per met villager — get applied at completion via the existing reward pipeline.
    private static QuestPosting? GusFestivalFeastWinter(QuestContext ctx)
    {

        var winterForage = ctx.Config.ForagingIgnoresVisitedLocations
            ? ctx.Items.GetForageItems("winter")
            : ctx.Items.GetForageItemsInVisitedLocations("winter");
        if (winterForage.Count < 3)
            return null;

        // Sample 3 distinct winter forageables for parallel Ship steps.
        var pool = new List<ResolvedItem>(winterForage);
        var picks = new List<ResolvedItem>(3);
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            picks.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(3, 2 * Game1.player.FarmingLevel)
            : 5;

        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            steps.Add(new AdventureStepState
            {
                Name = "ShipItem" + i,
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { picks[i].QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.festival.gusWinter.step.ship", new { count = qty, item = picks[i].DisplayName })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: "Gus");

        var posting = new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Title = ModEntry.I18n.Get("quest.festival.gusWinter.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusWinter.description", new
            {
                count = qty,
                item1 = picks.Count > 0 ? picks[0].DisplayName : string.Empty,
                item2 = picks.Count > 1 ? picks[1].DisplayName : string.Empty,
                item3 = picks.Count > 2 ? picks[2].DisplayName : string.Empty
            }),
            PreBuiltQuest = quest
        };

        // FriendshipMultiSmall to every met villager. Iterating
        // `Game1.player.friendshipData` covers vanilla + modded NPCs uniformly.
        foreach (var (name, _) in Game1.player.friendshipData.Pairs)
        {
            var npc = Game1.getCharacterFromName(name);
            if (npc == null || npc.IsMonster || !npc.IsVillager)
                continue;
            posting.Rewards.Add(new FriendshipReward(name, ctx.Config.FriendshipMultiSmall));
        }

        return posting;
    }

    /// Curated vanilla spring cooking ingredients. Modded ingredient pickup is a
    /// follow-up; for now we keep the pool focused on items the player can plausibly
    /// produce on the farm or forage by Spring 6 (the trigger date is early in the
    /// season so deep-game crops aren't reachable yet).

    /// Curated vanilla spring cooking ingredients. Modded ingredient pickup is a
    /// follow-up; for now we keep the pool focused on items the player can plausibly
    /// produce on the farm or forage by Spring 6 (the trigger date is early in the
    /// season so deep-game crops aren't reachable yet).
    private static readonly (string Id, string Name)[] SpringIngredientPool =
    {
        ("(O)20", "Leek"),
        ("(O)16", "Wild Horseradish"),
        ("(O)18", "Daffodil"),
        ("(O)22", "Dandelion"),
        ("(O)399", "Spring Onion")
    };

    /// Curated vanilla spring "sample" dishes Gus could plausibly hand back as a taste
    /// from his potluck testing. All have spring-themed ingredients in their vanilla
    /// recipes, so they read as in-character for the Egg Festival prep flavour.

    /// Curated vanilla spring "sample" dishes Gus could plausibly hand back as a taste
    /// from his potluck testing. All have spring-themed ingredients in their vanilla
    /// recipes, so they read as in-character for the Egg Festival prep flavour.
    private static readonly (string Id, string Name)[] SpringSampleDishPool =
    {
        ("(O)196", "Salad"),
        ("(O)244", "Roots Platter"),
        ("(O)457", "Vegetable Medley"),
        ("(O)195", "Omelet")
    };

    private static ResolvedItem? PickSpringIngredient(QuestContext ctx)
    {
        var (id, _) = SpringIngredientPool[Game1.random.Next(SpringIngredientPool.Length)];
        return ctx.Items.TryResolveItem(id);
    }

    private static ResolvedItem? PickSpringSampleDish(QuestContext ctx)
    {
        var (id, _) = SpringSampleDishPool[Game1.random.Next(SpringSampleDishPool.Length)];
        return ctx.Items.TryResolveItem(id);
    }

    // -------------------- Phase 8a: Preserves Season (SpecialOrder) --------------------

    /// Vanilla artisan-good context tags. Each entry is one shippable category the order
    /// can ask for. The synthetic `id_o_<itemid>` tag (vanilla auto-generates these per
    /// `Utility.getStandardDescriptionFromItem`) lets us target a single specific item
    /// without needing the item to declare a custom context tag in `Data/Objects`.

    /// Curated vanilla fall ingredients. CSV row 31 calls for a "large" delivery; we keep
    /// the pool focused on items the player can plausibly produce or forage by Fall 8.
    private static readonly (string Id, string Name)[] FallIngredientPool =
    {
        ("(O)24", "Parsnip"),
        ("(O)266", "Red Cabbage"),
        ("(O)272", "Eggplant"),
        ("(O)270", "Corn"),
        ("(O)276", "Pumpkin"),
        ("(O)278", "Bok Choy"),
        ("(O)408", "Hazelnut"),
        ("(O)404", "Common Mushroom")
    };

    /// Vanilla fall-themed sample dishes Gus could plausibly hand back from his Fair
    /// taste-testing. Picked for ingredients in the fall pool above.

    /// Vanilla fall-themed sample dishes Gus could plausibly hand back from his Fair
    /// taste-testing. Picked for ingredients in the fall pool above.
    private static readonly (string Id, string Name)[] FallSampleDishPool =
    {
        ("(O)205", "Fried Mushroom"),
        ("(O)225", "Fried Eel"),
        ("(O)240", "Farmer's Lunch"),
        ("(O)244", "Roots Platter"),
        ("(O)457", "Vegetable Medley"),
        ("(O)607", "Roasted Hazelnuts"),
        ("(O)608", "Pumpkin Pie")
    };

    /// Vanilla summer-themed dishes — ingredients that can be sourced by Summer 8 on a
    /// first-year save. Tighter than Fall because the trigger is earlier in the season.

    /// Vanilla summer-themed dishes — ingredients that can be sourced by Summer 8 on a
    /// first-year save. Tighter than Fall because the trigger is earlier in the season.
    private static readonly (string Id, string Name)[] SummerIngredientPool =
    {
        ("(O)190", "Cauliflower"),
        ("(O)20", "Leek"),
        ("(O)188", "Green Bean"),
        ("(O)24", "Parsnip"),
        ("(O)252", "Rhubarb"),
        ("(O)254", "Melon"),
        ("(O)256", "Tomato"),
        ("(O)258", "Blueberry"),
        ("(O)260", "Hot Pepper"),
        ("(O)262", "Wheat"),
        ("(O)16", "Wild Horseradish")
    };

    /// Fall 8 prep for the Stardew Valley Fair. CSV row 31. Multi-step Adventure: deliver
    /// `GusFestivalFeastIngredientCount` distinct fall ingredients to Gus. Reward = a sample
    /// dish via `ObjectReward` plus a `FestivalBias` Fair magnitude — the bias bumps the
    /// player's grange score on Fall 16. Tier = Intermediate per the CSV.

    /// Fall 8 prep for the Stardew Valley Fair. CSV row 31. Multi-step Adventure: deliver
    /// `GusFestivalFeastIngredientCount` distinct fall ingredients to Gus. Reward = a sample
    /// dish via `ObjectReward` plus a `FestivalBias` Fair magnitude — the bias bumps the
    /// player's grange score on Fall 16. Tier = Intermediate per the CSV.
    private static QuestPosting? GusFestivalFeastFall(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Gus") == null)
            return null;

        int ingredientCount = ModEntry.Config.GusFestivalFeastIngredientCount;
        var picks = PickDistinctIngredients(ctx, FallIngredientPool, ingredientCount);
        if (picks.Count == 0)
            return null;
        var sampleDish = PickSample(ctx, FallSampleDishPool);
        if (sampleDish == null)
            return null;

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(3, 2 * Game1.player.FarmingLevel)
            : 5;

        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            steps.Add(new AdventureStepState
            {
                Name = "DeliverIngredient" + i,
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Gus" },
                Items = new List<string> { picks[i].QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.festival.gusFall.step.deliver", new { count = qty, item = picks[i].DisplayName })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: "Gus", completionDialogue: ModEntry.I18n.Get("quest.festival.gusFall.targetMessage", new { dish = sampleDish.DisplayName }));

        string ingredientList = string.Join(", ", picks.Select(p => p.DisplayName));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new ObjectReward(sampleDish.QualifiedItemId),
                new FestivalBiasReward(FestivalKind.Fair, ModEntry.Config.FestivalBiasFairMagnitude)
            },
            Title = ModEntry.I18n.Get("quest.festival.gusFall.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusFall.description", new { count = qty, ingredients = ingredientList }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusFall.targetMessage", new { dish = sampleDish.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    /// Summer 8 prep for the Luau (Summer 11). CSV row 32. Multi-step Adventure: deliver 3
    /// summer/spring-themed ingredients to Gus. Reward = `FestivalBias` Luau magnitude only
    /// — no sample dish, since the CSV explicitly calls out "Festival Bonus" as the only
    /// reward kind (a higher base potluck score). Tier = Intermediate.

    /// Summer 8 prep for the Luau (Summer 11). CSV row 32. Multi-step Adventure: deliver 3
    /// summer/spring-themed ingredients to Gus. Reward = `FestivalBias` Luau magnitude only
    /// — no sample dish, since the CSV explicitly calls out "Festival Bonus" as the only
    /// reward kind (a higher base potluck score). Tier = Intermediate.
    private static QuestPosting? GusFestivalFeastSummer(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Gus") == null)
            return null;

        int ingredientCount = ModEntry.Config.GusFestivalFeastIngredientCount;
        var picks = PickDistinctIngredients(ctx, SummerIngredientPool, ingredientCount);
        if (picks.Count == 0)
            return null;

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(3, 2 * Game1.player.FarmingLevel)
            : 5;

        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            steps.Add(new AdventureStepState
            {
                Name = "DeliverIngredient" + i,
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Gus" },
                Items = new List<string> { picks[i].QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.festival.gusSummer.step.deliver", new { count = qty, item = picks[i].DisplayName })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: "Gus", completionDialogue: ModEntry.I18n.Get("quest.festival.gusSummer.targetMessage"));

        string ingredientList = string.Join(", ", picks.Select(p => p.DisplayName));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new FestivalBiasReward(FestivalKind.Luau, ModEntry.Config.FestivalBiasLuauMagnitude)
            },
            Title = ModEntry.I18n.Get("quest.festival.gusSummer.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusSummer.description", new { count = qty, ingredients = ingredientList }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusSummer.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// Picks up to `count` distinct entries from `pool`, dropping any whose id doesn't
    /// resolve (modded mismatch, Game1 not yet ready). Returns the resolved items in pick
    /// order. May return fewer than `count` when the pool can't satisfy that many resolves.

    /// Picks up to `count` distinct entries from `pool`, dropping any whose id doesn't
    /// resolve (modded mismatch, Game1 not yet ready). Returns the resolved items in pick
    /// order. May return fewer than `count` when the pool can't satisfy that many resolves.
    private static List<ResolvedItem> PickDistinctIngredients(QuestContext ctx, (string Id, string Name)[] pool, int count)
    {
        var picks = new List<ResolvedItem>(count);
        var indices = new List<int>(pool.Length);
        for (int i = 0; i < pool.Length; i++)
            indices.Add(i);
        for (int i = 0; i < count && indices.Count > 0; i++)
        {
            int j = Game1.random.Next(indices.Count);
            int poolIdx = indices[j];
            indices.RemoveAt(j);
            var resolved = ctx.Items.TryResolveItem(pool[poolIdx].Id);
            if (resolved != null)
                picks.Add(resolved);
            else
                i--; // try another
        }
        return picks;
    }

    private static ResolvedItem? PickSample(QuestContext ctx, (string Id, string Name)[] pool)
    {
        var (id, _) = pool[Game1.random.Next(pool.Length)];
        return ctx.Items.TryResolveItem(id);
    }

    /// CSV row 50. Winter 13 (Night Market middle day). Picks a met NPC to send the
    /// player on a non-current-season seed restock. Filter walks `Data/Crops` and keeps
    /// any seed whose `Seasons` list excludes Winter so the request reads as "stock up
    /// for next year while the Night Market's Magic Boat is in town". Reward =
    /// `FriendshipBasic` with the picked NPC.
    private static QuestPosting? MerchantUnpacking(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count == 0)
            return null;
        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];

        var seeds = ResolveNonWinterSeeds(ctx);
        if (seeds.Count == 0)
            return null;
        var pick = seeds[Game1.random.Next(seeds.Count)];

        int qty = Game1.random.Next(3, 7);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Ship,
            QuestGiver = giver,
            ObjectiveItemId = pick.QualifiedItemId,
            ObjectiveItemName = pick.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.festival.merchantUnpacking.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.festival.merchantUnpacking.description", new { npc = giver, qty, item = pick.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.merchantUnpacking.objective", new { qty, item = pick.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.merchantUnpacking.targetMessage")
        };
    }

    /// CSV row 52. Daily-board SlayMonster (any monster). Marlon dispatches the request
    /// from the Adventurer's Guild. Reward = `GoldIntermediateBase` + one random item
    /// from the framework's combat-food pool (seeded by this content mod with vanilla
    /// combat-buff foods at `RegistrationOpen`; consumer mods can extend through
    /// `IMoreQuestsApi.RegisterCombatFood`).

    /// CSV row 66. Winter 22 DateLocked. Resolves the player's Winter Star recipient via
    /// `Utility.GetRandomWinterStarParticipant` (the same deterministic random the game
    /// uses to assign secret-santa pairings) and embeds a hint about the recipient's
    /// loved gifts. Player can opt out via `SecretGiftHintEnabled`. Quest is a single
    /// Talk step targeted at the recipient — the festival event surfaces dialogue with
    /// them naturally so the quest closes on Winter 25 without bespoke event hooks.
    private static QuestPosting? SecretGiftHint(QuestContext ctx)
    {
        if (!ModEntry.Config.SecretGiftHintEnabled)
            return null;

        NPC? recipient;
        try
        {
            recipient = StardewValley.Utility.GetRandomWinterStarParticipant();
        }
        catch (Exception ex)
        {
            ctx.Monitor.Log($"SecretGiftHint: could not resolve Winter Star recipient: {ex.Message}", LogLevel.Trace);
            return null;
        }
        if (recipient == null)
            return null;

        var lovedNames = ResolveLovedItemNames(ctx, recipient.Name, max: 3);
        string lovedList = lovedNames.Count > 0
            ? string.Join(", ", lovedNames)
            : ModEntry.I18n.Get("quest.festival.secretGiftHint.fallback");

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DeliverWinterStarGift",
                Kind = AdventureStepKind.Talk,
                Targets = new List<string> { recipient.Name },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.festival.secretGiftHint.step", new { recipient = recipient.displayName, items = lovedList })
            }
        }, giver: "Lewis", completionDialogue: ModEntry.I18n.Get("quest.festival.secretGiftHint.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Special,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Lewis",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Title = ModEntry.I18n.Get("quest.festival.secretGiftHint.title"),
            Description = ModEntry.I18n.Get("quest.festival.secretGiftHint.description", new { recipient = recipient.displayName, items = lovedList }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.secretGiftHint.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// CSV row 73. Winter 20 DateLocked, mod-gated on Si.ExtraCraftingMaterials (Nexus
    /// 25467). Lewis asks the player to ship Paper + Tape so the town can wrap their
    /// Winter Star gifts. Reward = a Book of Stars from the same mod. Item ids are
    /// configurable so the quest still works if the source mod renames items.

    /// CSV row 73. Winter 20 DateLocked, mod-gated on Si.ExtraCraftingMaterials (Nexus
    /// 25467). Lewis asks the player to ship Paper + Tape so the town can wrap their
    /// Winter Star gifts. Reward = a Book of Stars from the same mod. Item ids are
    /// configurable so the quest still works if the source mod renames items.
    private static QuestPosting? WrappingPaper(QuestContext ctx)
    {
        if (!ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.SiExtraCraftingMaterials))
            return null;

        string paperId = string.IsNullOrWhiteSpace(ModEntry.Config.WrappingPaperPaperId)
            ? "Si.ECM_Paper"
            : ModEntry.Config.WrappingPaperPaperId;
        string tapeId = string.IsNullOrWhiteSpace(ModEntry.Config.WrappingPaperTapeId)
            ? "Si.ECM_Tape"
            : ModEntry.Config.WrappingPaperTapeId;
        string bookId = string.IsNullOrWhiteSpace(ModEntry.Config.WrappingPaperBookOfStarsId)
            ? "Si.ECM_BookOfStars"
            : ModEntry.Config.WrappingPaperBookOfStarsId;

        var paper = ctx.Items.TryResolveItem(paperId);
        var tape = ctx.Items.TryResolveItem(tapeId);
        if (paper == null || tape == null)
        {
            ctx.Monitor.Log($"WrappingPaper: Paper ({paperId}) or Tape ({tapeId}) item not found in registry; skipping. Override item ids in More Quests config if the source mod renamed them.", LogLevel.Trace);
            return null;
        }

        const string giver = "Lewis";
        const int qtyPerItem = 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipPaper",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { paper.QualifiedItemId },
                Count = qtyPerItem,
                Description = ModEntry.I18n.Get("quest.festival.wrappingPaper.step.paper", new { count = qtyPerItem, item = paper.DisplayName })
            },
            new AdventureStepState
            {
                Name = "ShipTape",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { tape.QualifiedItemId },
                Count = qtyPerItem,
                Description = ModEntry.I18n.Get("quest.festival.wrappingPaper.step.tape", new { count = qtyPerItem, item = tape.DisplayName })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new ObjectReward(bookId) },
            Title = ModEntry.I18n.Get("quest.festival.wrappingPaper.title"),
            Description = ModEntry.I18n.Get("quest.festival.wrappingPaper.description", new { paper = paper.DisplayName, tape = tape.DisplayName, count = qtyPerItem }),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Phase 9.5a helpers --------------------

    /// Picks a single random loved or liked item id from the NPC's `Data/NPCGiftTastes`
    /// entry, then resolves it through `ItemResolver`. Skips items that can't be
    /// resolved (modded id whose source mod isn't loaded, etc.) and falls back through
    /// the candidate pool until one resolves.

    /// Walks `Data/Crops` for seeds whose Seasons list excludes Winter. Returns the
    /// resolved seed items so the picker can hand one to the Ship objective.
    private static List<ResolvedItem> ResolveNonWinterSeeds(QuestContext ctx)
    {
        var results = new List<ResolvedItem>();
        try
        {
            foreach (var (seedId, data) in ctx.Data.Crops)
            {
                if (data.Seasons == null || data.Seasons.Count == 0)
                    continue;
                bool hasWinter = false;
                foreach (var s in data.Seasons)
                {
                    if (string.Equals(s.ToString(), "winter", StringComparison.OrdinalIgnoreCase))
                    {
                        hasWinter = true;
                        break;
                    }
                }
                if (hasWinter)
                    continue;
                var seed = ctx.Items.TryResolveItem("(O)" + seedId);
                if (seed != null)
                    results.Add(seed);
            }
        }
        catch (Exception ex)
        {
            ctx.Monitor.Log($"ResolveNonWinterSeeds: {ex.Message}", LogLevel.Warn);
        }
        return results;
    }

    /// Vanilla rare forage merged with anything carrying the `forage_item` context tag
    /// AND not an obviously common pick (drops anything tagged `season_<current>` so the
    /// daily-board posting feels rare, not an expanded SeasonalForaging).

    /// Curated decor pool for the Dance of the Moonlight Jellies festival reward.
    /// Vanilla Big-Craftable / Furniture ids picked for thematic fit (lights, decor).
    /// Unknown ids silently no-op via `RewardApplier`, so over-listing is safe across
    /// game versions.
    private static readonly string[] MoonlightJelliesDecorPool = { "(BC)21", "(BC)74", "(BC)272" };

    private static readonly string[] EggFestivalDecorPool = { "(BC)272", "(BC)143", "(BC)74" };

    private static readonly string[] LuauDecorPool = { "(BC)73", "(BC)74", "(BC)272" };

    /// Picks one item id from a curated decor pool. Returns the id verbatim — any
    /// resolution / instantiation happens later in `RewardApplier.ApplyOne` via
    /// `ItemRegistry.Create`. Empty pools return null (caller skips the reward step).

    /// Picks one item id from a curated decor pool. Returns the id verbatim — any
    /// resolution / instantiation happens later in `RewardApplier.ApplyOne` via
    /// `ItemRegistry.Create`. Empty pools return null (caller skips the reward step).
    private static string? PickDecor(string[] pool)
    {
        if (pool == null || pool.Length == 0)
            return null;
        return pool[Game1.random.Next(pool.Length)];
    }

    /// Splits a comma-separated NPC list (from `ModConfig.EastScarpFestivalNpcs` /
    /// `RidgesideFestivalNpcs`) into a deduplicated list of trimmed names. Empty entries
    /// are dropped; case-insensitive dedup so "Rosa, rosa" only counts once.

    /// Splits a comma-separated NPC list (from `ModConfig.EastScarpFestivalNpcs` /
    /// `RidgesideFestivalNpcs`) into a deduplicated list of trimmed names. Empty entries
    /// are dropped; case-insensitive dedup so "Rosa, rosa" only counts once.
    private static List<string> SplitNpcList(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
            return result;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            if (seen.Add(trimmed))
                result.Add(trimmed);
        }
        return result;
    }

    /// Row 20 — Dance of the Moonlight Jellies Festival Decor Supply (Lewis, Summer 24).
    /// Two-step Ship Adventure: Torches + Wood. Reward = GoldBasicBase + one random decor
    /// item from a curated Moonlight-Jellies pool. Decor-shipping bypass enabled in case
    /// any of the modded extension items in the pool wouldn't normally ship.

    /// Row 20 — Dance of the Moonlight Jellies Festival Decor Supply (Lewis, Summer 24).
    /// Two-step Ship Adventure: Torches + Wood. Reward = GoldBasicBase + one random decor
    /// item from a curated Moonlight-Jellies pool. Decor-shipping bypass enabled in case
    /// any of the modded extension items in the pool wouldn't normally ship.
    private static QuestPosting? MoonlightJelliesFestivalDecor(QuestContext ctx)
    {
        const string giver = "Lewis";
        const int torchCount = 5;
        const int woodCount = 10;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipTorches",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)93" },
                Count = torchCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.moonlightJellies.step.torches", new { count = torchCount })
            },
            new AdventureStepState
            {
                Name = "ShipWood",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)388" },
                Count = woodCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.moonlightJellies.step.wood", new { count = woodCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec> { new MoneyReward(ctx.Config.GoldBasicBase) };
        var decor = PickDecor(MoonlightJelliesDecorPool);
        if (!string.IsNullOrEmpty(decor))
            rewards.Add(new ObjectReward(decor));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.moonlightJellies.title"),
            Description = ModEntry.I18n.Get("quest.festival.moonlightJellies.description", new { torches = torchCount, wood = woodCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 22 — Egg Festival Decor Supply (Lewis, Spring 10). Single-step Ship Adventure
    /// for Hay Bales (a Big-Craftable that vanilla won't ship without the bypass).
    /// Reward = GoldBeginnerBase + one random decor from a curated Egg-Festival pool.

    /// Row 22 — Egg Festival Decor Supply (Lewis, Spring 10). Single-step Ship Adventure
    /// for Hay Bales (a Big-Craftable that vanilla won't ship without the bypass).
    /// Reward = GoldBeginnerBase + one random decor from a curated Egg-Festival pool.
    private static QuestPosting? EggFestivalDecor(QuestContext ctx)
    {
        const string giver = "Lewis";
        const int hayBaleCount = 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipHayBales",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(BC)45" },
                Count = hayBaleCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.eggFestivalDecor.step.hayBales", new { count = hayBaleCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec> { new MoneyReward(ctx.Config.GoldBeginnerBase) };
        var decor = PickDecor(EggFestivalDecorPool);
        if (!string.IsNullOrEmpty(decor))
            rewards.Add(new ObjectReward(decor));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.eggFestivalDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.eggFestivalDecor.description", new { count = hayBaleCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 23 — Stardew Valley Fair Decor Supply (Lewis, Fall 12). Three-step Ship
    /// Adventure: Wood + Wood Signs + flowers (any vanilla flower-category Object).
    /// Reward = `FestivalBiasReward(Fair, FestivalBiasFairMagnitude)` so the Fair-day
    /// grange judging bumps in the player's favour. Wood Signs need the decor bypass.

    /// Row 23 — Stardew Valley Fair Decor Supply (Lewis, Fall 12). Three-step Ship
    /// Adventure: Wood + Wood Signs + flowers (any vanilla flower-category Object).
    /// Reward = `FestivalBiasReward(Fair, FestivalBiasFairMagnitude)` so the Fair-day
    /// grange judging bumps in the player's favour. Wood Signs need the decor bypass.
    private static QuestPosting? FairFestivalDecor(QuestContext ctx)
    {
        const string giver = "Lewis";
        const int woodCount = 10;
        const int signCount = 3;
        const int flowerCount = 5;
        const int flowerCategory = -80;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipWood",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)388" },
                Count = woodCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.fairDecor.step.wood", new { count = woodCount })
            },
            new AdventureStepState
            {
                Name = "ShipSigns",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(BC)37" },
                Count = signCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.fairDecor.step.signs", new { count = signCount })
            },
            new AdventureStepState
            {
                Name = "ShipFlowers",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { $"$category:{flowerCategory}" },
                Count = flowerCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.fairDecor.step.flowers", new { count = flowerCount })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards =
            {
                new FestivalBiasReward(FestivalKind.Fair, Math.Max(1, ModEntry.Config.FestivalBiasFairMagnitude))
            },
            Title = ModEntry.I18n.Get("quest.festival.fairDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.fairDecor.description", new { wood = woodCount, signs = signCount, flowers = flowerCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 24 — Luau Decor Supply (Lewis, Summer 6). Three-step Ship Adventure: Fiber +
    /// Log Section ("Basic Log" furniture) + Wood Lamp-post. Reward = GoldIntermediateBase
    /// + one random decor from a curated Luau pool. Both furniture / Big-Craftable steps
    /// need the decor-shipping bypass.

    /// Row 24 — Luau Decor Supply (Lewis, Summer 6). Three-step Ship Adventure: Fiber +
    /// Log Section ("Basic Log" furniture) + Wood Lamp-post. Reward = GoldIntermediateBase
    /// + one random decor from a curated Luau pool. Both furniture / Big-Craftable steps
    /// need the decor-shipping bypass.
    private static QuestPosting? LuauFestivalDecor(QuestContext ctx)
    {
        const string giver = "Lewis";
        const int fiberCount = 10;
        const int logCount = 1;
        const int lampCount = 1;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipFiber",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)771" },
                Count = fiberCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.luauDecor.step.fiber", new { count = fiberCount })
            },
            new AdventureStepState
            {
                Name = "ShipBasicLog",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(F)1376" },
                Count = logCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.luauDecor.step.basicLog", new { count = logCount })
            },
            new AdventureStepState
            {
                Name = "ShipLampPost",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(BC)21" },
                Count = lampCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.luauDecor.step.lampPost", new { count = lampCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec> { new MoneyReward(ctx.Config.GoldIntermediateBase) };
        var decor = PickDecor(LuauDecorPool);
        if (!string.IsNullOrEmpty(decor))
            rewards.Add(new ObjectReward(decor));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.luauDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.luauDecor.description", new { fiber = fiberCount, log = logCount, lamp = lampCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 26 — Spirit's Eve Decor Supply (Lewis, Fall 22). Three-step Ship Adventure:
    /// Pumpkins + Cloth + Torches. Reward = GoldIntermediateBase + Jack o' Lantern.
    /// Decor bypass enabled mostly for parity; vanilla ships all three objective items
    /// without help.

    /// Row 26 — Spirit's Eve Decor Supply (Lewis, Fall 22). Three-step Ship Adventure:
    /// Pumpkins + Cloth + Torches. Reward = GoldIntermediateBase + Jack o' Lantern.
    /// Decor bypass enabled mostly for parity; vanilla ships all three objective items
    /// without help.
    private static QuestPosting? SpiritsEveDecor(QuestContext ctx)
    {
        const string giver = "Lewis";
        const int pumpkinCount = 5;
        const int clothCount = 3;
        const int torchCount = 5;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipPumpkins",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)276" },
                Count = pumpkinCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.step.pumpkins", new { count = pumpkinCount })
            },
            new AdventureStepState
            {
                Name = "ShipCloth",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)428" },
                Count = clothCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.step.cloth", new { count = clothCount })
            },
            new AdventureStepState
            {
                Name = "ShipTorches",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)93" },
                Count = torchCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.step.torches", new { count = torchCount })
            }
        }, giver: giver);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards =
            {
                new MoneyReward(ctx.Config.GoldIntermediateBase),
                new ObjectReward("(BC)126")
            },
            Title = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.description", new { pumpkins = pumpkinCount, cloth = clothCount, torches = torchCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 21 — East Scarp Spirit's Eve Decor Supply (Rosa, Fall 24). Mod-gated on the
    /// East Scarp / Eli & Dylan / Lurking in the Dark modset (any of the three lights up
    /// the role). Three-step Ship Adventure: purple-dye items + slime + stone. Reward =
    /// `FriendshipMultiHeart` to each named ESV festival NPC; the reward summary collapses
    /// 3+ named friendships into one generic line so the loved-by pool isn't spoiled.

    /// Row 21 — East Scarp Spirit's Eve Decor Supply (Rosa, Fall 24). Mod-gated on the
    /// East Scarp / Eli & Dylan / Lurking in the Dark modset (any of the three lights up
    /// the role). Three-step Ship Adventure: purple-dye items + slime + stone. Reward =
    /// `FriendshipMultiHeart` to each named ESV festival NPC; the reward summary collapses
    /// 3+ named friendships into one generic line so the loved-by pool isn't spoiled.
    private static QuestPosting? EastScarpSpiritsEveDecor(QuestContext ctx)
    {
        if (!MoreQuestsFramework.ModCompat.HasEs(ctx.Helper.ModRegistry))
            return null;
        const string giver = "Rosa";
        const int purpleCount = 5;
        const int slimeCount = 10;
        const int stoneCount = 20;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipPurpleDye",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "$tag:color_purple" },
                Count = purpleCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.step.purple", new { count = purpleCount })
            },
            new AdventureStepState
            {
                Name = "ShipSlime",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)766" },
                Count = slimeCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.step.slime", new { count = slimeCount })
            },
            new AdventureStepState
            {
                Name = "ShipStone",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)390" },
                Count = stoneCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.step.stone", new { count = stoneCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec>();
        foreach (var npc in SplitNpcList(ModEntry.Config.EastScarpFestivalNpcs))
            rewards.Add(new FriendshipReward(npc, ctx.Config.FriendshipMultiHeart));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.title"),
            Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.description", new { purple = purpleCount, slime = slimeCount, stone = stoneCount }),
            PreBuiltQuest = quest
        };
    }

    /// Row 25 — Ridgeside Gathering Decor Supply (Lenny, Fall 15). Mod-gated on Ridgeside
    /// Village. Three-step Ship Adventure: Tub o' Flowers + Wood + any table furniture
    /// (matched via `$tag:furniture_table` so vanilla AND modded tables count). Reward =
    /// `FriendshipMultiHeart` to each named RSV festival NPC. The Tub o' Flowers crafting
    /// recipe is granted at quest-accept by `MoreQuests.ModEntry.OnQuestAccepted` if the
    /// player doesn't already know it (so they can craft tubs without scrambling for the
    /// vanilla recipe).

    /// Row 25 — Ridgeside Gathering Decor Supply (Lenny, Fall 15). Mod-gated on Ridgeside
    /// Village. Three-step Ship Adventure: Tub o' Flowers + Wood + any table furniture
    /// (matched via `$tag:furniture_table` so vanilla AND modded tables count). Reward =
    /// `FriendshipMultiHeart` to each named RSV festival NPC. The Tub o' Flowers crafting
    /// recipe is granted at quest-accept by `MoreQuests.ModEntry.OnQuestAccepted` if the
    /// player doesn't already know it (so they can craft tubs without scrambling for the
    /// vanilla recipe).
    private static QuestPosting? RidgesideGatheringDecor(QuestContext ctx)
    {
        if (!MoreQuestsFramework.ModCompat.HasRsv(ctx.Helper.ModRegistry))
            return null;
        const string giver = "Lenny";
        const int tubCount = 1;
        const int woodCount = 20;
        const int tableCount = 1;

        string tubId = string.IsNullOrWhiteSpace(ModEntry.Config.RsvTubOFlowersId)
            ? "(BC)272"
            : ModEntry.Config.RsvTubOFlowersId;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipTubOFlowers",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { tubId },
                Count = tubCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.rsvGathering.step.tub", new { count = tubCount })
            },
            new AdventureStepState
            {
                Name = "ShipWood",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)388" },
                Count = woodCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.rsvGathering.step.wood", new { count = woodCount })
            },
            new AdventureStepState
            {
                Name = "ShipTables",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "$tag:furniture_table" },
                Count = tableCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.rsvGathering.step.table", new { count = tableCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec>();
        foreach (var npc in SplitNpcList(ModEntry.Config.RidgesideFestivalNpcs))
            rewards.Add(new FriendshipReward(npc, ctx.Config.FriendshipMultiHeart));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.rsvGathering.title"),
            Description = ModEntry.I18n.Get("quest.festival.rsvGathering.description", new { wood = woodCount }),
            PreBuiltQuest = quest
        };
    }

    // -------------------- Phase 9.5e: Fishing-track quests --------------------

    /// Curated rare-tackle pool for the Rainy Day Catch reward. All vanilla qualified ids;
    /// modded saves get the same pool unless the picker resolves to a missing id, in which
    /// case the pick falls through.

    /// Row 60 — Rainbow Platter (Trout Derby, Summer 20-21). DateLocked yearly DailyBoard
    /// posting on Summer 20: catch `FestivalFishQty` Rainbow Trout (O)138. Giver dispatched
    /// via `SaloonChef`; reward = recipe (per-giver) + `ShopDiscountReward` on the dish for
    /// vanilla Gus saves only (the framework's discount writer needs a known shop id).
    private static QuestPosting? RainbowPlatter(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        const string rainbowTroutId = "(O)138";
        int qty = Math.Max(1, ModEntry.Config.FestivalFishQty);

        string recipeName = ResolveTroutDerbyRecipe(giver);
        var rewards = new List<RewardSpec>
        {
            new RecipeReward(recipeName)
        };
        // Only Gus has a known vanilla shop; for modded givers we grant just the recipe.
        if (string.Equals(giver, "Gus", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ModEntry.Config.TroutDerbyDishGus))
        {
            rewards.Add(new ShopDiscountReward(
                ShopId: "Saloon",
                PercentOff: ModEntry.Config.ShopDiscountPercent,
                DurationDays: ModEntry.Config.ShopDiscountDurationDays,
                AppliesTo: new List<string> { ModEntry.Config.TroutDerbyDishGus },
                GuaranteedStock: 5));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = rainbowTroutId,
            ObjectiveItemName = "Rainbow Trout",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.rainbowPlatter.title"),
            Description = ModEntry.I18n.Get("quest.festival.rainbowPlatter.description", new { npc = giver, qty, recipe = recipeName }),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.rainbowPlatter.objective", new { qty, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.rainbowPlatter.targetMessage")
        };
    }

    /// Row 70 — SquidFest Showcase (Winter 12-13). DateLocked yearly posting on Winter 12:
    /// catch `FestivalFishQty` Squid (O)151. Same shape as Rainbow Platter — saloon-chef
    /// giver, recipe reward per giver, ShopDiscountReward on the dish for vanilla Gus.

    /// Row 70 — SquidFest Showcase (Winter 12-13). DateLocked yearly posting on Winter 12:
    /// catch `FestivalFishQty` Squid (O)151. Same shape as Rainbow Platter — saloon-chef
    /// giver, recipe reward per giver, ShopDiscountReward on the dish for vanilla Gus.
    private static QuestPosting? SquidFestShowcase(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        const string squidId = "(O)151";
        int qty = Math.Max(1, ModEntry.Config.FestivalFishQty);

        string recipeName = ResolveSquidFestRecipe(giver);
        var rewards = new List<RewardSpec>
        {
            new RecipeReward(recipeName)
        };
        if (string.Equals(giver, "Gus", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ModEntry.Config.SquidFestDishGus))
        {
            rewards.Add(new ShopDiscountReward(
                ShopId: "Saloon",
                PercentOff: ModEntry.Config.ShopDiscountPercent,
                DurationDays: ModEntry.Config.ShopDiscountDurationDays,
                AppliesTo: new List<string> { ModEntry.Config.SquidFestDishGus },
                GuaranteedStock: 5));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = squidId,
            ObjectiveItemName = "Squid",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.squidFest.title"),
            Description = ModEntry.I18n.Get("quest.festival.squidFest.description", new { npc = giver, qty, recipe = recipeName }),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.squidFest.objective", new { qty, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.squidFest.targetMessage")
        };
    }

    private static string ResolveTroutDerbyRecipe(string giver) => giver switch
    {
        "Pika" => ModEntry.Config.TroutDerbyRecipePika,
        "Celestine" => ModEntry.Config.TroutDerbyRecipeCelestine,
        "Rosa" => ModEntry.Config.TroutDerbyRecipeRosa,
        _ => ModEntry.Config.TroutDerbyRecipeGus
    };

    private static string ResolveSquidFestRecipe(string giver) => giver switch
    {
        "Pika" => ModEntry.Config.SquidFestRecipePika,
        "Celestine" => ModEntry.Config.SquidFestRecipeCelestine,
        "Rosa" => ModEntry.Config.SquidFestRecipeRosa,
        _ => ModEntry.Config.SquidFestRecipeGus
    };

    /// Walks Data/Locations for the fish's spawn entries, intersected with the player's
    /// visited locations, returning the first matching location key. Returns null when
    /// the fish has no spawn in any visited spot. The CSV row asks for a fish at a
    /// specific spot, so we need an actual reachable location to ground the quest in.
}
