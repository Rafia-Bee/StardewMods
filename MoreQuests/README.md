# More Quests

A SMAPI content mod for Stardew Valley that ships a curated set of new daily-board, mail, and festival quests on top of the [More Quests Framework](../MoreQuestsFramework/README.md).

> Heavy work in progress. Sixty-plus quests are live; the remaining concepts will be added across later phases.

## Dependencies

**Required**

- **More Quests Framework** (`RafiaBee.MoreQuestsFramework`) — bundled in this repo at [../MoreQuestsFramework/](../MoreQuestsFramework/). Without it, this mod logs an error and registers nothing.

**Optional integrations** (auto-detected at runtime)

- **Generic Mod Config Menu** — in-game config page for the per-quest content toggles.
- **Ridgeside Village**, **East Scarp**, **Visit Mount Vapius**, **Stardew Valley Expanded** — adds modded NPCs to the framework's dispatch pools (saloon chefs, ecology-minded, conservation guides, etc.) so quest givers expand to match the installed roster.
- **Livestock Follows You** by RafiaBee — required for Marnie's Cow Offer, Marnie's Livestock Show, and Leah's Farm Painting.
- **Si's Extra Crafting Materials** — required for the Winter Star Wrapping Paper quest.
- **[Catch of the Day](https://www.nexusmods.com/stardewvalley/mods/30297)** — pairs especially well with Simple Fishing Request and other catch-X-fish quests, since it surfaces which fish are actually biting at the spot you're standing on.

## Configuration

`Mods/MoreQuests/config.json` carries per-quest content settings: animal/festival quest toggles, Adventurer's Guild board toggle, shop discount sizes, fish-haul quantities, Skull Cavern max level, secret-gift hint toggle, etc. Engine-level tunables (quests per day, weights, deadlines, reward sizes) live on the [framework's config](../MoreQuestsFramework/README.md#configuration). Both pages are surfaced through GMCM.

The **Adventurer's Guild board** toggle (default on) controls whether the mining + monster quests get their own board at the mine entrance, or whether they all fold back into the regular help-wanted board. With it on, the help-wanted board only shows Bar Delivery from the mining category; the deep dives, slime clearing, and vanilla monster eradication post on the guild board instead. With it off, every guild-tagged quest appears on the help-wanted board so the content stays reachable. Per-quest weights still let you disable individual quests on top of this.

## Quests

| Category | Quest | Trigger | Quest Giver | Objective | Reward | Constraints | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Mining | Bar Delivery | Daily board | Clint | Deliver X metal bars | Gold (Intermediate) + geode/gem | Cooldown 5d, OnePerGiver | Implemented |
| Farming | Basic Crop Delivery | Daily board | Any | Deliver X of a seasonal crop (any quality) | Gold scaled by sell price | Cooldown 7d | Implemented |
| Mining | Basic Slime Clearing | Daily board | Adventurer's Guild | Slay X slimes in the mines | Gold (Beginner) | Cooldown 7d | Implemented |
| Seasonal | Beach Cleanup | Daily board | Elliott / Willy / Dylan (ES) | Collect beach forageables (vanilla beach roster + modded `season_*` matches) | Friendship (Mid) with giver | Year-round, Cooldown Long | Implemented |
| Social | Check on George | Daily board | Evelyn | Gift George, talk to him, report to Evelyn | Friendship (Mid) with both | Cooldown 21d, OnePerGiver | Implemented |
| Cooking | Craving a Meal | Daily board | Any | Deliver a dish the giver loves/likes | Friendship + a random dish | Cooldown 2d | Implemented |
| Social | Elliott's Poem Inspiration | Periodic / Daily board | Elliott | Bring Elliott a flower or gem | Friendship (Basic) | Cooldown 7d, OnePerGiver | Implemented |
| Animal | Hay Supply Run | Periodic (mail, monthly) | Marnie | Deliver hay scaled to animal count | Gold scaled to amount | Cooldown 28d, OnePerGiver | Implemented |
| Seasonal | Floral Tea | Daily board | Any adult human (who doesn't dislike tea) | Deliver an in-season flower the giver loves or likes | Friendship (Basic) | Year-round, Cooldown 7d | Implemented |
| Foraging | Seasonal Foraging | Daily board | Any | Gather and ship X seasonal forage | Gold (Beginner) | Cooldown 2d | Implemented |
| Fishing | Simple Fishing Request | Daily board | Any | Catch X common fish | Gold scaled by sell price | Cooldown 2d | Implemented |
| Festival | Submarine Fuel | Winter 12 (mail) | Captain | Ship Battery Pack or Coal (weighted alternatives) | Pearl via NextDay mail | DateLocked, OneTimePerYear | Implemented |
| Festival | Wizard's Ritual Materials | Fall 24 (mail) | M. Rasmodius | Ship Void Essence + Bat Wings + Solar Essence | Book of Mysteries via NextDay mail | DateLocked, OneTimePerYear | Implemented |
| Festival | Evelyn's Holiday Cookies | Winter 21 (mail) | Evelyn | Deliver Flour, Sugar, and any edible egg | Friendship (Large) + 6x Cookie | DateLocked, OneTimePerYear | Implemented |
| Fishing | Location-Specific Overpopulation | Daily board | Demetrius / Maddie / Mr Aguar / Dylan | Catch a specific fish at a specific spot (visited locations only) | Gold (Intermediate) + 10x Challenge Bait | Cooldown 4d, requires Fishing 2 | Implemented (9.5e) |
| Animal | Alex's Protein Shakes | Periodic (mail, 14d) | Alex | Deliver eggs scaled to chicken count | Friendship (Basic) + Energy Tonic / Muscle Remedy / Life Elixir | Cooldown 14d, requires NpcMet Alex; Protein Bar reward deferred to asset drop | Implemented (9.5f) |
| Farming | Caroline's Tea Garden | Daily board | Caroline | Deliver spring flowers/herbs for tea | Friendship (Mid) + 10 Tea Leaves | Fall only, Cooldown 7d, OnePerGiver | Implemented (9.5g) |
| Social | Check on Friends | Daily board | Any met villager | Talk to 3 randomly-picked met villagers, then report back | Friendship (Intermediate) with the giver | Cooldown 7d | Implemented |
| Foraging | Clear Debris | Daily board | Any | Clear debris around town | Friendship (Mid) | Cooldown 5d | Implemented (9.5g) |
| Cooking | Dinner Party | Special Orders board | Any human NPC | Deliver multiple loved/liked dishes | Gold (sell-price scaled) + Friendship (Basic) | Cooldown 10d; auto-fire deferred (StartDate-only SpecialOrder eval), reachable via `mq_reemit_specialorders` | Implemented (9.5g) |
| Festival | Festival Decor: Moonlight Jellies | Summer 21 | Lewis | Ship Torches and Wood (qty scales with Foraging) | Gold (Basic) + random Pierre Moonlight Jellies stall decor | DateLocked, OneTimePerYear, 6-day deadline | Implemented |
| Festival | Festival Decor: ES Spirit's Eve | Fall 24 | Rosa | Ship purple-dye items, slime, stone | Friendship (MultiHeart, ES NPCs) | DateLocked, OneTimePerYear, 3-day deadline, requires ES/EliAndDylan/LurkingInTheDark | Implemented |
| Festival | Festival Decor: Egg Festival | Spring 10 | Lewis | Ship hay bales | Gold (Beginner) + random Pierre Egg Festival stall decor | DateLocked, OneTimePerYear, 3-day deadline | Implemented |
| Festival | Festival Decor: Fair | Fall 12 | Lewis | Ship fall flowers, Wood, any sign (Wood/Stone/Dark); quantities scale with Farming/Foraging | Grange-score bump OR bonus Fair star tokens (GMCM toggle) | DateLocked, OneTimePerYear, 3-day deadline | Implemented |
| Festival | Festival Decor: Luau | Summer 6 | Lewis | Ship Fiber, Hardwood, Wood Lamp-posts (qty scales with Foraging) | Gold (Intermediate) + random Pierre Luau stall decor | DateLocked, OneTimePerYear, 4-day deadline | Implemented |
| Festival | Festival Decor: Ridgeside Gathering | Fall 15 | Lenny | Ship Tub o' Flowers, Wood, any tables (qty scales with Farming/Foraging) | Friendship (MultiHeart, RSV NPCs) + Tub o' Flowers recipe at quest-accept | DateLocked, OneTimePerYear, requires RSV | Implemented |
| Festival | Festival Decor: Spirit's Eve | Fall 22 | Wizard | Ship Pumpkin Seeds, Cloth, Torches (qty scales with Farming) | Gold (Intermediate) + 5x Jack o' Lantern | DateLocked, OneTimePerYear, 4-day deadline | Implemented |
| Foraging | Forage with Linus | Daily board | Linus | Gift loved/liked forage to 5 different villagers | Friendship (Large) with Linus | Cooldown 14d, requires Linus met | Implemented (9d) |
| Social | Gift Delivery | Daily board | Any | Deliver a gift to the giver's friend | Gold (sell-price scaled) + Friendship (Basic) | Cooldown 4d | Implemented (9.5a) |
| Animal | Gunther's Dinosaur Study | First Dinosaur Egg held (mail) | Gunther | Deliver a spare Dinosaur Egg | Gold (Advanced) + Dinosaur Egg returned | OneTime, quality-tier-up reward deferred (plain regular-quality return) | Implemented (9.5f) |
| Festival | Gus's Feast: Egg Festival | Spring 6 (mail) | Gus | Deliver spring-themed ingredients | Sample of a spring dish | DateLocked, OneTimePerYear | Implemented |
| Festival | Gus's Feast: Fair | Fall 8 | Gus | Deliver 3 distinct fall ingredients (curated pool) | Sample fall dish + FestivalBias on the Fair grange judging (+15 grange score) | DateLocked, OneTimePerYear | Implemented (9d) |
| Festival | Gus's Feast: Luau | Summer 8 | Gus | Deliver 3 distinct first-year-friendly summer ingredients | FestivalBias on the Luau governor reaction (+1 tier, capped at 5) | DateLocked, OneTimePerYear | Implemented (9d) |
| Festival | Gus's Feast: Winter Star | Winter 18 (mail) | Gus | Ship winter-themed forageables | Friendship (MultiSmall) to every met NPC | DateLocked, OneTimePerYear | Implemented |
| Cooking | Saloon Grand Feast | Special Orders board | Gus / Rosa / Celestine / Pika | Ship aggregated ingredients across 3 complex recipes | Gold (Expert) + Friendship (MultiSmall) to liked-by NPCs + per-dish Tier 2 taste reaction | Cooldown 14d, vanilla `Week` window | Implemented (9b) |
| Cooking | Weekly Special (Common) | Daily board | Gus / Rosa / Celestine / Pika | Deliver ingredients for a common (≤ 3 ingredient) recipe | Gold (Beginner) + Friendship (MultiSmall) to liked-by NPCs + Tier 1 taste reaction | Cooldown 5d, OnePerGiver | Implemented (9b) |
| Cooking | Weekly Special (Complex) | Daily board | Gus / Rosa / Celestine / Pika | Deliver ingredients for a complex (≥ 4 ingredient) recipe | Gold (Intermediate) + Friendship (MultiSmall) to liked-by NPCs + Tier 2 taste reaction | Cooldown 7d, OnePerGiver | Implemented (9b) |
| Seasonal | Heat Wave Relief | Daily board | Harvey / Paula (RSV) | Deliver cold drinks, melons, or ice cream | Friendship (Basic) + clinic-themed item | Summer only, Cooldown 5d | Implemented (9.5a) |
| Festival | Jellyfish Watch Prep | Summer 4 (mail) | Demetrius / Maddie / Mr Aguar / Dylan | Deliver ocean forageables for Moonlight Jellies notes | Friendship (Basic) + a loved item | DateLocked, 6-day deadline, OneTimePerYear | Implemented |
| Animal | Krobus's Void Note | First Void Egg held (mail) | Krobus | Deliver a Void Egg to Krobus | Friendship (Mid) + Book of the Void | OneTime, requires Krobus present | Implemented (9.5f) |
| Animal | Leah's Farm Painting | Periodic (high friendship, mail) | Leah | Visit Leah's house with an animal in tow | Random houseplant furniture (placeholder until bespoke animal painting sprite ships) + Friendship (Basic) | Cooldown Long, OnePerGiver, requires Livestock Follows You + single-player | Implemented (9.5f, placeholder reward) |
| Fishing | Legendary Fish Quest | Daily board | Willy | Catch a legendary / boss fish in season (vanilla + any modded fish flagged `IsBossFish` in Data/Locations, e.g. RSV's Deep Ridge Angler / Waterfall Snakehead / Sockeye Salmon) | GoldExpertBase + 50 Challenge Bait (placeholder until per-fish display furniture lands) | Cooldown 21d, requires Fishing 6 | Implemented (placeholder reward) |
| Festival | Lewis's Egg Festival Dye | Spring 8 | Lewis | Ship dye materials in 2-5 random colors (quantity scales with Farming) | Three Egg Baskets (Cream, Pink, Rustic) | DateLocked, OneTimePerYear, deadline Spring 12 | Implemented |
| Animal | Marnie's Chicken Offer | Day after building Coop (mail) | Marnie | Bring 15 of one current-season seed type | Gold rebate (≈ chicken price) + Friendship (Basic) | OneTime; real animal-shop discount deferred | Implemented (9.5f) |
| Animal | Marnie's Cow Offer | Day after building Barn (mail) | Marnie | Bring 50 hay | Gold rebate (≈ cow price) + Friendship (Basic) | OneTime; Grazing Bell variant + real animal-shop discount deferred | Implemented (9.5f) |
| Animal | Marnie's Egg Request | After first egg laid (mail) | Marnie | Ship 10 Eggs through the bin | Gold (Basic) + Mayonnaise Machine recipe + Friendship (Basic) | OneTime | Implemented (9.5f) |
| Animal | Marnie's Livestock Show | After Deluxe Barn (with 2+ animals, mail) | Marnie | Walk into Town with 2+ animals in tow | Friendship (Large) with Marnie | OneTime, requires Livestock Follows You + single-player + non-winter season | Implemented (9.5f) |
| Animal | Marnie's Milk Request | After first milk produced (mail) | Marnie | Ship 10 Milk through the bin | Gold (Basic) + Cheese Press recipe + Friendship (Basic) | OneTime | Implemented (9.5f) |
| Farming | Massive Harvest Request | Daily board | Morris / MorrisTod (SVE) | Ship a Farming-scaled stack of one seasonal crop | Gold (below-sell scaled, high) + Tier 1 consequence on the requested crop's loved-by NPCs | Cooldown Long, requires Farming 7+ | Implemented (9c) |
| Fishing | Medium Fishing Haul | Daily board | Morris / MorrisTod (SVE) / Pierre | Catch FishHaulMediumQty+ of a specific seasonal fish | Gold (below-sell scaled) + Tier 2 ecology consequence (Demetrius / Maddie / Mr. Aguar / Dylan) | Cooldown 5d, requires Fishing 5 | Implemented (9c) |
| Festival | Merchant Unpacking | Winter 13 | Any met human NPC | Ship 1-3 seed varieties pulled from the Night Market Magic Boat stock (variety and qty scale with Farming) | Friendship (Basic) | DateLocked, OneTimePerYear, deadline Winter 18 | Implemented |
| Animal | Moira's Exotic Animal Offer | Player unlocks "Ewes" (VMV) | Moira (VMV) | Bring a modded crop | Discounted Ewe | OneTime, requires VMV; deferred (VMV ewes-unlock mail flag unknown + animal-shop discount infra missing) | Not started |
| Mining | Monster Hunt | Daily board | Marlon | Slay X monsters of any type | Gold (Intermediate) + combat-buff food | Cooldown 3d | Implemented (9.5a) |
| Mining | Monster Parts | Daily board | Wizard / Lance (SVE) / MarlonFay (SVE) / Mr. Aguar (RSV) / Jio (RSV) / Daia (RSV) / Eli (EliAndDylan) / Mariam (VMV) | Deliver one rare monster drop (Bat Wing / Solar Essence / Void Essence / Bug Meat) at Combat-scaled qty | One random gem or artifact (auto-picked from the live `Data/Objects` pool) sized to clear GoldIntermediateBase + Tier 1 negative consequence to Krobus / Dwarf / Sen (ESV) filtered by >=1 heart friendship | Cooldown Medium, requires Combat 2 + deepestMineLevel >= 40 + >=1 heart with one of Krobus/Dwarf/Sen | Implemented (9c) |
| Farming | Pierre's Stock-Up | Daily board | Pierre | Deliver bulk mixed seasonal crops (`RequestVariationCount` distinct, qty scales with farming) | ShopDiscount on the matching seeds at Pierre's | Cooldown 14d, requires Farming 4 | Implemented |
| Foraging | Plant Trees | Daily board | Linus / Dylan / Demetrius / Kimpoi / Aster | Plant X trees at a target location | Friendship (Intermediate) with giver | Cooldown 7d, OnePerGiver; per-NPC location dispatch routes every giver to vanilla Forest until modded location ids land | Implemented (9.5g) |
| Farming | Premium Crop Order | Daily board | Any | Deliver X Iridium-quality rare crops | Gold (Advanced) + 3x rare/ancient seeds | Cooldown 5d, Farming 7+ | Implemented (9.5b) |
| Farming | Preserves Jar Request | Mail (on first Preserves Jar recipe) | Any adult human you've met | Ship a scaling number of pickles or jams (any kind) | Friendship (2 hearts) | OneShot, no deadline | Implemented |
| Farming | Keg Request | Mail (on first Keg recipe) | Any adult human you've met | Ship a scaling number of wine, juice, mead, beer, or pale ale | Friendship (2 hearts) | OneShot, no deadline | Implemented |
| Farming | Dehydrator Request | Mail (on first Dehydrator recipe) | Any adult human you've met | Ship a scaling number of dried mushrooms or dried fruit | Friendship (2 hearts) | OneShot, no deadline | Implemented |
| Fishing | Fish Smoker Request | Mail (on first Fish Smoker recipe) | Any adult human you've met | Ship a scaling number of smoked fish (any kind) | Friendship (2 hearts) | OneShot, no deadline | Implemented |
| Farming | Quality Crop Delivery | Daily board | Any | Deliver X Gold-quality seasonal crops | Gold (Basic-Intermediate) + Friendship (Basic) | Cooldown 3d, Farming 4+ | Implemented (9.5b) |
| Fishing | Quality Fish Delivery | Daily board | Willy | Deliver X Gold-quality fish of a specific type | Gold (Basic-Intermediate) + Friendship (Basic) | Cooldown 3d, Fishing 4+ | Implemented (9.5b) |
| Festival | Rainbow Platter (Trout Derby) | Summer 20 | Gus / Pika / Rosa / Celestine | Catch FestivalFishQty Rainbow Trout | Recipe + shop discount on the dish (Gus saves only) | DateLocked, OneTimePerYear | Implemented (9.5e) |
| Fishing | Rainy Day Catch | Daily board / mail | Willy / Blair / Carmen | Catch fish that only spawn in rain (runtime weather gate) | Gold (Intermediate) + rare tackle | Cooldown 5d, mail trigger when forecast is rain | Implemented (9.5e) |
| Foraging | Rare Forage Hunt | Daily board | Any | Gather rare forage items | Gold (Intermediate) + 10x random seasonal seeds | Cooldown 4d, Foraging 5+ | Implemented (9.5a) |
| Mining | Rare Material Request | Daily board | Clint | Deliver Iridium Bars or rare gems | Gold (Advanced) + 3x Artifact Trove or 5x gems | Cooldown 7d, Mining 7+ | Implemented (9.5a) |
| Animal | Robin's Silo Offer | 2 days after first Coop/Barn (no Silo) (mail) | Robin | Bring one of {100 Stone, 10 Clay, 5 Copper Bar} | Gold rebate (covers silo cost) + Friendship (Basic) | OneTime; real free-silo-build hook deferred | Implemented (9.5f) |
| Fishing | Seafood Night | Daily board | Gus / Pika (RSV) / Rosa (ESV) / Celestine (VMV) | Catch FishHaulLargeQty+ of one edible non-poisonous seasonal fish (Pufferfish excluded) | Gold (fish-premium scaled, very high) + Tier 3 multi-day chain consequence to ecology pool + Linus | Cooldown 10d, requires Fishing 8 | Implemented (9c) |
| Festival | Secret Gift Hint | Winter 22 | Lewis | Hint about your assigned Winter Star recipient | Information (preference hint) | DateLocked, OneTimePerYear, opt-out via config | Implemented (9.5a) |
| Mining | Skull Cavern Deep Dive | Adventurer's Guild board | Marlon | Reach floor X in Skull Cavern and ship a Mining-scaled ore-only haul | Mail-delivered Radioactive Bars (1 per 5 ores, min 2) | Cooldown Long, requires Skull Cavern unlocked, configurable max floor | Implemented |
| Mining | Mines Deep Dive | Adventurer's Guild board | Marlon | Reach floor X in The Mines (max 120) and deliver an ore/stone haul | Gold (Intermediate) + bars matching the floor band | Cooldown 14d, requires deepestMineLevel >= 5 | Implemented |
| Fishing | Size-Specific Overpopulation | Daily board | Demetrius | Catch X fish at or above a size threshold (Small/Medium/Large bucket) | Gold (Intermediate) + 25x Bait | Cooldown 4d, OnePerGiver, requires Fishing 3 | Implemented (9.5e) |
| Seasonal | Spring Cleaning | Daily board | Any | Clear weeds around town | Friendship (Basic) | Spring only, Cooldown 5d | Implemented (9.5g) |
| Festival | SquidFest Showcase | Winter 12 | Gus / Pika / Rosa / Celestine | Catch FestivalFishQty Squid | Recipe + shop discount on the dish (Gus saves only) | DateLocked, OneTimePerYear | Implemented (9.5e) |
| Festival | Wrapping Paper | Winter 20 | Lewis | Ship Paper and Tape | Book of Stars | DateLocked, OneTimePerYear, requires Si.ExtraCraftingMaterials | Implemented (9.5a) |
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
- **Mail Services Mod and the Forage with Linus quest.** Forage with Linus tracks gifts to unique recipients via vanilla's `Quest.OnItemOfferedToNpc` hook. MSM bypasses that hook because it delivers via the mailbox rather than an in-person interaction, so mailed forage gifts don't tick the quest counter. Hand-deliver the gifts for now. Same fix path as above — once an MSM compatibility hook lands, mailed gifts will count automatically.
- **Friendship-decay / friendship-clamp mods.** Negative consequences (Massive Harvest, Weekly Specials, Medium Fishing Haul, Seafood Night, Monster Parts) call vanilla's `Game1.player.changeFriendship`. Mods that intercept that call to prevent friendship loss — CJB Cheats Menu's "No friendship decay" option, similar tuning mods — also neutralize the negative side of these consequences. Loved-side friendship gains still land. If you want the full trade-off these quests are designed around, turn that option off in CJB. The friendship deltas are logged at Debug level so you can verify the call fired even if the value didn't change.

## Notes

- Framework engine code (registry, pipeline, billboard, condition evaluator, reward applier, item resolver, NPC dispatch, vanilla wrappers) lives in the [framework mod](../MoreQuestsFramework/README.md) — this repo is content only.
- Mail prefix for in-world quest letters is `RafiaBee.MoreQuestsFramework.` (the framework owns the routing).
- Run `mq_refresh` in the SMAPI console to re-roll the daily board without reloading the save.
