using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EchoBox.Core.Models;
using EchoBox.Core.Services;
using EchoBox.Engine.Services;

namespace EchoBox.App.Views;

public enum ImportProgressStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public partial class ImportProgressItem : INotifyPropertyChanged
{
    private ImportProgressStatus _status = ImportProgressStatus.Pending;

    public string FilePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public ImportProgressStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusGlyph));
                OnPropertyChanged(nameof(StatusForegroundBrush));
            }
        }
    }

    public string StatusGlyph => Status switch
    {
        ImportProgressStatus.InProgress => "\uE712",
        ImportProgressStatus.Completed => "\uE73E",
        ImportProgressStatus.Failed => "\uEA39",
        _ => "\uE896"
    };

    public Brush StatusForegroundBrush => Status switch
    {
        ImportProgressStatus.InProgress => new SolidColorBrush(Colors.DodgerBlue),
        ImportProgressStatus.Completed => new SolidColorBrush(Colors.ForestGreen),
        ImportProgressStatus.Failed => new SolidColorBrush(Colors.Crimson),
        _ => new SolidColorBrush(Colors.Gray)
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public partial class ImportOptionsDialog : ContentDialog
{
    private static readonly HashSet<string> ValidExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".ico", ".svg"
    };

    private readonly IconStorageService _iconStorage;
    private readonly IcoConverter _converter = new();
    private readonly List<IconCategory> _allCategories;
    public ObservableCollection<CategorySelectionItem> CategorySelectionItems { get; } = new();
    public ObservableCollection<ImportProgressItem> ProgressItems { get; } = new();

    public bool IsFolderMode { get; }
    public string? SelectedFolderPath { get; }
    public List<string> SelectedFilesList { get; private set; } = new();
    public List<IconCategory> SelectedCategories => CategorySelectionItems
        .Where(item => item.IsSelected)
        .Select(item => item.Category)
        .ToList();

    public int ImportedCount { get; private set; }
    public List<ImportConflictItem> ConflictItems { get; } = new();

    public ImportOptionsDialog(
        List<IconCategory> existingCategories,
        IconStorageService iconStorage,
        List<string>? initialFiles = null,
        string? folderPath = null)
    {
        InitializeComponent();
        _iconStorage = iconStorage;
        _allCategories = existingCategories;

        foreach (var cat in existingCategories)
        {
            var item = new CategorySelectionItem
            {
                Category = cat,
                IsSelected = false
            };
            item.SelectionChanged += CategoryItem_SelectionChanged;
            CategorySelectionItems.Add(item);
        }

        CategoriesItemsControl.ItemsSource = CategorySelectionItems;
        UpdateSelectedCategoriesText();

        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
        {
            IsFolderMode = true;
            SelectedFolderPath = folderPath;
            Title = "Import Folder";
            ScanSubFoldersCheckBox.Visibility = Visibility.Visible;
            FolderLocationTextBlock.Visibility = Visibility.Visible;
            FolderLocationTextBlock.Text = $"Folder: {folderPath}";
            ScanAndPopulateFolderFiles();
        }
        else
        {
            IsFolderMode = false;
            Title = "Import Files";
            ScanSubFoldersCheckBox.Visibility = Visibility.Collapsed;
            FolderLocationTextBlock.Visibility = Visibility.Collapsed;
            if (initialFiles != null)
            {
                SelectedFilesList = initialFiles.Where(f => ValidExtensions.Contains(Path.GetExtension(f))).ToList();
            }
            PopulateFileList(SelectedFilesList);
        }
    }

    private void CategoryItem_SelectionChanged(object? sender, EventArgs e)
    {
        UpdateSelectedCategoriesText();
    }

    private void UpdateSelectedCategoriesText()
    {
        var selected = SelectedCategories;
        if (selected.Count == 0)
        {
            SelectedCategoriesTextBlock.Text = "Select Categories...";
        }
        else if (selected.Count == 1)
        {
            SelectedCategoriesTextBlock.Text = selected[0].Name;
        }
        else
        {
            SelectedCategoriesTextBlock.Text = $"{selected.Count} Categories Selected ({string.Join(", ", selected.Select(c => c.Name))})";
        }
    }

    private void ScanSubFoldersCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (IsFolderMode)
        {
            ScanAndPopulateFolderFiles();
        }
    }

    private void ScanAndPopulateFolderFiles()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath) || !Directory.Exists(SelectedFolderPath))
            return;

        bool scanSubFolders = ScanSubFoldersCheckBox.IsChecked == true;
        var searchOption = scanSubFolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var files = SafeGetFiles(SelectedFolderPath, searchOption, ValidExtensions);
        SelectedFilesList = files;
        PopulateFileList(files);
    }

    private static List<string> SafeGetFiles(string rootPath, SearchOption searchOption, HashSet<string> validExtensions)
    {
        var result = new List<string>();
        if (!Directory.Exists(rootPath)) return result;

        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            try
            {
                foreach (var file in Directory.GetFiles(rootPath))
                {
                    if (validExtensions.Contains(Path.GetExtension(file)))
                    {
                        result.Add(file);
                    }
                }
            }
            catch { }
            return result;
        }

        var dirsToScan = new Queue<string>();
        dirsToScan.Enqueue(rootPath);

        while (dirsToScan.Count > 0)
        {
            string currentDir = dirsToScan.Dequeue();
            try
            {
                foreach (var file in Directory.GetFiles(currentDir))
                {
                    if (validExtensions.Contains(Path.GetExtension(file)))
                    {
                        result.Add(file);
                    }
                }
            }
            catch { }

            try
            {
                foreach (var subDir in Directory.GetDirectories(currentDir))
                {
                    dirsToScan.Enqueue(subDir);
                }
            }
            catch { }
        }

        return result;
    }

    private void PopulateFileList(List<string> files)
    {
        if (IsFolderMode)
        {
            ItemsSummaryHeaderTextBlock.Text = $"Folder Images Found ({files.Count})";
        }
        else
        {
            ItemsSummaryHeaderTextBlock.Text = $"Selected Files ({files.Count})";
        }

        ProgressItems.Clear();
        foreach (var f in files)
        {
            string displayName = IsFolderMode && !string.IsNullOrEmpty(SelectedFolderPath)
                ? Path.GetRelativePath(SelectedFolderPath, f)
                : Path.GetFileName(f);

            ProgressItems.Add(new ImportProgressItem
            {
                FilePath = f,
                DisplayName = displayName,
                Status = ImportProgressStatus.Pending
            });
        }

        FilesListView.ItemsSource = ProgressItems;
    }

    private void NewCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        NewCategoryNameTextBox.Text = string.Empty;
    }

    private void AddCategoryConfirm_Click(object sender, RoutedEventArgs e)
    {
        string name = NewCategoryNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        var existing = _allCategories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var selectItem = CategorySelectionItems.FirstOrDefault(i => i.Category.Id == existing.Id);
            if (selectItem != null)
            {
                selectItem.IsSelected = true;
            }
        }
        else
        {
            var newCat = new IconCategory
            {
                Id = Guid.NewGuid().ToString(),
                Name = name
            };

            _allCategories.Add(newCat);
            _iconStorage.SaveCategories(_allCategories);

            string catDir = _iconStorage.GetCategoryFolderPath(newCat.Name);
            if (!Directory.Exists(catDir))
            {
                Directory.CreateDirectory(catDir);
            }

            var newItem = new CategorySelectionItem
            {
                Category = newCat,
                IsSelected = true
            };
            newItem.SelectionChanged += CategoryItem_SelectionChanged;

            CategorySelectionItems.Add(newItem);
        }

        UpdateSelectedCategoriesText();
        NewCategoryFlyout.Hide();
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            CategoryDropDownButton.IsEnabled = false;
            NewCategoryButton.IsEnabled = false;
            ScanSubFoldersCheckBox.IsEnabled = false;
            FilesListView.IsEnabled = false;
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;

            await PerformImportAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async Task PerformImportAsync()
    {
        if (ProgressItems == null || ProgressItems.Count == 0)
            return;

        var existingIcons = _iconStorage.LoadIcons();
        var existingByName = new Dictionary<string, IconItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var icon in existingIcons)
        {
            if (!string.IsNullOrWhiteSpace(icon.Name))
            {
                existingByName[icon.Name.Trim()] = icon;
            }
        }

        ConflictItems.Clear();
        ImportedCount = 0;

        string tempDir = Path.Combine(Path.GetTempPath(), "EchoBoxImportTemp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var selectedCats = SelectedCategories;
            string primaryTargetFolder = selectedCats.Count > 0
                ? _iconStorage.GetCategoryFolderPath(selectedCats[0].Name)
                : _iconStorage.IcoDirectory;

            Directory.CreateDirectory(primaryTargetFolder);

            foreach (var item in ProgressItems)
            {
                item.Status = ImportProgressStatus.InProgress;
                FilesListView.ScrollIntoView(item, ScrollIntoViewAlignment.Leading);
                await Task.Delay(30);

                try
                {
                    string file = item.FilePath;
                    string originalName = Path.GetFileNameWithoutExtension(file).Trim();
                    string tempIco = await _converter.ConvertAndSaveToIcoAsync(file, tempDir, preferredName: originalName, overwrite: true);

                    bool hasConflict = false;
                    string targetPath = Path.Combine(primaryTargetFolder, $"{originalName}.ico");

                    if (File.Exists(targetPath))
                    {
                        if (IconStorageService.AreFilesEqualByHash(tempIco, targetPath))
                        {
                            ImportedCount++;
                        }
                        else
                        {
                            existingByName.TryGetValue(originalName, out var existingIcon);
                            existingIcon ??= new IconItem { Name = originalName, FilePath = targetPath };

                            ConflictItems.Add(new ImportConflictItem
                            {
                                ImportedFilePath = file,
                                OriginalName = originalName,
                                ExistingIcon = existingIcon,
                                NewName = originalName
                            });
                            hasConflict = true;
                        }
                    }
                    else
                    {
                        File.Copy(tempIco, targetPath, overwrite: true);
                        ImportedCount++;
                    }

                    if (!hasConflict)
                    {
                        var iconItem = existingIcons.FirstOrDefault(i => string.Equals(i.Name, originalName, StringComparison.OrdinalIgnoreCase) || string.Equals(i.FilePath, targetPath, StringComparison.OrdinalIgnoreCase));
                        if (iconItem == null)
                        {
                            iconItem = new IconItem
                            {
                                Id = Guid.NewGuid().ToString(),
                                Name = originalName,
                                FilePath = targetPath,
                                RelativePath = Path.GetRelativePath(_iconStorage.IcoDirectory, targetPath),
                                CategoryIds = selectedCats.Select(c => c.Id).ToList()
                            };
                            existingIcons.Add(iconItem);
                        }
                        else
                        {
                            iconItem.FilePath = targetPath;
                            iconItem.RelativePath = Path.GetRelativePath(_iconStorage.IcoDirectory, targetPath);
                            iconItem.CategoryIds ??= new();
                            foreach (var cat in selectedCats)
                            {
                                if (!iconItem.CategoryIds.Contains(cat.Id))
                                {
                                    iconItem.CategoryIds.Add(cat.Id);
                                }
                            }
                        }
                    }

                    item.Status = ImportProgressStatus.Completed;
                }
                catch
                {
                    item.Status = ImportProgressStatus.Failed;
                }

                await Task.Delay(20);
            }

            _iconStorage.SaveIconsMetadata(existingIcons);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}

