using System;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;

namespace DeluxeGrabberFix.Framework;

// Reflective bridge to SpaceCore's static `Skills.AddExperience(Farmer, string, int)`.
// We don't take a hard dependency on SpaceCore so the mod still loads when it isn't
// installed; ModCompat XP grants become no-ops in that case.
internal static class SpaceCoreIntegration
{
    private const string SpaceCoreModId = "spacechase0.SpaceCore";
    public const string ArchaeologySkillId = "moonslime.Archaeology";
    public const string ArchaeologyModId = "moonslime.ArchaeologySkill";

    private static Action<Farmer, string, int> _addExperience;

    public static bool SpaceCoreLoaded { get; private set; }
    public static bool ArchaeologyLoaded { get; private set; }

    public static void Initialize(IModRegistry registry, ModEntry mod)
    {
        SpaceCoreLoaded = registry.IsLoaded(SpaceCoreModId);
        ArchaeologyLoaded = registry.IsLoaded(ArchaeologyModId);

        if (!SpaceCoreLoaded)
            return;

        try
        {
            Type skills = Type.GetType("SpaceCore.Skills, SpaceCore", throwOnError: false);
            if (skills == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "SpaceCore")
                        continue;
                    skills = asm.GetType("SpaceCore.Skills");
                    if (skills != null)
                        break;
                }
            }

            MethodInfo method = skills?.GetMethod(
                "AddExperience",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Farmer), typeof(string), typeof(int) },
                modifiers: null);

            if (method != null)
                _addExperience = (Action<Farmer, string, int>)Delegate.CreateDelegate(typeof(Action<Farmer, string, int>), method);
        }
        catch (Exception ex)
        {
            mod.LogDebug($"SpaceCore reflection failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (_addExperience == null)
            mod.LogDebug("SpaceCore loaded but Skills.AddExperience could not be resolved; custom skill XP will not be granted.");
    }

    public static void AddExperience(Farmer player, string skillId, int amount)
    {
        if (player == null || amount <= 0 || _addExperience == null)
            return;
        _addExperience(player, skillId, amount);
    }
}
