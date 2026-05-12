using System;
using System.Reflection;
using StardewValley;

namespace MoreQuestsFramework;

/// Reflective bridge to SpaceCore's `Skills.GetSkillLevel(Farmer, string)`. Lets quest
/// generators read SpaceCore custom-skill levels (e.g. the Cooking Skill mod's
/// `spacechase0.Cooking`) without taking a hard dependency on SpaceCore. Returns 0 when
/// SpaceCore isn't installed, the method isn't resolvable, or the skill id is unknown.
public static class SpaceCoreSkills
{
    private static Func<Farmer, string, int>? _getSkillLevel;
    private static bool _resolved;

    public static int GetLevel(Farmer player, string skillId)
    {
        if (player == null || string.IsNullOrEmpty(skillId))
            return 0;
        if (!_resolved)
            Resolve();
        try
        {
            return _getSkillLevel?.Invoke(player, skillId) ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void Resolve()
    {
        _resolved = true;
        Type? skills = Type.GetType("SpaceCore.Skills, SpaceCore", throwOnError: false);
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

        MethodInfo? method = skills?.GetMethod(
            "GetSkillLevel",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(Farmer), typeof(string) },
            modifiers: null);

        if (method != null)
            _getSkillLevel = (Func<Farmer, string, int>)Delegate.CreateDelegate(typeof(Func<Farmer, string, int>), method);
    }
}
