# More Quests

A SMAPI content mod for Stardew Valley that ships a curated set of new daily-board quests, mail-triggered quests, and custom completion logic on top of the [More Quests Framework](../MoreQuestsFramework/README.md).

> Heavy work in progress. Phase 1 quests are implemented and the framework split (Phase 2) is in place. The remaining 60+ quest concepts described in [this google sheet](https://docs.google.com/spreadsheets/d/13HQDEAYTcmi-x9Hp7R6lq2STRFtO5rUA3JxVOgitoDM/edit?usp=sharing) will be added across later phases.

## What this mod does

This mod is **content only**. It registers eleven `IQuestDefinition` implementations and three custom `Quest` subclasses with the framework at `GameLaunched`. The framework owns the billboard, the daily generation pipeline, the four vanilla wrappers, the reward path, and all engine-level config; this mod just supplies quests.

If the framework isn't installed, this mod logs an error and registers nothing.

## Status

**Implemented (Phase 1)**

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

Three of this mod's quests use bespoke `Quest` subclasses (registered with SpaceCore through the framework's API so saves round-trip cleanly):

- **`AnySlimeQuest`** — backs Basic Slime Clearing. Counts any slime kill, not just one species.
- **`CollectAndReportQuest`** — backs Beach Cleanup, Seasonal Foraging, etc. Player gathers items in the world, then reports to the giver to consume the stack and turn it in.
- **`CheckOnGeorgeQuest`** — backs Check on George. Multi-step quest: gift George, chat with him and finally report back to Evelyn.

For ItemDelivery and Fishing quests this mod uses the framework's `MoreQuestsItemDeliveryQuest` / `MoreQuestsFishingQuest` subclasses (constructed by the framework's `QuestFactory`). Both subclasses make rewards explicit (no hidden vanilla friendship bumps or prize tickets) and route every completion path through the same `RewardApplier`.

## Project layout

```
MoreQuests/
  ModEntry.cs            // hooks GameLaunched, fetches the framework API,
                         // registers all 11 quest definitions + 3 custom Quest types
  ModConfig.cs           // per-content tunables only
  GmcmRegistration.cs    // GMCM page for the content-mod toggles
  Quests/                // one file per IQuestDefinition implementation,
                         // plus the three custom Quest subclasses
  i18n/                  // quest titles/descriptions/objectives + content config strings
  manifest.json
```

## Adding a new quest

1. Drop a class under `Quests/` implementing `MoreQuestsFramework.IQuestDefinition`.
2. Pick a `PostingKind`: `DailyBoard` (board slot), `Mail` (auto-mailed when available), `SpecialOrder` (later), or `NpcDialogue` (later).
3. Register the class in [ModEntry.cs](ModEntry.cs) `OnGameLaunched` via `fw.RegisterQuest(new YourQuest())`.
4. Add `quest.<category>.<id>.title|description|objective|targetMessage` keys to `i18n/default.json` and look them up via `MoreQuests.ModEntry.I18n.Get(...)`.
5. If the quest is gated on a mod, check via `ctx.Helper.ModRegistry.IsLoaded(MoreQuestsFramework.ModCompat.<id>)` (or one of the `ModCompat.HasXxx` helpers) inside `IsAvailable`.
6. If the quest needs a custom `Quest` subclass, mark it `[XmlType("Mods_RafiaBee_MoreQuests_<Name>")]` and register it via `fw.RegisterCustomQuestType(typeof(YourQuest))` so SpaceCore can serialize it.

## Notes

- Framework engine code (registry, pipeline, billboard, condition evaluator, reward applier, item resolver, NPC dispatch, four vanilla wrappers) lives in the [framework mod](../MoreQuestsFramework/README.md) — this repo is content only.
- Mail prefix for in-world quest letters is `RafiaBee.MoreQuestsFramework.` (the framework owns the routing).
