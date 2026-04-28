# More Quests Framework

A SMAPI mod for Stardew Valley that provides a quest engine and a multi-slot help-wanted billboard. Other mods register quests through it; the framework handles generation, posting, rendering, completion, and rewards.

This mod is the engine that powers [More Quests](../MoreQuests/README.md). It also ships four configurable wrappers around the vanilla quest types so you get a working out-of-the-box billboard even without any consumer mod installed.

## What this mod provides

- **Multi-slot billboard.** Replaces vanilla's single "Quest of the Day" with a configurable per-day batch. Custom UI (`MoreQuestsBillboard`) renders all slots; `BillboardPatches` reroutes vanilla's billboard click + draw paths.
- **Daily generation pipeline.** At `DayStarted`, samples from the registry by weight (subject to `MaxPerDay`, `CooldownDays`, per-NPC dedup, friendship-cap dedup, and a recent-history filter) to fill up to `QuestsPerDay` slots, then runs a separate pass for `PostingKind.Mail` quests.
- **Quest factory.** Builds the right vanilla `Quest` subclass for each posting (`ItemDeliveryQuest`, `FishingQuest`, `SlayMonsterQuest`, `ResourceCollectionQuest`) and stamps a unique ID so external trackers (MH Quest Manager, etc.) attribute the quest correctly.
- **Reward applier.** Single funnel for friendship and item rewards, applied at `questComplete` so vanilla in-person delivery, Mail Services Mod, and any future delivery channel produce the same payout. Vanilla's hidden bonuses (every-3rd-quest prize ticket, default 150/255 friendship bumps) are suppressed so every reward is explicit.
- **Custom Quest subclasses.** `MoreQuestsItemDeliveryQuest` and `MoreQuestsFishingQuest` add `customItemReward` / `customItemRewardCount` / `friendshipRewardNpc` / `friendshipRewardPoints` NetFields and override the vanilla turn-in logic to actually consume the requested items (vanilla's `FishingQuest.OnNpcSocialized` doesn't reduce the stack — you could sell every fish and still claim the reward).
- **Four vanilla wrappers.** `VanillaItemDelivery`, `VanillaResourceCollection`, `VanillaSlayMonster`, `VanillaFishing` expose vanilla quest types as configurable `IQuestDefinition`s with their own GMCM weights, so the billboard has content even with no consumer mod installed.
- **Condition evaluator + game-data cache.** `ConditionEvaluator` is a dictionary-driven `IsAvailable` helper (the seed for the future JSON `Available { ... }` block). `GameDataCache` reads `Data/Crops`, `Data/Fish`, `Data/Locations`, `Data/CookingRecipes`, `Data/NPCGiftTastes` once per day so quest generators don't pay the load cost per Build call.
- **Item resolver + NPC dispatch.** `ItemResolver` reads the cached game data so modded items surface automatically. `NpcDispatch` is a role-based picker (saloon chefs, ecology-minded, conservation guides, etc.) that includes vanilla and modded NPCs and falls back gracefully when a referenced NPC mod is missing.
- **Custom assets.** Pad and pin sprites for the billboard, loaded from `Mods/RafiaBee.MoreQuestsFramework/Pad` and `.../Pin`.

## Dependencies

**Required**

- **SpaceCore** (`spacechase0.SpaceCore`) — used to register custom `Quest` subclasses with the serializer so saves round-trip cleanly. The framework hard-depends on SpaceCore.

**Optional**

- **Generic Mod Config Menu** (`spacechase0.GenericModConfigMenu`) — exposes the framework's tunables and per-quest weights as an in-game config page.

## Configuration

`Mods/MoreQuestsFramework/config.json`. Surfaced through GMCM when installed. Settings include:

