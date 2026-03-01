using System;
using StardewModdingAPI;

namespace BulkDerbyRewards;

/// <summary>Generic Mod Config Menu API surface used by this mod.</summary>
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string> tooltip = null,
        string fieldId = null);
}
