# More Quests

A SMAPI content mod for Stardew Valley that ships a curated set of new daily-board quests, mail-triggered quests, and custom completion logic on top of the [More Quests Framework](../MoreQuestsFramework/README.md).

> Heavy work in progress. Phases 1-6 are complete: framework split, declarative rewards/conditions, the JSON content-pack loader, the public `IMoreQuestsApi` (Beta), and the calendar/event trigger sources (Periodic, DateLocked, DateRange, OneShot, BuildingBuilt, MailReceived, WeatherForecast, NpcDialogue) with persistent save state are all in place. Phase 7a landed the `AdventureQuest` substrate plus `Deliver` / `Talk` / `Gift` step kinds with Check on George as the smoke test. Phase 7b adds single-objective `Ship` quests, multi-step `Ship` / `Catch` / `Slay` step kinds, item OR-alternatives in declarative objectives (`"Item": ["(O)787", "(O)382"]`), the `MailReward When: NextDay` alias, and migrates Submarine Fuel / Wizard's Ritual Materials / Evelyn's Holiday Cookies. The remaining 60+ quest concepts described in [this google sheet](https://docs.google.com/spreadsheets/d/13HQDEAYTcmi-x9Hp7R6lq2STRFtO5rUA3JxVOgitoDM/edit?usp=sharing) will be added across later phases.

## What this mod does

This mod is **content only**. It ships [assets/quests.json](assets/quests.json) — eleven quest definitions consumed by the framework's content-pack loader — plus a small set of named C# generators and three custom `Quest` subclasses for the quests that need runtime randomization (NPC dispatch, seasonal item pools, recipe walks). The framework owns the billboard, the daily generation pipeline, the four vanilla wrappers, the reward path, and all engine-level config; this mod just supplies quests.

If the framework isn't installed, this mod logs an error and registers nothing.

## Status

**Implemented**

| Category | Quest | Channel |
| --- | --- | --- |
| Farming | Basic Crop Delivery | DailyBoard |
| Fishing | Simple Fishing Request | DailyBoard |
| Mining | Basic Slime Clearing | DailyBoard |
| Mining | Bar Delivery | DailyBoard |
| Foraging | Seasonal Foraging | DailyBoard |
| Cooking | Craving a Meal | DailyBoard |
| Social | Elliott's Poem Inspiration | DailyBoard |
| Social | Check on George | DailyBoard |
| Animal | Hay Supply Run | Mail |
| Seasonal | Beach Cleanup (summer) | DailyBoard |
| Seasonal | Spring Tea (fall) | DailyBoard |
| Festival | Submarine Fuel (winter 12) | DateLocked / Mail |
| Festival | Wizard's Ritual Materials (fall 24) | DateLocked / Mail |
| Festival | Evelyn's Holiday Cookies (winter 21) | DateLocked / Mail |

**Planned (later phases)** — animal trigger letters, festival pre-quests, special-orders quests, NPC-dialogue quests, mod-gated quests for RSV / East Scarp / Visit Mount Vapius / SVE, custom-asset quests (Protein Bar, Animal Paintings, Void Chicken Statue, Egg Basket, Legendary Fish Displays), consequence dispatch via `Data/NPCGiftTastes`. [The google sheet](https://docs.google.com/spreadsheets/d/13HQDEAYTcmi-x9Hp7R6lq2STRFtO5rUA3JxVOgitoDM/edit?usp=sharing) and `docs/DECISION_LOG.md` have the full plan.

## Dependencies

**Required**

- **More Quests Framework** (`RafiaBee.MoreQuestsFramework`) — the engine that runs every quest in this mod. Bundled in this repo at [../MoreQuestsFramework/](../MoreQuestsFramework/).

**Optional integrations** (auto-detected at runtime)

- **Generic Mod Config Menu** (`spacechase0.GenericModConfigMenu`) for in-game configuration of the content-mod toggles (separate from the framework's GMCM page).
- **Ridgeside Village**, **East Scarp**, **Visit Mount Vapius**, **Stardew Valley Expanded** — adds the modded NPCs to the appropriate dispatch pools (saloon chefs, ecology-minded, conservation guides, etc.).
- **Livestock Follows You** by RafiaBee — required for a future quest batch (Marnie's Cow Offer, Marnie's Livestock Show, Leah's Farm Painting) (shameless plug).
- **Si's Extra Crafting Materials** (Nexus 25467) — required for the future Winter Star Wrapping Paper quest.

## Configuration

Two config files now exist, one per mod:

- **`Mods/MoreQuests/config.json`** (this mod) — per-quest content settings: animal/festival quest toggles, shop discount sizes, fish-haul quantities, Skull Cavern max level, secret-gift hint toggle, etc.
- **`Mods/MoreQuestsFramework/config.json`** ([framework](../MoreQuestsFramework/README.md#configuration)) — engine tunables: quests per day, per-quest weights, deadlines, friendship/gold reward sizes, reward multipliers, vanilla quest fishing flags.

Both are surfaced through Generic Mod Config Menu when GMCM is installed. The framework defers its GMCM registration by one tick so this mod's quests appear in the framework's per-quest weight list.

## Custom completion logic

Two of this mod's quests use bespoke `Quest` subclasses (registered with SpaceCore through the framework's API so saves round-trip cleanly):

- **`AnySlimeQuest`** — backs Basic Slime Clearing. Counts any slime kill, not just one species.
- **`CollectAndReportQuest`** — backs Beach Cleanup, Seasonal Foraging, etc. Player gathers items in the world, then reports to the giver to consume the stack and turn it in.

Check on George now runs on the framework's `AdventureQuest` (multi-step substrate), built directly from a generator: gift George → chat with him → report to Evelyn. The framework registers `AdventureQuest` with SpaceCore so the content mod doesn't need its own type registration.

For ItemDelivery and Fishing quests this mod uses the framework's `MoreQuestsItemDeliveryQuest` / `MoreQuestsFishingQuest` subclasses (constructed by the framework's `QuestFactory`). Both subclasses make rewards explicit (no hidden vanilla friendship bumps or prize tickets) and route every completion path through the same `RewardApplier`.

## Project layout

```
MoreQuests/
  ModEntry.cs            // hooks GameLaunched, fetches the framework API,
                         // registers custom Quest types + C# generators,
                         // loads assets/quests.json
  ModConfig.cs           // per-content tunables only
  GmcmRegistration.cs    // GMCM page for the content-mod toggles
  assets/
    quests.json          // every quest's metadata (Id, Trigger, Available, Generator/Objective)
  Quests/
    Generators.cs        // every Build() body, registered by name with the framework
    AnySlimeQuest.cs     // custom Quest subclass: counts any slime kill
    CollectAndReportQuest.cs  // custom Quest subclass: gather then talk
  i18n/                  // quest titles/descriptions/objectives + content config strings
  manifest.json
```

## Adding a new quest

For **most quests** (anything needing runtime randomization — NPC dispatch, item-pool selection, scaling quantities), declare metadata in JSON and register a generator in C#:

1. Add an entry to [assets/quests.json](assets/quests.json) with a unique `Name`, a `Category`, a `Trigger` block (`Source`, `Weight`, `MaxPerDay`, `CooldownDays`, optional `Available` condition dictionary), and a `Generator` name.
2. Implement the generator in [Quests/Generators.cs](Quests/Generators.cs) as a `Func<QuestContext, QuestPosting?>`. Return `null` to abstain when no candidate is available; the framework drops the slot.
3. Register the generator in `Generators.RegisterAll(...)` via `scope.RegisterGenerator("<name>", MyGenerator)` (where `scope` is the `IMoreQuestsModApi` returned by `fw.GetModApi(ModManifest)`).
4. Add `quest.<category>.<id>.title|description|objective|targetMessage` keys to [i18n/default.json](i18n/default.json) and look them up via `MoreQuests.ModEntry.I18n.Get(...)` from the generator.

For **fully-static quests** (a fixed item, a fixed giver, no runtime selection), skip the generator entirely and put `Title`, `Description`, `Giver`, `Objective`, and `Rewards` directly in JSON. See the example pack at [../MoreQuestsFramework/docs/example-pack/](../MoreQuestsFramework/docs/example-pack/).

Other notes:

- `Trigger.Source` accepts `DailyBoard` (board slot), `Mail` (auto-mailed each day conditions allow), `Periodic` (`EveryDays`), `DateLocked` (`Date`, optional `RepeatYearly`), `DateRange` (`From`, `To`), `OneShot` (`When`), `BuildingBuilt` (`Building`, optional `DayDelay`), `MailReceived` (`Flag`, optional `DayDelay`), `WeatherForecast` (`Weather`), `NpcDialogue` (`Npc`), or `SpecialOrder` (Phase 8). Set `Trigger.Delivery` to override the default delivery channel.
- The `Available` dictionary is fed into the framework's `ConditionEvaluator` — every key in `plan.md §2.6` is supported (`Season`, `MinDeepestMineLevel`, `SkillLevel`, `NpcExists`, `HasMod`, `GSQ`, etc.). `not:` prefix negates; `|` inside a value is OR.
- If the quest needs a custom `Quest` subclass, mark it `[XmlType("Mods_RafiaBee_MoreQuests_<Name>")]` and register it via `scope.RegisterCustomQuestType(typeof(YourQuest))` so SpaceCore can serialize it. The generator builds the subclass and assigns it to `posting.PreBuiltQuest`.
- During testing, run `mq_refresh` in the SMAPI console to re-roll the daily board without reloading the save.

## Known limitations

- **Mail Services Mod compatibility.** When you complete a More Quests delivery quest by mailing the item through Mail Services Mod, the recipient NPC will gain a full heart of friendship in addition to whatever the quest already rewards. Mail Services Mod mimics vanilla's in-person delivery flow, which hands out a fixed 250-point friendship bump regardless of what the quest itself defines, then it calls our completion handler which layers our declarative friendship reward on top. The result is the displayed reward plus an extra heart. Delivering the item to the NPC in person produces the correct reward. A request for upstream compatibility will be filed with the Mail Services Mod author once More Quests publishes.
TODO: Stamp custom quests with a modData marker (e.g. "RafiaBee.MoreQuests/SuppressVanillaFriendshipBonus" = "true") and ask the MSM author to honor it.

## Notes

- Framework engine code (registry, pipeline, billboard, condition evaluator, reward applier, item resolver, NPC dispatch, four vanilla wrappers) lives in the [framework mod](../MoreQuestsFramework/README.md) — this repo is content only.
- Mail prefix for in-world quest letters is `RafiaBee.MoreQuestsFramework.` (the framework owns the routing).
