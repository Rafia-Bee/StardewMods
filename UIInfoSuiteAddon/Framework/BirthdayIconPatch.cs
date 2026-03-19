#nullable enable
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace UIInfoSuiteAddon.Framework;

internal static class BirthdayIconPatch
{
    private static FieldInfo? _npcsField;
    private static FieldInfo? _iconsField;

    internal static List<NPC>? CurrentNPCs;
    internal static List<ClickableTextureComponent>? CurrentIcons;
    internal static int LastUpdateTick = -1;

    internal static bool Apply(Harmony harmony, IMonitor monitor)
    {
        var type = AccessTools.TypeByName("UIInfoSuite2Alt.UIElements.ShowBirthdayIcon");
        if (type == null)
        {
            monitor.Log("Could not find ShowBirthdayIcon type.", LogLevel.Warn);
            return false;
        }

        var method = AccessTools.Method(type, "EnqueueBirthdayIcons");
        if (method == null)
        {
            monitor.Log("Could not find EnqueueBirthdayIcons method.", LogLevel.Warn);
            return false;
        }

        _npcsField = AccessTools.Field(type, "_birthdayNPCs");
        _iconsField = AccessTools.Field(type, "_birthdayIcons");

        if (_npcsField == null || _iconsField == null)
        {
            monitor.Log("Could not find birthday NPC/icon fields.", LogLevel.Warn);
            return false;
        }

        harmony.Patch(method, postfix: new HarmonyMethod(typeof(BirthdayIconPatch), nameof(Postfix)));
        return true;
    }

    private static void Postfix(object __instance)
    {
        try
        {
            var npcsPerScreen = (PerScreen<List<NPC>>)_npcsField!.GetValue(__instance)!;
            var iconsPerScreen = (PerScreen<List<ClickableTextureComponent>>)_iconsField!.GetValue(__instance)!;

            CurrentNPCs = npcsPerScreen.Value;
            CurrentIcons = iconsPerScreen.Value;
            LastUpdateTick = Game1.ticks;
        }
        catch
        {
            CurrentNPCs = null;
            CurrentIcons = null;
        }
    }
}
