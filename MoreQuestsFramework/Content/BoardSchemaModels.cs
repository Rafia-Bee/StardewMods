using System.Collections.Generic;
using MoreQuestsFramework.Api;

namespace MoreQuestsFramework.Content;

/// Plain DTO shapes for `boards.json`. Deserialised directly via
/// SMAPI's `IDataHelper.ReadJsonFile` / `IContentPack.ReadJsonFile` and converted
/// into runtime `BoardDefinition` instances by `BoardPackLoader`.
public sealed class BoardPackDocument
{
    public string Schema { get; set; } = "1.0";
    public List<BoardDefinition> Boards { get; set; } = new();
}
