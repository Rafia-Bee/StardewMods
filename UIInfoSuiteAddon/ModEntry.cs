#nullable enable
using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using UIInfoSuiteAddon.Framework;

namespace UIInfoSuiteAddon;

public class ModEntry : Mod
{
    private readonly BirthdayLookupOverlay _overlay = new();

    public override void Entry(IModHelper helper)
    {
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
        helper.Events.Display.RenderedHud += OnRenderedHud;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var harmony = new Harmony(ModManifest.UniqueID);
        if (!BirthdayIconPatch.Apply(harmony, Monitor))
            Monitor.Log("Birthday lookup integration could not be initialized.", LogLevel.Warn);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        if (!Game1.onScreenMenus.Contains(_overlay))
            Game1.onScreenMenus.Add(_overlay);
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        Game1.onScreenMenus.Remove(_overlay);
        _overlay.HoveredNpc = null;
    }

    private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
    {
        if (BirthdayIconPatch.LastUpdateTick != Game1.ticks)
        {
            _overlay.HoveredNpc = null;
            return;
        }

        var npcs = BirthdayIconPatch.CurrentNPCs;
        var icons = BirthdayIconPatch.CurrentIcons;

        if (npcs == null || icons == null || npcs.Count == 0 || icons.Count == 0)
        {
            _overlay.HoveredNpc = null;
            return;
        }

        int mouseX = Game1.getMouseX();
        int mouseY = Game1.getMouseY();

        NPC? hovered = null;
        int count = Math.Min(npcs.Count, icons.Count);
        for (int i = 0; i < count; i++)
        {
            if (icons[i].containsPoint(mouseX, mouseY))
            {
                hovered = npcs[i];
                break;
            }
        }

        _overlay.HoveredNpc = hovered;
    }
}
