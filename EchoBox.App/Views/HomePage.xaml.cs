using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using EchoBox.Core.Models;
using EchoBox.Core.Services;
using EchoBox.Engine.Services;

namespace EchoBox.App.Views;

public partial class HomePage : Page
{
    private static readonly IconProfile NewProfileOption = new() { Id = "__NEW_PROFILE__", Name = "+ New Profile..." };

    private readonly ProfileService _profileService = new();
    private readonly SettingsService _settingsService = new();
    private readonly IconApplierPipeline _pipeline = new();

    private ObservableCollection<IconProfile> _profiles = new();
    private IconProfile? _activeProfile;
    private bool _isUpdatingSelection;

    private readonly ObservableCollection<IconMapping> _systemMappings = new();
    private readonly ObservableCollection<IconMapping> _fileExtensionMappings = new();
    private readonly ObservableCollection<IconMapping> _customMappings = new();

    public HomePage()
    {
        InitializeComponent();
        SystemMappingsListView.ItemsSource = _systemMappings;
        FileExtensionMappingsListView.ItemsSource = _fileExtensionMappings;
        CustomMappingsListView.ItemsSource = _customMappings;
        Loaded += HomePage_Loaded;
    }

    private void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadAllProfiles();
    }

    private void LoadAllProfiles()
    {
        var loaded = _profileService.LoadAllProfiles();
        _profiles = new ObservableCollection<IconProfile>(loaded);

        var comboList = new List<IconProfile>(_profiles)
        {
            NewProfileOption
        };

        _isUpdatingSelection = true;
        ProfileComboBox.ItemsSource = comboList;

        var settings = _settingsService.LoadSettings();
        var selected = _profiles.FirstOrDefault(p => p.Id == settings.SelectedProfileId) ?? _profiles.FirstOrDefault();

        if (selected != null)
        {
            ProfileComboBox.SelectedItem = selected;
            _isUpdatingSelection = false;
            SetActiveProfile(selected);
        }
        else
        {
            _isUpdatingSelection = false;
        }
    }

    private void SetActiveProfile(IconProfile profile)
    {
        _activeProfile = profile;
        RefreshMappingsList();

        TargetScopeComboBox.SelectedIndex = (int)_activeProfile.Scope;
        UpdateSpecificFolderPanelVisibility();
        TargetFolderTextBox.Text = _activeProfile.TargetFolderPath;
    }

    private void SaveActiveProfile()
    {
        if (_activeProfile == null) return;
        _activeProfile.Scope = (TargetScopeOption)Math.Max(0, TargetScopeComboBox.SelectedIndex);
        _activeProfile.IsFullDriveScan = _activeProfile.Scope == TargetScopeOption.FullDrive;
        _activeProfile.TargetFolderPath = TargetFolderTextBox?.Text ?? string.Empty;
        _profileService.SaveProfile(_activeProfile);
    }

    private void TargetScopeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeProfile != null && TargetScopeComboBox.SelectedIndex >= 0)
        {
            _activeProfile.Scope = (TargetScopeOption)TargetScopeComboBox.SelectedIndex;
            _activeProfile.IsFullDriveScan = _activeProfile.Scope == TargetScopeOption.FullDrive;
            SaveActiveProfile();
        }
        UpdateSpecificFolderPanelVisibility();
    }

    private void TargetFolderTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_activeProfile != null)
        {
            SaveActiveProfile();
        }
    }

    private void UpdateSpecificFolderPanelVisibility()
    {
        if (SpecificFolderPanel == null || TargetScopeComboBox == null) return;
        SpecificFolderPanel.Visibility = TargetScopeComboBox.SelectedIndex == 1
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;
        if (ProfileComboBox.SelectedItem is not IconProfile selected) return;

        if (selected.Id == "__NEW_PROFILE__")
        {
            await CreateNewProfileAsync();
        }
        else if (_activeProfile == null || selected.Id != _activeProfile.Id)
        {
            var reloaded = _profileService.LoadProfile(selected.Id);
            SetActiveProfile(reloaded);

            var settings = _settingsService.LoadSettings();
            settings.SelectedProfileId = selected.Id;
            _settingsService.SaveSettings(settings);
        }
    }

    private async Task CreateNewProfileAsync()
    {
        var textBox = new TextBox { PlaceholderText = "Enter profile name..." };
        var dialog = new ContentDialog
        {
            Title = "Create New Profile",
            Content = textBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            var newProfile = _profileService.CreateDefaultProfile();
            newProfile.Id = Guid.NewGuid().ToString();
            newProfile.Name = textBox.Text.Trim();
            _profileService.SaveProfile(newProfile);

            LoadAllProfiles();

            var created = _profiles.FirstOrDefault(p => p.Id == newProfile.Id);
            if (created != null)
            {
                _isUpdatingSelection = true;
                ProfileComboBox.SelectedItem = created;
                _isUpdatingSelection = false;
                SetActiveProfile(created);

                var settings = _settingsService.LoadSettings();
                settings.SelectedProfileId = created.Id;
                _settingsService.SaveSettings(settings);
            }
        }
        else
        {
            // User cancelled - revert selection back to current active profile
            if (_activeProfile != null)
            {
                _isUpdatingSelection = true;
                ProfileComboBox.SelectedItem = _profiles.FirstOrDefault(p => p.Id == _activeProfile.Id);
                _isUpdatingSelection = false;
            }
        }
    }

    private async void RenameProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeProfile == null) return;

        var textBox = new TextBox { Text = _activeProfile.Name };
        var dialog = new ContentDialog
        {
            Title = "Rename Profile",
            Content = textBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            _activeProfile.Name = textBox.Text.Trim();
            SaveActiveProfile();
            LoadAllProfiles();
        }
    }

    private async void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeProfile == null || _profiles.Count <= 1)
        {
            ShowNotificationDialog("Cannot Delete", "You must keep at least one profile.");
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Delete Profile",
            Content = $"Are you sure you want to delete profile '{_activeProfile.Name}'?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _profileService.DeleteProfile(_activeProfile.Id);
            LoadAllProfiles();
        }
    }

    private async void ExportProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeProfile == null) return;

        SaveActiveProfile();

        string tempZipPath = Path.Combine(Path.GetTempPath(), $"EchoBox_Export_{Guid.NewGuid()}.zip");
        try
        {
            // Build relative profile clone for export
            var exportedProfile = new IconProfile
            {
                Id = _activeProfile.Id,
                Name = _activeProfile.Name,
                Description = _activeProfile.Description,
                TargetFolderPath = _activeProfile.TargetFolderPath,
                IsFullDriveScan = _activeProfile.IsFullDriveScan,
                Scope = _activeProfile.Scope,
                CreatedDate = _activeProfile.CreatedDate,
                LastModifiedDate = DateTime.UtcNow,
                Mappings = new List<IconMapping>()
            };

            var iconFilesToInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in _activeProfile.Mappings)
            {
                var copyMapping = new IconMapping
                {
                    TargetType = mapping.TargetType,
                    TargetName = mapping.TargetName,
                    FileExtension = mapping.FileExtension,
                    IsSingleIconOnly = mapping.IsSingleIconOnly,
                    LastAppliedIndex = mapping.LastAppliedIndex,
                    IsCustom = mapping.IsCustom,
                    SelectedIconPaths = new List<string>()
                };

                if (mapping.SelectedIconPaths != null)
                {
                    foreach (var path in mapping.SelectedIconPaths)
                    {
                        if (File.Exists(path))
                        {
                            iconFilesToInclude.Add(path);
                            copyMapping.SelectedIconPaths.Add(Path.GetFileName(path));
                        }
                    }
                }

                exportedProfile.Mappings.Add(copyMapping);
            }

            using (var zipStream = File.Create(tempZipPath))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                string jsonString = JsonSerializer.Serialize(exportedProfile, new JsonSerializerOptions { WriteIndented = true });
                var jsonEntry = archive.CreateEntry("profile.json");
                using (var writer = new StreamWriter(jsonEntry.Open()))
                {
                    writer.Write(jsonString);
                }

                foreach (var iconPath in iconFilesToInclude)
                {
                    string filename = Path.GetFileName(iconPath);
                    archive.CreateEntryFromFile(iconPath, filename);
                }
            }

            var savePicker = new FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

            savePicker.SuggestedStartLocation = PickerLocationId.Downloads;
            savePicker.FileTypeChoices.Add("Zip Archive", new List<string> { ".zip" });

            string safeProfileName = string.Concat(_activeProfile.Name.Split(Path.GetInvalidFileNameChars()));
            savePicker.SuggestedFileName = $"{safeProfileName}_Profile.zip";

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                File.Copy(tempZipPath, file.Path, overwrite: true);
                ShowNotificationDialog("Export Successful", $"Profile '{_activeProfile.Name}' was exported successfully.");
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "HomePage.ExportProfile");
            ShowNotificationDialog("Export Error", $"Failed to export profile: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                try { File.Delete(tempZipPath); } catch { }
            }
        }
    }

    private async void ImportProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedStartLocation = PickerLocationId.Downloads;
        picker.FileTypeFilter.Add(".zip");

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        // Step 1: Validate internal format (must contain .json and at least one .ico)
        bool hasJson = false;
        bool hasIco = false;

        try
        {
            using var archive = ZipFile.OpenRead(file.Path);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    hasJson = true;
                }
                if (entry.FullName.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    hasIco = true;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "HomePage.ImportProfile.ReadZip");
            ShowNotificationDialog("Invalid Zip File", $"Failed to open zip file: {ex.Message}");
            return;
        }

        if (!hasJson || !hasIco)
        {
            ShowNotificationDialog("Invalid Zip Error", "The zip file is invalid. It must contain a profile .json file and at least one .ico file.");
            return;
        }

        // Step 2: Extract to temp folder & validate JSON
        string tempExtractDir = Path.Combine(Path.GetTempPath(), $"EchoBoxImport_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempExtractDir);

        try
        {
            ZipFile.ExtractToDirectory(file.Path, tempExtractDir);

            string[] jsonFiles = Directory.GetFiles(tempExtractDir, "*.json", SearchOption.AllDirectories);
            if (jsonFiles.Length == 0)
            {
                ShowNotificationDialog("Invalid Zip Error", "No .json file found in zip file.");
                return;
            }

            IconProfile? importedProfile = null;
            try
            {
                string jsonText = File.ReadAllText(jsonFiles[0]);
                importedProfile = JsonSerializer.Deserialize<IconProfile>(jsonText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                // Invalid JSON format
            }

            if (importedProfile == null || importedProfile.Mappings == null)
            {
                ShowNotificationDialog("Invalid JSON Error", "The profile .json file in the zip is improperly formatted or corrupted.");
                return;
            }

            // Step 3: Check missing icons
            string[] extractedIcoFiles = Directory.GetFiles(tempExtractDir, "*.ico", SearchOption.AllDirectories);
            var icoDict = extractedIcoFiles.ToDictionary(
                f => Path.GetFileName(f),
                f => f,
                StringComparer.OrdinalIgnoreCase
            );

            bool missingAnyIcon = false;
            foreach (var mapping in importedProfile.Mappings)
            {
                if (mapping.SelectedIconPaths == null) continue;
                foreach (var rawPath in mapping.SelectedIconPaths)
                {
                    string filename = Path.GetFileName(rawPath);
                    if (!icoDict.ContainsKey(filename))
                    {
                        missingAnyIcon = true;
                        break;
                    }
                }
                if (missingAnyIcon) break;
            }

            if (missingAnyIcon)
            {
                var dialog = new ContentDialog
                {
                    Title = "Missing Icons Warning",
                    Content = "The profile file is valid, but some icon files (.ico) are missing from the zip package. Would you like to load the profile anyway?",
                    PrimaryButtonText = "Load Anyway",
                    CloseButtonText = "Cancel",
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            // Step 4: Copy ico files to app ico folder & save profile
            string appIcoDir = new IconStorageService().IcoDirectory;
            Directory.CreateDirectory(appIcoDir);

            var mappedDestPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in icoDict)
            {
                string filename = kvp.Key;
                string sourcePath = kvp.Value;
                string destPath = Path.Combine(appIcoDir, filename);

                try
                {
                    File.Copy(sourcePath, destPath, overwrite: true);
                    mappedDestPaths[filename] = destPath;
                }
                catch
                {
                    // Copy fallback
                }
            }

            foreach (var mapping in importedProfile.Mappings)
            {
                if (mapping.SelectedIconPaths == null)
                {
                    mapping.SelectedIconPaths = new List<string>();
                    continue;
                }

                var resolvedPaths = new List<string>();
                foreach (var rawPath in mapping.SelectedIconPaths)
                {
                    string filename = Path.GetFileName(rawPath);
                    if (mappedDestPaths.TryGetValue(filename, out string? destPath))
                    {
                        resolvedPaths.Add(destPath);
                    }
                    // Missing icons are omitted (empty values loaded)
                }
                mapping.SelectedIconPaths = resolvedPaths;
            }

            importedProfile.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrWhiteSpace(importedProfile.Name))
            {
                importedProfile.Name = "Imported Profile";
            }

            _profileService.SaveProfile(importedProfile);

            LoadAllProfiles();

            var created = _profiles.FirstOrDefault(p => p.Id == importedProfile.Id);
            if (created != null)
            {
                _isUpdatingSelection = true;
                ProfileComboBox.SelectedItem = created;
                _isUpdatingSelection = false;
                SetActiveProfile(created);

                var settings = _settingsService.LoadSettings();
                settings.SelectedProfileId = created.Id;
                _settingsService.SaveSettings(settings);
            }

            ShowNotificationDialog("Import Successful", $"Profile '{importedProfile.Name}' was imported successfully!");
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "HomePage.ImportProfile.Extract");
            ShowNotificationDialog("Import Error", $"An error occurred during import: {ex.Message}");
        }
        finally
        {
            if (Directory.Exists(tempExtractDir))
            {
                try { Directory.Delete(tempExtractDir, recursive: true); } catch { }
            }
        }
    }

    private async void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            TargetFolderTextBox.Text = folder.Path;
            SaveActiveProfile();
        }
    }

    private void AddCustomMappingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeProfile == null) return;

        var newMapping = new IconMapping
        {
            IsCustom = true,
            TargetType = IconTargetType.CustomExtension,
            TargetName = "",
            FileExtension = "",
            IsSingleIconOnly = true,
            SelectedIconPaths = new List<string>()
        };

        _activeProfile.Mappings.Add(newMapping);
        SaveActiveProfile();
        RefreshMappingsList();
    }

    private void DeleteCustomMapping_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is IconMapping mapping && _activeProfile != null)
        {
            _activeProfile.Mappings.Remove(mapping);
            SaveActiveProfile();
            RefreshMappingsList();
        }
    }

    private async void PickIconsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is IconMapping mapping)
        {
            bool isSingleIconOnly = mapping.IsSingleIconOnly;
            if (mapping.IsCustom)
            {
                var entries = mapping.GetParsedEntries().ToList();
                isSingleIconOnly = entries.Count == 0 || entries.Any(entry => IconMapping.IsFileExtensionEntry(entry));
                mapping.IsSingleIconOnly = isSingleIconOnly;
            }

            var pickerDialog = new IconPickerFlyout(isSingleIconOnly, mapping.SelectedIconPaths)
            {
                XamlRoot = XamlRoot
            };

            if (await pickerDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                mapping.SelectedIconPaths = pickerDialog.SelectedPaths;
                mapping.NotifySelectedIconPathsChanged();
                SaveActiveProfile();
                RefreshMappingsList();
            }
        }
    }

    private void ClearIconsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is IconMapping mapping)
        {
            mapping.SelectedIconPaths.Clear();
            mapping.NotifySelectedIconPathsChanged();
            SaveActiveProfile();
            RefreshMappingsList();
        }
    }

    private void RefreshMappingsList()
    {
        if (_activeProfile == null) return;

        _systemMappings.Clear();
        _fileExtensionMappings.Clear();
        _customMappings.Clear();

        foreach (var mapping in _activeProfile.Mappings)
        {
            if (mapping.IsCustom)
            {
                _customMappings.Add(mapping);
            }
            else if (IsSystemMapping(mapping))
            {
                _systemMappings.Add(mapping);
            }
            else
            {
                _fileExtensionMappings.Add(mapping);
            }
        }

        if (NoCustomMappingsTextBlock != null)
        {
            NoCustomMappingsTextBlock.Visibility = _customMappings.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private static bool IsSystemMapping(IconMapping mapping)
    {
        if (mapping.IsCustom) return false;
        return mapping.TargetType == IconTargetType.Folder ||
               mapping.TargetType == IconTargetType.MyComputer ||
               mapping.TargetType == IconTargetType.Network ||
               mapping.TargetType == IconTargetType.RecycleBinEmpty ||
               mapping.TargetType == IconTargetType.RecycleBinFull;
    }

    private async void ApplyIconsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeProfile == null) return;

        SaveActiveProfile();

        var settings = _settingsService.LoadSettings();
        var cts = new CancellationTokenSource();
        var progressDialog = new ApplyProgressDialog(cts)
        {
            XamlRoot = XamlRoot
        };

        var progress = new Progress<ApplyProgressReport>(report =>
        {
            progressDialog.ReportProgress(report);
        });

        var applyTask = _pipeline.ApplyProfileAsync(_activeProfile, settings, progress, cts.Token);
        await progressDialog.ShowAsync();
        await applyTask;
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
