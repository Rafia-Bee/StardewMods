# Livestock Follows You

When you buy an animal, it follows you around instead of teleporting straight to its building. Once you reach the farm and enter the barn or coop, the animal is registered inside and goes back to normal. This only happens on the walk home after purchase -- your existing animals won't start following you around.

## Features

- **Follow the leader**: Purchased animals trail behind you as you walk, forming a chain if you buy more than one.
- **NPC reactions**: Villagers you pass will comment on your animals with speech bubbles -- 55 unique lines ranging from sweet to snarky.
- **Outdoor stall support**: Works with outdoor animal vendors too (e.g. Moira's Glimsap Fair stall from Visit Mount Vapius). Animals appear right away.
- **Livestock Bazaar compatible**: Hooks into `AnimalHouse.adoptAnimal`, so any mod that routes through the standard adoption path is supported.
- **Farm arrival**: When you reach the farm, animals spread out in front of their barn/coop and wait for you to enter the building.
- **Auto-delivery**: Animals are delivered to their buildings when you enter them, or automatically at a configurable curfew time (default 8:00 PM).
- **Rubber-banding**: Animals that fall too far behind teleport closer so they don't get lost.
- **Obstacle handling**: If an animal gets stuck on a fence, tree, or rock, it teleports past the obstacle after a short delay.

## Configuration

All options are available through [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) or by editing `config.json`.

| Setting | Default | Description |
|---|---|---|
| Enabled | true | Toggle the mod on/off. When off, animals teleport to barns as usual. |
| Follow Speed | 1.0 | Speed multiplier for catch-up when animals fall behind. |
| Catch-up Distance | 12 | Tile distance before an animal rubber-bands to the player. |
| Auto-deliver Time | 2000 (8 PM) | Game time when undelivered animals are sent home automatically. |
| Animal Sounds | true | Whether animals make sounds while following. |
| Sound Interval | 15s | Seconds between animal sounds. |
| Show Notifications | true | HUD messages for follow/delivery events. |
| NPC Reactions | true | Nearby villagers react with speech bubbles when you escort animals. |
| Debug Logging | true | Log debug messages to the SMAPI console. |

## Requirements

- [SMAPI](https://smapi.io/) 4.1.0+
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional, for in-game config)

## Compatibility

- Works with Livestock Bazaar, Visit Mount Vapius, and other mods that use `AnimalHouse.adoptAnimal`.
- Multiplayer: works for both host and farmhands.

## Install

1. Install SMAPI.
2. Drop the `LivestockFollowsYou` folder into your `Mods` directory.
3. Run the game through SMAPI.
