using System;
using System.Collections;
using System.Collections.Generic;
using StardewValley;

namespace BiggerAutoGrabber.Framework;

internal class InventorySlice : IList<Item>
{
    private readonly IList<Item> _source;
    private readonly int _windowSize;
    private int _scrollRow;
    private const int Cols = 12;

    public InventorySlice(IList<Item> source, int windowSize)
    {
        _source = source;
        _windowSize = windowSize;
    }

    public IList<Item> Source => _source;

    public int ScrollRow
    {
        get => Math.Clamp(_scrollRow, 0, MaxScrollRow);
        set => _scrollRow = Math.Clamp(value, 0, MaxScrollRow);
    }

    public int MaxScrollRow
    {
        get
        {
            int totalRows = (int)Math.Ceiling(_source.Count / (double)Cols);
            int visibleRows = _windowSize / Cols;
            return Math.Max(0, totalRows - visibleRows);
        }
    }

    public bool CanScrollUp => ScrollRow > 0;
    public bool CanScrollDown => ScrollRow < MaxScrollRow;

    private int Offset => ScrollRow * Cols;

    public Item this[int index]
    {
        get
        {
            int real = index + Offset;
            return real < _source.Count ? _source[real] : null;
        }
        set
        {
            int real = index + Offset;
            if (real < _source.Count)
                _source[real] = value;
        }
    }

    public int Count => _windowSize;
    public bool IsReadOnly => false;

    public void Add(Item item) => _source.Add(item);
    public void Clear() => _source.Clear();
    public bool Contains(Item item) => _source.Contains(item);

    public void CopyTo(Item[] array, int arrayIndex)
    {
        for (int i = 0; i < _windowSize; i++)
            array[arrayIndex + i] = this[i];
    }

    public int IndexOf(Item item)
    {
        for (int i = 0; i < _windowSize; i++)
        {
            if (ReferenceEquals(this[i], item))
                return i;
        }
        return -1;
    }

    public void Insert(int index, Item item) => _source.Insert(index + Offset, item);
    public bool Remove(Item item) => _source.Remove(item);
    public void RemoveAt(int index) => _source.RemoveAt(index + Offset);

    public IEnumerator<Item> GetEnumerator()
    {
        for (int i = 0; i < _windowSize; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
