using System;
using Microsoft.Xna.Framework;

namespace QuestJournal.Integrations;

// Our copy of the Iconic Framework mod's API, just the bit for adding a toolbar icon.
public interface IIconicFrameworkApi
{
    void AddToolbarIcon(
        string id,
        string texturePath,
        Rectangle? sourceRect,
        Func<string>? getTitle,
        Func<string>? getDescription,
        Action onClick,
        Action? onRightClick = null);
}
