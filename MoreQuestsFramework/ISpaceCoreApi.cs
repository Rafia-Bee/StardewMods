using System;
using StardewValley;

namespace MoreQuestsFramework;

// Registered types MUST be public and carry [XmlType("Mods_...")].
public interface ISpaceCoreApi
{
    void RegisterSerializerType(Type type);

    // Base level of a custom (SpaceCore-registered) skill for the given farmer.
    // Returns 0 for skills SpaceCore doesn't know about. Used for the cooking and
    // archaeology skill mods, which all store their levels in SpaceCore.
    int GetLevelForCustomSkill(Farmer farmer, string skill);
}
