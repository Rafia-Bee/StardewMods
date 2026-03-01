# Bulk Derby Rewards

Exchange all your Golden Tags at once during the Trout Derby and collect prizes from a chest-style menu — just like the SquidFest reward system.

## What It Does

During the **Trout Derby** (Summer 20–21), the vanilla game forces you to exchange Golden Tags one by one at the booth. This mod replaces that with a single bulk exchange:

1. Interact with the derby booth as usual.
2. The mod detects you have multiple Golden Tags and asks: *"Exchange all X Golden Tags for prizes?"*
3. All prizes are generated and placed into a chest-style grab menu.
4. Take the items at your own pace. Any items left behind when you close the menu are dropped on the ground.

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| Enabled | `true` | Enable or disable the mod entirely. |
| Always Bulk | `false` | When enabled, even a single Golden Tag uses the chest-style reward menu instead of the vanilla exchange. |

Configuration is available through [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) or by editing `config.json`.

## Install

1. Install [SMAPI](https://smapi.io/).
2. Drop the `BulkDerbyRewards` folder into your `Mods` directory.
3. Launch the game.

## Compatibility

- **Stardew Valley** 1.6+
- **SMAPI** 4.1.0+
- Works in single-player and multiplayer (each player needs the mod installed).

## Known Limitations

- The prize table is replicated from the [wiki](https://stardewvalleywiki.com/Trout_Derby#Prizes). If a future game update changes the prize list or item IDs, the mod may need an update.
- The mod tracks its own prize cycle index in `modData`, separate from the game's internal tracking. Use the mod consistently for the best experience.

## First-Run Debugging

On the first Trout Derby with the mod installed, check the SMAPI console for `[BulkDerby]` messages. These log every `answerDialogueAction` call during the derby, which helps verify the correct dialogue key is being intercepted. If the mod doesn't trigger, look for the actual key in the log and [open an issue](https://github.com/Rafia-Bee/StardewMods/issues).
