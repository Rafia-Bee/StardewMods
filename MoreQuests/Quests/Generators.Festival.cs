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
    /// Captain's letter arrives Winter 12 (DateLocked mail delivery). Ship Battery Packs
    /// AND Coal as parallel Adventure steps, with the coal count locked to 5x the battery
    /// count. Scaling on: batteries = 1 + Random.Next(1, max(1, mining/2)+1); off: 2
    /// batteries. Coal is always 5*batteries. Deadline 3 days (active Winter 12-14,
    /// auto-fails Winter 15 morning, the day the Night Market opens). Pearl reward
    /// arrives by mail the morning after completion. Mail-only quest (no NPC turn-in),
    /// so no `TargetMessage` is set.
    private static QuestPosting? SubmarineFuel(QuestContext ctx)
    {
        int batteryQty;
        if (ctx.Config.DifficultyScaling)
        {
            int upper = Math.Max(1, Game1.player.MiningLevel / 2);
            batteryQty = 1 + Game1.random.Next(1, upper + 1);
        }
        else
        {
            batteryQty = 2;
        }
        int coalQty = 5 * batteryQty;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipBattery",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)787" },
                Count = batteryQty,
                Description = ModEntry.I18n.Get("quest.festival.submarineFuel.step.battery", new { count = batteryQty })
            },
            new AdventureStepState
            {
                Name = "ShipCoal",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)382" },
                Count = coalQty,
                Description = ModEntry.I18n.Get("quest.festival.submarineFuel.step.coal", new { count = coalQty })
            }
        }, giver: "Captain");

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Captain",
            ObjectiveQuantity = 1,
            DeadlineDays = 3,
            Rewards =
            {
                new MailReward("RafiaBee.MoreQuests.SubmarineFuelReward", MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.festival.submarineFuel.title"),
            Description = ModEntry.I18n.Get("quest.festival.submarineFuel.description", new { batteries = batteryQty, coal = coalQty }),
            PreBuiltQuest = quest
        };
    }

    /// Ship Void Essence + Bat Wings + Solar Essence in any order. Scaling on: 2*CombatLevel
    /// per item. Off: 3 each. Reward: Book of Mysteries. Letter arrives Fall 24 (DateLocked),
    /// deadline hardcoded to Fall 28 (5 active days), auto-fails Winter 1 morning.
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
            DeadlineDays = 5,
            Rewards =
            {
                new MailReward("RafiaBee.MoreQuests.WizardsRitualReward", MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.festival.wizardsRitual.title"),
            Description = ModEntry.I18n.Get("quest.festival.wizardsRitual.description", new { count = countPer }),
            PreBuiltQuest = quest
        };
    }

    /// Deliver Flour + Sugar + Egg to Evelyn in any order. Scaling on: 6*FarmingLevel per
    /// ingredient. Off: 3 each. Egg step uses $edible-egg so vanilla and modded eggs both
    /// count. Dinosaur Egg is excluded (Edibility = -300).
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

    /// Spring 6 (Egg Festival prep). Gus is taste-testing for the festival. Player delivers
    /// one spring crop AND one spring forage in parallel, gets a sample cooked dish back.
    /// Year 1 restricts to vanilla items (excluding Rhubarb, which lives in the Desert);
    /// Year 2+ opens the pool to any season_spring forage and any Data/Crops spring entry.
    /// Dish pool searches Data/CookingRecipes for any recipe whose ingredient list mentions
    /// the picked crop, the picked forage, or matches the crop's fruit/vegetable category,
    /// so modded recipes ride along automatically. Reward kind: Dish only.
    private static QuestPosting? GusFestivalFeastSpring(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Gus") == null)
            return null;

        var crop = PickSpringCrop(ctx);
        if (crop == null)
            return null;
        var forage = PickSpringForage(ctx);
        if (forage == null)
            return null;
        var sampleDish = PickSampleDishForIngredients(ctx, new[] { crop, forage }, new HashSet<string> { crop.QualifiedItemId });
        if (sampleDish == null)
            return null;

        int cropQty;
        int forageQty;
        if (ctx.Config.DifficultyScaling)
        {
            int cropUpper = Math.Max(3, 2 * Game1.player.FarmingLevel);
            cropQty = Game1.random.Next(3, cropUpper + 1);
            int forageUpper = Math.Max(2, 2 * Game1.player.ForagingLevel);
            forageQty = Game1.random.Next(2, forageUpper + 1);
        }
        else
        {
            cropQty = 5;
            forageQty = 5;
        }

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DeliverCrop",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Gus" },
                Items = new List<string> { crop.QualifiedItemId },
                Count = cropQty,
                Description = ModEntry.I18n.Get("quest.festival.gusSpring.step.deliverCrop", new { count = cropQty, item = crop.DisplayName })
            },
            new AdventureStepState
            {
                Name = "DeliverForage",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Gus" },
                Items = new List<string> { forage.QualifiedItemId },
                Count = forageQty,
                Description = ModEntry.I18n.Get("quest.festival.gusSpring.step.deliverForage", new { count = forageQty, item = forage.DisplayName })
            }
        }, giver: "Gus", completionDialogue: ModEntry.I18n.Get("quest.festival.gusSpring.targetMessage", new { dish = sampleDish.DisplayName }));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            // Spring 6 trigger, Egg Festival on Spring 13: 6 days puts the auto-fail on
            // Spring 12, one day before the festival. Same shape as Rows 20-26.
            DeadlineDays = 6,
            Rewards = { new ObjectReward(sampleDish.QualifiedItemId) },
            Title = ModEntry.I18n.Get("quest.festival.gusSpring.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusSpring.description", new
            {
                cropCount = cropQty,
                crop = crop.DisplayName,
                forageCount = forageQty,
                forage = forage.DisplayName
            }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusSpring.targetMessage", new { dish = sampleDish.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    /// Winter 18 (Winter Star prep). Ship winter forageables. Reward: FriendshipMultiSmall
    /// to every met villager (one FriendshipReward each).
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
            // Winter 18 trigger, Feast of the Winter Star on Winter 25: 6 days puts the
            // auto-fail on Winter 24, one day before the festival. Same shape as the other
            // Gus festival feasts (Rows 30-32).
            DeadlineDays = 6,
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

    /// Spring 6 ingredient pool sources from Data/Crops (`GetSeasonalCrops("spring")`) and
    /// Data/Objects context tags (`GetForageItems("spring")`). On year 1, filters to items
    /// with a numeric vanilla id and excludes Rhubarb (`(O)252`, Desert-only).
    private static ResolvedItem? PickSpringCrop(QuestContext ctx)
    {
        var pool = ctx.Items.GetSeasonalCrops("spring");
        var filtered = new List<ResolvedItem>();
        foreach (var c in pool)
        {
            string bare = StripPrefix(c.QualifiedItemId);
            if (bare == "252")
                continue;
            if (Game1.year < 2 && !int.TryParse(bare, out _))
                continue;
            filtered.Add(c);
        }
        return filtered.Count == 0 ? null : filtered[Game1.random.Next(filtered.Count)];
    }

    private static ResolvedItem? PickSpringForage(QuestContext ctx)
    {
        var pool = ctx.Items.GetForageItems("spring");
        var filtered = new List<ResolvedItem>();
        foreach (var f in pool)
        {
            string bare = StripPrefix(f.QualifiedItemId);
            if (Game1.year < 2 && !int.TryParse(bare, out _))
                continue;
            filtered.Add(f);
        }
        return filtered.Count == 0 ? null : filtered[Game1.random.Next(filtered.Count)];
    }

    /// Recipe pool widens past the asked ingredients to include any dish whose ingredient
    /// list matches any picked crop's fruit/vegetable category, so modded recipes ride
    /// along. `cropQualifiedIds` flags which entries in `ingredients` came from the crop
    /// pool. Forage's own category doesn't widen the pool (forage_item items aren't
    /// categorized as fruit/veg in vanilla; the crop pick is the broader signal).
    private static ResolvedItem? PickSampleDishForIngredients(QuestContext ctx, IReadOnlyList<ResolvedItem> ingredients, HashSet<string> cropQualifiedIds)
    {
        var allRecipes = ctx.Items.GetAllCookingRecipes();
        if (allRecipes.Count == 0)
            return null;

        var ingredientBareIds = new HashSet<string>();
        bool anyCropFruit = false;
        bool anyCropVeg = false;
        foreach (var item in ingredients)
        {
            ingredientBareIds.Add(StripPrefix(item.QualifiedItemId));
            if (cropQualifiedIds.Contains(item.QualifiedItemId))
            {
                if (item.Category == StardewValley.Object.FruitsCategory) anyCropFruit = true;
                if (item.Category == StardewValley.Object.VegetableCategory) anyCropVeg = true;
            }
        }

        var pool = new List<CookingRecipeInfo>();
        foreach (var recipe in allRecipes)
        {
            bool match = false;
            foreach (var ing in recipe.Ingredients)
            {
                if (ing.IsCategoryToken)
                {
                    if (anyCropFruit && ing.CategoryId == StardewValley.Object.FruitsCategory) { match = true; break; }
                    if (anyCropVeg && ing.CategoryId == StardewValley.Object.VegetableCategory) { match = true; break; }
                }
                else
                {
                    string ingBare = StripPrefix(ing.Item.QualifiedItemId);
                    if (ingredientBareIds.Contains(ingBare)) { match = true; break; }
                    if (anyCropFruit && ing.Item.Category == StardewValley.Object.FruitsCategory) { match = true; break; }
                    if (anyCropVeg && ing.Item.Category == StardewValley.Object.VegetableCategory) { match = true; break; }
                }
            }
            if (match)
                pool.Add(recipe);
        }
        if (pool.Count == 0)
            return null;
        return pool[Game1.random.Next(pool.Count)].OutputItem;
    }

    /// Fall 8 (Stardew Valley Fair prep). Player delivers N distinct fall ingredients to
    /// Gus (count = `GusFestivalFeastIngredientCount`, default 3). The combined pool reads
    /// `ctx.Items.GetSeasonalCrops("fall")` and `ctx.Items.GetForageItems("fall")`, so any
    /// modded fall crop or fall forage rides along automatically. Year-1 limits to vanilla
    /// numeric ids and excludes Beet `(O)284` (Desert/Oasis only) and Sweet Gem Berry
    /// `(O)417` (Rare Seed, 24-day grow) so a fresh save can't roll an unreachable pick.
    /// Per-item quantity scales by Farming for crop picks and Foraging for forage picks.
    /// Dish pool searches `Data/CookingRecipes` for recipes whose ingredient list matches
    /// any picked id or any picked crop's fruit/vegetable category, so modded recipes ride
    /// along. Reward: sample dish + 5 Prize Tickets (Fair prize-wheel currency).
    private static QuestPosting? GusFestivalFeastFall(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Gus") == null)
            return null;

        var (pool, cropQualifiedIds) = BuildFallIngredientPool(ctx);
        if (pool.Count == 0)
            return null;

        int requested = ModEntry.Config.GusFestivalFeastIngredientCount;
        int count = Math.Min(pool.Count, Math.Max(1, requested));
        var picks = new List<ResolvedItem>(count);
        var indices = new List<int>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
            indices.Add(i);
        for (int i = 0; i < count && indices.Count > 0; i++)
        {
            int j = Game1.random.Next(indices.Count);
            picks.Add(pool[indices[j]]);
            indices.RemoveAt(j);
        }

        var sampleDish = PickSampleDishForIngredients(ctx, picks, cropQualifiedIds);
        if (sampleDish == null)
            return null;

        bool scaling = ctx.Config.DifficultyScaling;
        var qtyByIndex = new int[picks.Count];
        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            bool isCrop = cropQualifiedIds.Contains(picks[i].QualifiedItemId);
            int qty;
            if (scaling)
            {
                int upper = isCrop
                    ? Math.Max(3, 2 * Game1.player.FarmingLevel)
                    : Math.Max(2, 2 * Game1.player.ForagingLevel);
                int lower = isCrop ? 3 : 2;
                qty = Game1.random.Next(lower, upper + 1);
            }
            else
            {
                qty = 5;
            }
            qtyByIndex[i] = qty;
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

        string ingredientList = JoinItemList(picks.Select((p, i) => $"{qtyByIndex[i]} {p.DisplayName}"));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            // Fall 8 trigger, Stardew Valley Fair on Fall 16: 7 days puts the auto-fail on
            // Fall 15, one day before the festival. Same shape as Spring egg festival prep.
            DeadlineDays = 7,
            Rewards =
            {
                new ObjectReward(sampleDish.QualifiedItemId),
                new ObjectReward("(O)PrizeTicket", 5)
            },
            Title = ModEntry.I18n.Get("quest.festival.gusFall.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusFall.description", new { ingredients = ingredientList }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusFall.targetMessage", new { dish = sampleDish.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    /// Combined fall ingredient pool: Data/Crops fall harvest items + Data/Objects fall
    /// forage. Year 1 filters to vanilla numeric ids and drops Beet `(O)284` (Desert/Oasis
    /// only) and Sweet Gem Berry `(O)417` (Rare Seed) so the quest can't ask for items the
    /// player has no path to. Returned `CropQualifiedIds` flags which entries originated
    /// from the crop pool, so the caller can pick the right skill (Farming vs Foraging)
    /// for quantity scaling.
    private static (List<ResolvedItem> Pool, HashSet<string> CropQualifiedIds) BuildFallIngredientPool(QuestContext ctx)
    {
        var pool = new List<ResolvedItem>();
        var seen = new HashSet<string>();
        var cropQualifiedIds = new HashSet<string>();

        foreach (var c in ctx.Items.GetSeasonalCrops("fall"))
        {
            string bare = StripPrefix(c.QualifiedItemId);
            if (bare == "284" || bare == "417")
                continue;
            if (Game1.year < 2 && !int.TryParse(bare, out _))
                continue;
            if (seen.Add(c.QualifiedItemId))
            {
                pool.Add(c);
                cropQualifiedIds.Add(c.QualifiedItemId);
            }
        }

        foreach (var f in ctx.Items.GetForageItems("fall"))
        {
            string bare = StripPrefix(f.QualifiedItemId);
            if (Game1.year < 2 && !int.TryParse(bare, out _))
                continue;
            if (seen.Add(f.QualifiedItemId))
                pool.Add(f);
        }

        return (pool, cropQualifiedIds);
    }

    /// Summer 8 (Luau prep). Player delivers N distinct summer-or-spring ingredients to Gus
    /// (count = `GusFestivalFeastIngredientCount`, default 2). The combined pool reads
    /// `ctx.Items.GetSeasonalCrops("summer"|"spring")` and `ctx.Items.GetForageItems` for
    /// both seasons so modded crops and forage ride along. Spring entries widen the Y1 pool
    /// per the CSV intent ("Ingredients should be Spring/Summer themed to prevent
    /// gatekeeping first years"). Year 1 limits to vanilla numeric ids and drops Beet
    /// `(O)284` and Sweet Gem Berry `(O)417`. Per-item quantity scales by Farming (crop
    /// picks) or Foraging (forage picks). Dish pool searches `Data/CookingRecipes` for
    /// recipes matching any picked id or any picked crop's fruit/veg category. Reward:
    /// sample dish + FestivalBias Luau magnitude (still tuned via GMCM).
    private static QuestPosting? GusFestivalFeastSummer(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Gus") == null)
            return null;

        var (pool, cropQualifiedIds) = BuildSummerIngredientPool(ctx);
        if (pool.Count == 0)
            return null;

        int requested = ModEntry.Config.GusFestivalFeastIngredientCount;
        int count = Math.Min(pool.Count, Math.Max(1, requested));
        var picks = new List<ResolvedItem>(count);
        var indices = new List<int>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
            indices.Add(i);
        for (int i = 0; i < count && indices.Count > 0; i++)
        {
            int j = Game1.random.Next(indices.Count);
            picks.Add(pool[indices[j]]);
            indices.RemoveAt(j);
        }

        var sampleDish = PickSampleDishForIngredients(ctx, picks, cropQualifiedIds);
        if (sampleDish == null)
            return null;

        bool scaling = ctx.Config.DifficultyScaling;
        var qtyByIndex = new int[picks.Count];
        var steps = new List<AdventureStepState>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            bool isCrop = cropQualifiedIds.Contains(picks[i].QualifiedItemId);
            int qty;
            if (scaling)
            {
                int upper = isCrop
                    ? Math.Max(3, 2 * Game1.player.FarmingLevel)
                    : Math.Max(2, 2 * Game1.player.ForagingLevel);
                int lower = isCrop ? 3 : 2;
                qty = Game1.random.Next(lower, upper + 1);
            }
            else
            {
                qty = 5;
            }
            qtyByIndex[i] = qty;
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
        quest.Initialize(steps, giver: "Gus", completionDialogue: ModEntry.I18n.Get("quest.festival.gusSummer.targetMessage", new { dish = sampleDish.DisplayName }));

        string ingredientList = JoinItemList(picks.Select((p, i) => $"{qtyByIndex[i]} {p.DisplayName}"));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Gus",
            ObjectiveQuantity = 1,
            // Summer 8 trigger, Luau on Summer 11: 2 days puts the auto-fail on Summer 10,
            // one day before the festival. Same shape as the Spring and Fall feasts.
            DeadlineDays = 2,
            Rewards =
            {
                new ObjectReward(sampleDish.QualifiedItemId),
                new FestivalBiasReward(FestivalKind.Luau, ModEntry.Config.FestivalBiasLuauMagnitude)
            },
            Title = ModEntry.I18n.Get("quest.festival.gusSummer.title"),
            Description = ModEntry.I18n.Get("quest.festival.gusSummer.description", new { ingredients = ingredientList }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.gusSummer.targetMessage", new { dish = sampleDish.DisplayName }),
            PreBuiltQuest = quest
        };
    }

    /// Combined summer + spring ingredient pool for the Luau prep. Reads `Data/Crops`
    /// summer + spring entries and `Data/Objects` summer + spring forage so modded crops
    /// and forage ride along. Year 1 filters to vanilla numeric ids and drops Beet
    /// `(O)284` (Desert/Oasis only) and Sweet Gem Berry `(O)417` (Rare Seed). Per the
    /// CSV's "Ingredients should be Spring/Summer themed to prevent gatekeeping first
    /// years" note, spring entries are folded in so a fresh save isn't stuck on the
    /// narrow summer crop list. `CropQualifiedIds` flags which entries came from the
    /// crop pool, so the caller picks the right skill (Farming vs Foraging) for qty.
    private static (List<ResolvedItem> Pool, HashSet<string> CropQualifiedIds) BuildSummerIngredientPool(QuestContext ctx)
    {
        var pool = new List<ResolvedItem>();
        var seen = new HashSet<string>();
        var cropQualifiedIds = new HashSet<string>();

        void AddCrops(string season)
        {
            foreach (var c in ctx.Items.GetSeasonalCrops(season))
            {
                string bare = StripPrefix(c.QualifiedItemId);
                if (bare == "284" || bare == "417")
                    continue;
                if (Game1.year < 2 && !int.TryParse(bare, out _))
                    continue;
                if (seen.Add(c.QualifiedItemId))
                {
                    pool.Add(c);
                    cropQualifiedIds.Add(c.QualifiedItemId);
                }
            }
        }

        void AddForage(string season)
        {
            foreach (var f in ctx.Items.GetForageItems(season))
            {
                string bare = StripPrefix(f.QualifiedItemId);
                if (Game1.year < 2 && !int.TryParse(bare, out _))
                    continue;
                if (seen.Add(f.QualifiedItemId))
                    pool.Add(f);
            }
        }

        AddCrops("summer");
        AddCrops("spring");
        AddForage("summer");
        AddForage("spring");

        return (pool, cropQualifiedIds);
    }

    /// Winter 13 (Night Market). A met NPC posts a Help Wanted ask for seeds pulled
    /// programmatically from the Magic Shop Boat's actual stock (Days 1-3, unioned,
    /// filtered to Category -74 seed items). Multi-step Adventure Ship. Variety count
    /// and quantity per seed scale with Farming; deadline is fixed at 5 days so the
    /// quest auto-fails Winter 18, one day after the Night Market ends on Winter 17.
    /// Reward: FriendshipBasic to the giver.
    private static QuestPosting? MerchantUnpacking(QuestContext ctx)
    {
        var metNpcs = DispatchRegistry.MetHumanNpcs();
        if (metNpcs.Count == 0)
            return null;
        string giver = metNpcs[Game1.random.Next(metNpcs.Count)];

        var seeds = ResolveMagicBoatSeeds(ctx);
        if (seeds.Count == 0)
            return null;

        int varietyCount;
        int qtyPer;
        if (ctx.Config.DifficultyScaling)
        {
            int farming = Difficulty.GetSkillLevel(QuestCategory.Farming);
            int varietyUpper = Math.Max(1, farming / 2);
            varietyCount = Game1.random.Next(1, varietyUpper + 1);
            int qtyUpper = Math.Max(3, farming * 4);
            qtyPer = Game1.random.Next(3, qtyUpper + 1);
        }
        else
        {
            varietyCount = Game1.random.Next(1, 4);
            qtyPer = Game1.random.Next(3, 12);
        }

        varietyCount = Math.Min(varietyCount, seeds.Count);
        var picked = seeds.OrderBy(_ => Game1.random.Next()).Take(varietyCount).ToList();

        var steps = new List<AdventureStepState>();
        foreach (var seed in picked)
        {
            steps.Add(new AdventureStepState
            {
                Name = "Ship_" + seed.QualifiedItemId,
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { seed.QualifiedItemId },
                Count = qtyPer,
                Description = ModEntry.I18n.Get("quest.festival.merchantUnpacking.step", new { count = qtyPer, item = seed.DisplayName })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps.ToArray(), giver: giver);

        string itemList = string.Join(", ", picked.Select(s => s.DisplayName));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = 5,
            Rewards = { new FriendshipReward(giver, ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.festival.merchantUnpacking.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.festival.merchantUnpacking.description", new { qty = qtyPer, varietyCount, items = itemList }),
            PreBuiltQuest = quest
        };
    }

    /// Posted when the player speaks to Lewis on Winter 22-25 (NpcDialogue source +
    /// date gate). Resolves the player's secret-friend recipient via
    /// Utility.GetRandomWinterStarParticipant and embeds one loved item as a hint.
    /// Single Gift step targeting the recipient with an empty Items list, so any
    /// loved/liked item passes (the framework's WinterStarGiftPatch forwards the
    /// festival's secret-santa exchange into OnItemOfferedToNpc so the step ticks
    /// at the festival even though `chooseSecretSantaGift` bypasses
    /// `tryToReceiveActiveObject`). Skipping the festival or handing over a
    /// disliked / neutral / hated item leaves the quest open until it auto-fails
    /// the morning after Winter 25. Reward: FriendshipMid to the recipient.
    private static QuestPosting? SecretGiftHint(QuestContext ctx)
    {
        if (!ModEntry.Config.SecretGiftHintEnabled)
            return null;

        // Hint text resolves item names via Data/Objects, which MoreQuests edits at
        // SaveLoaded. Pre-save Build (a future quest-browser preview, a dry-run tool)
        // would render with the fallback string instead of an actual loved item. Bail
        // rather than ship a half-resolved posting.
        if (!Context.IsWorldReady)
            return null;

        NPC? recipient;
        try
        {
            recipient = StardewValley.Utility.GetRandomWinterStarParticipant();
        }
        catch (Exception ex)
        {
            ModEntry.LogDebug($"SecretGiftHint: could not resolve Winter Star recipient: {ex.Message}");
            return null;
        }
        if (recipient == null)
            return null;

        var lovedNames = ResolveLovedItemNames(ctx, recipient.Name, max: 1);
        string lovedItem = lovedNames.Count > 0
            ? lovedNames[0]
            : ModEntry.I18n.Get("quest.festival.secretGiftHint.fallback");

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "GiveSecretSantaGift",
                Kind = AdventureStepKind.Gift,
                Targets = new List<string> { recipient.Name },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.festival.secretGiftHint.step", new { recipient = recipient.displayName, item = lovedItem })
            }
        }, giver: "Lewis");

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Special,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Lewis",
            ObjectiveQuantity = 1,
            DeadlineDays = 4,
            Rewards = { new FriendshipReward(recipient.Name, ctx.Config.FriendshipMid) },
            Title = ModEntry.I18n.Get("quest.festival.secretGiftHint.title"),
            Description = ModEntry.I18n.Get("quest.festival.secretGiftHint.description", new { recipient = recipient.displayName, item = lovedItem }),
            PreBuiltQuest = quest
        };
    }

    /// Winter 20, mod-gated on Si.ExtraCraftingMaterials. Lewis asks the player to ship
    /// Paper + Tape for the town's Winter Star gift wrapping. Reward: Friendship 101
    /// (Book_Friendship). Scaling on: 2 + Random.Next((int)(FarmingLevel*1.5)). Off:
    /// Random.Next(2, 6). Single roll applied to both steps. Deadline hardcoded to 5
    /// days so the quest spans Winter 20-24 and auto-fails Winter 25 morning, the day
    /// of the Feast of the Winter Star.
    private static QuestPosting? WrappingPaper(QuestContext ctx)
    {
        if (!ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.SiExtraCraftingMaterials))
            return null;

        const string paperId = "Si.ECM_Paper";
        const string tapeId = "Si.ECM_Tape";

        var paper = ctx.Items.TryResolveItem(paperId);
        var tape = ctx.Items.TryResolveItem(tapeId);
        if (paper == null || tape == null)
        {
            ModEntry.LogDebug($"WrappingPaper: Paper ({paperId}) or Tape ({tapeId}) item not found in registry; skipping.");
            return null;
        }

        const string giver = "Lewis";

        int qtyPerItem;
        if (ctx.Config.DifficultyScaling)
        {
            int upper = Math.Max(1, (int)(Game1.player.FarmingLevel * 1.5));
            qtyPerItem = 2 + Game1.random.Next(upper);
        }
        else
        {
            qtyPerItem = Game1.random.Next(2, 6);
        }

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
            DeadlineDays = 5,
            Rewards = { new ObjectReward("(O)Book_Friendship") },
            Title = ModEntry.I18n.Get("quest.festival.wrappingPaper.title"),
            Description = ModEntry.I18n.Get("quest.festival.wrappingPaper.description", new { count = qtyPerItem }),
            PreBuiltQuest = quest
        };
    }

    /// Pulls Magic Shop Boat stock from all three Night Market days and dedupes by
    /// qualified id, then keeps only Category -74 (Seeds) entries. Content Patcher
    /// edits to these vanilla shop ids are picked up automatically since the
    /// framework reads `Data/Shops` at call time.
    private static readonly string[] MagicBoatShopIds =
    {
        "Festival_NightMarket_MagicBoat_Day1",
        "Festival_NightMarket_MagicBoat_Day2",
        "Festival_NightMarket_MagicBoat_Day3"
    };

    private static List<ResolvedItem> ResolveMagicBoatSeeds(QuestContext ctx)
    {
        var results = new List<ResolvedItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var shopId in MagicBoatShopIds)
        {
            foreach (var item in ctx.Items.GetShopItems(shopId))
            {
                if (item.Category != -74)
                    continue;
                if (seen.Add(item.QualifiedItemId))
                    results.Add(item);
            }
        }
        return results;
    }

    /// Item ids that should never land as a decor reward. Stardrop is the canonical one
    /// (unique permanent boost). Extend if other shops turn up non-decor specials.
    private static readonly HashSet<string> FestivalShopRewardExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "(O)434" // Stardrop
    };

    private static ResolvedItem? PickFestivalShopReward(QuestContext ctx, string shopId)
    {
        var pool = ctx.Items.GetShopItems(shopId);
        if (pool.Count == 0)
            return null;
        var filtered = new List<ResolvedItem>(pool.Count);
        foreach (var item in pool)
        {
            if (FestivalShopRewardExclusions.Contains(item.QualifiedItemId))
                continue;
            filtered.Add(item);
        }
        if (filtered.Count == 0)
            return null;
        return filtered[Game1.random.Next(filtered.Count)];
    }

    /// Picks one id from a decor pool. Resolution happens later in RewardApplier.ApplyOne.
    /// Empty pools return null.
    private static string? PickDecor(string[] pool)
    {
        if (pool == null || pool.Length == 0)
            return null;
        return pool[Game1.random.Next(pool.Length)];
    }

    /// Splits a comma-separated NPC list into trimmed, case-insensitively deduplicated names.
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

    /// Moonlight Jellies decor: Lewis, Summer 21. Ship Torches + Wood. Reward: GoldBasicBase
    /// + a random non-Stardrop item from Pierre's festival shop. Explicit 6-day deadline so
    /// the quest expires on Summer 28 regardless of GMCM DeadlineShort.
    private static QuestPosting? MoonlightJelliesFestivalDecor(QuestContext ctx)
    {
        const string giver = "Lewis";
        int torchCount;
        int woodCount;
        if (ctx.Config.DifficultyScaling)
        {
            int foraging = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            torchCount = Game1.random.Next(5, Math.Max(5, (int)(foraging * 1.5)) + 1);
            woodCount = Game1.random.Next(10, Math.Max(10, foraging * 5) + 1);
        }
        else
        {
            torchCount = 5;
            woodCount = 10;
        }

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
        var decor = PickFestivalShopReward(ctx, "Festival_DanceOfTheMoonlightJellies_Pierre");
        if (decor != null)
            rewards.Add(new ObjectReward(decor.QualifiedItemId));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = 6,
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.moonlightJellies.title"),
            Description = ModEntry.I18n.Get("quest.festival.moonlightJellies.description", new { torches = torchCount, wood = woodCount }),
            PreBuiltQuest = quest
        };
    }

    /// Egg Festival decor: Lewis, Spring 10. Ship Hay Bales (BC that needs the decor bypass).
    /// Reward: GoldBeginnerBase + a random non-Stardrop item from Pierre's Egg-Festival shop.
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
        var decor = PickFestivalShopReward(ctx, "Festival_EggFestival_Pierre");
        if (decor != null)
            rewards.Add(new ObjectReward(decor.QualifiedItemId));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = 3,
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.eggFestivalDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.eggFestivalDecor.description", new { count = hayBaleCount }),
            PreBuiltQuest = quest
        };
    }

    /// NpcDialogue, Spring 10-12. Vincent pulls the farmer into a "let's beat Abigail"
    /// plan during the Egg Festival lead-up. The single Custom step is the egg hunt
    /// itself, marked done at Spring 13 day-end based on festivalScore. The "talk to
    /// the other kids" flavor happens via dialogue lines pushed onto each met child
    /// from ModEntry.OnDayStarted; framework AdventureQuest doesn't expose force-mark
    /// for non-Custom steps, so per-kid steps stay out of the journal. Win path:
    /// per-kid FriendshipMid + thank-you mail Spring 14 + an iridium-quality egg
    /// granted in code (mail attachments don't carry quality). Loss path: per-kid
    /// -FriendshipMultiSmall and the quest is removed silently.
    private static QuestPosting? EggHuntSabotage(QuestContext ctx)
    {
        const string giver = "Vincent";

        var kids = MetChildHumanGivers();
        if (!kids.Contains("Vincent", StringComparer.OrdinalIgnoreCase) || !kids.Contains("Jas", StringComparer.OrdinalIgnoreCase))
            return null;

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "WinTheHunt",
                Kind = AdventureStepKind.Custom,
                Targets = new List<string> { ModEntry.EggHuntSabotageStepHandler },
                Count = 1,
                Description = ModEntry.I18n.Get("quest.festival.eggHuntSabotage.step.win")
            }
        }, giver: giver);

        var rewards = new List<RewardSpec>
        {
            new MailReward(ModEntry.EggHuntSabotageRewardMailKey, MailWhen.Tomorrow)
        };
        foreach (var kid in kids)
            rewards.Add(new FriendshipReward(kid, ctx.Config.FriendshipMid));

        // Spring 10 trigger needs daysLeft = 4 to auto-fail Spring 14 morning. Trigger
        // on Spring 12 needs daysLeft = 2.
        int daysLeft = Math.Max(2, 14 - Game1.dayOfMonth);

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = daysLeft,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.eggHuntSabotage.title"),
            Description = ModEntry.I18n.Get("quest.festival.eggHuntSabotage.description"),
            DialogueText = ModEntry.I18n.Get("quest.festival.eggHuntSabotage.offer"),
            FriendshipSummaryOverride = ModEntry.I18n.Get("quest.festival.eggHuntSabotage.reward.friendship").ToString(),
            PreBuiltQuest = quest
        };
    }

    /// Fair decor: Lewis, Fall 12. Three Ship steps: Wood, any Sign BC (Wood/Stone/Dark),
    /// fall flowers (scanned from Data/Crops so modded fall flowers come along). Reward
    /// depends on FairFestivalRewardKind: GrangeScoreBonus adds flat grange points, StarTokens
    /// adds extra festivalScore tokens to spend at the Fair.
    private static QuestPosting? FairFestivalDecor(QuestContext ctx)
    {
        const string giver = "Lewis";

        int woodCount;
        int signCount;
        int flowerCount;
        if (ctx.Config.DifficultyScaling)
        {
            int farming = Difficulty.GetSkillLevel(QuestCategory.Farming);
            int foraging = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            flowerCount = Game1.random.Next(5, Math.Max(5, (int)(farming * 1.5)) + 1);
            woodCount = Game1.random.Next(10, Math.Max(10, foraging * 3) + 1);
            signCount = Game1.random.Next(3, 11);
        }
        else
        {
            woodCount = 10;
            signCount = 3;
            flowerCount = 5;
        }

        var flowerItems = GetFallFlowerItemIds(ctx);

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
                Items = new List<string> { "(BC)37", "(BC)38", "(BC)39" },
                Count = signCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.fairDecor.step.signs", new { count = signCount })
            },
            new AdventureStepState
            {
                Name = "ShipFlowers",
                Kind = AdventureStepKind.Ship,
                Items = flowerItems,
                Count = flowerCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.fairDecor.step.flowers", new { count = flowerCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec>();
        bool starTokens = string.Equals(ModEntry.Config.FairFestivalRewardKind, "StarTokens", StringComparison.OrdinalIgnoreCase);
        if (starTokens)
        {
            int amount = Math.Max(0, ModEntry.Config.FairStarTokensAmount);
            if (amount > 0)
                rewards.Add(new FairStarTokensReward(amount));
        }
        else
        {
            int magnitude = Math.Max(0, ModEntry.Config.FestivalBiasFairMagnitude);
            if (magnitude > 0)
                rewards.Add(new FestivalBiasReward(FestivalKind.Fair, magnitude));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = 3,
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.fairDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.fairDecor.description", new { wood = woodCount, signs = signCount, flowers = flowerCount }),
            PreBuiltQuest = quest
        };
    }

    /// Every flower-category (-80) harvest id whose Data/Crops season list contains Fall.
    /// Picks up modded fall flowers. Falls back to Sunflower + Fairy Rose if the crop table
    /// has been wiped (the quest still needs something to accept).
    private static List<string> GetFallFlowerItemIds(QuestContext ctx)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var crop in ctx.Items.GetSeasonalCrops("fall"))
        {
            if (crop.Category != -80)
                continue;
            if (seen.Add(crop.QualifiedItemId))
                ids.Add(crop.QualifiedItemId);
        }
        if (ids.Count == 0)
        {
            ids.Add("(O)421"); // Sunflower (also summer)
            ids.Add("(O)595"); // Fairy Rose
        }
        return ids;
    }

    /// Luau decor: Lewis, Summer 6. Ship Fiber + Hardwood + Wood Lamp-post (BC)152.
    /// Reward: GoldIntermediateBase + a random non-Stardrop item from the Luau shop.
    private static QuestPosting? LuauFestivalDecor(QuestContext ctx)
    {
        const string giver = "Lewis";

        int fiberCount;
        int hardwoodCount;
        int lampCount;
        if (ctx.Config.DifficultyScaling)
        {
            int foraging = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            fiberCount = Game1.random.Next(10, Math.Max(10, foraging * 5) + 1);
            hardwoodCount = Game1.random.Next(3, Math.Max(3, (int)(foraging * 1.5)) + 1);
            lampCount = Game1.random.Next(3, 16);
        }
        else
        {
            fiberCount = 10;
            hardwoodCount = 5;
            lampCount = 4;
        }

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
                Name = "ShipHardwood",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)709" },
                Count = hardwoodCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.luauDecor.step.hardwood", new { count = hardwoodCount })
            },
            new AdventureStepState
            {
                Name = "ShipLampPost",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(BC)152" },
                Count = lampCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.luauDecor.step.lampPost", new { count = lampCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec> { new MoneyReward(ctx.Config.GoldIntermediateBase) };
        var decor = PickFestivalShopReward(ctx, "Festival_Luau_Pierre");
        if (decor != null)
            rewards.Add(new ObjectReward(decor.QualifiedItemId));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = 4,
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.luauDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.luauDecor.description", new { fiber = fiberCount, hardwood = hardwoodCount, lamps = lampCount }),
            PreBuiltQuest = quest
        };
    }

    private static readonly string[] VanillaRarecrowIds =
    {
        "(BC)110", "(BC)113", "(BC)126", "(BC)136",
        "(BC)137", "(BC)138", "(BC)139", "(BC)140"
    };

    /// Spirit's Eve decor: Wizard, Fall 22. Ship Pumpkin Seeds + Cloth + Torches (seeds,
    /// not pumpkins; the Wizard is building atmosphere, not stocking a bake-off).
    /// Reward: GoldIntermediateBase + 1 random vanilla rarecrow.
    private static QuestPosting? SpiritsEveDecor(QuestContext ctx)
    {
        const string giver = "Wizard";

        int pumpkinSeedCount;
        int clothCount;
        int torchCount;
        if (ctx.Config.DifficultyScaling)
        {
            int farming = Difficulty.GetSkillLevel(QuestCategory.Farming);
            pumpkinSeedCount = Game1.random.Next(5, Math.Max(5, (int)(farming * 1.5)) + 1);
            clothCount = Game1.random.Next(3, 11);
            torchCount = Game1.random.Next(5, 21);
        }
        else
        {
            pumpkinSeedCount = 5;
            clothCount = 3;
            torchCount = 5;
        }

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "ShipPumpkinSeeds",
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "(O)490" },
                Count = pumpkinSeedCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.step.pumpkinSeeds", new { count = pumpkinSeedCount })
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
            DeadlineDays = 4,
            AllowDecorShipping = true,
            Rewards =
            {
                new MoneyReward(ctx.Config.GoldIntermediateBase),
                new ObjectReward(VanillaRarecrowIds[Game1.random.Next(VanillaRarecrowIds.Length)], 1)
            },
            Title = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.title"),
            Description = ModEntry.I18n.Get("quest.festival.spiritsEveDecor.description", new { pumpkinSeeds = pumpkinSeedCount, cloth = clothCount, torches = torchCount }),
            PreBuiltQuest = quest
        };
    }

    /// East Scarp Spirit's Eve decor: Rosa, Fall 24. Mod-gated on East Scarp / Eli &amp; Dylan /
    /// Lurking in the Dark. Ship purple-dye items + slime + stone. Reward: FriendshipMultiHeart
    /// to each named ESV festival NPC (summary collapses 3+ to a generic line).
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
            DeadlineDays = 3,
            AllowDecorShipping = true,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.title"),
            Description = ModEntry.I18n.Get("quest.festival.esvSpiritsEve.description", new { purple = purpleCount, slime = slimeCount, stone = stoneCount }),
            PreBuiltQuest = quest
        };
    }

    /// Ridgeside Gathering decor: Lenny, Fall 15. Mod-gated on RSV. Ship Tub o' Flowers +
    /// Wood + any table furniture ($furniture-table matches Furniture items whose
    /// furniture_type is table=11 or longTable=5, so vanilla and modded tables both count).
    /// Reward: FriendshipMultiHeart per named RSV festival NPC. Tub o' Flowers recipe is
    /// granted at quest-accept by ModEntry.OnQuestAccepted if the player doesn't know it.
    private static QuestPosting? RidgesideGatheringDecor(QuestContext ctx)
    {
        if (!MoreQuestsFramework.ModCompat.HasRsv(ctx.Helper.ModRegistry))
            return null;
        const string giver = "Lenny";

        int tubCount;
        int woodCount;
        int tableCount;
        if (ctx.Config.DifficultyScaling)
        {
            int farming = Difficulty.GetSkillLevel(QuestCategory.Farming);
            int foraging = Difficulty.GetSkillLevel(QuestCategory.Foraging);
            tubCount = 2 + Game1.random.Next(1, Math.Max(1, farming / 2) + 1);
            woodCount = Game1.random.Next(20, Math.Max(20, foraging * 5) + 1);
            tableCount = 1 + Game1.random.Next(1, 6);
        }
        else
        {
            tubCount = 2;
            woodCount = 20;
            tableCount = 2;
        }

        string tubId = string.IsNullOrWhiteSpace(ModEntry.Config.RsvTubOFlowersId)
            ? "(BC)108"
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
                Items = new List<string> { "$furniture-table" },
                Count = tableCount,
                AllowDecorShipping = true,
                Description = ModEntry.I18n.Get("quest.festival.rsvGathering.step.table", new { count = tableCount })
            }
        }, giver: giver);

        var rewards = new List<RewardSpec>();
        foreach (var npc in SplitNpcList(ModEntry.Config.RidgesideFestivalNpcs))
            rewards.Add(new FriendshipReward(npc, ctx.Config.FriendshipMultiHeart));

        string farmerName = Game1.player?.Name ?? "Farmer";

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
            Description = ModEntry.I18n.Get("quest.festival.rsvGathering.description", new { farmer = farmerName, tubs = tubCount, wood = woodCount, tables = tableCount }),
            PreBuiltQuest = quest
        };
    }

    /// Rainbow Platter (Trout Derby, posted Summer 19 so the player has 19 + 20-21 +
    /// auto-fail Summer 22, one day after the Derby ends): catch Rainbow Trout for a
    /// SaloonChef-pool giver. Quantity scales with Fishing (on: 2 + fishing/2; off:
    /// Random.Next(1, 5) for 1-4 inclusive). Reward: recipe (per-giver) + per-giver
    /// ShopDiscountReward on the dish, looked up via ModConfig so RSV / ES / VMV
    /// saloons all get the discount even though they use different shop ids.
    private static QuestPosting? RainbowPlatter(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        const string rainbowTroutId = "(O)138";

        int qty;
        if (ctx.Config.DifficultyScaling)
            qty = Math.Max(1, 2 + Game1.player.FishingLevel / 2);
        else
            qty = Game1.random.Next(1, 5);

        string recipeName = ResolveTroutDerbyRecipe(giver);
        var rewards = new List<RewardSpec>
        {
            new RecipeReward(recipeName)
        };
        var (shopId, dishId) = ResolveTroutDerbyDiscount(giver);
        if (!string.IsNullOrEmpty(shopId) && !string.IsNullOrEmpty(dishId))
        {
            rewards.Add(new ShopDiscountReward(
                ShopId: shopId,
                PercentOff: ModEntry.Config.ShopDiscountPercent,
                DurationDays: ModEntry.Config.ShopDiscountDurationDays,
                AppliesTo: new List<string> { dishId },
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
            DeadlineDays = 3,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.rainbowPlatter.title"),
            Description = ModEntry.I18n.Get("quest.festival.rainbowPlatter.description", new { qty, dish = recipeName }),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.rainbowPlatter.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.rainbowPlatter.targetMessage")
        };
    }

    private static (string shopId, string dishId) ResolveTroutDerbyDiscount(string giver) => giver switch
    {
        "Pika" => (ModEntry.Config.TroutDerbyShopPika, ModEntry.Config.TroutDerbyDishPika),
        "Celestine" => (ModEntry.Config.TroutDerbyShopCelestine, ModEntry.Config.TroutDerbyDishCelestine),
        "Rosa" => (ModEntry.Config.TroutDerbyShopRosa, ModEntry.Config.TroutDerbyDishRosa),
        _ => (ModEntry.Config.TroutDerbyShopGus, ModEntry.Config.TroutDerbyDishGus)
    };

    /// SquidFest Showcase (posted Winter 11 so the quest spans the two-day festival
    /// on Winter 12-13 and auto-fails Winter 14, one day after the festival ends):
    /// catch Squid and turn the stack in to the SaloonChef-pool giver. Quantity
    /// scales with Fishing (on: 2 + fishing/2; off: Random.Next(1, 5)) and the shop
    /// discount resolves per giver via config so RSV / ES / VMV saloons all get the
    /// discount alongside Gus, matching the Rainbow Platter parallel.
    private static QuestPosting? SquidFestShowcase(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        const string squidId = "(O)151";

        int qty;
        if (ctx.Config.DifficultyScaling)
            qty = Math.Max(1, 2 + Game1.player.FishingLevel / 2);
        else
            qty = Game1.random.Next(1, 5);

        string recipeName = ResolveSquidFestRecipe(giver);
        var rewards = new List<RewardSpec>
        {
            new RecipeReward(recipeName)
        };
        var (shopId, dishId) = ResolveSquidFestDiscount(giver);
        if (!string.IsNullOrEmpty(shopId) && !string.IsNullOrEmpty(dishId))
        {
            rewards.Add(new ShopDiscountReward(
                ShopId: shopId,
                PercentOff: ModEntry.Config.ShopDiscountPercent,
                DurationDays: ModEntry.Config.ShopDiscountDurationDays,
                AppliesTo: new List<string> { dishId },
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
            DeadlineDays = 3,
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.festival.squidFest.title"),
            Description = ModEntry.I18n.Get("quest.festival.squidFest.description", new { qty, recipe = recipeName }),
            CurrentObjective = ModEntry.I18n.Get("quest.festival.squidFest.objective", new { qty, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.festival.squidFest.targetMessage")
        };
    }

    private static (string shopId, string dishId) ResolveSquidFestDiscount(string giver) => giver switch
    {
        "Pika" => (ModEntry.Config.SquidFestShopPika, ModEntry.Config.SquidFestDishPika),
        "Celestine" => (ModEntry.Config.SquidFestShopCelestine, ModEntry.Config.SquidFestDishCelestine),
        "Rosa" => (ModEntry.Config.SquidFestShopRosa, ModEntry.Config.SquidFestDishRosa),
        _ => (ModEntry.Config.SquidFestShopGus, ModEntry.Config.SquidFestDishGus)
    };

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

    /// Lewis's Easter Eggs: posted Spring 8, deadline Spring 12 (one day before the Egg
    /// Festival on Spring 13). Picks 2-5 distinct dye colors from a festival-appropriate
    /// palette and asks the player to ship N items per color, where N is shared across
    /// every step (scaling on: rand(3, max(3, farming*2)); off: 3). color_white is left
    /// off the pool so plain cloth doesn't blanket-satisfy a step. Reward: all three
    /// authored Egg Basket variants (Cream, Pink, Rustic) at once.
    private static QuestPosting? DyeForEggs(QuestContext ctx)
    {
        const string giver = "Lewis";

        string[] palette = { "red", "orange", "yellow", "green", "blue", "purple", "pink" };

        int colorCount = Math.Min(Game1.random.Next(2, 6), palette.Length);
        var pickedColors = palette.OrderBy(_ => Game1.random.Next()).Take(colorCount).ToList();

        int countPer;
        if (ctx.Config.DifficultyScaling)
        {
            int farming = Difficulty.GetSkillLevel(QuestCategory.Farming);
            int upper = Math.Max(3, farming * 2);
            countPer = Game1.random.Next(3, upper + 1);
        }
        else
        {
            countPer = 3;
        }

        var steps = new List<AdventureStepState>();
        foreach (var color in pickedColors)
        {
            string colorDisplay = ModEntry.I18n.Get("color." + color).ToString();
            steps.Add(new AdventureStepState
            {
                Name = "Ship_" + color,
                Kind = AdventureStepKind.Ship,
                Items = new List<string> { "$tag:color_" + color },
                Count = countPer,
                Description = ModEntry.I18n.Get("quest.festival.dyeForEggs.step", new { count = countPer, color = colorDisplay })
            });
        }

        var quest = new AdventureQuest();
        quest.Initialize(steps.ToArray(), giver: giver);

        string colorList = string.Join(", ", pickedColors.Select(c => ModEntry.I18n.Get("color." + c).ToString()));

        return new QuestPosting
        {
            Category = QuestCategory.Festival,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = 4,
            Rewards =
            {
                new ObjectReward("(O)" + ModEntry.EggBasketCreamId),
                new ObjectReward("(O)" + ModEntry.EggBasketPinkId),
                new ObjectReward("(O)" + ModEntry.EggBasketRusticId)
            },
            Title = ModEntry.I18n.Get("quest.festival.dyeForEggs.title"),
            Description = ModEntry.I18n.Get("quest.festival.dyeForEggs.description", new
            {
                countPer,
                colorCount = pickedColors.Count,
                colors = colorList
            }),
            PreBuiltQuest = quest
        };
    }

}
