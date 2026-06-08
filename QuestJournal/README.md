# Quest Journal

A SMAPI mod for Stardew Valley that adds a quest journal that has a full quest list, a details panel, action buttons (complete, cancel, postpone), a top-right HUD that pins the quests you care about, custom tabs you build yourself, a search bar, and a warp helper. It also adds extra features when [More Quests Framework](../MoreQuestsFramework/README.md) is installed.

## What it does

The vanilla quest log is one flat scroll: a title, one objective line, a reward, and that's it. No multi-step view, no way to cancel or complete a quest from the log, no label saying which mod a quest came from, nothing pinned to your screen while you play. Quest Journal fixes all of that.

- **Three-column journal.** A quest list on the left, a details panel in the middle (description, objective or step list, rewards, giver, days left, source), and action buttons on the right.
- **Action buttons.** Cancel a quest you don't want, or postpone a quest's deadline by a week so it doesn't expire on you.
- **Claim your reward.** Finish a quest by playing it and the gold waits for you to pick up. The finished quest shows at the top of the Active tab with a little gold coin next to its name and a Claim Reward button. Click it to take the gold (you'll get a little popup with the amount), and the quest moves to the Completed tab. Special Orders work the same way: a finished order with gold waiting shows at the top of the Special Orders tab with the coin and a Claim Reward button. No more dipping into the old quest log just to grab your pay.
- **Stops the flashing "!".** When you get a new quest, the game's quest button flashes a "!" until you open the old quest log. Since you're using this journal instead, opening it counts as checking, so the flashing stops. You can turn this off in the settings if you'd rather keep it. (A quest that's done but still owes you gold keeps flashing until you collect, on purpose.)
- **Honest deadlines.** The days-left line shows the real deadline. A quest or special order is removed the morning after its last day, so the journal says "Final day!" on the day you have to finish it, "Due tomorrow!" when the deadline is the next day, and counts down the real days after that. This reads one lower than the game's own number on purpose (the game's "2 days" is really "due tomorrow"), because that's how much time you actually have.
- **Special Orders too.** The Special Orders board quests show up on their own tab with their rewards itemised.
- **Pin to the HUD.** Pin any quest and its current objective shows in the top-right corner while you're out in the world. Click a pinned quest to jump straight to it in the journal, or hover it to see all the steps still left to do (handy for quests with more than one step). You can drag the pin panel anywhere you like, and fade it down with an opacity slider if it's in the way.
- **See your pins at a glance.** Pinned quests get a little pushpin next to their name in the list and float to the top so they're easy to find (you can turn the float off if you'd rather keep the normal order). There's a key to pin or unpin (P by default): in the journal it works on the quest you have selected, and while you're playing you can point at a pinned quest in the on-screen box and press it to unpin that one. You can also set a key to quickly show or hide the on-screen box.
- **Custom tabs.** Build your own tabs that filter the list by title, source mod, category, quest kind, or deadline. Saved per save file. Each box takes more than one word: separate them with commas to match any of them ("RSV, More Quests" in Source shows quests from either), and start a word with "!" to leave it out ("!Robin, crop order" in Title shows crop order quests that aren't from Robin). The Deadline box takes a number (exactly that many days left), "<=5" (5 or fewer), "None", a range like "1-3", a comparison like ">28", or a mix like "<=3, !2" (3 or fewer except exactly 2).
- **Sort the list.** A Sort dropdown at the top left orders the list by deadline (soonest first), name, giver, source, or category. Your choice is remembered and used on every tab.
- **Search bar.** Type in the box at the top to filter the list by quest title.
- **Complete button** (optional, off by default). A button that finishes a quest and pays its reward without doing the objective. Works on special orders too, including the ones that ask you to drop items in a box. It's a shortcut, so you turn it on yourself.
- **Warp helper** (optional, off by default). A button that warps you next to a quest's NPC. If a quest touches more than one NPC, you get a little picker.
- **Item helper** (optional, off by default). A button that hands you the item a quest is asking for so you can do the real turn-in, or bumps a fishing quest's catch count.
- **Resize the window.** Scale the whole journal up or down to taste.

## How to open it

- Press **F6** (you can rebind this in the settings).
- If you have Better Game Menu, click the **Quest Journal tab** in the Esc menu.
- If you have Iconic Framework, click the **journal icon** on your toolbar.

## Playing with a controller

The journal works with a gamepad. Once it's open you can move around it with the stick or d-pad, just like the game's own menus. To switch between tabs, use the shoulder buttons (LB/RB) or the triggers (LT/RT). To make a new custom tab, press X; to edit the custom tab you have selected, press Y. All of these can be rebound in the settings.

If you have quests pinned to the HUD, you can open one with a controller too: point at it with the right stick and press A.

To open it with a controller, the easiest way is [Star Control](https://www.nexusmods.com/stardewvalley/mods/27562). If you have it, the journal shows up in its radial menu (this works through Iconic Framework, so you'll want both installed). You can also bind the open-journal key to a controller button in the settings, or use the Quest Journal tab in the Esc menu.

## Dependencies

**Required**

- **[StardewUI](https://www.nexusmods.com/stardewvalley/mods/43861)** (use Mushymato's fork since it's the most updated one). The journal is drawn with StardewUI, so without it the journal can't open.

**Optional (auto-detected at runtime)**

- **[More Quests Framework](../MoreQuestsFramework/README.md)** (`RafiaBee.MoreQuestsFramework`) **2.4.0 or newer**, bundled in this repo. When it's installed, multi-step Adventure quests show each step with its progress, rewards are itemised properly, and quests are labelled with the mod they came from. Without it, vanilla quests still get the full journal treatment.
- **[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)**, for an in-game settings page.
- **[Better Game Menu](https://www.nexusmods.com/stardewvalley/mods/32032)**. When it's installed, the Quest Journal tab shows up in the Esc menu.

## Settings

If you have Generic Mod Config Menu installed, there's an in-game settings page. Otherwise edit `Mods/QuestJournal/config.json`.

- **Open journal key** - the hotkey that opens the journal (default F6).
- **Pin or unpin key** - pins or unpins a quest (default P). In the journal it works on the quest you have selected; while playing, point at a pinned quest on the HUD and press it to unpin that one.
- **Show or hide pinned quests key** - shows or hides the on-screen pinned quests box while you play. Not set to a key to start with, so pick one here if you want it.
- **Controller buttons** - while the journal is open: previous tab (default LB/LT), next tab (default RB/RT), edit the selected custom tab (default Y), and make a new tab (default X). All rebindable.
- **Add a game-menu tab** - adds a Quest Journal tab to the Esc menu. This only works if you have Better Game Menu installed (without it the tab is left off so it doesn't get in the way of other menu mods). Needs a restart to take effect.
- **Show pinned quests on the HUD** - draws your pinned quests in the top-right while you play.
- **Pinned quests at the top** - floats your pinned quests to the top of the list so they're easy to find. On by default. Turn it off to leave the list in its normal order.
- **Show all steps on hover** - some quests have more than one step, but the HUD only shows the first. Rest the mouse on a pinned quest to see all the steps still left to do in a little popup, without opening the journal. On by default.
- **Auto-pin new quests** - pins a quest the moment you accept it (does nothing if "Show pinned quests on the HUD" is off).
- **Clear the new-quest mark on open** - opening this journal stops the game's quest button from flashing its "new quest" mark, since opening counts as checking. On by default. A quest that's done but still owes you gold keeps flashing until you collect.
- **Debug logging** - writes extra status messages to the SMAPI console (handy for bug reports). Warnings and errors always show.
- **Sort quests by** - the order the list starts in (deadline, name, giver, source, or category). You can also change it from the dropdown inside the journal.
- **Journal size** - makes the whole window bigger or smaller.
- **Pinned quests opacity** - how see-through the pinned quests panel is on the HUD. Turn it down to let it fade into the background.
- **Complete button** - adds the "complete quest" button, which finishes a quest and pays its reward without doing the objective. Covers special orders too. A cheat, so off by default.
- **Item helper** - adds the "get the quest item" button. A cheat, so off by default.
- **Warp helper** - adds the "warp to the NPC" button. A cheat, so off by default.

The journal's colours can also be re-themed by a Content Patcher pack through the `Mods/RafiaBee.QuestJournal/Theme` data asset, so it can match UI recolour mods.

## Known limitations

- **Lookup Anything integration isn't in yet.** The plan was a button that opens Lookup Anything on a quest item or NPC, but Lookup Anything doesn't expose a way for other mods to do that, so it's parked until it does.
- **The font doesn't scale.** Making the journal bigger gives the text more room but keeps it at its normal size (scaling the font breaks word-wrap and click positions), so very large sizes just have roomier boxes.
- **Multiplayer.** Pins and custom tabs are saved per player but aren't synced between players. Fine for single-player; untested in multiplayer.
