using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using CommunityToolkit.WinUI.Collections;
using EchoBox.Core.Models;
using EchoBox.Core.Services;
using EchoBox.Engine.Services;

namespace EchoBox.App.Views;

public partial class IconsPage : Page
{
    private readonly IconStorageService _iconStorage = new();
    private readonly IcoConverter _converter = new();
    private readonly SettingsService _settingsService = new();

    private AppSettings _settings = new();
    private ObservableCollection<IconItem> _iconsList = new();
    private ObservableCollection<IconCategory> _categoriesList = new();
    private ObservableCollection<IconCategoryGroup> _groupedCategories = new();
    private DispatcherTimer? _searchDebounceTimer;

    public IconsPage()
    {
        InitializeComponent();
        Loaded += IconsPage_Loaded;
    }

    private void IconsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = _settingsService.LoadSettings();
        ReloadCategories();
        ReloadIcons();
    }

    private void ReloadCategories()
    {
        var categories = _iconStorage.LoadCategories();
        var sortedCategories = categories.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        _categoriesList = new ObservableCollection<IconCategory>(sortedCategories);

        var filterList = new List<IconCategory>
        {
            new IconCategory { Id = "all", Name = "All Categories" }
        };
        filterList.AddRange(_categoriesList);

        CategoryFilterComboBox.ItemsSource = filterList;
        CategoryFilterComboBox.SelectedIndex = 0;
    }

    private void UpdateIconSortKeys()
    {
        var catMap = _categoriesList.ToDictionary(c => c.Id, c => c.Name);
        var folderCatMap = _categoriesList.ToDictionary(c => c.Name.Trim().ToLowerInvariant(), c => c.Id);

        foreach (var icon in _iconsList)
        {
            if (!string.IsNullOrEmpty(icon.RelativePath))
            {
                string[] parts = icon.RelativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (parts.Length > 1)
                {
                    string folderName = parts[0].Trim().ToLowerInvariant();
                    if (folderCatMap.TryGetValue(folderName, out var catId))
                    {
                        icon.CategoryIds ??= new List<string>();
                        if (!icon.CategoryIds.Contains(catId))
                        {
                            icon.CategoryIds.Add(catId);
                        }
                    }
                }
            }

            var validCat = icon.CategoryIds?.FirstOrDefault(id => catMap.ContainsKey(id));
            if (validCat != null && catMap.TryGetValue(validCat, out var catName) && !string.IsNullOrWhiteSpace(catName))
            {
                icon.DisplayCategoryName = catName;
                icon.CategorySortKey = "0_" + catName;
            }
            else
            {
                icon.DisplayCategoryName = "UnCategorized";
                icon.CategorySortKey = "1_UnCategorized";
            }
        }
    }

    private void ReloadIcons()
    {
        var icons = _iconStorage.LoadIcons();
        _iconsList = new ObservableCollection<IconItem>(icons);
        UpdateIconSortKeys();
        ApplyFilter();
    }

    private async void ImportIconsButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".ico");

        var files = await picker.PickMultipleFilesAsync();
        if (files != null && files.Count > 0)
        {
            var filePaths = files.Select(f => f.Path).ToList();
            var optionsDialog = new ImportOptionsDialog(_categoriesList.ToList(), _iconStorage, initialFiles: filePaths)
            {
                XamlRoot = XamlRoot
            };

            if (await optionsDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await HandlePostImportAsync(optionsDialog.ConflictItems, optionsDialog.SelectedCategories, optionsDialog.ImportedCount);
            }
        }
    }

    private async void ImportFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPicker = new FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
        folderPicker.FileTypeFilter.Add("*");

        var folder = await folderPicker.PickSingleFolderAsync();
        if (folder != null)
        {
            var optionsDialog = new ImportOptionsDialog(_categoriesList.ToList(), _iconStorage, folderPath: folder.Path)
            {
                XamlRoot = XamlRoot
            };

            if (await optionsDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await HandlePostImportAsync(optionsDialog.ConflictItems, optionsDialog.SelectedCategories, optionsDialog.ImportedCount);
            }
        }
    }

    private async System.Threading.Tasks.Task HandlePostImportAsync(List<ImportConflictItem> conflictItems, List<IconCategory> selectedCategories, int initialImportedCount)
    {
        int totalImportedCount = initialImportedCount;

        if (conflictItems != null && conflictItems.Count > 0)
        {
            var dialog = new ImportConflictDialog(conflictItems)
            {
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var existingIcons = _iconStorage.LoadIcons();
                string primaryTargetFolder = selectedCategories.Count > 0
                    ? _iconStorage.GetCategoryFolderPath(selectedCategories[0].Name)
                    : _iconStorage.IcoDirectory;

                Directory.CreateDirectory(primaryTargetFolder);

                foreach (var conflict in dialog.Items)
                {
                    try
                    {
                        string finalName = string.IsNullOrWhiteSpace(conflict.NewName)
                            ? conflict.OriginalName
                            : conflict.NewName.Trim();

                        bool isNameSame = string.Equals(finalName, conflict.ExistingIcon.Name, StringComparison.OrdinalIgnoreCase);

                        string savedPath = await _converter.ConvertAndSaveToIcoAsync(
                            conflict.ImportedFilePath,
                            primaryTargetFolder,
                            preferredName: finalName,
                            overwrite: isNameSame);

                        totalImportedCount++;

                        var iconItem = existingIcons.FirstOrDefault(i => string.Equals(i.Name, finalName, StringComparison.OrdinalIgnoreCase) || string.Equals(i.FilePath, savedPath, StringComparison.OrdinalIgnoreCase));
                        if (iconItem == null)
                        {
                            iconItem = new IconItem
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = finalName,
                                FilePath = savedPath,
                                RelativePath = Path.GetRelativePath(_iconStorage.IcoDirectory, savedPath),
                                CategoryIds = selectedCategories.Select(c => c.Id).ToList()
                            };
                            existingIcons.Add(iconItem);
                        }
                        else
                        {
                            iconItem.FilePath = savedPath;
                            iconItem.RelativePath = Path.GetRelativePath(_iconStorage.IcoDirectory, savedPath);
                            iconItem.CategoryIds ??= new();
                            foreach (var cat in selectedCategories)
                            {
                                if (!iconItem.CategoryIds.Contains(cat.Id))
                                {
                                    iconItem.CategoryIds.Add(cat.Id);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError(ex, $"IconsPage.ImportFiles ({conflict.ImportedFilePath})");
                    }
                }

                _iconStorage.SaveIconsMetadata(existingIcons);
            }
        }

        ReloadCategories();
        ReloadIcons();
        ShowNotificationDialog("Import Complete", $"Successfully processed {totalImportedCount} icon file operation(s).");
    }

    private async void ManageCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ManageCategoriesDialog(_iconStorage, _categoriesList.ToList(), _iconsList.ToList())
        {
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();

        if (dialog.CategoriesChanged)
        {
            ReloadCategories();
            ReloadIcons();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _searchDebounceTimer.Tick += (s, args) =>
        {
            _searchDebounceTimer.Stop();
            ApplyFilter();
        };
        _searchDebounceTimer.Start();
    }

    private void CategoryFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = SearchBox.Text.Trim().ToLowerInvariant();
        var selectedCat = CategoryFilterComboBox.SelectedItem as IconCategory;

        var catMap = _categoriesList.ToDictionary(c => c.Id, c => c.Name);
        var groups = new List<IconCategoryGroup>();
        var categorizedIconIds = new HashSet<string>();

        // 1. Regular categories sorted alphabetically A-Z
        foreach (var cat in _categoriesList.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            var categoryIcons = _iconsList.Where(icon =>
            {
                if (icon.CategoryIds != null && icon.CategoryIds.Contains(cat.Id)) return true;
                if (!string.IsNullOrEmpty(icon.RelativePath))
                {
                    string[] parts = icon.RelativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (parts.Length > 1 && string.Equals(parts[0], cat.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            })
            .GroupBy(icon => !string.IsNullOrEmpty(icon.Name) ? icon.Name.Trim().ToLowerInvariant() : icon.Id)
            .Select(g => g.First())
            .ToList();

            foreach (var icon in categoryIcons)
            {
                categorizedIconIds.Add(icon.Id);
            }

            if (selectedCat != null && selectedCat.Id != "all" && selectedCat.Id != cat.Id)
            {
                continue;
            }

            var matchingIcons = categoryIcons
                .Where(icon => string.IsNullOrEmpty(query) || icon.Name.ToLowerInvariant().Contains(query))
                .OrderBy(icon => icon.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matchingIcons.Count > 0 || (selectedCat != null && selectedCat.Id == cat.Id))
            {
                bool isExpanded = true;
                if (_settings.CategoryExpandedStates != null && _settings.CategoryExpandedStates.TryGetValue(cat.Id, out bool savedState))
                {
                    isExpanded = savedState;
                }

                groups.Add(new IconCategoryGroup
                {
                    CategoryId = cat.Id,
                    Name = cat.Name,
                    IsExpanded = isExpanded,
                    AllIcons = matchingIcons,
                    Icons = isExpanded ? new ObservableCollection<IconItem>(matchingIcons) : new ObservableCollection<IconItem>()
                });
            }
        }

        // 2. UnCategorized group (STRICTLY ONLY icons that DO NOT belong to ANY valid category)
        if (selectedCat == null || selectedCat.Id == "all" || selectedCat.Id == "uncategorized")
        {
            var uncategorizedIcons = _iconsList
                .Where(icon => !categorizedIconIds.Contains(icon.Id))
                .Where(icon => string.IsNullOrEmpty(query) || icon.Name.ToLowerInvariant().Contains(query))
                .OrderBy(icon => icon.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (uncategorizedIcons.Count > 0 || (selectedCat != null && selectedCat.Id == "uncategorized"))
            {
                bool isExpanded = true;
                if (_settings.CategoryExpandedStates != null && _settings.CategoryExpandedStates.TryGetValue("uncategorized", out bool savedState))
                {
                    isExpanded = savedState;
                }

                groups.Add(new IconCategoryGroup
                {
                    CategoryId = "uncategorized",
                    Name = "UnCategorized",
                    IsExpanded = isExpanded,
                    AllIcons = uncategorizedIcons,
                    Icons = isExpanded ? new ObservableCollection<IconItem>(uncategorizedIcons) : new ObservableCollection<IconItem>()
                });
            }
        }

        _groupedCategories = new ObservableCollection<IconCategoryGroup>(groups);
        GroupedCategoryCvs.Source = _groupedCategories;
    }

    private void CategoryHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is IconCategoryGroup group)
        {
            group.IsExpanded = !group.IsExpanded;
            group.Icons.Clear();
            if (group.IsExpanded)
            {
                foreach (var icon in group.AllIcons)
                {
                    group.Icons.Add(icon);
                }
            }

            _settings.CategoryExpandedStates ??= new Dictionary<string, bool>();
            _settings.CategoryExpandedStates[group.CategoryId] = group.IsExpanded;
            _settingsService.SaveSettings(_settings);
        }
    }

    private async void EditIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is IconItem icon)
        {
            var nameBox = new TextBox { Text = icon.Name, Header = "Icon Name" };
            var catListView = new ListView
            {
                Header = "Categories (Select multiple)",
                ItemsSource = _categoriesList,
                DisplayMemberPath = "Name",
                SelectionMode = ListViewSelectionMode.Multiple,
                Height = 150
            };

            foreach (var cat in _categoriesList)
            {
                if (icon.CategoryIds.Contains(cat.Id))
                {
                    catListView.SelectedItems.Add(cat);
                }
            }

            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(nameBox);
            stack.Children.Add(catListView);

            var dialog = new ContentDialog
            {
                Title = "Edit Icon",
                Content = stack,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                icon.Name = nameBox.Text.Trim();
                var selectedCats = catListView.SelectedItems.Cast<IconCategory>().ToList();
                icon.CategoryIds = selectedCats.Select(c => c.Id).ToList();

                _iconStorage.SyncIconToCategoryFolders(icon, selectedCats);
                _iconStorage.SaveIconsMetadata(_iconsList.ToList());

                ReloadIcons();
            }
        }
    }

    private async void DeleteIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is IconItem icon)
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Icon",
                Content = $"Are you sure you want to delete icon '{icon.Name}'?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                _iconStorage.DeleteIcon(icon);
                ReloadIcons();
            }
        }
    }

    private async void ShowNotificationDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }
}
