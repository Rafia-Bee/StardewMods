# More Quests

A SMAPI content mod for Stardew Valley that ships a curated set of new daily-board, mail, and festival quests on top of the [More Quests Framework](../MoreQuestsFramework/README.md).

> Heavy work in progress. Twenty quests are live; the remaining concepts in [this google sheet](https://docs.google.com/spreadsheets/d/13HQDEAYTcmi-x9Hp7R6lq2STRFtO5rUA3JxVOgitoDM/edit?usp=sharing) will be added across later phases.

## Dependencies

**Required**

- **More Quests Framework** (`RafiaBee.MoreQuestsFramework`) — bundled in this repo at [../MoreQuestsFramework/](../MoreQuestsFramework/). Without it, this mod logs an error and registers nothing.

**Optional integrations** (auto-detected at runtime)

- **Generic Mod Config Menu** — in-game config page for the per-quest content toggles.
- **Ridgeside Village**, **East Scarp**, **Visit Mount Vapius**, **Stardew Valley Expanded** — adds modded NPCs to the framework's dispatch pools (saloon chefs, ecology-minded, conservation guides, etc.) so quest givers expand to match the installed roster.
- **Livestock Follows You** by RafiaBee — required for Marnie's Cow Offer, Marnie's Livestock Show, and Leah's Farm Painting.
- **Si's Extra Crafting Materials** — required for the Winter Star Wrapping Paper quest.

## Configuration

`Mods/MoreQuests/config.json` carries per-quest content settings: animal/festival quest toggles, shop discount sizes, fish-haul quantities, Skull Cavern max level, secret-gift hint toggle, etc. Engine-level tunables (quests per day, weights, deadlines, reward sizes) live on the [framework's config](../MoreQuestsFramework/README.md#configuration). Both pages are surfaced through GMCM.

## Quests

| Category | Quest | Trigger | Quest Giver | Objective | Reward | Constraints | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Mining | Bar Delivery | Daily board | Clint | Deliver X metal bars | Gold (Intermediate) + geode/gem | Cooldown 5d, OnePerGiver | Implemented |
| Farming | Basic Crop Delivery | Daily board | Any | Deliver X of a seasonal crop (any quality) | Gold scaled by sell price | Cooldown 7d | Implemented |
| Mining | Basic Slime Clearing | Daily board | Adventurer's Guild | Slay X slimes in the mines | Gold (Beginner) | Cooldown 7d | Implemented |
| Seasonal | Beach Cleanup | Daily board | Elliott / Willy / Dylan (ES) | Collect beach forageables | Friendship with giver | Summer only, Cooldown 7d, OnePerGiver | Implemented |
| Social | Check on George | Daily board | Evelyn | Gift George, talk to him, report to Evelyn | Friendship (Mid) with both | Cooldown 21d, OnePerGiver | Implemented |
| Cooking | Craving a Meal | Daily board | Any | Deliver a dish the giver loves/likes | Friendship + a random dish | Cooldown 2d | Implemented |
| Social | Elliott's Poem Inspiration | Periodic / Daily board | Elliott | Bring Elliott a flower or gem | Friendship (Basic) | Cooldown 7d, OnePerGiver | Implemented |
| Animal | Hay Supply Run | Periodic (mail, monthly) | Marnie | Deliver hay scaled to animal count | Gold scaled to amount | Cooldown 28d, OnePerGiver | Implemented |
| Seasonal | Spring Tea | Daily board | Any | Ship spring flowers | Friendship (Basic) | Fall only, Cooldown 3d | Implemented |
| Foraging | Seasonal Foraging | Daily board | Any | Gather and ship X seasonal forage | Gold (Beginner) | Cooldown 2d | Implemented |
| Fishing | Simple Fishing Request | Daily board | Any | Catch X common fish | Gold scaled by sell price | Cooldown 2d | Implemented |
| Festival | Submarine Fuel | Winter 12 (mail) | Captain | Ship Battery Pack or Coal (weighted alternatives) | Pearl via NextDay mail | DateLocked, OneTimePerYear | Implemented |
| Festival | Wizard's Ritual Materials | Fall 24 (mail) | M. Rasmodius | Ship Void Essence + Bat Wings + Solar Essence | Book of Mysteries via NextDay mail | DateLocked, OneTimePerYear | Implemented |
| Festival | Evelyn's Holiday Cookies | Winter 21 (mail) | Evelyn | Deliver Flour, Sugar, and any edible egg | Friendship (Large) + 6x Cookie | DateLocked, OneTimePerYear | Implemented |
| Fishing | Location-Specific Overpopulation | Daily board | Demetrius / Maddie / Mr Aguar / Dylan | Catch a specific fish at a specific spot | Gold (Intermediate) + 10x Challenge Bait | Cooldown 4d | Not started |
| Animal | Alex's Protein Shakes | Periodic | Alex | Deliver eggs scaled to chicken count | Energy Tonic / Muscle Remedy / Protein Bar | Cooldown 14d, OnePerGiver | Not started |
| Farming | Caroline's Tea Garden | Daily board | Caroline | Deliver spring flowers/herbs for tea | Friendship (Mid) + 10 Tea Leaves | Fall only, Cooldown 7d, OnePerGiver | Not started |
| Social | Check on Friends | Daily board | Any met villager | Talk to 3 randomly-picked met villagers, then report back | Friendship (Intermediate) with the giver | Cooldown 7d | Implemented |
| Foraging | Clear Debris | Daily board | Any | Clear debris around town | Friendship (Mid) | Cooldown 5d | Not started |
| Cooking | Dinner Party | Daily board | Any human NPC | Deliver multiple liked dishes | Gold (sell-price scaled) + Friendship (Basic) | Cooldown 10d | Not started |
| Festival | Festival Decor: Moonlight Jellies | Summer 24 | Lewis | Ship Torches and Wood | Gold (Basic) + random Pierre Moonlight Jellies decor | DateLocked, OneTimePerYear | Not started |
| Festival | Festival Decor: ES Spirit's Eve | Fall 24 | Rosa | Ship purple-dye items, slime, stone | Friendship (MultiHeart, ES NPCs) | DateLocked, OneTimePerYear | Not started |
| Festival | Festival Decor: Egg Festival | Spring 10 | Lewis | Ship hay bales | Gold (Beginner) + random Pierre Egg Festival decor | DateLocked, OneTimePerYear | Not started |
| Festival | Festival Decor: Fair | Fall 12 | Lewis | Ship Wood, Wood Signs, Flowers | Bonus Star Tokens at the Fair | DateLocked, OneTimePerYear | Not started |
| Festival | Festival Decor: Luau | Summer 6 | Lewis | Ship Fiber, Basic Log, Wood Lamp-post | Gold (Intermediate) + random Pierre Luau decor | DateLocked, OneTimePerYear | Not started |
| Festival | Festival Decor: Ridgeside Gathering | Fall 15 | Lenny | Ship Tub o' Flowers, Wood, Tables | Friendship (MultiHeart, RSV NPCs) | DateLocked, OneTimePerYear, requires RSV | Not started |
| Festival | Festival Decor: Spirit's Eve | Fall 22 | Lewis | Ship Pumpkins, Cloth, Torches | Gold (Intermediate) + Jack o' Lantern | DateLocked, OneTimePerYear | Not started |
| Foraging | Forage with Linus | Daily board | Linus | Gift loved/liked forage to 5 people | Friendship (Large) with Linus | Cooldown 14d, OnePerGiver | Not started |
| Social | Gift Delivery | Daily board | Any | Deliver a gift to the giver's friend | Friendship (sell-price scaled) | Cooldown 4d | Not started |
| Animal | Gunther's Dinosaur Study | First Dinosaur Egg hatched | Gunther | Deliver a spare Dinosaur Egg | Gold (Advanced) + upgraded-quality Dinosaur Egg | OneTime | Not started |
| Festival | Gus's Feast: Egg Festival | Spring 6 (mail) | Gus | Deliver spring-themed ingredients | Sample of a spring dish | DateLocked, OneTimePerYear | Implemented |
| Festival | Gus's Feast: Fair | Fall 8 | Gus | Large ingredient delivery | Sample dishes + Fair token bonus | DateLocked, OneTimePerYear | Deferred to Phase 9 (Festival Bonus reward kind) |
| Festival | Gus's Feast: Luau | Summer 8 | Gus | Deliver 3 random spring/summer ingredients | Higher base potluck score | DateLocked, OneTimePerYear | Deferred to Phase 9 (Festival Bonus reward kind) |
| Festival | Gus's Feast: Winter Star | Winter 18 (mail) | Gus | Ship winter-themed forageables | Friendship (MultiSmall) to every met NPC | DateLocked, OneTimePerYear | Implemented |
| Cooking | Saloon Grand Feast | Daily board | Gus / Rosa / Celestine / Pika | Deliver ingredients for multiple recipes | Gold (Expert) + Friendship (MultiSmall) | Cooldown 14d, OnePerGiver | Not started |
| Cooking | Weekly Special (Common) | Daily board | Gus / Rosa / Celestine / Pika | Deliver ingredients for a common recipe | Gold (Beginner) + Friendship (MultiSmall) | Cooldown 5d, OnePerGiver | Not started |
| Cooking | Weekly Special (Complex) | Daily board | Gus / Rosa / Celestine / Pika | Deliver ingredients for a complex recipe | Gold (Intermediate) + Friendship (MultiSmall) | Cooldown 7d, OnePerGiver | Not started |
| Seasonal | Heat Wave Relief | Daily board | Harvey / Paula (RSV) | Ship cold drinks, melons, ice cream | Random items from Harvey's shop | Summer only, Cooldown 5d, OnePerGiver | Not started |
| Seasonal | Jellyfish Watch Prep | Daily board | Demetrius / Maddie / Mr Aguar / Dylan | Deliver beach forageables for study | Friendship (Basic) + a loved item | Summer only, Cooldown 5d, OnePerGiver | Not started |
| Animal | Krobus's Void Note | First Void Egg + 1 heart Krobus | Krobus | Deliver a Void Egg to Krobus | Void Chicken Statue + Friendship (Mid) | OneTime | Not started |
| Animal | Leah's Farm Painting | Periodic (high friendship) | Leah | Visit Leah's house with an animal following | Custom animal painting | Cooldown 21d, OnePerGiver, requires Livestock Follows You + single-player | Not started |
| Fishing | Legendary Fish Quest | Daily board | Willy | Catch a legendary or very rare fish | Unique fish display furniture | Cooldown 21d, OnePerGiver | Not started |
| Festival | Lewis's Easter Eggs | Spring 8 | Lewis | Ship dye materials | Egg Basket (custom asset) | DateLocked, OneTimePerYear | Not started |
| Animal | Marnie's Chicken Offer | Day after building Coop | Marnie | Bring 15 mixed seasonal seeds | Discounted chicken | OneTime | Not started |
| Animal | Marnie's Cow Offer | Day after building Barn | Marnie | Buy a Grazing Bell from Marnie | Discounted cow | OneTime, requires Livestock Follows You | Not started |
| Animal | Marnie's Egg Request | After first egg collected | Marnie | Ship 10 Eggs over the next week | Gold (Basic) + Mayonnaise Machine recipe | OneTime | Not started |
| Animal | Marnie's Livestock Show | After Deluxe Barn (with 2+ animals) | Marnie | Walk animals around town | Friendship (Large) with Marnie | OneTime, requires Livestock Follows You + single-player | Not started |
| Animal | Marnie's Milk Request | After first milk collected | Marnie | Ship 10 Milk | Gold (Basic) + Cheese Press recipe | OneTime | Not started |
| Farming | Massive Harvest Request | Daily board | Morris / Joja co. | Ship CropMassiveQty+ of a single crop | Gold (sell-price scaled, high) | Cooldown 10d, OnePerGiver | Not started |
| Fishing | Medium Fishing Haul | Daily board | Morris / Pierre | Catch FishHaulMediumQty+ of a specific fish | Gold (sell-price scaled, high) | Cooldown 5d, OnePerGiver | Not started |
| Festival | Merchant Unpacking | Winter 13 | Any | Ship out-of-season seeds matching Magic Boat stock | Friendship (Basic) | DateLocked, OneTimePerYear | Not started |
| Animal | Moira's Exotic Animal Offer | Player unlocks "Ewes" (VMV) | Moira (VMV) | Bring a modded crop | Discounted Ewe | OneTime, requires VMV | Not started |
| Mining | Monster Hunt | Daily board | Adventurer's Guild | Slay X of any monsters | Gold (Intermediate) + combat-buff food | Cooldown 3d | Not started |
| Mining | Monster Parts | Daily board | Wizard / Lance / Marlon / Mr Aguar / Abigail / Eli / Mariam | Collect rare monster drops | High-value gem or artifact | Cooldown 5d, OnePerGiver | Not started |
| Farming | Pierre's Stock-Up | Daily board | Pierre | Deliver bulk mixed seasonal crops (3 distinct, qty scales with farming) | ShopDiscount on the matching seeds at Pierre's | Cooldown 7d, requires Farming 4 | Implemented |
| Foraging | Plant Trees | Daily board | Linus / Dylan / Demetrius / Kimpoi / Aster | Plant X trees at a target location | Friendship (Intermediate) with giver | Cooldown 7d, OnePerGiver | Not started |
| Farming | Premium Crop Order | Daily board | Any | Deliver X Iridium-quality rare crops | Gold (Advanced) + rare/ancient seeds | Cooldown 5d | Not started |
| Seasonal | Preserves Season | Special Orders board (Fall 1) | Single dispatched villager | Ship a scaling number of jam, pickle, wine, and dried mushroom artisan goods (counts + objective count both scale with Farming) | Gold (above-sell bonus) + Friendship (Basic) to requester | Fall 1, Cooldown 21d, vanilla `Month` window | Implemented (8a) |
| Farming | Quality Crop Delivery | Daily board | Any | Deliver X Gold-quality seasonal crops | Gold (Basic-Intermediate) + Friendship (Basic) | Cooldown 3d | Not started |
| Fishing | Quality Fish Delivery | Daily board | Willy | Catch X Gold-quality fish of a specific type | Gold (Basic-Intermediate) + Friendship (Basic) | Cooldown 3d, OnePerGiver | Not started |
| Festival | Rainbow Platter (Trout Derby) | Summer 20-21 | Gus / Pika / Rosa / Celestine | Catch FestivalFishQty Rainbow Trout | Recipe + shop discount on the dish | DateLocked, OneTimePerYear | Not started |
| Fishing | Rainy Day Catch | Daily board / mail | Willy / Blair / Carmen | Catch fish that only spawn in rain | Gold (Intermediate) + rare tackle | Cooldown 5d, OnePerGiver | Not started |
| Foraging | Rare Forage Hunt | Daily board | Any | Gather rare forage items | Gold (Intermediate) + 10x random seasonal seeds | Cooldown 4d | Not started |
| Mining | Rare Material Request | Daily board | Clint | Deliver Iridium Bars or rare gems | Gold (Advanced) + 3x Artifact Trove or 5x gems | Cooldown 7d, OnePerGiver | Not started |
| Animal | Robin's Silo Offer | After first Coop/Barn (no Silo) | Robin | Gather one type of build material | Discounted Silo | OneTime | Not started |
| Fishing | Seafood Night | Daily board | Gus / Pika / Rosa / Celestine | Catch FishHaulLargeQty+ edible non-poisonous fish | Gold (sell-price premium, very high) | Cooldown 10d, OnePerGiver | Not started |
| Festival | Secret Gift Hint | Winter 22 | Board | Hint about your assigned Winter Star recipient | Information (preference hint) | DateLocked, OneTimePerYear | Not started |
| Mining | Skull Cavern Deep Dive | Adventurer's Guild board | Marlon | Reach floor X in Skull Cavern and deliver an ore/stone haul | Gold (Advanced) + Iridium Bars | Cooldown 14d, requires Skull Cavern unlocked, configurable max floor | Implemented |
| Mining | Mines Deep Dive | Adventurer's Guild board | Marlon | Reach floor X in The Mines (max 120) and deliver an ore/stone haul | Gold (Intermediate) + bars matching the floor band | Cooldown 14d, requires deepestMineLevel >= 5 | Implemented |
| Fishing | Size-Specific Overpopulation | Daily board | Demetrius | Catch X fish of a specific size | Gold (Intermediate) + bait | Cooldown 4d, OnePerGiver | Not started |
| Seasonal | Spring Cleaning | Daily board | Any | Clear weeds around town | Friendship (Basic) | Spring only, Cooldown 5d | Not started |
| Festival | SquidFest Showcase | Winter 12-13 | Gus / Pika / Rosa / Celestine | Catch FestivalFishQty Squid | Recipe + shop discount on the dish | DateLocked, OneTimePerYear | Not started |
| Festival | Wrapping Paper | Winter 20 | Lewis | Ship Paper and Tape | Book of Stars | DateLocked, OneTimePerYear, requires Nexus 25467 | Not started |
| Social | Deep Friendship Quest | Heart-level trigger | Various | NPC-specific requests at higher hearts | Unique per NPC | — | Won't do |
| Seasonal | Harvest Bounty Competition | Special Orders board | Multiple | NPCs compete for crop donations | Varies | Fall only | Won't do |
| Seasonal | Holiday Cooking Help | Daily board | Evelyn / Gus | Deliver baking ingredients | Friendship (Basic) + baked goods | Winter only | Won't do |
| Seasonal | Ice Fishing Challenge | Daily board | Willy | Winter-exclusive fishing challenges | Gold (Intermediate) + winter fishing gear | Winter only | Won't do |
| Seasonal | Snowbound Deliveries | Daily board | Various | Deliver supplies to far-flung NPCs | Friendship (Large) with recipient | Winter only | Won't do |

## Custom completion logic

Two of this mod's quests use bespoke `Quest` subclasses:

- **`AnySlimeQuest`** — backs Basic Slime Clearing. Counts any slime kill, not just one species.
- **`CollectAndReportQuest`** — backs Beach Cleanup, Seasonal Foraging, etc. Player gathers items in the world, then reports to the giver to consume the stack and turn it in.

Multi-step quests run on the framework's `AdventureQuest` substrate: Check on George (gift, chat, report), the three Phase-7b festival migrations (Submarine Fuel, Wizard's Ritual, Holiday Cookies), the Phase-7c content (Check on Friends, Gus's Feast Spring 6, Gus's Feast Winter 18), and the Phase-8c content (Skull Cavern Deep Dive, Mines Deep Dive, Pierre's Stock-Up). All other quests use the framework's `MoreQuestsItemDeliveryQuest`, `MoreQuestsFishingQuest`, or `MoreQuestsShipQuest` subclasses, all built by the framework's `QuestFactory`.

## Known limitations

- **Mail Services Mod compatibility.** Completing a delivery quest by mailing the item through Mail Services Mod gives the recipient an extra full heart of friendship on top of the configured reward. MSM mimics vanilla's in-person delivery, which always pays a fixed 250-point friendship bump regardless of the quest's declared rewards. Delivering in person produces the correct reward. An upstream compatibility request will be filed once More Quests publishes.

## Notes

- Framework engine code (registry, pipeline, billboard, condition evaluator, reward applier, item resolver, NPC dispatch, vanilla wrappers) lives in the [framework mod](../MoreQuestsFramework/README.md) — this repo is content only.
- Mail prefix for in-world quest letters is `RafiaBee.MoreQuestsFramework.` (the framework owns the routing).
- Run `mq_refresh` in the SMAPI console to re-roll the daily board without reloading the save.
