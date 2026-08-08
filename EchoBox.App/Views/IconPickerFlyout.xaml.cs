using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Controls;
using EchoBox.Core.Models;
using EchoBox.Core.Services;

namespace EchoBox.App.Views;

public partial class IconPickerFlyout : ContentDialog
{
    private readonly IconStorageService _iconStorage = new();
    private readonly bool _isSingleIconOnly;
    private List<IconItem> _allIcons = new();

    public List<string> SelectedPaths { get; private set; } = new();

    public IconPickerFlyout(bool isSingleIconOnly, List<string>? currentSelectedPaths = null)
    {
        InitializeComponent();
        _isSingleIconOnly = isSingleIconOnly;
        PrimaryButtonClick += ContentDialog_PrimaryButtonClick;

        if (_isSingleIconOnly)
        {
            IconListView.SelectionMode = ListViewSelectionMode.Single;
        }
        else
        {
            IconListView.SelectionMode = ListViewSelectionMode.Multiple;
        }

        LoadIcons(currentSelectedPaths);
    }

    private void LoadIcons(List<string>? currentSelectedPaths)
    {
        _allIcons = _iconStorage.LoadIcons();
        IconListView.ItemsSource = _allIcons;

        if (currentSelectedPaths != null && currentSelectedPaths.Count > 0)
        {
            foreach (var item in _allIcons)
            {
                if (currentSelectedPaths.Contains(item.FilePath))
                {
                    IconListView.SelectedItems.Add(item);
                }
            }
        }
        UpdateCountText();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = SearchBox.Text.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(query))
        {
            IconListView.ItemsSource = _allIcons;
        }
        else
        {
            IconListView.ItemsSource = _allIcons.Where(i => i.Name.ToLowerInvariant().Contains(query)).ToList();
        }
    }

    private void IconListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCountText();
    }

    private void UpdateCountText()
    {
        int count = IconListView.SelectedItems.Count;
        SelectionCountTextBlock.Text = $"{count} icon(s) selected.";
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SelectedPaths = IconListView.SelectedItems.Cast<IconItem>().Select(i => i.FilePath).ToList();
    }
}
