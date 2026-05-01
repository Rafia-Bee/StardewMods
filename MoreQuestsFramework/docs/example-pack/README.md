# Example Content Pack

Demonstrates a fully-declarative `quests.json` content pack for the More Quests Framework.

## What it ships

- `manifest.json` — declares the pack as `ContentPackFor: RafiaBee.MoreQuestsFramework`. The framework auto-loads any pack with this declaration.
- `quests.json` — two purely-declarative daily-board quests. No C# generators required.
- `i18n/default.json` — translation strings; `quests.json` references them via `{i18n:key}` tokens.

## How it loads

At `GameLaunched`, the framework iterates `Helper.ContentPacks.GetOwned()` and calls `QuestPackLoader.LoadContentPack(pack)` on each. Token resolution uses the pack's own `Translation` helper, so each pack's keys are scoped to its own `i18n/` folder.

## How to try it

1. Copy this folder to your `Stardew Valley/Mods/` directory.
2. Launch the game. The two quests appear in the daily-board pool subject to their `Available` conditions.

## Schema reference

A `quests.json` document is:

```jsonc
{
    "Schema": "1.0",
    "Quests": [ /* QuestDef */ ]
}
```

Each `QuestDef` must set either `Generator` (a name registered in C# via `RegisterGenerator`) or `Objective` (a fully-declarative single-step quest). Trigger metadata, conditions, and rewards work in both modes. See `plan.md §5` for the full schema.

Reward kinds supported in Phase 4: `Money`, `Friendship`, `Object`, `Recipe`, `Mail`.
