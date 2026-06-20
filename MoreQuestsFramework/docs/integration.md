# Integration notes for other mods

The framework's [README](../README.md) covers what the framework does and how to register quests against it. This page collects the smaller integration caveats: how to declare the dependency, how to share types without copying the DLL, and how the framework interacts with mods that read `Game1.questOfTheDay` or deliver items on the player's behalf.

## Notes for consumer mods

- Declare `RafiaBee.MoreQuestsFramework` as a `Dependencies` entry with `IsRequired: true` so your mod loads after the framework.
- For shared types (`IQuestDefinition`, `QuestPosting`, `QuestContext`), use a `<ProjectReference>` with `<Private>false</Private>` and `<ExcludeAssets>runtime</ExcludeAssets>` so the framework DLL isn't copied into the consumer mod's deploy folder. SMAPI's `AssemblyResolve` finds the engine types in the framework's loaded assembly at runtime.
- `Game1.questOfTheDay` is null on framework-board days. The billboard is driven by the currently-selected slot, and reads inside the vanilla `Billboard` constructor, draw, click and hover paths are rewritten to point at that slot. Anything else that reads `Game1.questOfTheDay` directly (third-party HUD overlays, quest trackers) will see null. If your mod needs the active board quest, ask the framework via `IMoreQuestsApi` rather than reading `Game1.questOfTheDay` directly.
- Counting what's still on the vanilla quest board (the help-wanted billboard by Pierre's): use `CountUnacceptedDailyBoardQuests()` for a quick count, or `GetDailyBoardSlots()` for the full per-slot snapshot (quest, definition id, owner). A slot drops off both the moment the player accepts it, so the count is exactly "how many are still waiting to be accepted today". This is the right call for hiding a "new quest" notification icon once the board is cleared. The list refreshes at day-start and on `RefreshOffers`, so re-query rather than caching. Note these cover the vanilla board only; for custom boards use `GetCustomBoardSlots(...)`.

## Notes for mods that deliver items on the player's behalf (Mail Services Mod, etc.)

The framework's gift-step quests (`Gift`, `GiftUniqueNpcs` in `AdventureQuest`, plus the vanilla `ItemDeliveryQuest`-style turn-ins) advance via `Quest.OnItemOfferedToNpc`. Mods that mail items on the player's behalf skip that hook because no in-person interaction happens, so steps that count gifted items don't tick.

If your mod delivers items to NPCs outside the in-person flow, call `quest.OnItemOfferedToNpc(npc, item, probe: false)` on each in-progress quest in `Game1.player.questLog` after the delivery succeeds. The framework's quest subclasses respond to the call, gift steps tick, friendship rewards land, and the consequence (if any) fires on completion. Probe-mode (`probe: true`) returns whether the quest accepts the item without consuming it, which mirrors vanilla's accept-check pattern.
