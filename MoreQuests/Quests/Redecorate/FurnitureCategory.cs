using StardewValley.Objects;

namespace MoreQuests.Quests;

// Maps the quest's objective categories onto Furniture.furniture_type. Categories are
// flavor-named ("lamp", "rug", "chair"); rooms don't matter, only the type does.
internal static class FurnitureCategory
{
    public const string Lamp = "lamp";
    public const string Rug = "rug";
    public const string Chair = "chair";
    public const string Table = "table";

    // The pool a quest draws its objectives from.
    public static readonly string[] All = { Lamp, Rug, Chair, Table };

    public static bool Matches(Furniture f, string category)
    {
        int t = f.furniture_type.Value;
        return category switch
        {
            Lamp => t == Furniture.lamp || t == Furniture.sconce || t == Furniture.torch || t == Furniture.fireplace,
            Rug => t == Furniture.rug,
            Chair => t == Furniture.chair || t == Furniture.armchair,
            Table => t == Furniture.table || t == Furniture.longTable,
            _ => false
        };
    }

    public static string I18nKey(string category) => "quest.redecorate.objective." + category;
}
