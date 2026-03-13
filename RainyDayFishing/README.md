# Rainy Day Fishing

A [To-Dew](https://www.nexusmods.com/stardewvalley/mods/7409) overlay addon for Stardew Valley that shows which rain-exclusive fish you can catch today.

## Features

- Displays a **"Rainy Day Fish"** section in the To-Dew overlay on rainy days
- Only lists fish that **require rain** to spawn — not every fish that happens to be available
- Only shows fish from **locations you've already visited**
- Shows all rain-exclusive fish available **at any point during the day**, not just the current time
- Automatically includes fish from **modded locations** (Stardew Valley Expanded, Visit Mount Vapius, East Scarp, etc.)
- Updates dynamically when the day starts, time changes, or you visit a new location

## Requirements

- [SMAPI](https://smapi.io) 4.1.0+
- [To-Dew](https://www.nexusmods.com/stardewvalley/mods/7409) 2.0.0+

## Installation

1. Install SMAPI and To-Dew
2. Drop the `RainyDayFishing` folder into your `Stardew Valley/Mods` directory
3. Launch the game — the overlay section appears automatically on rainy days

## How It Works

The mod reads fish spawn data from `Data/Locations` at runtime, so it works with any mod that adds fish through the standard game data system. A fish is considered "rain-exclusive" if:

- Its `Data/Fish` entry has weather set to `"rainy"`, or
- Its spawn condition requires rain (e.g., `LOCATION_WEATHER Here Rain`, `!WEATHER Here Sun`)

Fish that can be caught in any weather (Sunfish, Bream, etc.) are excluded.
