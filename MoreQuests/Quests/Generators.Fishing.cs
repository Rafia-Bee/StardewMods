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
    /// One-time mail when the player learns the Fish Smoker recipe. Asks for qty of any
    /// SmokedFish output (every smoked-fish stack shares (O)SmokedFish, varying only by
    /// preservedParentSheetIndex). Qty scales with Fishing when scaling on. Reward: 500 friendship pts.
    private static QuestPosting? FishSmokerRequest(QuestContext ctx)
    {
        var candidates = MetAdultHumanGiftReceivers();
        var ecology = EcologyMindedSet(ctx);
        candidates.RemoveAll(n => ecology.Contains(n));
        if (candidates.Count == 0)
            return null;
        string giver = candidates[Game1.random.Next(candidates.Count)];

        int qty;
        if (ctx.Config.DifficultyScaling)
        {
            int fishing = Game1.player.FishingLevel;
            int upper = Math.Max(5, fishing * 2);
            qty = Game1.random.Next(5, upper + 1);
        }
        else
        {
            qty = Game1.random.Next(2, 7);
        }

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Ship,
            QuestGiver = giver,
            ObjectiveItemId = "(O)SmokedFish",
            ObjectiveItemName = string.Empty,
            ObjectiveQuantity = qty,
            ObjectiveItemWeight = 1,
            DeadlineDays = 0,
            Rewards = { new FriendshipReward(giver, 500) },
            Title = ModEntry.I18n.Get("quest.fishing.fishSmokerRequest.title"),
            Description = ModEntry.I18n.Get("quest.fishing.fishSmokerRequest.description", new { qty }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.fishSmokerRequest.objective", new { qty }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.fishSmokerRequest.targetMessage")
        };
    }

    /// Seasonal fish pool minus IsBossFish entries from Data/Locations. Keeps quests from
    /// asking for Legendaries or Extended-Family variants. Modded fish using the same flag
    /// are stripped too. `ignoresVisited` overrides FishingIgnoresVisitedLocations.
    private static List<ResolvedItem> GetSeasonalNonBossFish(QuestContext ctx, string? weatherFilter = null, bool? ignoresVisited = null)
    {
        bool useFullPool = ignoresVisited ?? ctx.Config.FishingIgnoresVisitedLocations;
        var seasonal = useFullPool
            ? ctx.Items.GetSeasonalFish(ctx.Season, weatherFilter)
            : ctx.Items.GetSeasonalFishInVisitedLocations(ctx.Season, weatherFilter);
        if (seasonal.Count == 0)
            return seasonal;

        var bossIds = new HashSet<string>(
            ctx.Items.GetBossFish().Select(f => f.QualifiedItemId),
            StringComparer.OrdinalIgnoreCase);
        if (bossIds.Count == 0)
            return seasonal;
        return seasonal.Where(f => !bossIds.Contains(f.QualifiedItemId)).ToList();
    }

    /// Fishing quest from any adult human who has a fish in their loved/liked list. Common-
    /// fish filter (Difficulty &lt; 60). Scaling off: fish must be catchable 600-2600 so the
    /// player isn't cornered into a narrow window. Description grounds the catch in a
    /// visited spawn location; candidates without one are dropped.
    private static QuestPosting? SimpleFishingRequest(QuestContext ctx)
    {
        var givers = MetAdultHumanFishLovers(ctx);
        if (givers.Count == 0)
            return null;

        bool scaling = ctx.Config.DifficultyScaling;

        var fish = GetSeasonalNonBossFish(ctx);
        if (fish.Count == 0)
            return null;

        var pool = fish.Where(f => f.Difficulty < 60).ToList();
        if (!scaling)
            pool = pool.Where(f => IsAllDayFish(ctx, f.QualifiedItemId)).ToList();
        if (pool.Count == 0)
            return null;

        ResolvedItem? target = null;
        string? targetLocation = null;
        for (int i = 0; i < 8 && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            var candidate = pool[idx];
            pool.RemoveAt(idx);
            string? loc = ResolveVisitedSpawnLocation(ctx, candidate.QualifiedItemId);
            if (loc != null)
            {
                target = candidate;
                targetLocation = loc;
                break;
            }
        }
        if (target == null || targetLocation == null)
            return null;

        int qtyMax = scaling
            ? Math.Max(2, (int)Math.Floor(Game1.player.FishingLevel * 1.5))
            : 5;
        int qty = Game1.random.Next(2, qtyMax + 1);
        string giver = givers[Game1.random.Next(givers.Count)];
        int gold = (int)(target.SellPrice * qty * ctx.Config.RewardMultiplierAboveSell);

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Title = ModEntry.I18n.Get("quest.fishing.simple.title"),
            Description = ModEntry.I18n.Get("quest.fishing.simple.description", new
            {
                npc = giver,
                qty,
                item = target.DisplayName,
                location = LocationDisplayName(targetLocation)
            }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.simple.objective", new { qty, item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.simple.targetMessage")
        };
    }

    /// Pierre or Joja asks for a bulk haul of one seasonal fish. Reward = sell-price below
    /// market. Tier 2 ecology consequence: every EcologyMinded NPC present gets a negative
    /// line and the Tier 2 friendship delta. Linus is reserved for Seafood Night (Tier 3).
    private static QuestPosting? MediumFishingHaul(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.BulkFishBuyer);
        if (giver == null)
            return null;

        var fish = GetSeasonalNonBossFish(ctx);
        if (fish.Count == 0)
            return null;

        var target = fish[Game1.random.Next(fish.Count)];
        int qty = Math.Max(1, ModEntry.Config.FishHaulMediumQty);
        int gold = (int)(target.SellPrice * qty * ctx.Config.RewardMultiplierBelowSell);

        ConsequenceSpec? consequence = null;
        if (ModEntry.Config.ConsequencesEnabled)
        {
            var ecology = ResolveEcologyTargets(ctx, includeLinus: false, exclude: giver);
            if (ecology.Count > 0)
            {
                consequence = new ConsequenceSpec
                {
                    Tier = ConsequenceTier.Tier2,
                    Source = ConsequenceSource.Static,
                    Targets = ecology,
                    HatedLine = ModEntry.I18n.Get(
                        "quest.fishing.mediumHaul.consequence.hated",
                        new { item = target.DisplayName, npc = giver })
                };
            }
        }

        string flavour = string.Equals(giver, "Pierre", StringComparison.OrdinalIgnoreCase)
            ? ModEntry.I18n.Get("quest.fishing.mediumHaul.description.pierre", new { qty, item = target.DisplayName })
            : ModEntry.I18n.Get("quest.fishing.mediumHaul.description.joja", new { qty, item = target.DisplayName, npc = giver });

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Consequence = consequence,
            Title = ModEntry.I18n.Get("quest.fishing.mediumHaul.title", new { npc = giver }),
            Description = flavour,
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.mediumHaul.objective", new { qty, item = target.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.mediumHaul.targetMessage")
        };
    }

    /// SaloonChef asks for a large haul of one edible non-poisonous fish. Reward uses the
    /// fish-premium multiplier. Tier 3 chain consequence: every ecology NPC plus Linus gets
    /// one line and a -FriendshipMid hit on each of ChainDays. Boss fish excluded upstream.
    private static QuestPosting? SeafoodNight(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.SaloonChef);
        if (giver == null)
            return null;

        var fish = GetSeasonalNonBossFish(ctx);
        var pool = fish.Where(IsEdibleNonPoisonous).ToList();
        if (pool.Count == 0)
            return null;

        var target = pool[Game1.random.Next(pool.Count)];
        int qty = Math.Max(1, ModEntry.Config.FishHaulLargeQty);
        int gold = (int)(target.SellPrice * qty * ctx.Config.RewardMultiplierFishPremium);

        ConsequenceSpec? consequence = null;
        if (ModEntry.Config.ConsequencesEnabled)
        {
            var ecology = ResolveEcologyTargets(ctx, includeLinus: true, exclude: giver);
            if (ecology.Count > 0)
            {
                consequence = new ConsequenceSpec
                {
                    Tier = ConsequenceTier.Tier3,
                    Source = ConsequenceSource.Static,
                    Targets = ecology,
                    ChainDays = 3,
                    FriendshipPerDay = -ctx.Config.FriendshipMid,
                    ChainLines = new List<string>
                    {
                        ModEntry.I18n.Get("quest.fishing.seafoodNight.consequence.chain.day1", new { item = target.DisplayName, npc = giver }),
                        ModEntry.I18n.Get("quest.fishing.seafoodNight.consequence.chain.day2", new { item = target.DisplayName, npc = giver }),
                        ModEntry.I18n.Get("quest.fishing.seafoodNight.consequence.chain.day3", new { item = target.DisplayName, npc = giver })
                    }
                };
            }
        }

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Expert,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = { new MoneyReward(gold) },
            Consequence = consequence,
            Title = ModEntry.I18n.Get("quest.fishing.seafoodNight.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.fishing.seafoodNight.description", new { qty, item = target.DisplayName, npc = giver }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.seafoodNight.objective", new { qty, item = target.DisplayName, npc = giver }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.seafoodNight.targetMessage")
        };
    }

    /// Pufferfish causes Nausea, the only vanilla fish a cook would call poisonous.
    /// Filtered out of Seafood Night. Modded fish stay in if Edibility &gt; 0.
    private static readonly HashSet<string> SeafoodNightExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        "(O)128" // Pufferfish
    };

    private static bool IsEdibleNonPoisonous(ResolvedItem fish)
    {
        if (SeafoodNightExclusions.Contains(fish.QualifiedItemId))
            return false;
        var data = StardewValley.ItemRegistry.GetData(fish.QualifiedItemId);
        if (data?.RawData is StardewValley.GameData.Objects.ObjectData obj)
            return obj.Edibility > 0;
        return false;
    }

    /// EcologyMinded pool (live) optionally with Linus appended for Tier 3. Excludes the
    /// quest giver so a shopkeeper-also-ecologist doesn't shame themselves.
    private static List<string> ResolveEcologyTargets(QuestContext ctx, bool includeLinus, string exclude)
    {
        var pool = ctx.Dispatch.ResolvePool(DispatchRoles.EcologyMinded);
        var targets = new List<string>(pool.Count + 1);
        foreach (var npc in pool)
        {
            if (string.Equals(npc, exclude, StringComparison.OrdinalIgnoreCase))
                continue;
            targets.Add(npc);
        }
        if (includeLinus
            && Game1.getCharacterFromName("Linus") != null
            && !targets.Any(n => string.Equals(n, "Linus", StringComparison.OrdinalIgnoreCase))
            && !string.Equals(exclude, "Linus", StringComparison.OrdinalIgnoreCase))
        {
            targets.Add("Linus");
        }
        return targets;
    }

    /// Two-step Adventure from a FishermenNpcs-pool giver. Catch X of a seasonal fish then
    /// deliver the gold-quality stack. Quality enforcement is on the Deliver step
    /// (MinQuality = 2) since vanilla's fish-caught event doesn't expose quality.
    private static QuestPosting? QualityFishDelivery(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.FishermenNpcs);
        if (giver == null)
            return null;

        var fish = GetSeasonalNonBossFish(ctx);
        if (fish.Count == 0)
            return null;
        var target = fish[Game1.random.Next(fish.Count)];

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(1, Game1.player.FishingLevel) * Game1.random.Next(1, 5)
            : Game1.random.Next(2, 8);
        qty = Math.Max(1, qty);

        int gold = (int)(target.SellPrice * qty * ctx.Config.RewardMultiplierBelowSell);

        string qualityName = QualityName(2);

        var quest = new AdventureQuest();
        quest.Initialize(new[]
        {
            new AdventureStepState
            {
                Name = "Catch",
                Kind = AdventureStepKind.Catch,
                Items = new List<string> { target.QualifiedItemId },
                Count = qty,
                Description = ModEntry.I18n.Get("quest.fishing.qualityFish.step.catch", new { qty, item = target.DisplayName })
            },
            new AdventureStepState
            {
                Name = "Deliver",
                Kind = AdventureStepKind.Deliver,
                Targets = new List<string> { giver },
                Items = new List<string> { target.QualifiedItemId },
                Count = qty,
                MinQuality = 2,
                Requires = new List<string> { "Catch" },
                Description = ModEntry.I18n.Get("quest.fishing.qualityFish.step.deliver", new { qty, quality = qualityName, item = target.DisplayName, npc = giver })
            }
        }, giver: giver, completionDialogue: ModEntry.I18n.Get("quest.fishing.qualityFish.targetMessage"));

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards =
            {
                new MoneyReward(gold),
                new FriendshipReward(giver, ctx.Config.FriendshipBasic)
            },
            Title = ModEntry.I18n.Get("quest.fishing.qualityFish.title", new { npc = giver }),
            Description = ModEntry.I18n.Get("quest.fishing.qualityFish.description", new { npc = giver, qty, quality = qualityName, item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.qualityFish.targetMessage"),
            PreBuiltQuest = quest
        };
    }

    /// Vanilla rare/ancient seeds for the Premium Crop Order reward. Resolved at
    /// pick-time so a missing modded id falls through to the next entry.

    /// Vanilla 1.6 Challenge Bait, high-attract bait used as the location-overpopulation
    /// quest reward (qty * 2 per quest). String-id item, so the qualified form is
    /// `(O)ChallengeBait`. If a content pack removes the item the resolver returns null
    /// and the reward no-ops.
    private const string ChallengeBaitId = "(O)ChallengeBait";

    private const string BaitId = "(O)685";

    /// Row 13, `<Location>` fish overpopulation. Daily-board FishingQuest gated on a
    /// specific location: pick a fish from the player's visited-location pool, then read
    /// Asks for a fish at a specific visited spawn spot. Reward: GoldIntermediateBase +
    /// Challenge Bait at 2x the fish qty. Giver from EcologyMinded.
    private static QuestPosting? LocationFishOverpopulation(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.EcologyMinded);
        if (giver == null)
            return null;

        // "Specific fish at a specific spot" only makes sense if the player has visited a
        // spawn for the fish. Force visited-only; helper strips boss/legendary fish.
        var fish = GetSeasonalNonBossFish(ctx, ignoresVisited: false);
        if (fish.Count == 0)
            return null;

        // Try a handful so a fish whose only spawn spots the player hasn't visited drops out.
        // Each pick walks Data/Locations to verify a visited spawn exists.
        ResolvedItem? target = null;
        string? targetLocation = null;
        var pool = new List<ResolvedItem>(fish);
        for (int i = 0; i < 8 && pool.Count > 0; i++)
        {
            int idx = Game1.random.Next(pool.Count);
            var candidate = pool[idx];
            pool.RemoveAt(idx);
            string? loc = ResolveVisitedSpawnLocation(ctx, candidate.QualifiedItemId);
            if (loc != null)
            {
                target = candidate;
                targetLocation = loc;
                break;
            }
        }
        if (target == null || targetLocation == null)
            return null;

        int qty = Math.Max(2, Math.Min(5, 2 + Game1.player.FishingLevel / 3));
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var bait = ctx.Items.TryResolveItem(ChallengeBaitId);
        if (bait != null)
            rewards.Add(new ObjectReward(ChallengeBaitId, qty * 2));

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            CatchLocationName = targetLocation,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.fishing.locationOverpop.title", new { location = LocationDisplayName(targetLocation) }),
            Description = ModEntry.I18n.Get("quest.fishing.locationOverpop.description", new
            {
                npc = giver,
                qty,
                item = target.DisplayName,
                location = LocationDisplayName(targetLocation)
            }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.locationOverpop.objective", new
            {
                qty,
                item = target.DisplayName,
                location = LocationDisplayName(targetLocation)
            }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.locationOverpop.targetMessage")
        };
    }

    /// Handler id the report-back Custom step points at (Targets[0]). Registered with
    /// the framework in RegisterKnowYourWatersReportBack.
    private const string KnowYourWatersReportHandler = "KnowYourWaters.Report";

    private const string FishSmokerId = "(BC)FishSmoker";
    private const string BaitAndBobberBookId = "(O)SkillBook_1";

    /// "Know your waters": catch one of every (non-boss) fish that lives at a single
    /// visited location this season, then report back to the giver in person. Talking to
    /// the giver after the catch set is done opens a question with three answers; the more
    /// humble the answer, the better the reward (see RegisterKnowYourWatersReportBack).
    /// Giver from the FishermenNpcs pool. Season-long deadline, so it's posted early in a
    /// season (gated by DayRange in quests.json).
    private static QuestPosting? KnowYourWaters(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.FishermenNpcs);
        if (giver == null)
            return null;

        var visited = Game1.player?.locationsVisited;
        if (visited == null || visited.Count == 0)
            return null;
        var visitedSet = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);

        // Real, catchable, non-boss fish for this season, keyed by id. GetSeasonalFish
        // already drops trap fish, seaweed/algae, and out-of-season legendaries; we just
        // strip the boss/legendary entries on top.
        var bossIds = new HashSet<string>(
            ctx.Items.GetBossFish().Select(f => f.QualifiedItemId),
            StringComparer.OrdinalIgnoreCase);
        var allowed = new Dictionary<string, ResolvedItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in ctx.Items.GetSeasonalFish(ctx.Season))
        {
            if (bossIds.Contains(f.QualifiedItemId))
                continue;
            allowed[f.QualifiedItemId] = f;
        }
        if (allowed.Count == 0)
            return null;

        // Each visited location that has at least two of those fish is a candidate.
        var candidates = new List<(string Loc, List<ResolvedItem> Fish)>();
        foreach (var (locName, data) in ctx.Data.Locations)
        {
            if (string.Equals(locName, "Default", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!visitedSet.Contains(locName) || data.Fish == null)
                continue;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fish = new List<ResolvedItem>();
            foreach (var spawn in data.Fish)
            {
                if (spawn?.ItemId == null)
                    continue;
                string qualified = StardewValley.ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                if (!allowed.TryGetValue(qualified, out var item))
                    continue;
                if (seen.Add(qualified))
                    fish.Add(item);
            }
            if (fish.Count >= 2)
                candidates.Add((locName, fish));
        }
        if (candidates.Count == 0)
            return null;

        var chosen = candidates[Game1.random.Next(candidates.Count)];
        string locationDisplay = LocationDisplayName(chosen.Loc);

        var steps = new List<AdventureStepState>(chosen.Fish.Count + 1);
        var catchNames = new List<string>(chosen.Fish.Count);
        for (int i = 0; i < chosen.Fish.Count; i++)
        {
            string name = $"Catch{i}";
            catchNames.Add(name);
            steps.Add(new AdventureStepState
            {
                Name = name,
                Kind = AdventureStepKind.Catch,
                Items = new List<string> { chosen.Fish[i].QualifiedItemId },
                Count = 1,
                LocationName = chosen.Loc,
                Description = ModEntry.I18n.Get("quest.fishing.knowYourWaters.step.catch", new { item = chosen.Fish[i].DisplayName, location = locationDisplay })
            });
        }
        steps.Add(new AdventureStepState
        {
            Name = "Report",
            Kind = AdventureStepKind.Custom,
            Targets = new List<string> { KnowYourWatersReportHandler },
            Requires = catchNames,
            Count = 1,
            Description = ModEntry.I18n.Get("quest.fishing.knowYourWaters.step.report", new { npc = giver })
        });

        var quest = new AdventureQuest();
        quest.Initialize(steps, giver: giver);

        // Posted early in a season, due by the last day of that season (day 28).
        int deadline = Math.Max(1, 29 - Game1.dayOfMonth);

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Adventure,
            QuestGiver = giver,
            ObjectiveQuantity = 1,
            DeadlineDays = deadline,
            Title = ModEntry.I18n.Get("quest.fishing.knowYourWaters.title", new { location = locationDisplay }),
            Description = ModEntry.I18n.Get("quest.fishing.knowYourWaters.description", new { location = locationDisplay }),
            PreBuiltQuest = quest
        };
    }

    /// Registers the three-answer "report back" prompt for Know Your Waters. The humblest
    /// answer hands over the most (three fishing books); the proud answer two Fish Smokers;
    /// the modest one in between. Called once from Generators.RegisterAll.
    private static void RegisterKnowYourWatersReportBack(IMoreQuestsModApi fw)
    {
        fw.RegisterReportBackChoice(KnowYourWatersReportHandler, new ReportBackPrompt
        {
            Question = ModEntry.I18n.Get("quest.fishing.knowYourWaters.report.question"),
            Options = new List<ReportBackOption>
            {
                new ReportBackOption
                {
                    Answer = ModEntry.I18n.Get("quest.fishing.knowYourWaters.report.option.proud"),
                    Reply = ModEntry.I18n.Get("quest.fishing.knowYourWaters.report.reply.proud"),
                    OnChosen = ctx => GrantKnowYourWatersReward(ctx, tier: 1)
                },
                new ReportBackOption
                {
                    Answer = ModEntry.I18n.Get("quest.fishing.knowYourWaters.report.option.modest"),
                    Reply = ModEntry.I18n.Get("quest.fishing.knowYourWaters.report.reply.modest"),
                    OnChosen = ctx => GrantKnowYourWatersReward(ctx, tier: 2)
                },
                new ReportBackOption
                {
                    Answer = ModEntry.I18n.Get("quest.fishing.knowYourWaters.report.option.struggled"),
                    Reply = ModEntry.I18n.Get("quest.fishing.knowYourWaters.report.reply.struggled"),
                    OnChosen = ctx => GrantKnowYourWatersReward(ctx, tier: 3)
                }
            }
        });
    }

    private static void GrantKnowYourWatersReward(ReportBackContext ctx, int tier)
    {
        switch (tier)
        {
            case 1:
                GiveQuestItem(ctx.Player, FishSmokerId, 2);
                break;
            case 2:
                GiveQuestItem(ctx.Player, FishSmokerId, 1);
                GiveQuestItem(ctx.Player, BaitAndBobberBookId, 1);
                break;
            default:
                GiveQuestItem(ctx.Player, BaitAndBobberBookId, 3);
                break;
        }
    }

    private static void GiveQuestItem(Farmer player, string qualifiedId, int count)
    {
        if (player == null || count <= 0)
            return;
        var item = StardewValley.ItemRegistry.Create(qualifiedId, count);
        if (item == null)
            return;
        player.addItemByMenuIfNecessary(item);
    }

    /// Mail quest when tomorrow is forecast rain. Filters to rainy Data/Fish entries
    /// with a runtime gate that the player is actually fishing in rain. Reward:
    /// GoldIntermediateBase + one rare tackle.
    private static QuestPosting? RainyDayCatch(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.FishermenNpcs);
        if (giver == null)
            return null;

        var fish = GetSeasonalNonBossFish(ctx, weatherFilter: "rainy");
        if (fish.Count == 0)
            return null;

        var target = fish[Game1.random.Next(fish.Count)];

        int qty = ctx.Config.DifficultyScaling
            ? Math.Max(1, Game1.player.FishingLevel + Game1.random.Next(1, 5))
            : Game1.random.Next(1, 5);
        int gold = (int)(target.SellPrice * qty * ctx.Config.RewardMultiplierBelowSell);

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var tackles = ctx.Items.GetTackles();
        if (tackles.Count > 0)
        {
            var tackle = tackles[Game1.random.Next(tackles.Count)];
            rewards.Add(new ObjectReward(tackle.QualifiedItemId));
        }

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Advanced,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = qty,
            CatchWeather = "Rain",
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Medium, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.fishing.rainyDay.title"),
            Description = ModEntry.I18n.Get("quest.fishing.rainyDay.description", new { qty, item = target.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.rainyDay.objective", new { qty, item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.rainyDay.targetMessage")
        };
    }

    /// Fish-agnostic FishingQuest filtered by size bucket: Small (1-24"), Medium (25-49"),
    /// Large (50+). Any caught fish in the bucket counts. Names a size category, not a fish
    /// or location. Giver from EcologyMinded. Reward: GoldIntermediateBase + qty*3 Wild Bait.
    private const string WildBaitId = "(O)774";

    private const int SizeBucketSmallMaxInches = 24;
    private const int SizeBucketMediumMaxInches = 49;

    private static QuestPosting? SizeFishOverpopulation(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.EcologyMinded);
        if (giver == null)
            return null;

        // Pick a bucket. Max = 0 means no upper bound (Large = any fish 50+ inches).
        int bucket = Game1.random.Next(3); // 0=Small, 1=Medium, 2=Large
        (int minSize, int maxSize) = bucket switch
        {
            0 => (1, SizeBucketSmallMaxInches),
            1 => (SizeBucketSmallMaxInches + 1, SizeBucketMediumMaxInches),
            _ => (SizeBucketMediumMaxInches + 1, 0)
        };
        string bucketKey = bucket switch
        {
            0 => "small",
            1 => "medium",
            _ => "large"
        };

        // The catch counter doesn't gate on ItemId (CatchAnyFish bypasses it), but vanilla's
        // FishingQuest.loadQuestInfo only short-circuits when both target and ItemId are set.
        var fish = GetSeasonalNonBossFish(ctx);
        if (fish.Count == 0)
            return null;
        var placeholder = fish[Game1.random.Next(fish.Count)];

        int qty = Math.Max(2, Math.Min(5, 2 + Game1.player.FishingLevel / 3));
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var wildBait = ctx.Items.TryResolveItem(WildBaitId);
        if (wildBait != null)
            rewards.Add(new ObjectReward(WildBaitId, qty * 3));

        string bucketLabel = ModEntry.I18n.Get($"quest.fishing.sizeOverpop.bucket.{bucketKey}");
        string flavour = ModEntry.I18n.Get(
            bucketKey == "small"
                ? "quest.fishing.sizeOverpop.description.small"
                : "quest.fishing.sizeOverpop.description.predator",
            new { qty, bucket = bucketLabel, minSize, maxSize, npc = giver });

        // {0} and {1} are intentionally left for `string.Format` at journal-render time
        // (SMAPI's i18n only substitutes mustache tokens like `{{bucket}}`).
        string progressTemplate = ModEntry.I18n.Get(
            "quest.fishing.sizeOverpop.progress",
            new { bucket = bucketLabel });

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = placeholder.QualifiedItemId,
            ObjectiveItemName = placeholder.DisplayName,
            ObjectiveQuantity = qty,
            CatchMinSize = minSize,
            CatchMaxSize = maxSize,
            CatchAnyFish = true,
            CatchProgressTemplate = progressTemplate,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.fishing.sizeOverpop.title", new { bucket = bucketLabel }),
            Description = flavour,
            CurrentObjective = ModEntry.I18n.Get(
                maxSize > 0 ? "quest.fishing.sizeOverpop.objective.bounded" : "quest.fishing.sizeOverpop.objective.large",
                new
                {
                    qty,
                    bucket = bucketLabel,
                    minSize,
                    maxSize
                }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.sizeOverpop.targetMessage")
        };
    }

    /// FishingQuest restricted to IsBossFish entries from Data/Locations (vanilla legendaries
    /// + modded equivalents). Skipped when no legendary is in-season. Reward: GoldExpertBase
    /// + 5 Challenge Bait.
    private const int LegendaryChallengeBaitQty = 5;

    private static QuestPosting? LegendaryFishQuest(QuestContext ctx)
    {
        const string giver = "Willy";

        var bosses = ctx.Items.GetCatchableBossFish();
        if (bosses.Count == 0)
            return null;

        var seasonal = ctx.Items.GetSeasonalFish(ctx.Season);
        if (seasonal.Count == 0)
            return null;

        var seasonalIds = new HashSet<string>(seasonal.Select(f => f.QualifiedItemId), StringComparer.OrdinalIgnoreCase);
        var pool = bosses.Where(f => seasonalIds.Contains(f.QualifiedItemId)).ToList();
        if (pool.Count == 0)
            return null;

        // Legendaries are once-per-save by vanilla design (CatchLimit=1 on their
        // spawn row, and fishCaught is keyed by qualified id), so don't ask for one
        // the player has already landed. Modded legendaries (SVE Turret Fish etc.)
        // follow the same pattern and get the same treatment.
        pool.RemoveAll(f => Game1.player.fishCaught.ContainsKey(f.QualifiedItemId));
        if (pool.Count == 0)
            return null;

        // Drop fish that were targeted by a recent posting so the quest doesn't ask
        // for the same legendary twice in a row. If filtering empties the pool (only
        // one legendary in season), fall back to the original list so the quest can
        // still post.
        var freshPool = pool.Where(f => !ctx.IsItemRecent(f.QualifiedItemId)).ToList();
        if (freshPool.Count > 0)
            pool = freshPool;

        var target = pool[Game1.random.Next(pool.Count)];

        int gold = ctx.Config.GoldExpertBase;
        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var bait = ctx.Items.TryResolveItem(ChallengeBaitId);
        if (bait != null)
            rewards.Add(new ObjectReward(ChallengeBaitId, LegendaryChallengeBaitQty));

        return new QuestPosting
        {
            Category = QuestCategory.Fishing,
            Tier = DifficultyTier.Expert,
            QuestType = BoardQuestType.Fishing,
            QuestGiver = giver,
            ObjectiveItemId = target.QualifiedItemId,
            ObjectiveItemName = target.DisplayName,
            ObjectiveQuantity = 1,
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Long, ctx.Config),
            Rewards = rewards,
            IsReportBack = true,
            Title = ModEntry.I18n.Get("quest.fishing.legendary.title", new { item = target.DisplayName }),
            Description = ModEntry.I18n.Get("quest.fishing.legendary.description", new { item = target.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.legendary.objective", new { item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.legendary.targetMessage")
        };
    }

    /// Walks Data/Locations for the fish's spawn entries intersected with the player's visited
    /// locations. Returns the first match, or null if the fish has no visited spawn spot.
    private static string? ResolveVisitedSpawnLocation(QuestContext ctx, string fishQualifiedId)
    {
        try
        {
            var visited = Game1.player?.locationsVisited;
            if (visited == null || visited.Count == 0)
                return null;
            var visitedSet = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);

            string season = ctx.Season;
            foreach (var (locName, data) in ctx.Data.Locations)
            {
                if (string.Equals(locName, "Default", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!visitedSet.Contains(locName))
                    continue;
                if (data.Fish == null)
                    continue;
                foreach (var spawn in data.Fish)
                {
                    if (spawn?.ItemId == null) continue;
                    if (spawn.Season.HasValue && !string.Equals(spawn.Season.Value.ToString(), season, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string qualified = StardewValley.ItemRegistry.QualifyItemId(spawn.ItemId) ?? spawn.ItemId;
                    if (string.Equals(qualified, fishQualifiedId, StringComparison.OrdinalIgnoreCase))
                        return locName;
                }
            }
        }
        catch (Exception ex)
        {
            ctx.Monitor.Log($"ResolveVisitedSpawnLocation: {ex.Message}", LogLevel.Warn);
        }
        return null;
    }

    /// Pretty-printer for location keys in quest descriptions. Maps common vanilla keys to
    /// their in-game labels. Unknown keys fall back to the runtime location's DisplayName,
    /// then to a humanized form of the raw key. Detects "(no translation:..." (Content
    /// Patcher's marker for a missing i18n token) and treats it as a translation failure.
    private static string LocationDisplayName(string key)
    {
        string label = key?.ToLowerInvariant() switch
        {
            "town" => "Pelican Town",
            "beach" => "the beach",
            "mountain" => "the mountain",
            "forest" => "Cindersap Forest",
            "woods" => "the Secret Woods",
            "backwoods" => "the Backwoods",
            "desert" => "the Calico Desert",
            "submarine" => "the Night Market submarine",
            "islandsouth" or "islandnorth" or "islandwest" or "islandeast" or "islandsoutheast" => "Ginger Island",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(label))
            return label;
        var loc = Game1.getLocationFromName(key);
        string? display = loc?.DisplayName;
        if (!string.IsNullOrWhiteSpace(display) && !display.StartsWith("(no translation:", StringComparison.Ordinal))
            return display;
        return HumanizeLocationKey(key);
    }

    private static string HumanizeLocationKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        if (key.StartsWith("Custom_Ridgeside_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("RidgesideVillage", StringComparison.OrdinalIgnoreCase))
            return "Ridgeside Village";
        if (key.StartsWith("Lumisteria.MtVapius", StringComparison.OrdinalIgnoreCase)
            || key.IndexOf("MtVapius", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Mount Vapius";
        if (key.StartsWith("Custom_EastScarp_", StringComparison.OrdinalIgnoreCase)
            || key.StartsWith("EastScarp_", StringComparison.OrdinalIgnoreCase))
            return "East Scarp";
        if (key.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
            return key.Substring("Custom_".Length).Replace('_', ' ');
        return key.Replace('_', ' ');
    }

}
