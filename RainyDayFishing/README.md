# Rainy Day Fishing

A Stardew Valley mod that shows rain-exclusive fish icons on the right side of the HUD when it's raining.

## Features

- Displays **fish sprite icons** on the right side of the screen on rainy days
- **Hover over any icon** to see the fish name, spawn locations (only places you've visited), and catchable time windows
- **Uncaught fish** are marked with "(Uncaught)" in the tooltip — updates in real time when you catch one
- Only shows fish that **require rain** to spawn — not every fish that happens to be available
- Shows all rain-exclusive fish available **at any point during the day**, not just the current time
- Automatically includes fish from **modded locations** (Stardew Valley Expanded, Visit Mount Vapius, East Scarp, etc.)
- Updates dynamically when the day starts or you visit a new location
- Fully configurable via [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (optional)

## Requirements

- [SMAPI](https://smapi.io) 4.1.0+
- [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) *(optional, for in-game settings)*

## Configuration

All settings can be changed in-game via Generic Mod Config Menu, or by editing `config.json`:

| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Show/hide the fish icon overlay entirely |
| `HudX` | `-51` | Horizontal position. Negative values offset from the right edge |
| `HudY` | `300` | Vertical position (pixels from top of screen) |
| `HorizontalLayout` | `false` | Arrange icons in a horizontal row instead of a vertical column |
| `MaxLocations` | `4` | Max spawn locations shown per tooltip (0 = show all) |

## Installation

1. Install SMAPI
2. Drop the `RainyDayFishing` folder into your `Stardew Valley/Mods` directory
3. Launch the game — fish icons appear automatically on rainy days

## How It Works

The mod reads fish spawn data from `Data/Locations` at runtime, so it works with any mod that adds fish through the standard game data system. A fish is considered "rain-exclusive" if:

- Its `Data/Fish` entry has weather set to `"rainy"`, or
- Its spawn condition requires rain (e.g., `LOCATION_WEATHER Here Rain`, `!WEATHER Here Sun`)

Fish that can be caught in any weather (Sunfish, Bream, etc.) are excluded.

Hovering over a fish icon shows the fish name, which visited locations it spawns in, and the time window(s) it can be caught (e.g., "6pm – 2am").
