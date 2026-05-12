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
        int animals = CountAnimals();
        if (animals < 4)
            return null;

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            qty = Game1.player.FarmingLevel * 3 + animals * 2;
        }
        else
        {
            int upper = Math.Max(6, animals * 5 + 1);
            qty = Game1.random.Next(5, upper);
        }
        qty = Math.Max(5, qty);

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

    /// CSV row 29. OneShot triggered when the player first holds a Dinosaur Egg `(O)107`
    /// (framework `FirstHeldItem` predicate). Mail+ItemDelivery to Gunther for one
    /// Dinosaur Egg. Reward = `GoldAdvancedBase` plus one Dinosaur Egg returned via
    /// CSV row 29. OneShot triggered when the player first holds a Dinosaur Egg `(O)107`
    /// (framework `FirstHeldItem` predicate). Mail+ItemDelivery to Gunther for one
    /// Dinosaur Egg. Reward = `GoldAdvancedBase` + a Dinosaur Egg returned one quality
    /// tier higher than the delivered one (regular → silver, silver → gold, gold →
    /// iridium, iridium stays iridium). The quality bump is granted via a
    /// `QuestCompleted` listener in `MoreQuests.ModEntry` that reads the framework's
    /// `MoreQuestsItemDeliveryQuest.deliveredQuality` field.
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

    /// CSV row 39. OneShot triggered when the player first holds a Void Egg `(O)305`,
    /// gated on Krobus heart-1. Mail+ItemDelivery for one Void Egg to Krobus. Reward =
    /// FriendshipMid for Krobus + a Monster Compendium book (`(O)Book_Void`) as a
    /// placeholder for the CSV's Void Chicken Statue (asset deferred until Rafia
    /// produces the sprite).
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

    /// CSV row 40. Periodic mail-delivered Adventure quest gated on LivestockFollowsYou
    /// being installed, single-player, and 2+ hearts with Leah. AdventureQuest holds a
    /// single `Visit` step on LeahHouse with a `$follower-count:1` gate — the quest only
    /// closes when the player walks into Leah's house with at least one animal in tow.
    /// Reward is a random in-game `Data/Furniture` houseplant entry (CSV's bespoke animal
    /// painting deferred until Rafia ships the sprite pack).
    private static QuestPosting? LeahFarmPainting(QuestContext ctx)
    {
        if (Game1.IsMultiplayer)
            return null;
        if (!ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.LivestockFollowsYou))
            return null;
        if (Game1.getCharacterFromName("Leah") == null)
            return null;
        if (!Game1.player.friendshipData.TryGetValue("Leah", out var leahFriendship) || leahFriendship.Points < 2 * 250)
            return null;

        string? houseplantId = PickRandomHouseplantFurnitureId(ctx);
        if (houseplantId == null)
            return null;

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
                new ObjectReward(houseplantId)
            },
            Title = ModEntry.I18n.Get("quest.animal.leahFarmPainting.title"),
            Description = ModEntry.I18n.Get("quest.animal.leahFarmPainting.description"),
            TargetMessage = ModEntry.I18n.Get("quest.animal.leahFarmPainting.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// Returns a randomly-chosen `(F)<id>` furniture id whose name in `Data/Furniture`
    /// starts with "House Plant". Scans the live asset dictionary at posting time so
    /// modded houseplants surface automatically alongside vanilla ones. Null when no
    /// houseplant rows are present (vanilla ships ~15, so realistically only triggers
    /// in a heavily-pared content config).
    private static string? PickRandomHouseplantFurnitureId(QuestContext ctx)
    {
        try
        {
            var furniture = ctx.Helper.GameContent.Load<System.Collections.Generic.Dictionary<string, string>>("Data/Furniture");
            var matches = new List<string>();
            foreach (var pair in furniture)
            {
                if (pair.Value == null) continue;
                int slash = pair.Value.IndexOf('/');
                if (slash <= 0) continue;
                string name = pair.Value.Substring(0, slash);
                if (name.StartsWith("House Plant", StringComparison.OrdinalIgnoreCase))
                    matches.Add(pair.Key);
            }
            if (matches.Count == 0)
                return null;
            return "(F)" + matches[Game1.random.Next(matches.Count)];
        }
        catch
        {
            return null;
        }
    }

    /// CSV row 46. OneShot (post-Deluxe-Barn upgrade) mail-delivered Adventure quest.
    /// Marnie asks the player to walk their animals into Town to show them off. Gates:
    /// LivestockFollowsYou installed, single-player, Marnie present, season != Winter,
    /// and at least 2 animals on the farm. Completion = entering Town with at least 2
    /// animals in tow (`$follower-count:2` Visit-step gate). Reward = FriendshipLarge
    /// for Marnie.
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

    /// CSV row 43. BuildingBuilt(Coop)+1 day. Mail+ItemDelivery to Marnie asking for a
    /// stack of Mixed Seeds (per CSV's "15 mixed seeds" wording, which the wiki resolves
    /// to the vanilla `(O)770` item). Reward = a free White Chicken adopted directly into
    /// the player's coop on completion (see `MoreQuests.ModEntry.GrantFreeChicken`) plus a
    /// FriendshipBasic bump for Marnie. The `MarnieChickenOfferRebate` config still ships
    /// as a fallback paid out when the player has no coop slot free.
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

    /// CSV row 44. BuildingBuilt(Barn)+1 day. Mail+ItemDelivery whose ask flips based on
    /// whether LivestockFollowsYou is installed: with LFY, Marnie wants the player to show
    /// up with a Grazing Bell (queried from LFY's API); without LFY, she asks for a Milk
    /// Pail. Reward = a free Dairy Cow adopted directly into the player's barn on
    /// completion (see `MoreQuests.ModEntry.GrantFreeCow`). `MarnieCowOfferRebate` stays
    /// as the fallback paid when every barn is full at completion time.
    private static QuestPosting? MarnieCowOffer(QuestContext ctx)
    {
        if (Game1.getCharacterFromName("Marnie") == null)
            return null;

        bool lfyLoaded = ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.LivestockFollowsYou);
        string objectiveId;
        string objectiveName;
        if (lfyLoaded && !string.IsNullOrEmpty(ModEntry.Lfy?.GrazingBellQualifiedItemId))
        {
            objectiveId = ModEntry.Lfy!.GrazingBellQualifiedItemId;
            objectiveName = "Grazing Bell";
        }
        else
        {
            objectiveId = "(T)MilkPail";
            objectiveName = "Milk Pail";
        }

        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = "Marnie",
            ObjectiveItemId = objectiveId,
            ObjectiveItemName = objectiveName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards =
            {
                new FriendshipReward("Marnie", ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.animal.marnieCowOffer.title"),
            Description = ModEntry.I18n.Get("quest.animal.marnieCowOffer.description", new { item = objectiveName }),
            CurrentObjective = ModEntry.I18n.Get("quest.animal.marnieCowOffer.objective", new { item = objectiveName }),
            TargetMessage = ModEntry.I18n.Get("quest.animal.marnieCowOffer.targetMessage")
        };
    }

    /// CSV row 45. OneShot on `chickenEggsLayed >= 1`. Mail+Ship 10 eggs through the bin
    /// with `AlternativeObjectiveItemIds` populated from a live scan of `Game1.objectData`
    /// (every edible-egg entry: Category -5, Edibility != -300) so brown / large / Void /
    /// Golden / Ostrich / Duck / modded eggs all count toward the haul. Reward =
    /// `GoldBasicBase` plus the Mayonnaise Machine — as a `RecipeReward` when the player
    /// doesn't know the recipe yet, or as a direct `(BC)24` `ObjectReward` when they do
    /// (so the quest doesn't silently no-op the reward on a re-roll save).
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

    /// Yields every `(O)<id>` Object whose `Data/Objects` row has Category -5 (egg) and
    /// `Edibility != -300` (inedible — excludes the Dinosaur Egg). Scans the live
    /// `Game1.objectData` so modded eggs registered via content-pack edits surface
    /// alongside vanilla ones. Used by `MarnieEggRequest` to widen its Ship-bin matcher
    /// beyond the vanilla white egg.
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

    /// CSV row 47. Mail+Ship 10 milk through the bin. Now widened beyond vanilla cow
    /// milk: `AlternativeObjectiveItemIds` is populated from a live scan of
    /// `Game1.farmAnimalData` for every animal whose `HarvestTool == "Milk Pail"` — picks
    /// up vanilla cow + goat milks plus any modded milking animal (Buffalo / Llama /
    /// content-pack additions). Reward = `GoldBasicBase` plus the Cheese Press —
    /// `RecipeReward` when not known, falls back to a direct `(BC)16` `ObjectReward`
    /// when the player already learned the recipe (Farming 6 / Mr. Qi etc).
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

    /// Yields every produce id (qualified `(O)<id>`) from every `Data/FarmAnimals` entry
    /// whose `HarvestTool == "Milk Pail"`. Picks up vanilla cow / goat milks (both
    /// regular and Large variants via `DeluxeProduceItemIds`) plus modded milking
    /// animals registered via content-pack edits.
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

    /// CSV row 64. Two JSON entries (one for Coop, one for Barn) gate this generator on
    /// `BuildingBuilt` + `not:BuildingExists Silo`, so the offer fires the first time the
    /// player builds either an animal house without already owning a silo. Mail+ItemDelivery
    /// where the player can satisfy the posting with EITHER 100 Stone, 10 Clay, or 5 Copper
    /// Bars (the actual silo recipe inputs). Reward = a free Silo *credit* on Robin's
    /// carpenter menu — `ModEntry.GrantFreeSilo` flips a player ModData flag that the
    /// `Data/Buildings` asset edit reads to zero the Silo entry's BuildCost and
    /// BuildMaterials. The player still chooses when and where to place the silo through
    /// Robin's shop; the credit is cleared on `BuildingListChanged` when a Silo is built.
    private static QuestPosting? RobinSiloOffer(QuestContext ctx)
    {
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

    // -------------------- Phase 9.5g: Multi-step / misc --------------------

    /// CSV row 15. Daily-board ItemDeliveryQuest. Caroline asks for off-season edible
    /// forage or flowers she loves or likes (no herbs) so she can brew a new batch of
    /// tea. Off-season pool: Y1 = seasons that have already passed in the current year
    /// (so Y1 spring skips), Y2+ = every season except the current one. Quantity scales
    /// with foraging level when DifficultyScaling is on; flat 5 when off. Reward =
    /// `FriendshipMid` to Caroline + Tea Leaves equal to twice the requested quantity.
}
