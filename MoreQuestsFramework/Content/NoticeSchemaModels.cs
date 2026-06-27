using System.Collections.Generic;
using MoreQuestsFramework.Api;

namespace MoreQuestsFramework.Content;

// Shape of a consumer mod's notices.json, mirroring BoardPackDocument / QuestPackDocument.
// A DLL mod reads this and injects each entry into the Notices content asset; CP-only authors
// EditData the asset directly instead.
public sealed class NoticePackDocument
{
    public string Schema { get; set; } = "1.0";
    public List<NoticeDef> Notices { get; set; } = new();
}
