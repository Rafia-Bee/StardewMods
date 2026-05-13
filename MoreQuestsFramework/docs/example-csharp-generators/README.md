# Example: C# Mod with Bundled JSON + Generators

Pattern B from the framework README. A C# SMAPI mod that:

1. Subscribes to the framework's `RegistrationOpen` event.
2. Registers one or more named C# generator functions.
3. Calls `LoadQuestsFromMod` to read its `assets/quests.json`. Each JSON entry references a generator by name.

Use this pattern when you want declarative metadata (trigger, weight, cooldown, conditions) handled by JSON, plus runtime randomization done in C#.

## What it ships

- `manifest.json`, declares the mod as a SMAPI mod (not a content pack) with `RafiaBee.MoreQuestsFramework` as a required dependency.
- `ModEntry.cs`, the SMAPI entry point. Fetches the framework API on `GameLaunched`, subscribes to `RegistrationOpen`, registers the generator, and loads the JSON.
- `assets/quests.json`, one quest entry that points at the named generator.
- `i18n/default.json`, the strings the generator passes through the translation helper.

This folder doesn't include a `.csproj`. Drop the files into your own SMAPI mod project, point the project at the framework's DLL (`<ProjectReference>` with `<Private>false</Private>` and `<ExcludeAssets>runtime</ExcludeAssets>` is the recommended setup), and you're ready!

## What the example quest does

`RandomCropDelivery` is a daily-board quest where a random met villager asks for a stack of one in-season crop. The JSON sets the trigger source, weight, cooldown, and a `Season` gate. The C# generator picks the giver, picks the crop, scales the quantity off Farming, and computes the gold reward from the crop's sell price.

| Field | Lives in |
| --- | --- |
| Trigger source, weight, cooldown, `Available` conditions | `quests.json` |
| Giver, item, quantity, gold, description text | `ModEntry.BuildRandomCropDelivery` |

## Why split the work this way

- JSON is the only place the framework can read to expose **per-quest GMCM weights**. Anything you want a config slider for has to live in JSON.
- Generators get the live `QuestContext` (current season, met villagers, cached crop pool, player stats), so any logic that depends on save state is much easier to write in C#.

## See also

- [`../example-pack/`](../example-pack/), pattern A. Pure JSON content pack, no C# at all.
- [`../example-csharp-iquestdef/`](../example-csharp-iquestdef/), pattern C. Pure C# with no JSON.
- [`../../README.md`](../../README.md), the framework README with the full API reference.
- [`../../../MoreQuests/`](../../../MoreQuests/), the production content mod that uses pattern B for the majority of its quests.
