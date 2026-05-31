using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;

namespace QuestJournal.Integrations;

// Registers a single toolbar icon with Iconic Framework. One registration
// covers two surfaces at once: the on-screen toolbar icon (mouse / keyboard)
// and, because Star Control reads Iconic Framework's icons, an entry in its
// controller radial menu. Iconic Framework is optional, so this no-ops cleanly
// when it isn't installed.
internal static class IconicFrameworkIntegration
{
    private const string IconicModId = "furyx639.ToolbarIcons";

    public static void Register(
        IModHelper helper,
        IManifest manifest,
        string iconAssetPath,
        Func<string> getTitle,
        Func<string> getDescription,
        Action onClick)
    {
        if (!helper.ModRegistry.IsLoaded(IconicModId))
            return;

        var api = helper.ModRegistry.GetApi<IIconicFrameworkApi>(IconicModId);
        if (api == null)
            return;

        api.AddToolbarIcon(
            id: manifest.UniqueID,
            texturePath: iconAssetPath,
            sourceRect: new Rectangle(0, 0, 16, 16),
            getTitle: getTitle,
            getDescription: getDescription,
            onClick: onClick);

        ModEntry.DebugLog(
            "Registered Quest Journal with Iconic Framework (also shows in Star Control's radial menu).");
    }
}
