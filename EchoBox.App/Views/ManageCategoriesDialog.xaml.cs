using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EchoBox.Core.Models;
using EchoBox.Core.Services;

namespace EchoBox.App.Views;

public partial class ManageCategoriesDialog : ContentDialog
{
    private readonly IconStorageService _iconStorage;
    private readonly ObservableCollection<IconCategory> _categories;
    private readonly List<IconItem> _allIcons;
    public bool CategoriesChanged { get; private set; }

    public ManageCategoriesDialog(IconStorageService iconStorage, List<IconCategory> categories, List<IconItem> allIcons)
    {
        InitializeComponent();
        _iconStorage = iconStorage;
        _categories = new ObservableCollection<IconCategory>(categories);
        _allIcons = allIcons;
        CategoriesListView.ItemsSource = _categories;
    }

    private void CategoriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoriesListView.SelectedItem is IconCategory selectedCat)
        {
            CategoryNameTextBox.Text = selectedCat.Name;
            ActionButton.Content = "Rename";
        }
        else
        {
            CategoryNameTextBox.Text = string.Empty;
            ActionButton.Content = "Add Category";
        }
    }

    private void CategoryNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CategoriesListView.SelectedItem == null)
        {
            ActionButton.Content = "Add Category";
        }
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        string text = CategoryNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        if (CategoriesListView.SelectedItem is IconCategory selectedCat)
        {
            // Rename mode
            if (!string.Equals(selectedCat.Name, text, StringComparison.Ordinal))
            {
                _iconStorage.RenameCategory(selectedCat, text, _allIcons);
                CategoriesChanged = true;

                int idx = _categories.IndexOf(selectedCat);
                if (idx >= 0)
                {
                    _categories[idx] = selectedCat;
                }
            }

            CategoriesListView.SelectedItem = null;
            CategoryNameTextBox.Text = string.Empty;
            ActionButton.Content = "Add Category";
        }
        else
        {
            // Add Category mode
            var newCat = new IconCategory
            {
                Id = Guid.NewGuid().ToString(),
                Name = text
            };
            _categories.Add(newCat);
            _iconStorage.SaveCategories(_categories.ToList());
            
            string catDir = _iconStorage.GetCategoryFolderPath(newCat.Name);
            if (!Directory.Exists(catDir))
            {
                Directory.CreateDirectory(catDir);
            }

            CategoriesChanged = true;
            CategoryNameTextBox.Text = string.Empty;
        }
    }

    private async void DeleteCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is IconCategory cat)
        {
            if (CategoriesListView.SelectedItem == cat)
            {
                CategoriesListView.SelectedItem = null;
            }

            var catList = _categories.ToList();
            var conflicts = _iconStorage.DeleteCategory(cat, catList, _allIcons);
            _categories.Remove(cat);
            CategoriesChanged = true;

            if (conflicts != null && conflicts.Count > 0)
            {
                var conflictItems = conflicts.Select(c => new ImportConflictItem
                {
                    ImportedFilePath = c.CategoryFilePath,
                    OriginalName = c.Name,
                    ExistingIcon = new IconItem { Name = c.Name, FilePath = c.RootFilePath },
                    NewName = c.Name
                }).ToList();

                var conflictDialog = new ImportConflictDialog(conflictItems)
                {
                    XamlRoot = XamlRoot
                };

                if (await conflictDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    foreach (var item in conflictDialog.Items)
                    {
                        try
                        {
                            string finalName = string.IsNullOrWhiteSpace(item.NewName)
                                ? item.OriginalName
                                : item.NewName.Trim();

                            bool isNameSame = string.Equals(finalName, item.ExistingIcon.Name, StringComparison.OrdinalIgnoreCase);
                            string destPath = Path.Combine(_iconStorage.IcoDirectory, isNameSame ? $"{item.ExistingIcon.Name}.ico" : $"{finalName}.ico");

                            if (File.Exists(item.ImportedFilePath))
                            {
                                File.Copy(item.ImportedFilePath, destPath, overwrite: isNameSame);
                                try { File.Delete(item.ImportedFilePath); } catch { }
                            }
                        }
                        catch
                        {
                            // Skip failed file
                        }
                    }
                }
            }
        }
    }
}
