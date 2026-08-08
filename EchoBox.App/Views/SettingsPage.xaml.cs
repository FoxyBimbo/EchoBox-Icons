using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using EchoBox.Core.Models;
using EchoBox.Core.Services;
using EchoBox.Engine.Interop;
using EchoBox.Engine.Services;

namespace EchoBox.App.Views;

public partial class SettingsPage : Page
{
    private readonly SettingsService _settingsService = new();
    private readonly IconStorageService _iconStorage = new();
    private readonly ProfileService _profileService = new();

    private AppSettings _settings = new();
    private ObservableCollection<string> _exclusions = new();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = _settingsService.LoadSettings();

        ContextMenuToggle.IsOn = _settings.EnableContextMenuIntegration;
        ThreadsSlider.Value = _settings.MaxParallelThreads;
        ThreadsValueTextBlock.Text = $"{_settings.MaxParallelThreads} Worker Threads";

        _exclusions = new ObservableCollection<string>(_settings.CustomExcludedPaths);
        ExclusionsListView.ItemsSource = _exclusions;

        IcoPathTextBlock.Text = _iconStorage.IcoDirectory;
        ProfilesPathTextBlock.Text = _profileService.ProfilesDirectory;
    }

    private void ContextMenuToggle_Toggled(object sender, RoutedEventArgs e)
    {
        bool enable = ContextMenuToggle.IsOn;
        _settings.EnableContextMenuIntegration = enable;
        _settingsService.SaveSettings(_settings);

        if (enable)
        {
            ContextMenuRegistrar.RegisterContextMenu();
        }
        else
        {
            ContextMenuRegistrar.UnregisterContextMenu();
        }
    }

    private void ThreadsSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ThreadsValueTextBlock == null) return;
        int threads = (int)e.NewValue;
        ThreadsValueTextBlock.Text = $"{threads} Worker Threads";

        if (_settings != null)
        {
            _settings.MaxParallelThreads = threads;
            _settingsService.SaveSettings(_settings);
        }
    }

    private async void BrowseExclusionButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            NewExclusionTextBox.Text = folder.Path;
        }
    }

    private void AddExclusionButton_Click(object sender, RoutedEventArgs e)
    {
        string path = NewExclusionTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(path) && !_exclusions.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            _exclusions.Add(path);
            _settings.CustomExcludedPaths = _exclusions.ToList();
            _settingsService.SaveSettings(_settings);
            NewExclusionTextBox.Text = string.Empty;
        }
    }

    private void RemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            _exclusions.Remove(path);
            _settings.CustomExcludedPaths = _exclusions.ToList();
            _settingsService.SaveSettings(_settings);
        }
    }

    private void RefreshShellButton_Click(object sender, RoutedEventArgs e)
    {
        NativeMethods.RefreshShell();
        ShowNotificationDialog("Shell Refreshed", "Windows Explorer icon cache notification broadcasted.");
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
