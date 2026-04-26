# More Quests

A SMAPI mod for Stardew Valley that overhauls the quest experience. Adds a much wider catalogue of quests across multiple delivery channels, with progressive difficulty, varied rewards, and modded-content awareness.

> Heavy work in progress. The architecture is in place and a Phase 1 batch of quests is implemented; the remaining 60+ quest concepts described in [this google sheet](https://docs.google.com/spreadsheets/d/13HQDEAYTcmi-x9Hp7R6lq2STRFtO5rUA3JxVOgitoDM/edit?usp=sharing) will be added across later phases.

## Delivery channels

Each quest declares one of four `PostingKind` values describing how it reaches the player:

- **DailyBoard** — quests that appear on the help-wanted board in town. Multiple per day, short windows.
- **SpecialOrder** — quests that appear on the special-orders board (the second tab). Multi-objective, longer windows.
- **Mail** — quests delivered as a mail letter. Used for first-time triggers (first coop, first egg, etc.) and for ongoing/periodic quests like the hay supply run.
- **NpcDialogue** — quests that surface when the farmer next speaks with the quest giver. *(Planned for a later phase.)*

`SpecialOrder` and `NpcDialogue` channels are stubbed in `QuestPoster` and will be filled in alongside the matching quest rows from the table.

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
| Seasonal | Planting Drive (spring) | DailyBoard |

**Planned (later phases)** — animal trigger letters, festival pre-quests, special-orders quests, NPC-dialogue quests, mod-gated quests for RSV / East Scarp / Visit Mount Vapius / SVE, custom-asset quests (Protein Bar, Animal Paintings, Void Chicken Statue, Egg Basket, Legendary Fish Displays), consequence dispatch via `Data/NPCGiftTastes`. [this google sheet](https://docs.google.com/spreadsheets/d/13HQDEAYTcmi-x9Hp7R6lq2STRFtO5rUA3JxVOgitoDM/edit?usp=sharing) and `docs/DECISION_LOG.md` has the full plan.

## Dependencies

Optional integrations (auto-detected at runtime):

- **Generic Mod Config Menu** (`spacechase0.GenericModConfigMenu`) for in-game configuration.
- **Ridgeside Village**, **East Scarp**, **Visit Mount Vapius**, **Stardew Valley Expanded** — adds the modded NPCs to the appropriate dispatch pools (saloon chefs, ecology-minded, conservation guides, etc.).
- **Livestock Follows You** by me lol — required for a future quest batch (Marnie's Cow Offer, Marnie's Livestock Show, Leah's Farm Painting) (shameless plug)
- **Si's Extra Crafting Materials** (Nexus 25467) — required for the future Winter Star Wrapping Paper quest.

There are no required dependencies; the mod builds, generates, and posts quests entirely against vanilla SMAPI + Stardew Valley.

## How it works

Every morning at `DayStarted`, `QuestPipeline` samples from `BoardQuestRegistry` to produce two batches:

1. `GenerateDailyPostings()` — fills up to `QuestsPerDay` slots from definitions whose `Kind == PostingKind.DailyBoard`.
2. `GenerateTriggeredMail()` — emits any `PostingKind.Mail` definitions whose `IsAvailable` returns true.

Both batches are handed to `QuestPoster`, which builds the appropriate vanilla `Quest` subclass (`ItemDeliveryQuest`, `FishingQuest`, `SlayMonsterQuest`), sets `daysLeft` from the posting's deadline, and adds it to `Game1.player.questLog`. A flavour mail letter is sent at the same time so the quest reads as a real in-world prompt rather than a silent journal entry.

Per-quest definitions live under `Framework/Quests/` and implement the `IQuestDefinition` interface:

```csharp
internal interface IQuestDefinition
{
    string Id { get; }
    QuestCategory Category { get; }
    PostingKind Kind { get; }
    bool IsAvailable(QuestContext ctx);
    QuestPosting? Build(QuestContext ctx);
}
```

The pipeline tries to pick from non-recently-used definitions and avoids picking two daily-board quests of the same category in one day.

## Configuration

All numeric tunables described in `docs/variables.csv` are exposed via Generic Mod Config Menu and mirrored on `ModConfig`. Settings are grouped:

- **Quest Board** — quests per day.
- **Toggles** — difficulty scaling, consequences, modded items / NPCs, festival quests, animal quests, secret-gift hint.
- **Friendship reward sizes** — `FriendshipBasic`, `FriendshipMid`, `FriendshipIntermediate`, `FriendshipLarge`, `FriendshipMultiSmall`, `FriendshipMultiHeart`.
- **Gold reward bases** — beginner / basic / intermediate / advanced / expert.
- **Reward multipliers** — `RewardMultiplierBelowSell`, `RewardMultiplierAboveSell`, `RewardMultiplierFishPremium`.
- **Shop discounts** — festival shop and seed shop, percent and duration.
- **Deadlines** — short / medium / long / extended (in-game days).
- **Quantity tunables** — fish hauls, festival fish, massive crop, hay base, Skull Cavern max.

## Project layout

```
MoreQuests/
  ModEntry.cs                 // wires events, holds Config singleton
  ModConfig.cs                // all variables.csv values
  IGenericModConfigMenuApi.cs // GMCM interface
  Framework/
    ModCompat.cs              // mod IDs + IsLoaded helpers
    Difficulty.cs             // tier + deadline + skill mapping
    ItemResolver.cs           // resolves items from Data/Crops, Data/Fish, Data/Objects, Data/CookingRecipes
    NpcDispatch.cs            // role-based quest-giver picker (mod aware)
    AntiRepetition.cs         // recent-item / NPC / definition history
    QuestPosting.cs           // shared DTO + PostingKind enum + BoardQuestType enum
    IQuestDefinition.cs       // interface for individual quest definitions
    QuestContext.cs           // helpers passed to each definition
    BoardQuestRegistry.cs     // list of all registered IQuestDefinition implementations
    QuestPipeline.cs          // daily + triggered generation orchestrator
    QuestPoster.cs            // builds vanilla Quest instances and sends mail letters
    GmcmRegistration.cs       // GMCM page setup
    Quests/                   // one file per IQuestDefinition implementation
```

## Adding a new quest

1. Drop a class under `Framework/Quests/` implementing `IQuestDefinition`.
2. Pick a `PostingKind`: `DailyBoard` (board slot), `Mail` (auto-mailed when available), `SpecialOrder` (later), or `NpcDialogue` (later).
3. Register the class in `BoardQuestRegistry.All`.
4. Add `quest.<category>.<id>.title|description|objective|targetMessage` keys to `i18n/default.json`.
5. If the quest is gated on a mod, check via `ctx.Helper.ModRegistry.IsLoaded(ModCompat.<id>)` (or one of the `ModCompat.HasXxx` helpers) inside `IsAvailable`.

## Notes

- `NpcDispatch` includes vanilla and modded NPCs; modded entries are filtered by `Game1.getCharacterFromName != null` so dispatch falls back gracefully when a referenced NPC mod is missing.
- `ItemResolver` reads `Data/Crops`, `Data/Fish`, `Data/Objects`, `Data/CookingRecipes` directly so modded entries surface automatically without further wiring.
