# Bigger Auto-Grabber

If you use the [Deluxe Grabber Redux 1.6](https://www.nexusmods.com/stardewvalley/mods/20799?tab=description) mod, you probably faced the issue of the auto-grabber overflowing and having to press the global grabber button multiple times to gather all the items. This mod expands the auto-grabber's internal chest beyond the vanilla 36-slot limit, so the deluxe grabber can grab everything in one go.

## Features

- Configurable auto-grabber capacity (default: 36, vanilla)
- Works with any mod that uses auto-grabbers (e.g., Deluxe Grabber Redux)
- Capacity changes are applied to all existing and newly placed auto-grabbers
- Compatible with Generic Mod Config Menu for in-game configuration

## Configuration

| Setting | Default | Description |
|---|---|---|
| Capacity | 36 | Number of item slots in each auto-grabber. Vanilla is 36. Range: 36–288 in the config menu (edit `config.json` for custom values). |

## Install

1. Install [SMAPI](https://smapi.io/)
2. Drop the `BiggerAutoGrabber` folder into your `Mods` directory
3. Run the game

## How It Works

The mod uses a Harmony postfix on `Chest.GetActualCapacity()` to override the capacity of chests inside auto-grabbers. It stamps each auto-grabber's chest with a `modData` key on save load, day start, and whenever a new auto-grabber is placed.

## Notes

- If you reduce the capacity below the number of occupied slots, items in excess slots remain in the save but won't be visible in the UI until capacity is increased again. No items are ever deleted.
- If you uninstall the mod, auto-grabbers revert to 36 slots. Items in extra slots persist in the save and become accessible again if the mod is reinstalled.
