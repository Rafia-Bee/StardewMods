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
    /// Seasonal fish pool minus anything flagged `IsBossFish` in `Data/Locations`. Used
    /// by every non-Legendary fishing generator so quests never ask for vanilla Crimsonfish
    /// / Angler / Legend / Glacierfish / Mutant Carp or the Extended-Family variants
    /// (Son of Crimsonfish, Ms. Angler, Legend II, Glacierfish Jr., Radioactive Carp) that
    /// only spawn during Mr. Qi's Extended Family special order. Modded fish that follow
    /// the same `IsBossFish` convention are stripped too. `ignoresVisited` overrides the
    /// `FishingIgnoresVisitedLocations` config when a quest deliberately wants one or the
    /// other (LocationFishOverpopulation always wants visited-only since it grounds the
    /// quest in a real spot).
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

    /// CSV row 12. Daily-board fishing quest. Giver is any met adult human who has at
    /// least one fish in their loved/liked gift-taste pool, so the request reads as the
    /// NPC asking for a fish they'd actually want. Common-fish filter = Difficulty < 60.
    /// Time gate: with `DifficultyScaling` on the time field is unconstrained; with it
    /// off the fish must be catchable for the entire vanilla day (600 to 2600), so the
    /// player isn't cornered into fishing at a narrow window. Description grounds the
    /// catch in a visited spawn location; if no visited location for the picked fish
    /// resolves the candidate is dropped and another is tried.
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

    /// CSV row 49. Daily-board fishing quest. Pierre or Joja (Morris/MorrisTod) ask the
    /// player for a bulk haul of one specific seasonal fish; reward is sell-price scaled
    /// below market (`RewardMultiplierBelowSell`). Tier 2 ecology consequence: every
    /// member of the `EcologyMinded` pool present on the save (Demetrius + RSV's Maddie /
    /// Mr. Aguar + East Scarp's Dylan) gets a single negative line + the Tier 2 default
    /// negative friendship delta on the next chat. Linus is intentionally excluded — the
    /// plan reserves him for Tier 3 (Seafood Night).
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

    /// CSV row 65. Daily-board fishing quest. SaloonChef-pool giver (Gus / Pika RSV /
    /// Rosa ESV / Celestine VMV) asks for a large haul of one edible non-poisonous fish;
    /// reward is `RewardMultiplierFishPremium` of the fish's price × qty. Tier 3 ecology
    /// chain consequence: every ecology NPC present on the save plus Linus gets one
    /// chained dialogue line and a `-FriendshipMid` friendship hit on each of `ChainDays`
    /// consecutive days (per-day, no division). Static source so the engine pushes a
    /// chain to every target rather than sampling a single NPC.
    /// Legendary / boss fish are excluded upstream by `GetSeasonalNonBossFish`, which
    /// strips anything flagged `IsBossFish` in `Data/Locations` (vanilla legendaries +
    /// modded fish following the same convention). Sell-price floor was dropped, the
    /// multiplier + quantity already do the reward scaling work.
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

    /// Pufferfish carries the Nausea status effect when eaten — the only vanilla "fish"
    /// any reasonable cook would call poisonous. Filtered out of the Seafood Night pool
    /// so the CSV's "edible non-poisonous" framing holds. Modded fish stay in as long as
    /// their Edibility is positive.
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

    /// Resolves the live `EcologyMinded` pool (filtered by mod presence + NPC existence)
    /// and optionally appends Linus for Tier 3 quests. Excludes the quest giver — a
    /// shopkeeper who's also coincidentally in the ecology role shouldn't shame
    /// themselves on the next chat. The list is the resolved snapshot, so saves where a
    /// modded NPC is missing simply drop that entry.

    /// Resolves the live `EcologyMinded` pool (filtered by mod presence + NPC existence)
    /// and optionally appends Linus for Tier 3 quests. Excludes the quest giver — a
    /// shopkeeper who's also coincidentally in the ecology role shouldn't shame
    /// themselves on the next chat. The list is the resolved snapshot, so saves where a
    /// modded NPC is missing simply drop that entry.
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

    /// Krobus + Dwarf are vanilla; Sen ships with East Scarp. The list is the literal
    /// CSV row 53 set; the engine filters to met villagers downstream so unknown NPCs
    /// silently drop out. We still pre-filter by `getCharacterFromName` so we never queue
    /// a line for an NPC whose mod isn't loaded.

    /// CSV row 59. Daily-board two-step Adventure quest. Giver is dispatched from the
    /// `FishermenNpcs` pool (Willy / Pam / Elliott vanilla, plus Carmen / Blair RSV and
    /// modded fishermen Arumi + Gunnar when their packs are loaded). The player catches
    /// X of a seasonal fish first, then delivers the gold-quality stack to the giver.
    /// Quality enforcement lives on the Deliver step (`MinQuality = 2`) since vanilla's
    /// fish-caught event doesn't expose quality, so the Catch step only counts catches.
    /// Reward is unclamped sell-price * qty * `RewardMultiplierBelowSell` (no 30g floor).
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

    /// Curated rare-tackle pool for the Rainy Day Catch reward. All vanilla qualified ids;
    /// modded saves get the same pool unless the picker resolves to a missing id, in which
    /// case the pick falls through.
    private static readonly (string Id, string Name)[] RainyDayTacklePool =
    {
        ("(O)694", "Spinner"),
        ("(O)695", "Trap Bobber"),
        ("(O)691", "Barbed Hook"),
        ("(O)693", "Treasure Hunter"),
        ("(O)877", "Curiosity Lure")
    };

    /// Vanilla 1.6 Challenge Bait, high-attract bait used as the location-overpopulation
    /// quest reward (qty * 2 per quest). String-id item, so the qualified form is
    /// `(O)ChallengeBait`. If a content pack removes the item the resolver returns null
    /// and the reward no-ops.
    private const string ChallengeBaitId = "(O)ChallengeBait";

    private const string BaitId = "(O)685";

    /// Row 13, `<Location>` fish overpopulation. Daily-board FishingQuest gated on a
    /// specific location: pick a fish from the player's visited-location pool, then read
    /// its first eligible spawn location from the visited set so the quest description
    /// can ground the request in a real spot. Reward = `GoldIntermediateBase` + Challenge
    /// Bait at 2x the requested fish quantity. Giver is dispatched via `EcologyMinded`.
    private static QuestPosting? LocationFishOverpopulation(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.EcologyMinded);
        if (giver == null)
            return null;

        // The CSV's "fish for a specific fish at a specific spot" only makes sense if the
        // player has actually visited a spawn location for the fish. Force the visited-only
        // pool regardless of the global `FishingIgnoresVisitedLocations` config; the helper
        // strips boss/legendary fish so we never ask for one-per-save targets.
        var fish = GetSeasonalNonBossFish(ctx, ignoresVisited: false);
        if (fish.Count == 0)
            return null;

        // Try a handful of fish so a fish whose only spawn locations the player hasn't
        // actually visited drops out. Each pick walks Data/Locations to verify the fish
        // has at least one spawn in a visited spot.
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
            Title = ModEntry.I18n.Get("quest.fishing.locationOverpop.title"),
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

    /// Row 61. Mail-delivered rainy-weather fishing quest. Fires through `WeatherForecast`
    /// when tomorrow is forecast as rain. A configurable `RainyDayCatchMailChancePercent`
    /// roll gates each qualifying day (default 100 = always). Giver is dispatched from
    /// the shared `FishermenNpcs` pool. Filters to fish whose `Data/Fish` weather field
    /// includes "rainy" with a runtime gate that the player is fishing in actual rain.
    /// Reward = `GoldIntermediateBase` + one rare tackle. Deadline = Short so the quest
    /// stays within the rainy window.
    private static QuestPosting? RainyDayCatch(QuestContext ctx)
    {
        int chance = Math.Clamp(ModEntry.Config.RainyDayCatchMailChancePercent, 0, 100);
        if (chance <= 0 || Game1.random.Next(100) >= chance)
            return null;

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
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var tackle = PickResolved(ctx, RainyDayTacklePool);
        if (tackle != null)
            rewards.Add(new ObjectReward(tackle.QualifiedItemId));

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
            DeadlineDays = Difficulty.Deadline(DeadlineKind.Short, ctx.Config),
            Rewards = rewards,
            Title = ModEntry.I18n.Get("quest.fishing.rainyDay.title"),
            Description = ModEntry.I18n.Get("quest.fishing.rainyDay.description", new { qty, item = target.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.rainyDay.objective", new { qty, item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.rainyDay.targetMessage")
        };
    }

    /// Row 68 — Small/Medium/Large fish overpopulation. Daily-board fish-agnostic
    /// FishingQuest filtered by a hardcoded size bucket: Small (1-24 inches), Medium
    /// (25-49), Large (50+). Any caught fish whose reported size in inches lands in the
    /// active bucket counts (`CatchAnyFish` + `CatchMinSize` + `CatchMaxSize`). Quest
    /// names neither a fish nor a location, only the size category and its inch range.
    /// Giver dispatched from `EcologyMindedNpcs` (Demetrius vanilla, plus Maddie /
    /// Mr. Aguar RSV, Dylan East Scarp). Reward = `GoldIntermediateBase` + `qty * 3`
    /// Magic Bait. Boss / legendary fish are excluded upstream by `GetSeasonalNonBossFish`
    /// (their `IsBossFish` flag suppresses them across the whole fishing track).
    private const string MagicBaitId = "(O)908";

    private const int SizeBucketSmallMaxInches = 24;
    private const int SizeBucketMediumMaxInches = 49;

    private static QuestPosting? SizeFishOverpopulation(QuestContext ctx)
    {
        string? giver = ctx.Dispatch.Pick(DispatchRoles.EcologyMinded);
        if (giver == null)
            return null;

        // Pick a bucket. Mapping bucket → (min, max) inches. Max = 0 means no upper
        // bound (Large catches all fish ≥ 50 inches).
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

        // Pick any seasonal non-boss fish for the underlying ItemId. The catch counter
        // doesn't gate on this (CatchAnyFish bypasses the id check), but vanilla
        // FishingQuest.loadQuestInfo only short-circuits when both target and ItemId are
        // set, so we always supply a non-null fish.
        var fish = GetSeasonalNonBossFish(ctx);
        if (fish.Count == 0)
            return null;
        var placeholder = fish[Game1.random.Next(fish.Count)];

        int qty = Math.Max(2, Math.Min(5, 2 + Game1.player.FishingLevel / 3));
        int gold = ctx.Config.GoldIntermediateBase;

        var rewards = new List<RewardSpec> { new MoneyReward(gold) };
        var magicBait = ctx.Items.TryResolveItem(MagicBaitId);
        if (magicBait != null)
            rewards.Add(new ObjectReward(MagicBaitId, qty * 3));

        string bucketLabel = ModEntry.I18n.Get($"quest.fishing.sizeOverpop.bucket.{bucketKey}");
        string flavour = ModEntry.I18n.Get(
            bucketKey == "small"
                ? "quest.fishing.sizeOverpop.description.small"
                : "quest.fishing.sizeOverpop.description.predator",
            new { qty, bucket = bucketLabel, minSize, maxSize, npc = giver });

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

    /// Row 41 - Legendary Fish Quest. Daily-board FishingQuest restricted to legendary /
    /// boss fish (anything flagged `IsBossFish = true` in Data/Locations: vanilla Crimsonfish
    /// / Angler / Legend / Glacierfish / Mutant Carp + their family variants, plus modded
    /// equivalents like RSV's Deep Ridge Angler / Waterfall Snakehead / Sockeye Salmon).
    /// Quest is skipped when no legendary in the current season can be caught. Reward
    /// placeholder = `GoldExpertBase` + 50 Challenge Bait until per-fish display furniture
    /// assets are ready (CSV's "unique fish display furniture per fish" reward).
    private const int LegendaryChallengeBaitQty = 50;

    private static QuestPosting? LegendaryFishQuest(QuestContext ctx)
    {
        const string giver = "Willy";

        var bosses = ctx.Items.GetBossFish();
        if (bosses.Count == 0)
            return null;

        var seasonal = ctx.Items.GetSeasonalFish(ctx.Season);
        if (seasonal.Count == 0)
            return null;

        var seasonalIds = new HashSet<string>(seasonal.Select(f => f.QualifiedItemId), StringComparer.OrdinalIgnoreCase);
        var pool = bosses.Where(f => seasonalIds.Contains(f.QualifiedItemId)).ToList();
        if (pool.Count == 0)
            return null;

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
            Title = ModEntry.I18n.Get("quest.fishing.legendary.title"),
            Description = ModEntry.I18n.Get("quest.fishing.legendary.description", new { item = target.DisplayName }),
            CurrentObjective = ModEntry.I18n.Get("quest.fishing.legendary.objective", new { item = target.DisplayName }),
            TargetMessage = ModEntry.I18n.Get("quest.fishing.legendary.targetMessage")
        };
    }

    /// Row 60 — Rainbow Platter (Trout Derby, Summer 20-21). DateLocked yearly DailyBoard
    /// posting on Summer 20: catch `FestivalFishQty` Rainbow Trout (O)138. Giver dispatched
    /// via `SaloonChef`; reward = recipe (per-giver) + `ShopDiscountReward` on the dish for
    /// vanilla Gus saves only (the framework's discount writer needs a known shop id).

    /// Walks Data/Locations for the fish's spawn entries, intersected with the player's
    /// visited locations, returning the first matching location key. Returns null when
    /// the fish has no spawn in any visited spot. The CSV row asks for a fish at a
    /// specific spot, so we need an actual reachable location to ground the quest in.
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

    /// Lightweight pretty-printer for vanilla location keys used in quest descriptions.
    /// Maps the common keys back to their in-game labels; unknown keys (modded) pass
    /// through verbatim, which is usually what the player sees on the map anyway.

    /// Lightweight pretty-printer for vanilla location keys used in quest descriptions.
    /// Maps the common keys back to their in-game labels; unknown keys (modded) pass
    /// through verbatim, which is usually what the player sees on the map anyway.
    private static string LocationDisplayName(string key) => key?.ToLowerInvariant() switch
    {
        "town" => "Pelican Town",
        "beach" => "the beach",
        "mountain" => "the mountain lake",
        "forest" => "Cindersap Forest",
        "woods" => "the Secret Woods",
        "backwoods" => "the Backwoods",
        "desert" => "the Calico Desert",
        "submarine" => "the Night Market submarine",
        "islandsouth" or "islandnorth" or "islandwest" or "islandeast" or "islandsoutheast" => "Ginger Island",
        _ => key ?? string.Empty
    };

    // -------------------- Phase 9.5f: One-shot triggered animal/farm rows --------------------

    /// Counts farm animals whose `displayType` / animal type name contains the given
    /// substring (case-insensitive). Used by Alex's Protein Shakes to scale the egg ask
    /// by chicken count, where `kind = "Chicken"` matches White / Brown / Blue / Void
    /// chickens and any modded chicken type. Walks both `location.animals` and the
    /// indoor animals of every farm building in case the save spreads animals across
    /// multiple coops/barns.
}
