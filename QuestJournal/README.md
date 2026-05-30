# Quest Journal

A SMAPI mod for Stardew Valley that adds a quest journal that has a full quest list, a details panel, action buttons (complete, cancel, postpone), a top-right HUD that pins the quests you care about, custom tabs you build yourself, and a warp helper. It also adds extra features when [More Quests Framework](../MoreQuestsFramework/README.md) is installed.

> Still a work in progress. The core journal is in and being polished.

## What it does

The vanilla quest log is one flat scroll: a title, one objective line, a reward, and that's it. No multi-step view, no way to cancel or complete a quest from the log, no label saying which mod a quest came from, nothing pinned to your screen while you play. Quest Journal fixes all of that.

- **Three-column journal.** A quest list on the left, a details panel in the middle (description, objective or step list, rewards, giver, days left, source), and action buttons on the right.
- **Action buttons.** Cancel a quest you don't want, or postpone a quest's deadline by a week so it doesn't expire on you.
- **Special Orders too.** The Special Orders board quests show up on their own tab with their rewards itemised.
- **Pin to the HUD.** Pin any quest and its current objective shows in the top-right corner while you're out in the world. Click a pinned quest to jump straight to it in the journal. You can drag the pin panel anywhere you like.
- **Custom tabs.** Build your own tabs that filter the list by title, source mod, category, or quest kind. They're saved per save file.
- **Complete button** (optional, off by default). A button that finishes a quest and pays its reward without doing the objective. It's a shortcut, so you turn it on yourself.
- **Warp helper** (optional, off by default). A button that warps you next to a quest's NPC. If a quest touches more than one NPC, you get a little picker.
- **Item helper** (optional, off by default). A button that hands you the item a quest is asking for so you can do the real turn-in, or bumps a fishing quest's catch count.
- **Resize the window.** Scale the whole journal up or down to taste.

## How to open it

- Press **F6** (you can rebind this in the settings).
- If you have Better Game Menu, click the **Quest Journal tab** in the Esc menu.

## Dependencies

**Required**

- **[StardewUI](https://www.nexusmods.com/stardewvalley/mods/43861)** (use Mushymato's fork since it's the most updated one). The journal is drawn with StardewUI, so without it the journal can't open.

**Optional (auto-detected at runtime)**

- **[More Quests Framework](../MoreQuestsFramework/README.md)** (`RafiaBee.MoreQuestsFramework`) **2.3.0 or newer**, bundled in this repo. When it's installed, multi-step Adventure quests show each step with its progress, rewards are itemised properly, and quests are labelled with the mod they came from. Without it, vanilla quests still get the full journal treatment.
- **[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098)**, for an in-game settings page.
- **[Better Game Menu](https://www.nexusmods.com/stardewvalley/mods/32032)**. When it's installed, the Quest Journal tab shows up in the Esc menu.

## Settings

If you have Generic Mod Config Menu installed, there's an in-game settings page. Otherwise edit `Mods/QuestJournal/config.json`.

- **Open journal key** - the hotkey that opens the journal (default F6).
- **Add a game-menu tab** - adds a Quest Journal tab to the Esc menu. Needs a restart to take effect.
- **Show pinned quests on the HUD** - draws your pinned quests in the top-right while you play.
- **Auto-pin new quests** - pins a quest the moment you accept it (does nothing if "Show pinned quests on the HUD" is off).
- **Journal size** - makes the whole window bigger or smaller.
- **Complete button** - adds the "complete quest" button, which finishes a quest and pays its reward without doing the objective. A cheat, so off by default.
- **Item helper** - adds the "get the quest item" button. A cheat, so off by default.
- **Warp helper** - adds the "warp to the NPC" button. A cheat, so off by default.

The journal's colours can also be re-themed by a Content Patcher pack through the `Mods/RafiaBee.QuestJournal/Theme` data asset, so it can match UI recolour mods.

## Known limitations

- **Lookup Anything integration isn't in yet.** The plan was a button that opens Lookup Anything on a quest item or NPC, but Lookup Anything doesn't expose a way for other mods to do that, so it's parked until it does.
- **The font doesn't scale.** Making the journal bigger gives the text more room but keeps it at its normal size (scaling the font breaks word-wrap and click positions), so very large sizes just have roomier boxes.
- **Multiplayer.** Pins and custom tabs are saved per player but aren't synced between players. Fine for single-player; untested in multiplayer.