- **Quest board** — `QuestsPerDay`, `AllowDuplicateGiverPerDay`, `SkipFriendshipQuestsAtMaxHeart`.
- **Per-quest weights** — one entry per registered `IQuestDefinition` (built dynamically; consumer mods' quests appear here too).
- **Vanilla wrappers** — toggle and tune the four bundled vanilla quest types.
- **Friendship reward sizes** — `FriendshipBasic`, `FriendshipMid`, `FriendshipIntermediate`, `FriendshipLarge`, `FriendshipMultiSmall`, `FriendshipMultiHeart`.
- **Gold reward bases** — beginner / basic / intermediate / advanced / expert tiers.
- **Reward multipliers** — `RewardMultiplierBelowSell`, `RewardMultiplierAboveSell`, `RewardMultiplierFishPremium`.
- **Deadlines** — short / medium / long / extended (in-game days).

GMCM registration is deferred until the first `UpdateTicking` so consumer-mod quests that register during their own `OnGameLaunched` appear in the per-quest weight list.

## Registering a quest from another mod

```csharp
public override void Entry(IModHelper helper)
{
    helper.Events.GameLoop.GameLaunched += OnGameLaunched;
}

private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
{
    var fw = Helper.ModRegistry.GetApi<MoreQuestsFramework.Api.IInternalApi>(
        "RafiaBee.MoreQuestsFramework");
    if (fw == null) return;

    fw.RegisterQuest(new MyQuestDefinition());
    fw.RegisterCustomQuestType(typeof(MyCustomQuestSubclass));
}
```

`IInternalApi` is the current registration seam ([Api/IInternalApi.cs](Api/IInternalApi.cs)). It's deliberately minimal until the public `IMoreQuestsApi` lands in Phase 5 (see `docs/plan.md` in the content mod). Consumer mods need a hard SMAPI dependency on `RafiaBee.MoreQuestsFramework` and either a project reference to this assembly or their own copy of the `IQuestDefinition` / `QuestPosting` / `QuestContext` shapes.

`IQuestDefinition`:

```csharp
public interface IQuestDefinition
{
    string Id { get; }
    QuestCategory Category { get; }
    PostingKind Kind { get; }
    int DefaultWeight { get; }
    int MaxPerDay { get; }
    int CooldownDays { get; }
    bool IsAvailable(QuestContext ctx);
    QuestPosting? Build(QuestContext ctx);
}
```

`Build` returns `null` if generation failed (no matching items, no available NPC, etc.); the pipeline simply skips the slot.

## Project layout

```
MoreQuestsFramework/
  ModEntry.cs                         // hooks events, builds the API, runs the daily pipeline
  Api/
    IInternalApi.cs                   // current registration interface for consumer mods
    InternalApi.cs                    // public top-level concrete impl (top-level required by SMAPI)
  Registry/QuestRegistry.cs           // runtime registry with Register/WithKind/Freeze/Clear
  Pipeline/
    QuestPipeline.cs                  // daily + triggered generation orchestrator
    QuestPoster.cs                    // routes postings to the board / mail / asset edits
  Posting/QuestFactory.cs             // builds the vanilla Quest subclass for a posting
  Conditions/ConditionEvaluator.cs    // dictionary-driven IsAvailable helpers
  Cache/GameDataCache.cs              // per-day cache of Data/Crops, Data/Fish, etc.
  Rewards/RewardApplier.cs            // single funnel for friendship + item rewards
  Patches/BillboardPatches.cs         // Harmony patches that reroute the vanilla billboard
  MoreQuestsBillboard.cs              // custom multi-slot billboard UI
  BillboardSlots.cs                   // model behind the billboard UI
  Quests/
    MoreQuestsItemDeliveryQuest.cs    // explicit-rewards subclass with NetFields
    MoreQuestsFishingQuest.cs         // overrides vanilla turn-in to actually consume fish
    Vanilla/                          // four configurable wrappers around vanilla quest types
  IQuestDefinition.cs                 // interface for individual quest generators
  QuestPosting.cs                     // shared DTO + PostingKind / BoardQuestType / consequences
  QuestContext.cs                     // helpers passed to each definition's Build
  ItemResolver.cs                     // resolves items via the cached game data
  NpcDispatch.cs                      // role-based quest-giver picker (mod aware)
  AntiRepetition.cs                   // recent-item / NPC / definition history
  ModCompat.cs                        // mod IDs + IsLoaded helpers
  Difficulty.cs                       // tier + deadline + skill mapping
  ISpaceCoreApi.cs                    // SpaceCore interface
  IGenericModConfigMenuApi.cs         // GMCM interface
  Config/MoreQuestsFrameworkConfig.cs // engine tunables
  i18n/                               // engine config + reward summary + per-quest weight labels
  assets/                             // pad + pin textures
  manifest.json
```

## Notes for consumer mods

- The framework is loaded by SMAPI as its own mod. Consumer mods should declare `RafiaBee.MoreQuestsFramework` as a `Dependencies` entry with `IsRequired: true` so they load after the framework.
- For shared types (`IQuestDefinition`, `QuestPosting`, `QuestContext`), use a `<ProjectReference>` with `<Private>false</Private>` and `<ExcludeAssets>runtime</ExcludeAssets>` so the framework DLL isn't copied into the consumer mod's deploy folder. SMAPI's `AssemblyResolve` finds the engine types in the framework's loaded assembly at runtime.
- Custom `Quest` subclasses must carry a unique `[XmlType("Mods_<owner>_<name>")]` attribute. Register them via `fw.RegisterCustomQuestType(typeof(...))` so SpaceCore's serializer factory knows about them.
