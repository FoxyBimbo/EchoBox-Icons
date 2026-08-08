using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using EchoBox.Core.Models;
using EchoBox.Core.Services;
using EchoBox.App.Views;

namespace EchoBox.App;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;
    private bool _isRestoringWindowState;

    public MainWindow()
    {
        InitializeComponent();
        Title = "EchoBox - Icons";
        SetAppIcon();

        _settings = _settingsService.LoadSettings();
        RestoreWindowState();

        AppWindow.Changed += AppWindow_Changed;
        Closed += MainWindow_Closed;
    }

    private void RestoreWindowState()
    {
        _isRestoringWindowState = true;
        try
        {
            if (_settings.WindowWidth > 100 && _settings.WindowHeight > 100)
            {
                var targetRect = new RectInt32(_settings.WindowX, _settings.WindowY, _settings.WindowWidth, _settings.WindowHeight);
                var displayArea = DisplayArea.GetFromRect(targetRect, DisplayAreaFallback.None);

                if (displayArea != null)
                {
                    AppWindow.MoveAndResize(targetRect);
                }
            }

            if (_settings.IsMaximized)
            {
                if (AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }
            }
        }
        finally
        {
            _isRestoringWindowState = false;
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_isRestoringWindowState) return;

        if (AppWindow.Presenter is OverlappedPresenter presenter && args.DidPresenterChange)
        {
            var settings = _settingsService.LoadSettings();
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                settings.IsMaximized = true;
            }
            else if (presenter.State == OverlappedPresenterState.Restored)
            {
                settings.IsMaximized = false;
                settings.WindowX = AppWindow.Position.X;
                settings.WindowY = AppWindow.Position.Y;
                settings.WindowWidth = AppWindow.Size.Width;
                settings.WindowHeight = AppWindow.Size.Height;
            }

            _settingsService.SaveSettings(settings);
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var settings = _settingsService.LoadSettings();
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
            {
                settings.IsMaximized = true;
            }
            else if (presenter.State == OverlappedPresenterState.Restored)
            {
                settings.IsMaximized = false;
                settings.WindowX = AppWindow.Position.X;
                settings.WindowY = AppWindow.Position.Y;
                settings.WindowWidth = AppWindow.Size.Width;
                settings.WindowHeight = AppWindow.Size.Height;
            }
        }

        _settingsService.SaveSettings(settings);
    }

    private void SetAppIcon()
    {
        string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (System.IO.File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        string initialTag = string.IsNullOrEmpty(_settings.LastActivePageTag) ? "Home" : _settings.LastActivePageTag;

        NavigateToTag(initialTag);
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateToTag(tag);
        }
    }

    private void NavigateToTag(string tag)
    {
        Type? pageType = tag switch
        {
            "Home" => typeof(HomePage),
            "Icons" => typeof(IconsPage),
            "Themes" => typeof(ThemesPage),
            "Settings" => typeof(SettingsPage),
            _ => typeof(HomePage)
        };

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
            
            // Save state
            var settings = _settingsService.LoadSettings();
            settings.LastActivePageTag = tag;
            _settingsService.SaveSettings(settings);
        }

        // Set selected item in nav view
        var navItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => (string)i.Tag == tag);
        if (navItem != null)
        {
            NavView.SelectedItem = navItem;
        }
    }
}

