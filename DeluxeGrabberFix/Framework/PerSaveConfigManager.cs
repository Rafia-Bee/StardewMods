using StardewModdingAPI;

namespace DeluxeGrabberFix.Framework;

internal class PerSaveConfigManager
{
    private readonly ModEntry _mod;
    private ModConfig _globalConfig;
    private string _currentSaveFolderName;

    internal PerSaveConfigManager(ModEntry mod)
    {
        _mod = mod;
    }

    internal void SetGlobalConfig(ModConfig config)
    {
        _globalConfig = config;
    }

    internal void OnSaveLoaded()
    {
        _currentSaveFolderName = Constants.SaveFolderName;

        if (_currentSaveFolderName == null)
        {
            _mod.Monitor.Log("SaveFolderName is null, using global config", LogLevel.Warn);
            return;
        }

        var path = GetPerSaveConfigPath();
        var perSave = _mod.Helper.Data.ReadJsonFile<ModConfig>(path);

        if (perSave != null)
        {
            _mod.Config = perSave;
            _mod.LogDebug($"Loaded per-save config from {path}");
        }
        else
        {
            _mod.Config = _globalConfig.Clone();
            SavePerSaveConfig();
            _mod.Monitor.Log($"Created per-save config from global defaults for {_currentSaveFolderName}", LogLevel.Info);
        }
    }

    internal void SaveActiveConfig()
    {
        if (_currentSaveFolderName != null)
            SavePerSaveConfig();
        else
            SaveGlobalConfig();
    }

    internal void SavePerSaveConfig()
    {
        if (_currentSaveFolderName == null)
            return;

        _mod.Helper.Data.WriteJsonFile(GetPerSaveConfigPath(), _mod.Config);
    }

    internal void SaveGlobalConfig()
    {
        _globalConfig = _mod.Config;
        _mod.Helper.WriteConfig(_mod.Config);
    }

    internal void OnReturnedToTitle()
    {
        _currentSaveFolderName = null;
        _mod.Config = _globalConfig;
    }

    private string GetPerSaveConfigPath()
    {
        return $"configs/{_currentSaveFolderName}.json";
    }
}
