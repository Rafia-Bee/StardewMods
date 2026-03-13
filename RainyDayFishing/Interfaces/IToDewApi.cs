#nullable enable
using System;
using System.Collections.Generic;

namespace RainyDayFishing.Interfaces;

public interface IToDewApi
{
    uint AddOverlayDataSource(IToDewOverlayDataSource src);
    void RemoveOverlayDataSource(uint handle);
    void RefreshOverlay();
}

public interface IToDewOverlayDataSource
{
    string GetSectionTitle();
    List<(string text, bool isBold, Action? onDone)> GetItems(int limit);
}
