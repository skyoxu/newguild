using System;
using Godot;

namespace Game.Godot.Scripts.UI.Components;

/// <summary>
/// Reusable list panel component.
/// Intended for adapter/UI layer usage per ADR-0018.
/// </summary>
public partial class ListPanel : PanelContainer
{
    [Export]
    public string TitleText { get; set; } = "List";

    private Label _titleLabel = default!;
    private ItemList _itemsList = default!;
    private string[]? _pendingItems;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("Root/Title");
        _itemsList = GetNode<ItemList>("Root/Items");
        Apply();

        if (_pendingItems is not null)
        {
            SetItemsFromStrings(_pendingItems);
            _pendingItems = null;
        }
    }

    public void Refresh()
    {
        ApplyIfReady();
    }

    public void SetTitle(string title)
    {
        TitleText = title;
        ApplyIfReady();
    }

    public void SetItemsFromStrings(string[] items)
    {
        if (items is null)
        {
            ClearItems();
            return;
        }

        if (!IsNodeReady())
        {
            var copy = new string[items.Length];
            Array.Copy(items, copy, items.Length);
            _pendingItems = copy;
            return;
        }

        _itemsList.Clear();
        foreach (var item in items)
            _itemsList.AddItem(item);
    }

    public void SetItems(global::Godot.Collections.Array items)
    {
        if (items is null)
        {
            ClearItems();
            return;
        }

        var values = new string[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var value = items[i];
            values[i] = value.VariantType == Variant.Type.Nil ? string.Empty : value.ToString();
        }

        SetItemsFromStrings(values);
    }

    public void ClearItems()
    {
        if (!IsNodeReady())
        {
            _pendingItems = Array.Empty<string>();
            return;
        }

        _itemsList.Clear();
    }

    private void Apply()
    {
        _titleLabel.Text = TitleText;
    }

    private void ApplyIfReady()
    {
        if (!IsNodeReady())
            return;

        Apply();
    }
}
