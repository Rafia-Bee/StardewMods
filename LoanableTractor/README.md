# Loanable Tractor

A [Stardew Valley](https://www.stardewvalley.net/) mod that lets you rent a tractor from Joja Corp. for a daily fee, so you can use heavy farming equipment in the early game before you can afford to build the tractor garage.

![Stardew Valley](https://img.shields.io/badge/Stardew%20Valley-1.6%2B-green)
![SMAPI](https://img.shields.io/badge/SMAPI-4.1%2B-blue)

## What does this mod do?

Ever wished you could use the tractor early in your playthrough, but couldn't scrape together the gold and materials for Robin's garage? Joja Corp. has you covered with their new **AgriLease™ Tractor Rental Program**.

After installing the mod, you'll receive a letter from Joja Corp. introducing their tractor loan service. Once you've read the letter, just walk up to your mailbox when it's empty and you'll see a new **"Loan Tractor"** option. Select it, pay the daily fee, and a tractor will appear right next to you, ready to use for the rest of the day. At the end of the day, Joja collects their tractor and you're free to rent again tomorrow.

The loaned tractor works exactly like the regular Tractor Mod tractor. You can till, water, harvest, clear rocks, chop trees, and everything else the tractor normally does.

## Requirements

You'll need these mods installed for Loanable Tractor to work:

| Mod | Why it's needed |
|---|---|
| [SMAPI](https://smapi.io/) (4.1+) | The mod framework that makes everything tick |
| [Tractor Mod](https://www.nexusmods.com/stardewvalley/mods/1401) (4.15+) | Provides the actual tractor and all its functionality |

Optional but recommended:

| Mod | Why you'd want it |
|---|---|
| [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) | Edit all the mod's settings from an in game menu instead of editing config files |
| [Mail Services Mod](https://www.nexusmods.com/stardewvalley/mods/7842) | If you have this installed, the "Loan Tractor" option coexists with gift-shipping options in the mailbox |

## Install

1. Install SMAPI and Tractor Mod (see requirements above).
2. Optionally install Generic Mod Config Menu for in-game settings.
3. Download the latest release of Loanable Tractor.
4. Unzip it into your `Stardew Valley/Mods` folder.
5. Run the game through SMAPI.

That's it! You can install the mod at any point during a playthrough.

## How to use

1. **Check your mail.** After installing, you'll get a letter from Joja Corp. explaining their tractor rental service. Give it a read.
2. **Visit your mailbox.** Once you've read the letter, click on your mailbox when there are no unread letters. You'll see a "Loan Tractor" option with the rental cost displayed.
3. **Select "Loan Tractor."** The fee gets deducted from your wallet and a tractor appears next to you, ready to ride.
4. **Farm away!** Use the tractor just like you normally would. It works with all the tools and attachments from Tractor Mod.
5. **Go to sleep.** At the end of the day, Joja collects their tractor automatically. You can rent again the next day.

## Configuration

If you have [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) installed, you can tweak all of these from the in game options screen. Otherwise, edit the `config.json` file in the mod folder.

| Setting | Default | What it does |
|---|---|---|
| Loan Cost Per Day | 500g | How much it costs to rent the tractor each day |
| Max Loan Days | 1 | The longest you can rent the tractor in one go |
| Default Loan Days | 1 | Default number of days selected when loaning |
| Charge Upfront | Yes | When renting for multiple days, pay the full cost upfront instead of daily |
| Allow Loan With Garage | No | Whether the rental option still shows up if you already own a tractor garage |
| Show Mail Daily | Yes | Whether the Joja mail reappears each day or only shows up once |
| Dismiss Service | No | Turn this on if you want to stop receiving the Joja mail entirely |
| Require Minimum Gold | Yes | Only shows the loan option if you have enough gold to cover the fee |
| Late Return Penalty | Yes | Charges a bonus fee if you pass out at 2am while the tractor is still out |
| Penalty Amount | 250g | How much the late return penalty costs |
| Enable Speed Reduction | No | Makes the loaned tractor slightly slower than an owned tractor |

## Compatibility

Works with Stardew Valley 1.6+ on Windows, macOS, and Linux in both single player and multiplayer.

In multiplayer, the host needs to have the mod installed along with all its dependencies. Farmhands can use the loan service as long as the host has everything set up.

## FAQ

**Can I install this mid playthrough?**
Yes! Install it whenever you want. The introductory letter will show up in your mailbox the next morning.

**Does the loaned tractor do everything the regular tractor does?**
Yep. It's the same tractor with the same functionality. The only difference is that it disappears at the end of the day.

**What happens if I don't have enough money?**
The "Loan Tractor" option won't appear in the mailbox menu unless you have enough gold to cover the fee.

**I completed the Community Center. Does that change anything?**
Not currently. In a future update, the loan service letter may come from Pierre instead of Joja Corp. For now, the service is always Joja-branded.

**Does this work with custom tractor textures?**
The loaned tractor uses whatever textures you have set up for Tractor Mod, so yes, custom skins work just fine.

## Credits

This mod wouldn't exist without the amazing work of:

* **Pathoschild** for [Tractor Mod](https://www.nexusmods.com/stardewvalley/mods/1401) and [SMAPI](https://smapi.io/)
* **Digus** for [Mail Framework Mod](https://www.nexusmods.com/stardewvalley/mods/1536) and [Mail Services Mod](https://www.nexusmods.com/stardewvalley/mods/7842) (whose mailbox interaction pattern this mod follows)
* **spacechase0** for [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)

## License

This mod is released under the [MIT License](LICENSE).
