# More Quests

A SMAPI content mod for Stardew Valley that adds a big batch of new daily-board, mail, and festival quests on top of the [More Quests Framework](../MoreQuestsFramework/README.md).

> Still a work in progress. Lots of quests are in already, with more on the way.

## Dependencies

**Required**

- **More Quests Framework** (`RafiaBee.MoreQuestsFramework`) **2.0.0 or newer**, bundled in this repo at [../MoreQuestsFramework/](../MoreQuestsFramework/). Without it this mod logs an error and does nothing. Older framework versions are incompatible, please update both at the same time.

**Optional (auto-detected at runtime)**

- **[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)**, for an in-game config page with the per-quest toggles.
- **[Ridgeside Village](https://www.nexusmods.com/stardewvalley/mods/7286)**, **[East Scarp](https://www.nexusmods.com/stardewvalley/mods/5787)**, **[Visit Mount Vapius](https://www.nexusmods.com/stardewvalley/mods/9600)**, **[Stardew Valley Expanded](https://www.nexusmods.com/stardewvalley/mods/3753)**, **[Eli and Dylan - Custom NPCs for East Scarp](https://www.nexusmods.com/stardewvalley/mods/13883)**, **[Arumi the Actress](https://www.nexusmods.com/stardewvalley/mods/44286)**, **[Lurking in the Dark - NPC Sen (East Scarp)](https://www.nexusmods.com/stardewvalley/mods/10770)**, and **[The Bear Family - East Scarp](https://www.nexusmods.com/stardewvalley/mods/16197)** add modded NPCs into the quest-giver and reaction pools (saloon chefs, ecology folks, conservation guides, fishermen, underground-NPC reactions etc.) so the quests pick from a wider roster.
- **[Livestock Follows You](https://www.nexusmods.com/stardewvalley/mods/44349)**, needed for Marnie's Cow Offer, Marnie's Livestock Show, and Leah's Farm Painting.
- **[Si's Extra Crafting Materials](https://www.nexusmods.com/stardewvalley/mods/25467)**, needed for the Winter Star Wrapping Paper quest.
- **[Catch of the Day](https://www.nexusmods.com/stardewvalley/mods/43668)**, pairs nicely with fishing quests since it lets you track time/season/weather-specific fish.
- **[Love of Cooking](https://www.nexusmods.com/stardewvalley/mods/14387)** or **[CookingSkill Redux (YACS)](https://www.nexusmods.com/stardewvalley/mods/30457)**, either one lets the cooking quests scale their ingredient counts off your cooking level. Without one of these the quests still work, they just fall back to a flat baseline.

## Configuration

`Mods/MoreQuests/config.json` holds the per-quest content settings: animal and festival toggles, the Adventurer's Guild board on/off switch, shop discount sizes, fish haul quantities, Skull Cavern max floor, secret-gift hint toggle, Leah painting frame style, and so on. Engine-level stuff (how many quests per day, weights, deadlines, reward sizes) lives in the [framework's config](../MoreQuestsFramework/README.md#configuration). Both pages show up in GMCM.

There's an **Advanced > Debug logging** toggle at the bottom of each page. Leave it off for normal play. Flip it on if you hit a bug and want to share a SMAPI log; otherwise it just adds noise.

The **Adventurer's Guild board** toggle (on by default) decides whether the mining and monster quests get their own board at the mine entrance, or fold back into the regular help-wanted board. When it's on, the help-wanted board only shows Bar Delivery from the mining category, and the deep dives, slime clearing, monster hunts, monster parts, and rare material requests land on the guild board instead. When it's off, every guild-tagged quest goes onto the help-wanted board so you can still reach the content. Per-quest weights still let you disable individual quests on top of this.

## Quests

Cooldowns are tunable in the framework config. The defaults are: **Short = 2 days**, **Medium = 7 days**, **Long = 14 days**. A quest just says "Short", "Medium", or "Long" unless it uses a fixed number.

Quests are grouped by category below and listed alphabetically within each group.

### Animal

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Alex's Protein Shakes | Every 14 days (mail) | Alex | Deliver eggs scaled to your chicken count | Friendship + Energy Tonic, Muscle Remedy, or Life Elixir | Needs to have met Alex and have at least one chicken |
| Gunther's Dinosaur Study | First Dinosaur Egg held (mail) | Gunther | Deliver one Dinosaur Egg | Gold + a Dinosaur Egg back, bumped one quality tier up | One-time |
| Hay Supply Run | Mail | Marnie | Deliver hay scaled to your animal count | Animal-shop discount window (when the discount is on in config) | Cooldown 28 days, needs at least 4 animals |
| Krobus's Void Note | First Void Egg held (mail) | Krobus | Deliver one Void Egg to Krobus | Friendship + Book of the Void | One-time, needs Krobus around at 1+ hearts |
| Leah's Farm Painting | Every 21 days (mail) | Leah | Visit Leah's house with an animal following you | A 2x2 painting of a random farm animal, frame style picked in GMCM, mailed the next morning + Friendship | Needs Livestock Follows You, single-player, and 2+ hearts with Leah |
| Marnie's Chicken Offer | Day after building a Coop (mail) | Marnie | Bring Mixed Seeds | A free White Chicken at Marnie's shop (or 800g if you don't visit in 14 days) + Friendship. With Livestock Bazaar, picking any chicken variant from her shop counts. | One-time |
| Marnie's Cow Offer | Day after building a Barn (mail) | Marnie | Bring a Grazing Bell (with Livestock Follows You) or a Milk Pail (without) | A free White Cow at Marnie's shop (or 1500g if you don't visit in 14 days) + Friendship. With Livestock Bazaar, picking any cow variant counts. | One-time |
| Marnie's Egg Request | After your first egg is laid (mail) | Marnie | Ship 10 of any edible egg (vanilla and modded) through the bin | Gold + Mayonnaise Machine recipe + Friendship | One-time |
| Marnie's Livestock Show | After your Deluxe Barn is built with 2+ animals (mail) | Marnie | Walk into Town with 2+ animals following you | Big friendship bump with Marnie | One-time, needs Livestock Follows You, single-player, and non-winter |
| Marnie's Milk Request | After your first milk is produced (mail) | Marnie | Ship 10 of any milk (cow, goat, modded buffalo / llama / etc.) through the bin | Gold + Cheese Press recipe + Friendship | One-time |
| Robin's Silo Offer | 2 days after your first Coop or Barn, if you don't have a Silo (mail) | Robin | Bring one of: 100 Stone, 10 Clay, or 5 Copper Bars | Free Silo on Robin's carpenter menu (the build cost gets zeroed once) + Friendship | One-time |

### Cooking

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Craving a Meal | Daily board | Any met villager | Bring them a dish they love, like, or feel neutral about | Friendship + a random dish they'd love | Cooldown Short |
| Dinner Party | Daily board | Any met villager | Deliver multiple loved, liked, or neutral dishes the host accepts | Gold (sell-price scaled) + Friendship | Cooldown Long |
| Saloon Grand Feast | Special Orders board | Gus, Pika, Rosa, or Celestine | Ship the combined ingredients across several complex recipes | Big gold reward + friendship to everyone who'd love the chosen dishes + per-dish taste reaction | Cooldown Long, one-week window |
| Weekly Special (Common) | Daily board | Gus, Pika, Rosa, or Celestine | Deliver in-season ingredients the chef picks from a visited-location pool | Gold + small friendship to everyone who'd love the matching dish + taste reaction | Cooldown Medium, needs Cooking 2 or Farming 3 |
| Weekly Special (Complex) | Daily board | Gus, Pika, Rosa, or Celestine | Same as Common, but the pool spans all four seasons and asks for more | Bigger gold + medium friendship + stronger taste reaction | Cooldown Medium, needs Cooking 4 or Farming 5 |

### Farming

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Basic Crop Delivery | Daily board | Any met villager | Deliver X of an in-season crop (any quality) | Gold based on the crop's sell price | Cooldown Medium |
| Caroline's Tea Garden | Daily board | Caroline | Bring an off-season flower or forage she loves or likes (no herbs) | Friendship + 2x count Tea Leaves | Cooldown Long, needs Foraging 3 and 1 heart with Caroline |
| Dehydrator Request | Mail when you learn the Dehydrator recipe | Any met adult villager | Ship a Farming-scaled number of dried mushrooms or dried fruit | 2 hearts of friendship | One-time, no deadline |
| Keg Request | Mail when you learn the Keg recipe | Any met adult villager | Ship a Farming-scaled number of wine, juice, mead, beer, or pale ale | 2 hearts of friendship | One-time, no deadline |
| Massive Harvest Request | Daily board | Morris or MorrisTod (SVE) | Ship a Farming-scaled stack of one in-season crop | Gold (below sell price, but the stack is big) + a taste reaction from villagers who love that crop | Cooldown Long, needs Farming 7 |
| Pierre's Stock-Up | Daily board | Pierre | Deliver bulk in-season crops (a few different kinds, qty scales with Farming) | Seed-shop discount on the matching seeds at Pierre's | Cooldown Long, needs Farming 4 |
| Premium Crop Order | Daily board | Any met villager | Deliver X Iridium-quality in-season crops | Gold + rare or ancient seeds (count scales with the haul) | Cooldown Long, needs Farming 7 |
| Preserves Jar Request | Mail when you learn the Preserves Jar recipe | Any met adult villager | Ship a Farming-scaled number of jams or pickles (any kind) | 2 hearts of friendship | One-time, no deadline |
| Quality Crop Delivery | Daily board | Any met villager | Deliver X Silver-or-better in-season crops | Gold (scales with sell price) + Friendship | Cooldown Short, needs Farming 2 |

### Festival

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Evelyn's Holiday Cookies | Winter 21 mail | Evelyn | Deliver Flour, Sugar, and any edible egg | Big friendship bump + 6 cookies | One-time per year |
| Festival Decor: East Scarp Spirit's Eve | Fall 24 | Rosa | Ship purple-dye items, slime, and stone | Friendship with the ES festival NPCs | One-time per year, 3-day deadline, needs ES / Eli and Dylan / Lurking in the Dark |
| Festival Decor: Egg Festival | Spring 10 | Lewis | Ship hay bales | Gold + a random Pierre Egg Festival stall item | One-time per year, 3-day deadline |
| Festival Decor: Fair | Fall 12 | Lewis | Ship Wood, any sign (Wood / Stone / Dark), and fall flowers (scales with Farming and Foraging) | A bump to your grange score OR extra Fair star tokens (pick which in GMCM) | One-time per year, 3-day deadline |
| Festival Decor: Luau | Summer 6 | Lewis | Ship Fiber, Hardwood, and Wood Lamp-posts (qty scales with Foraging) | Gold + a random Pierre Luau stall item | One-time per year, 4-day deadline |
| Festival Decor: Moonlight Jellies | Summer 21 | Lewis | Ship Torches and Wood (qty scales with Foraging) | Gold + a random Pierre Moonlight Jellies stall item | One-time per year, 6-day deadline |
| Festival Decor: Ridgeside Gathering | Fall 15 | Lenny | Ship Tub o' Flowers, Wood, and any tables (qty scales with Farming and Foraging) | Friendship with the RSV festival NPCs + Tub o' Flowers recipe on accept | One-time per year, needs RSV |
| Festival Decor: Spirit's Eve | Fall 22 | Wizard | Ship Pumpkin Seeds, Cloth, and Torches (qty scales with Farming) | Gold + a random rarecrow | One-time per year, 4-day deadline |
| Gus's Feast: Egg Festival | Spring 6 mail | Gus | Deliver one spring crop and one spring forage | A sample of a spring dish made from your ingredients | One-time per year |
| Gus's Feast: Fair | Fall 8 | Gus | Deliver a few different fall ingredients | A sample dish + bonus points on the Fair grange judging | One-time per year |
| Gus's Feast: Luau | Summer 8 | Gus | Deliver a few first-year-friendly summer or spring ingredients | A sample dish + a nudge up the Luau governor reaction tier | One-time per year |
| Gus's Feast: Winter Star | Winter 18 mail | Gus | Ship a few winter forageables | A small friendship bump to every villager you've met | One-time per year |
| Jellyfish Watch Prep | Summer 21 mail | Ecology-minded NPC (Demetrius, Maddie, Mr. Aguar, Dylan, etc.) | Deliver beach forage for the Moonlight Jellies notes | Friendship + a loved item from the giver | One-time per year, 6-day deadline |
| Lewis's Easter Eggs | Spring 8 | Lewis | Ship dye-color items in 2 to 5 random colors (qty scales with Farming) | All three egg basket variants (Cream, Pink, Rustic) | One-time per year, deadline Spring 12 |
| Merchant Unpacking | Winter 13 | Any met villager | Ship 1 to 3 seed types from the Night Market Magic Boat stock (variety and qty scale with Farming) | Friendship | One-time per year, deadline Winter 18 |
| Rainbow Platter (Trout Derby) | Summer 19 | Gus, Pika, Rosa, or Celestine | Catch Rainbow Trout during the Derby (qty scales with Fishing) | A recipe and a shop discount on the dish at the giver's saloon | One-time per year, Derby-spanning window |
| Secret Gift Hint | Talk to Lewis Winter 22 to 25 | Lewis | Give your secret-friend Winter Star recipient a loved or liked item at the festival | Friendship with the recipient | Opt-out via config, 4-day deadline. Only ticks on the festival's Secret Santa exchange |
| SquidFest Showcase | Winter 11 | Gus, Pika, Rosa, or Celestine | Catch Squid during the festival (qty scales with Fishing) | A recipe and a shop discount on the dish at the giver's saloon | One-time per year, festival-spanning window |
| Submarine Fuel | Winter 12 mail | Captain | Ship Battery Packs and Coal (qty scales with Mining, coal is 5x the batteries) | Pearl in the mail the next morning | One-time per year, deadline Winter 15 |
| Wizard's Ritual Materials | Fall 24 mail | M. Rasmodius | Ship Void Essence, Bat Wings, and Solar Essence | Book of Mysteries in the mail | One-time per year, deadline Fall 28 |
| Wrapping Paper | Winter 20 mail | Lewis | Ship Paper and Tape (qty scales with Farming) | Friendship 101 (Book_Friendship) | One-time per year, deadline Winter 24, needs Si.ExtraCraftingMaterials |

### Fishing

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Fish Smoker Request | Mail when you learn the Fish Smoker recipe | Any met adult villager | Ship a Fishing-scaled number of smoked fish (any kind) | 2 hearts of friendship | One-time, no deadline |
| Legendary Fish Quest | Daily board | Willy | Catch a legendary or boss fish that's in season (vanilla + any modded fish flagged `IsBossFish`, like RSV's Deep Ridge Angler, Waterfall Snakehead, Sockeye Salmon) | Big gold reward + 5 Challenge Bait | Cooldown Long, needs Fishing 6. Skips fish you've already caught and won't repeat back-to-back |
| Location Overpopulation | Daily board | Ecology-minded NPC (Demetrius, Maddie, Mr. Aguar, Dylan, etc.) | Catch a specific fish at a specific spot (only spots you've visited) | Gold + 2x catch count in Challenge Bait | Cooldown Medium, needs Fishing 2 |
| Medium Fishing Haul | Daily board | Pierre or a Joja rep (Morris, MorrisTod) | Catch the Medium Haul amount of one specific in-season fish | Gold (below sell) + an ecology pushback from Demetrius, Maddie, Mr. Aguar, Dylan, etc. | Cooldown Medium, needs Fishing 5 |
| Quality Fish Delivery | Daily board | A fisherman NPC (Willy + modded) | Catch and deliver X Gold-quality fish of a specific in-season type | Gold (below sell) + Friendship | Cooldown Short, needs Fishing 4 |
| Rainy Day Catch | Mail when tomorrow is forecast rain | A fisherman NPC (Willy, Blair, Carmen, etc.) | Catch fish that only spawn in rain | Gold + a rare tackle | Cooldown Short, needs Fishing 3 |
| Seafood Night | Daily board | Saloon chef (Gus, Pika, Rosa, Celestine) | Catch the Large Haul amount of one edible non-poisonous in-season fish (Pufferfish excluded) | Big gold (fish-premium scaled) + a multi-day ecology fallout chain with Linus included | Cooldown Long, needs Fishing 8 |
| Simple Fishing Request | Daily board | Any adult villager with a fish in their loved or liked list | Catch a few of one common fish at a spot you've visited | Gold based on sell price | Cooldown Short |
| Size Overpopulation | Daily board | Ecology-minded NPC | Catch X fish at or above a size threshold (Small, Medium, or Large bucket) | Gold + 3x count Wild Bait | Cooldown Medium, needs Fishing 3 |

### Foraging

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Clear Debris | Daily board | Any adult villager | Clear 5 to 20 resource clumps anywhere except the farm | Friendship | Cooldown Long |
| Forage with Linus | Daily board | Linus | Gift loved or liked forage to a few different villagers (count scales with Foraging) | Big friendship bump with Linus | Cooldown Long, needs Linus met |
| Plant Trees | Daily board | A conservation NPC (Linus, Demetrius, Dylan, Kimpoi, Aster, etc.) | Plant X trees anywhere outside the farm | Friendship | Cooldown Long |
| Rare Forage Hunt | Daily board | Any met villager | Gather a few rare forageables (Rainbow Shell, Cactus Fruit, Magma Cap, plus any modded forage that isn't in this season) | Gold + 2x count of one in-season seed | Cooldown Medium, needs Foraging 1 |
| Seasonal Foraging | Daily board | Any met villager | Gather and ship X seasonal forage | Gold | Cooldown Short |

### Mining

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Bar Delivery | Daily board | A blacksmith (Clint or modded equivalents) | Deliver X metal bars (tier scales with mine depth, up to Radioactive on Ginger Island unlock) | Gold + a random geode or trove | Cooldown Medium, needs Mining 1 and mine floor 40+ |
| Basic Slime Clearing | Adventurer's Guild board | Marlon (or modded combat NPCs) | Slay X slimes in the mines | Gold | Cooldown Medium |
| Mines Deep Dive | Adventurer's Guild board | Marlon | Reach floor X in The Mines (max 120) and ship an ore or stone haul | Gold + bars matching the floor band | Cooldown Short, needs Mining 1 and mine floor 5+ |
| Monster Hunt | Adventurer's Guild board | Marlon (or modded combat NPCs) | Slay X monsters of any kind | Gold + a combat-buff food (sized to a random magnitude bucket) | Cooldown Medium |
| Monster Parts | Adventurer's Guild board | A combat NPC (Wizard, Lance from SVE, MarlonFay, Mr. Aguar from RSV, Jio, Daia, Eli from EliAndDylan, Mariam from VMV) | Deliver one rare monster drop (Bat Wing, Solar Essence, Void Essence, or Bug Meat) at a Combat-scaled qty | A random gem or artifact sized to clear a solid gold value + a taste reaction from Krobus / Dwarf / Sen at 1+ hearts | Cooldown Medium, needs Combat 2, mine floor 40+, and 1+ heart with one of the underground NPCs |
| Rare Material Request | Adventurer's Guild board | A blacksmith (Clint or modded equivalents) | Deliver X of a random gem (vanilla or modded) | Gold + Artifact Trove count matching the gems | Cooldown Medium, needs Mining 7 |
| Skull Cavern Deep Dive | Adventurer's Guild board | Marlon | Reach floor X in Skull Cavern and ship a Mining-scaled ore haul | Radioactive Bars in the mail next morning (1 per 5 ores, min 2) | Cooldown Long, needs Skull Cavern unlocked, max floor configurable |

> The Mining-category quests above (everything except Bar Delivery) post to the Adventurer's Guild board by default. When the guild board is turned off in config, they fall back to the help-wanted board instead.

### Seasonal

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Beach Cleanup | Daily board | Beach folks (Elliott, Willy, Dylan from ES, etc.) | Collect beach forage, then report back | Friendship with the giver | Year-round, Cooldown Long |
| Floral Tea | Daily board | Any adult villager who doesn't dislike tea | Bring them an in-season flower they love or like | Friendship | Year-round (skips when there's no in-season flower), Cooldown Medium |
| Heat Wave Relief | Daily board | Harvey or Paula (RSV) | Deliver Ice Cream, Melon, or Juice | Friendship + a random item from the Hospital shop | Summer only, Cooldown Medium |
| Spring Cleaning | Daily board | Any met villager | Clear weeds around town | Friendship | Spring only, Cooldown Medium |

### Social

| Quest | When | Quest Giver | Objective | Reward | Notes |
| --- | --- | --- | --- | --- | --- |
| Check on Friends | Daily board | Any met villager | Talk to 3 randomly picked met villagers, then report back to the giver | Friendship with the giver | Cooldown 30 days |
| Check on George | Daily board | Evelyn | Gift George, talk to him, report to Evelyn | Friendship with both | Cooldown 21 days |
| Elliott's Poem Inspiration | Daily board | Elliott | Bring Elliott one flower or gem | Friendship (big bump) | Cooldown Long, needs Elliott met |
| Gift Delivery | Daily board | Any met villager | Drop off a loved or liked gift to one of the giver's friends | Gold + Friendship with both NPCs | Cooldown Long |

### Not planning to add

These were on the original list but we decided to skip them for now:

| Category | Quest | Why |
| --- | --- | --- |
| Animal | Moira's Exotic Animal Offer | Needs a VMV unlock flag and proper animal-shop discount support, neither of which is in place yet. Parked until those land. |
| Social | Deep Friendship Quest | Heart-level NPC requests, too much custom content per NPC for the scope |
| Seasonal | Harvest Bounty Competition | NPCs competing for crop donations, scope creep |
| Seasonal | Holiday Cooking Help | Covered well enough by Holiday Cookies + Gus's Winter feast |
| Seasonal | Ice Fishing Challenge | Winter fishing is already covered by other quests |
| Seasonal | Snowbound Deliveries | Too narrow, falls into the same space as Gift Delivery |

## Custom completion logic

Two quests use special `Quest` subclasses:

- **`AnySlimeQuest`**, backs Basic Slime Clearing. Counts any slime kill, not just one species.
- **`CollectAndReportQuest`**, backs Beach Cleanup. You gather the items in the world, then talk to the giver to hand the stack in.

Multi-step quests run on the framework's `AdventureQuest`: Check on George (gift, chat, report), the festival mail quests (Submarine Fuel, Wizard's Ritual, Holiday Cookies), Check on Friends, the Gus festival feasts, Skull Cavern Deep Dive, Mines Deep Dive, Pierre's Stock-Up, the Weekly Specials, the Grand Feast (Special Order), Dinner Party, Clear Debris, Plant Trees, Spring Cleaning, and several others. All other quests use the framework's `MoreQuestsItemDeliveryQuest`, `MoreQuestsFishingQuest`, or `MoreQuestsShipQuest`, built by the framework's `QuestFactory`.

## Known limitations

- **Mail Services Mod compatibility.** Completing a delivery quest by mailing the item through Mail Services Mod gives the recipient an extra full heart of friendship on top of the normal reward. MSM mimics vanilla's in-person delivery, which always pays a flat 250 friendship points no matter what the quest's actual reward is. Delivering in person gives the right reward. We'll file an upstream compatibility request once More Quests is published.
- **Mail Services Mod and the Forage with Linus quest.** Forage with Linus tracks gifts to different recipients via vanilla's `Quest.OnItemOfferedToNpc` hook. MSM skips that hook because it delivers through the mailbox rather than in-person, so mailed forage gifts don't count toward the quest. Hand-deliver the gifts for now. Same fix path as above.
- **Friendship-decay / friendship-clamp mods.** The negative consequence reactions (Massive Harvest, Weekly Specials, Medium Fishing Haul, Seafood Night, Monster Parts) call vanilla's `Game1.player.changeFriendship`. Mods that intercept that call to prevent friendship loss (CJB Cheats Menu's "No friendship decay" option, similar tuning mods) also stop the negative side of these reactions. The positive friendship side still lands. If you want the full back-and-forth these quests are designed around, turn that option off in CJB. The friendship deltas are logged at Debug level so you can confirm the call fired even if nothing changed.

## Notes

- Framework engine code (registry, pipeline, billboard, condition evaluator, reward applier, item resolver, NPC dispatch, vanilla wrappers) lives in the [framework mod](../MoreQuestsFramework/README.md). This repo is content only; quests are handed to the framework through Content Patcher's asset system, which means other content packs can add or tweak quests via the same pipeline.
- Mail prefix for in-world quest letters is `RafiaBee.MoreQuests.` (the framework names mail keys after the owning consumer mod, so trackers like Mail Services Mod attribute these quests to MoreQuests rather than to the framework).
- Run `mq_refresh` in the SMAPI console to re-roll the daily board without reloading the save.
