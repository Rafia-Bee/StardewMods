using System;
using Microsoft.Xna.Framework;

namespace QuestJournal.Integrations;

// Local duck-typed mirror of Iconic Framework's (furyx639.ToolbarIcons)
// IIconicFrameworkApi. SMAPI's ModRegistry.GetApi proxies this against the real
// one, so we don't reference the mod's assembly. Only the one AddToolbarIcon
// overload we use is declared; the signature must match the real one exactly.
public interface IIconicFrameworkApi
{
    // Adds (or replaces) a toolbar icon. The same registration is read by Star
    // Control and shown in its controller radial menu, so getTitle is what the
    // radial menu labels the entry with and onClick is what both surfaces run.
    void AddToolbarIcon(
        string id,
        string texturePath,
        Rectangle? sourceRect,
        Func<string>? getTitle,
        Func<string>? getDescription,
        Action onClick,
        Action? onRightClick = null);
}
