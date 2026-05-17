using System.Collections.Generic;
using MoreQuestsFramework.Api;

namespace MoreQuestsFramework.Content;

internal sealed class BoardPackDocument
{
    public string Schema { get; set; } = "1.0";
    public List<BoardDefinition> Boards { get; set; } = new();
}
