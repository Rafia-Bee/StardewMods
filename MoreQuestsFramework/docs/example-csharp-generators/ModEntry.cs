using System.Collections.Generic;
using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ExampleCSharpGenerators;

/// Pattern B: C# mod that registers JSON quest metadata plus C# generator functions.
///
/// JSON (in `assets/quests.json`) carries the static fields: trigger source, weight,
/// cooldown, category, `Available` conditions, deadline tier. The C# generator named
/// in the JSON's `Generator` field runs at trigger time and fills in the dynamic
/// parts (random giver, random in-season crop, scaled quantity, sell-price-based gold).
///
/// This example also shows the three "escape hatch" registrations that need C# code
/// but can be referenced from a content pack's JSON: `RegisterCustomTrigger` (a custom
/// trigger source), `RegisterCustomReward` (a custom reward kind), and a `SpecialOrder`
/// generator.
public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var fw = this.Helper.ModRegistry.GetApi<IMoreQuestsApi>("RafiaBee.MoreQuestsFramework");
        if (fw == null)
        {
            this.Monitor.Log("More Quests Framework not loaded. Quests disabled.", LogLevel.Warn);
            return;
        }

        // The framework fires RegistrationOpen on its own first update tick (after every
        // consumer mod's GameLaunched runs), so subscribing in our GameLaunched is the
        // right timing. The framework freezes the registry on RegistrationClosed.
        fw.RegistrationOpen += (_, _) =>
        {
            var scope = fw.GetModApi(this.ModManifest);

            // Daily-board generator. JSON entry references this name in its `Generator` field.
            scope.RegisterGenerator("RandomCropDelivery", this.BuildRandomCropDelivery);

            // SpecialOrder generator. Trigger.Source = "SpecialOrder" routes through this
            // and the returned QuestPosting carries a SpecialOrderSpec instead of an
            // Objective. Vanilla owns accept, objective tracking, and reward grant from there.
            scope.RegisterGenerator("MarnieEggDrive", this.BuildMarnieEggDrive);

            // Custom trigger source. The handler is called once per DayStarted; the framework
            // fires the quest the first day it returns true (subject to the def's CooldownDays).
            scope.RegisterCustomTrigger("PlayerOwnsStardrop", _ =>
                Game1.player.maxStamina.Value > 270);

            // Custom reward kind. `apply` runs at questComplete with the raw payload string.
            // `summarize` is optional and renders the reward preview line in the journal.
            scope.RegisterCustomReward(
                "RestoreHealthAndStamina",
                _ =>
                {
                    Game1.player.health = Game1.player.maxHealth;
                    Game1.player.Stamina = Game1.player.MaxStamina;
                },
                summarize: (_, giver, t) =>
                    t.Get("example.customReward.summary", new { npc = giver })
                        .Default($"{giver} will help you rest up").ToString());

            // Read the JSON file. Each entry stitches its declarative trigger metadata
            // to the named generator or to one of the registered Custom handlers above.
            scope.LoadQuestsFromMod(this.Helper, "assets/quests.json");
        };
    }

    /// Generator: a random met villager wants a random in-season crop delivered.
    /// Quantity scales with Farming. Gold reward scales with the crop's sell price.
    /// Returning null cancels this attempt (e.g. no in-season crops, no met NPCs).
    private QuestPosting? BuildRandomCropDelivery(QuestContext ctx)
    {
        var crops = ctx.Items.GetSeasonalCrops(ctx.Season);
        if (crops.Count == 0)
            return null;
        var crop = crops[Game1.random.Next(crops.Count)];

        var villagers = ctx.Dispatch.ResolvePool("MetAdultHumans");
        if (villagers.Count == 0)
            return null;
        string giver = villagers[Game1.random.Next(villagers.Count)];

        int qty = Game1.player.FarmingLevel + Game1.random.Next(2, 6);
        int gold = (int)(crop.SellPrice * qty * 1.05f);

        return new QuestPosting
        {
            Category = QuestCategory.Farming,
            Tier = DifficultyTier.Beginner,
            QuestType = BoardQuestType.ItemDelivery,
            QuestGiver = giver,
            ObjectiveItemId = crop.QualifiedItemId,
            ObjectiveItemName = crop.DisplayName,
            ObjectiveQuantity = qty,
            DeadlineDays = ctx.Config.DeadlineShort,
            Rewards =
            {
                new MoneyReward(gold),
                new FriendshipReward(giver, ctx.Config.FriendshipBasic)
            },
            Title = this.Helper.Translation.Get("example.cropDelivery.title", new { npc = giver }),
            Description = this.Helper.Translation.Get("example.cropDelivery.description", new { npc = giver, qty, item = crop.DisplayName }),
            CurrentObjective = this.Helper.Translation.Get("example.cropDelivery.objective", new { npc = giver, qty, item = crop.DisplayName }),
            TargetMessage = this.Helper.Translation.Get("example.cropDelivery.targetMessage")
        };
    }

    /// Generator for a SpecialOrder: Marnie asks the farmer to ship 20 eggs (any kind,
    /// modded eggs included via the `egg_item` context tag). Money is paid via vanilla's
    /// reward path so the standard reward-box UI shows it; friendship rides the framework
    /// path so third-party Special Order tweak packs can't intercept it.
    private QuestPosting? BuildMarnieEggDrive(QuestContext ctx)
    {
        return new QuestPosting
        {
            Category = QuestCategory.Animal,
            Tier = DifficultyTier.Intermediate,
            QuestType = BoardQuestType.Custom,
            Kind = PostingKind.SpecialOrder,
            QuestGiver = "Marnie",
            SpecialOrder = new SpecialOrderSpec
            {
                Name = this.Helper.Translation.Get("example.specialOrder.title"),
                Text = this.Helper.Translation.Get("example.specialOrder.text"),
                Requester = "Marnie",
                Duration = "Week",
                Objectives = new List<SpecialOrderObjectiveSpec>
                {
                    new()
                    {
                        Type = "Ship",
                        Text = this.Helper.Translation.Get("example.specialOrder.objective"),
                        RequiredCount = 20,
                        Data = { ["AcceptedContextTags"] = "egg_item" }
                    }
                },
                Rewards = new List<SpecialOrderRewardSpec>
                {
                    new()
                    {
                        Type = "Money",
                        Data = { ["Amount"] = "5000" }
                    }
                },
                FrameworkRewards = new List<RewardSpec>
                {
                    new FriendshipReward("Marnie", 80)
                }
            }
        };
    }
}
