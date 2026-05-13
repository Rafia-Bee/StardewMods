using MoreQuestsFramework;
using MoreQuestsFramework.Api;
using MoreQuestsFramework.Pipeline;
using MoreQuestsFramework.Quests;
using MoreQuestsFramework.Rewards;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;

namespace ExampleCSharpGenerators;

/// Pattern B: C# mod that registers JSON quest metadata plus C# generator functions.
///
/// JSON (in `assets/quests.json`) carries the static fields: trigger source, weight,
/// cooldown, category, `Available` conditions, deadline tier. The C# generator named
/// in the JSON's `Generator` field runs at trigger time and fills in the dynamic
/// parts (random giver, random in-season crop, scaled quantity, sell-price-based gold).
///
/// Use this pattern when you want declarative metadata for free (GMCM weights show up
/// automatically) but still need runtime randomization for the body of the quest.
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

            // Register the named generator. The JSON file references this name in
            // each quest entry's `Generator` field.
            scope.RegisterGenerator("RandomCropDelivery", this.BuildRandomCropDelivery);

            // Load the JSON quest list. The framework parses each entry, looks up the
            // named generator, and stitches them together into a full quest definition.
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
}
