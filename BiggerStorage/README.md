# Bigger Storage

If you use the [Deluxe Grabber Redux 1.6](https://www.nexusmods.com/stardewvalley/mods/20799?tab=description) mod, you've probably run into the auto-grabber overflowing its 36-slot chest and having to hit the grab button over and over. This mod expands the auto-grabber's chest past the vanilla 36-slot limit so it grabs everything in one go.

Update in v2.0.0: now it makes your other storage bigger too!

You can set a different size for each kind of storage:

- Auto-grabbers (great with mods like Deluxe Grabber Redux that dump a lot of items at once)
- Chests: regular, big, stone, junimo, and modded chests
- The kitchen fridge and mini-fridges

## Features

- One default plus per-type sizes: set a single default for everything, or give any specific kind its own size, from 36 (vanilla) up to 516
- Modded chests too: chests added by other mods get their own row automatically
- Leave anything at 36 to keep that storage normal
- Safe to install and uninstall: no items are ever deleted. Removing the mod puts everything back to 36 slots, and items in the extra slots stay in your save and come back if you reinstall

## Configuration

Open the config menu (needs Generic Mod Config Menu) and you'll see:

- **Default size (everything else)** sets the size for anything that doesn't have its own row, including modded chests you haven't set yourself.
- **A row per storage type** you own: auto-grabber, the kitchen fridge, mini-fridge, regular chest, stone chest, big chest, junimo chest, and any modded chests. Set a row to give just that type its own size. Leave it matching the default and it keeps following the default.

New storage types appear as rows once you load a save that has them. If you uninstall a mod that added a chest, its row clears itself out next time you load. Every value runs from 36 up to 516 in the menu (36 is normal, so a row left at 36 changes nothing), or type any number by editing `config.json`.

## Install

1. Install [SMAPI](https://smapi.io/)
2. Drop the `BiggerAutoGrabber` folder into your `Mods` directory
3. Run the game

## Notes

- The storage window shows up to 72 slots (6 rows) at a time. If you set a size higher, you can scroll through the rest with the mouse wheel or the up/down arrows on the right side of the grid. On a controller, just push past the top or bottom row and the grid scrolls.
- If you set a size below what's already stored, those extra items stay safe in your save. They just hide until you make it bigger again.
- **Will clash with any mod that expands vanilla chest capacity.**
