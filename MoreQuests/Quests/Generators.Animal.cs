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
    // Hay Supply Run tuning. Quest only posts once the farm has at least this many animals,
    // and never asks for fewer than HayMinQty hay.
    private const int HayMinAnimals = 4;
    private const int HayMinQty = 5;
    // Scaling-on formula: farming level and herd size each add hay.
    private const int HayPerFarmingLevel = 3;
    private const int HayPerAnimalScaled = 2;
    // Scaling-off formula: random up to roughly this much per animal.
    private const int HayPerAnimalFlat = 5;

    private static QuestPosting? HaySupplyRun(QuestContext ctx)
    {
        int animals = CountAnimals();
        if (animals < HayMinAnimals)
            return null;

        int qty = Difficulty.Scaled(ctx,
            () => Game1.player.FarmingLevel * HayPerFarmingLevel + animals * HayPerAnimalScaled,
            () =>
            {
                int upper = Math.Max(HayMinQty + 1, animals * HayPerAnimalFlat + 1);
                return Game1.random.Next(HayMinQty, upper);
            });
        qty = Math.Max(HayMinQty, qty);

        var posting = new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = "(O)178",
            ObjectiveItemName = "Hay",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Title = ModEntry.I18n.Get("quest.animal.hay.title"),
            Description = ModEntry.I18n.Get("quest.animal.hay.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.hay.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.hay.targetMessage")
        };

        if (ModEntry.Config.ShopDiscountPercent > 0 && ModEntry.Config.ShopDiscountDurationDays > 0)
        {
            posting.Rewards.Add(new AnimalPurchaseDiscountReward(
                PercentOff: ModEntry.Config.ShopDiscountPercent,
                DurationDays: ModEntry.Config.ShopDiscountDurationDays));
        }

        return posting;
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

    /// Counts farm animals whose type name contains the given substring (case-insensitive).
    /// `kind = "Chicken"` matches every chicken variant including modded ones.
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

    /// Alex's Protein Shakes reward pool: Energy Tonic / Muscle Remedy / Life Elixir.
    /// Protein Bar is deferred until the spritework lands.
    private static readonly string[] AlexProteinRewardPool =
    {
        "(O)349", // Energy Tonic
        "(O)351", // Muscle Remedy
        "(O)773"  // Life Elixir
    };

    private static QuestPosting? AlexProteinShakes(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Alex") == null)
            return null;

        int chickens = CountAnimalsByType("Chicken");
        if (chickens <= 0)
            return null;

        int floor = Math.Max(1, ModEntry.Config.AlexProteinShakesBaseQty);
        int cap = Math.Max(floor, ModEntry.Config.AlexProteinShakesMaxQty);
        int qty = Math.Clamp(
            ModEntry.Config.AlexProteinShakesBaseQty + chickens * Math.Max(0, ModEntry.Config.AlexProteinShakesPerChicken),
            floor,
            cap);

        var rewardItem = ctx.Items.TryResolveItem(AlexProteinRewardPool[Game1.random.Next(AlexProteinRewardPool.Length)]);
        var rewards = new List<RewardSpec>
        {
            new FriendshipReward("Alex", ctx.Config.FriendshipBasic)
        };
        if (rewardItem != null)
            rewards.Add(new ObjectReward(rewardItem.QualifiedItemId));

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "DeliverEggs",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { "Alex" },
                Items = new List<string> { "$edible-egg" },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.animal.alexProtein.objective", new { qty })
            }
        }, giver: "Alex", completionDialogue: ModEntry.I18n.Get("quest.animal.alexProtein.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = "Alex",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.animal.alexProtein.title"),
            Description = ModEntry.I18n.Get("quest.animal.alexProtein.description", new { qty }),
            PreBuiltQuest = quest,
            CurrentObjective = ModEntry.I18n.Get("quest.animal.alexProtein.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.alexProtein.targetMessage")
        };
    }

    /// OneShot on first-held Dinosaur Egg. ItemDelivery to Gunther for one Dinosaur Egg.
    /// Reward: GoldAdvancedBase + a Dinosaur Egg one quality tier higher (iridium stays iridium).
    /// The quality bump runs in MoreQuests.ModEntry via QuestCompleted reading deliveredQuality.
    private static QuestPosting? GuntherDinosaurStudy(QuestContext ctx)
    {
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
                new MoneyReward(gold)
            },
            Title = ModEntry.I18n.Get("quest.animal.guntherDinosaur.title"),
            Description = ModEntry.I18n.Get("quest.animal.guntherDinosaur.description"),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.guntherDinosaur.objective"),
            TargetMessage = ModEntry.I18n.Get("quest.animal.guntherDinosaur.targetMessage")
        };
    }

    /// OneShot on first-held Void Egg, gated to Krobus heart-1. ItemDelivery for one
    /// Void Egg. Reward: FriendshipMid + the Void Monster Compendium (placeholder for the
    /// Void Chicken Statue until the sprite lands).
    private static QuestPosting? KrobusVoidNote(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Krobus") == null)
            return null;

        const string giver = "Krobus";
        const string voidEggId = "(O)305";

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = voidEggId,
            ObjectiveItemName = "Void Egg",
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new FriendshipReward(giver, ctx.Config.FriendshipMid),
                new ObjectReward("(O)Book_Void")
            },
            Title = ModEntry.I18n.Get("quest.animal.krobusVoidNote.title"),
            Description = ModEntry.I18n.Get("quest.animal.krobusVoidNote.description"),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.krobusVoidNote.objective"),
            TargetMessage = ModEntry.I18n.Get("quest.animal.krobusVoidNote.targetMessage")
        };
    }

    /// Periodic AdventureQuest gated on LFY + single-player + Leah heart-2. One Visit step
    /// on LeahHouse with `$follower-count:1`. Closes when the player walks into Leah's house
    /// with at least one animal in tow. Reward letter lands tomorrow with a painting of one
    /// of the player's farm animals (picked at posting time) in the frame style set in GMCM.
    private static QuestPosting? LeahFarmPainting(QuestContext ctx)
    {
        if (Game1.IsMultiplayer)
            return null;
        if (!ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.LivestockFollowsYou))
            return null;
        if (Game1.getCharacterFromName("Leah") == null)
            return null;
        if (!Game1.player.friendshipData.TryGetValue("Leah", out var leahFriendship) || leahFriendship.Points < 2 * Difficulty.FriendshipPointsPerHeart)
            return null;

        string frame = ModEntry.NormalizeLeahPaintingFrame(ModEntry.Config.LeahPaintingFrame);

        // Reward pool: paintings in the chosen frame the player hasn't gotten yet (their old
        // reward letters stay in the mail history, so we skip those). When the frame is fully
        // collected the pool is empty and the quest just doesn't post.
        var received = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in Game1.player.mailReceived) if (k != null) received.Add(k);
        foreach (var k in Game1.player.mailForTomorrow) if (k != null) received.Add(k);
        foreach (var k in Game1.player.mailbox) if (k != null) received.Add(k);

        var pool = ModEntry.GetLeahPaintings()
            .Where(p => p.Value != null
                && string.Equals(p.Value.Frame?.Trim(), frame, StringComparison.OrdinalIgnoreCase)
                && !received.Contains($"{ModEntry.LeahPaintingRewardKeyPrefix}{p.Key}"))
            .Select(p => p.Key)
            .ToList();

        if (pool.Count == 0)
            return null;

        string paintingId = pool[Game1.random.Next(pool.Count)];
        string letterKey = $"{ModEntry.LeahPaintingRewardKeyPrefix}{paintingId}";

        const string giver = "Leah";

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "VisitLeah",
                Kind = AdventureStepKind.Visit,
                Targets = new List<string> { "LeahHouse" },
                Items = new List<string> { "$follower-count:1" },
                Description = ModEntry.I18n.Get("quest.animal.leahFarmPainting.step.visit")
            }
        }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.animal.leahFarmPainting.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new FriendshipReward(giver, ctx.Config.FriendshipBasic),
                new MailReward(letterKey, MailWhen.Tomorrow)
            },
            Title = ModEntry.I18n.Get("quest.animal.leahFarmPainting.title"),
            Description = ModEntry.I18n.Get("quest.animal.leahFarmPainting.description"),
            TargetMessage = ModEntry.I18n.Get("quest.animal.leahFarmPainting.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// OneShot post-Deluxe-Barn AdventureQuest. Marnie asks the player to walk animals into
    /// Town. Gated on LFY + single-player + Marnie present + not winter + 2+ animals on the
    /// farm. Completion: enter Town with `$follower-count:2`. Reward: FriendshipLarge.
    private static QuestPosting? MarnieLivestockShow(QuestContext ctx)
    {
        if (Game1.IsMultiplayer)
            return null;
        if (!ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.LivestockFollowsYou))
            return null;
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;
        if (string.Equals(Game1.currentSeason, "winter", StringComparison.OrdinalIgnoreCase))
            return null;
        if (CountAnimals() < 2)
            return null;

        const string giver = "Marnie";

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "WalkToTown",
                Kind = AdventureStepKind.Visit,
                Targets = new List<string> { "Town" },
                Items = new List<string> { "$follower-count:2" },
                Description = ModEntry.I18n.Get("quest.animal.marnieLivestockShow.step.walk")
            }
        }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.animal.marnieLivestockShow.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards =
            {
                new FriendshipReward(giver, ctx.Config.FriendshipLarge)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieLivestockShow.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieLivestockShow.description"),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieLivestockShow.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// BuildingBuilt(Coop)+1 day. ItemDelivery to Marnie for Mixed Seeds (vanilla (O)770).
    /// Reward: a free White Chicken adopted into the player's coop on completion (see
    /// GrantFreeChicken) plus FriendshipBasic. MarnieChickenOfferRebate is the fallback
    /// when no coop slot is free.
    private static QuestPosting? MarnieChickenOffer(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        int qty = Math.Max(1, ModEntry.Config.MarnieChickenOfferSeedQty);

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = "(O)770",
            ObjectiveItemName = "Mixed Seeds",
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new FriendshipReward("Marnie", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieChickenOffer.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieChickenOffer.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieChickenOffer.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieChickenOffer.targetMessage")
        };
    }

    /// BuildingBuilt(Barn)+1 day. With LFY installed, Marnie wants a Grazing Bell delivered
    /// (regular Object, vanilla ItemDelivery path). Without LFY, she wants the player to pick
    /// up a Milk Pail (Tool, can't be gifted to NPCs), routed through PurchaseFromShopQuest
    /// and completed via the AnimalShop purchase hook. Reward: a free Dairy Cow adopted into
    /// the player's barn (see GrantFreeCow). MarnieCowOfferRebate is the fallback when every
    /// barn is full.
    private static QuestPosting? MarnieCowOffer(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        bool lfyLoaded = ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.LivestockFollowsYou);
        if (lfyLoaded && !string.IsNullOrEmpty(ModEntry.Lfy?.GrazingBellQualifiedItemId))
        {
            string bellId = ModEntry.Lfy!.GrazingBellQualifiedItemId;
            const string bellName = "Grazing Bell";
            return new QuestPosting
            {
                Category = QuestCategory.Animal,
                Tier = DifficultyTier.Beginner,
                QuestType = BoardQuestType.ItemDelivery,
                QuestGiver = "Marnie",
                ObjectiveItemId = bellId,
                ObjectiveItemName = bellName,
                ObjectiveQuantity = 1,
                DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
                Rewards = { new FriendshipReward("Marnie", ctx.Config.FriendshipBasic) },
                Title = ModEntry.I18n.Get("quest.animal.marnieCowOffer.title"),
                Description = ModEntry.I18n.Get("quest.animal.marnieCowOffer.description", new { item = bellName }),
                CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieCowOffer.objective", new { item = bellName }),
                TargetMessage = ModEntry.I18n.Get("quest.animal.marnieCowOffer.targetMessage")
            };
        }

        const string pailId = "(T)MilkPail";
        const string pailName = "Milk Pail";
        string targetMsg = ModEntry.I18n.Get("quest.animal.marnieCowOffer.targetMessage");

        var purchase = new PurchaseFromShopQuest
        {
            itemId = { Value = pailId },
            shopOwnerNpc = { Value = "Marnie" },
            targetMessage = { Value = targetMsg }
        };

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = pailId,
            ObjectiveItemName = pailName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new FriendshipReward("Marnie", ctx.Config.FriendshipBasic) },
            Title = ModEntry.I18n.Get("quest.animal.marnieCowOffer.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieCowOffer.descriptionBuy", new { item = pailName }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieCowOffer.objectiveBuy", new { item = pailName }),
            TargetMessage = targetMsg,
            PreBuiltQuest = purchase
        };
    }

    /// OneShot on first egg laid. Ship quest. Alternatives populated from a live scan of
    /// Game1.objectData (every Category -5 edible egg) so brown/large/Void/Golden/Ostrich/Duck
    /// and modded eggs all count. Reward: GoldBasicBase + Mayonnaise Machine (RecipeReward
    /// if unknown, direct (BC)24 ObjectReward if the player already learned it).
    private static QuestPosting? MarnieEggRequest(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        int qty = Math.Max(1, ModEntry.Config.MarnieEggRequestQty);
        int gold = ctx.Config.GoldBasicBase;

        bool knowsMayoRecipe = Game1.player?.craftingRecipes?.ContainsKey("Mayonnaise Machine") ?? false;
        RewardSpec mayoReward = knowsMayoRecipe
            ? new ObjectReward("(BC)24")
            : new RecipeReward("Mayonnaise Machine", RecipeKind.Crafting);

        var posting = new QuestPosting
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
                mayoReward,
                new FriendshipReward("Marnie", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieEgg.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieEgg.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieEgg.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieEgg.targetMessage")
        };

        foreach (var altId in EnumerateEdibleEggIds())
        {
            if (altId == "(O)176") continue;
            posting.AlternativeObjectiveItemIds.Add(altId);
            posting.AlternativeObjectiveItemWeights.Add(1);
        }

        return posting;
    }

    /// Every `(O)&lt;id&gt;` whose Data/Objects row has Category -5 (egg) and Edibility != -300
    /// (excludes Dinosaur Egg). Lets MarnieEggRequest match modded eggs too.
    private static IEnumerable<string> EnumerateEdibleEggIds()
    {
        const int eggCategory = -5;
        const int inedible = -300;
        foreach (var pair in Game1.objectData)
        {
            var data = pair.Value;
            if (data == null) continue;
            if (data.Category != eggCategory) continue;
            if (data.Edibility == inedible) continue;
            yield return "(O)" + pair.Key;
        }
    }

    /// Ship 10 milk. Alternatives populated from Game1.farmAnimalData for every animal whose
    /// HarvestTool is "Milk Pail" (vanilla cow/goat plus modded Buffalo/Llama/etc). Reward:
    /// GoldBasicBase + Cheese Press (RecipeReward if unknown, (BC)16 ObjectReward if known).
    private static QuestPosting? MarnieMilkRequest(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        int qty = Math.Max(1, ModEntry.Config.MarnieMilkRequestQty);
        int gold = ctx.Config.GoldBasicBase;

        bool knowsCheesePress = Game1.player?.craftingRecipes?.ContainsKey("Cheese Press") ?? false;
        RewardSpec cheesePressReward = knowsCheesePress
            ? new ObjectReward("(BC)16")
            : new RecipeReward("Cheese Press", RecipeKind.Crafting);

        var posting = new QuestPosting
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
                cheesePressReward,
                new FriendshipReward("Marnie", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieMilk.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieMilk.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieMilk.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieMilk.targetMessage")
        };

        foreach (var altId in EnumerateMilkProduceIds())
        {
            if (altId == "(O)184") continue;
            posting.AlternativeObjectiveItemIds.Add(altId);
            posting.AlternativeObjectiveItemWeights.Add(1);
        }

        return posting;
    }

    /// Every produce id from Data/FarmAnimals entries with HarvestTool "Milk Pail".
    /// Includes regular and Large variants via DeluxeProduceItemIds, plus modded milking animals.
    private static IEnumerable<string> EnumerateMilkProduceIds()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in Game1.farmAnimalData)
        {
            var animalData = pair.Value;
            if (animalData == null) continue;
            if (!string.Equals(animalData.HarvestTool, "Milk Pail", StringComparison.OrdinalIgnoreCase))
                continue;

            CollectIds(animalData.ProduceItemIds, seen);
            CollectIds(animalData.DeluxeProduceItemIds, seen);
        }
        foreach (var id in seen)
            yield return id;
    }

    private static void CollectIds(List<StardewValley.GameData.FarmAnimals.FarmAnimalProduce>? produce, HashSet<string> seen)
    {
        if (produce == null) return;
        foreach (var p in produce)
        {
            if (p == null || string.IsNullOrEmpty(p.ItemId)) continue;
            string qualified = p.ItemId.StartsWith("(") ? p.ItemId : "(O)" + p.ItemId;
            seen.Add(qualified);
        }
    }

    /// Fires the first time the player builds a Coop or Barn without already owning a Silo.
    /// ItemDelivery satisfied by EITHER 100 Stone, 10 Clay, or 5 Copper Bars (the silo recipe
    /// inputs). Reward: a free Silo credit on Robin's carpenter menu via GrantFreeSilo's
    /// ModData flag, which the Data/Buildings asset edit reads to zero the Silo's BuildCost
    /// and BuildMaterials. Cleared on BuildingListChanged once a Silo is built.
    private static QuestPosting? RobinSiloOffer(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Robin") == null)
            return null;

        // Bail if a silo got built between trigger eval and now (e.g. silo bought same day
        // as the coop). Trigger's Available already gates on `not:BuildingExists Silo`.
        var farm = Game1.getFarm();
        if (farm != null)
        {
            foreach (var b in farm.buildings)
                if (string.Equals(b.buildingType.Value, "Silo", StringComparison.OrdinalIgnoreCase))
                    return null;
        }

        int stoneQty = Math.Max(1, ModEntry.Config.RobinSiloOfferStoneQty);
        int clayQty = Math.Max(1, ModEntry.Config.RobinSiloOfferClayQty);
        int copperQty = Math.Max(1, ModEntry.Config.RobinSiloOfferCopperBarQty);

        var posting = new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Robin",
            ObjectiveItemId = "(O)390",
            ObjectiveItemName = "Stone",
            ObjectiveQuantity = stoneQty,
            AlternativeObjectiveItemIds = { "(O)330", "(O)334" },
            AlternativeObjectiveItemQuantities = { clayQty, copperQty },
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new FriendshipReward("Robin", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.robinSilo.title"),
            Description = ModEntry.I18n.Get("quest.animal.robinSilo.description", new { stoneQty, clayQty, copperQty }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.robinSilo.objective", new { stoneQty, clayQty, copperQty }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.robinSilo.targetMessage")
        };
        return posting;
    }

    // -------------------- Multi-step / misc --------------------

    /// Caroline asks for off-season edible forage or flowers she loves or likes (no herbs)
    /// for a new batch of tea. Off-season pool: Y1 = seasons already passed (Y1 spring skips),
    /// Y2+ = every season except the current one. Qty scales with foraging level when
    /// DifficultyScaling is on, flat 5 when off. Reward: FriendshipMid + 2x Tea Leaves.
}
