using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using EchoBox.Core.Models;
using EchoBox.Core.Services;
using EchoBox.Engine.Services;

namespace EchoBox.App.Views;

public partial class ThemesPage : Page
{
    private readonly WindowsThemeService _themeService = new();
    private readonly SettingsService _settingsService = new();
    private readonly AppSettings _settings;

    private readonly ObservableCollection<GradientColorStop> _gradientStops = new();
    private bool _isInitializing = true;
    private string _selectedSingleImagePath = string.Empty;
    private string _selectedFolderPath = string.Empty;

    // Preset accent colors
    private readonly List<Color> _quickPaletteColors = new()
    {
        Color.FromArgb(255, 255, 185, 0),   // Yellow
        Color.FromArgb(255, 255, 140, 0),   // Gold
        Color.FromArgb(255, 247, 99, 12),   // Orange
        Color.FromArgb(255, 232, 17, 35),   // Red
        Color.FromArgb(255, 234, 0, 94),    // Dark Red / Crimson
        Color.FromArgb(255, 195, 0, 82),    // Magenta
        Color.FromArgb(255, 180, 0, 158),   // Purple
        Color.FromArgb(255, 0, 120, 215),   // Blue (Default)
        Color.FromArgb(255, 0, 153, 188),   // Teal
        Color.FromArgb(255, 16, 124, 65),   // Green
        Color.FromArgb(255, 122, 117, 116)  // Slate
    };

    public ThemesPage()
    {
        InitializeComponent();
        _settings = _settingsService.LoadSettings();

        GradientStopsListView.ItemsSource = _gradientStops;

        Loaded += ThemesPage_Loaded;
    }

    private void ThemesPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        try
        {
            var freshSettings = _settingsService.LoadSettings();
            _settings.LastSelectedBackgroundMode = freshSettings.LastSelectedBackgroundMode;
            _settings.LastSingleImagePath = freshSettings.LastSingleImagePath;
            _settings.LastFolderPath = freshSettings.LastFolderPath;
            _settings.LastSlideshowIntervalMinutes = freshSettings.LastSlideshowIntervalMinutes;
            _settings.LastSlideshowShuffle = freshSettings.LastSlideshowShuffle;
            _settings.LastSolidColorHex = freshSettings.LastSolidColorHex;
            _settings.LastGradientAngle = freshSettings.LastGradientAngle;
            _settings.LastGradientStopsHex = freshSettings.LastGradientStopsHex;
            _settings.LastGradientFilename = freshSettings.LastGradientFilename;

            LoadThemeSettings();
            LoadQuickPalette();
            LoadBackgroundOptions();
            LoadBackgroundMode();
            LoadSavedGradientsGallery();

            var (width, height) = _themeService.GetScreenResolution();
            ResolutionText.Text = $"Desktop Resolution: {width} x {height}";
        }
        finally
        {
            _isInitializing = false;
        }

        UpdateGradientPreview();
    }

    #region Load Initial Settings

    private void LoadThemeSettings()
    {
        // System Theme Mode
        bool sysLight = _themeService.GetSystemUsesLightTheme();
        if (sysLight) SystemLightRadio.IsChecked = true;
        else SystemDarkRadio.IsChecked = true;

        // Apps Theme Mode
        bool appsLight = _themeService.GetAppsUseLightTheme();
        if (appsLight) AppsLightRadio.IsChecked = true;
        else AppsDarkRadio.IsChecked = true;

        // Accent Color
        var (r, g, b, a) = _themeService.GetAccentColor();
        Color accentColor = Color.FromArgb(a, r, g, b);
        AccentColorPicker.Color = accentColor;
        UpdateAccentColorUI(accentColor);
    }

    private void LoadQuickPalette()
    {
        QuickPaletteItemsControl.Items.Clear();
        foreach (var c in _quickPaletteColors)
        {
            var btn = new Button
            {
                Width = 32,
                Height = 32,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(c),
                BorderBrush = (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Tag = c
            };
            btn.Click += QuickPaletteColor_Click;
            QuickPaletteItemsControl.Items.Add(btn);
        }
    }

    private void LoadBackgroundOptions()
    {
        // 1. Single Image
        if (!string.IsNullOrEmpty(_settings.LastSingleImagePath))
        {
            _selectedSingleImagePath = _settings.LastSingleImagePath;
            SingleImagePathText.Text = _selectedSingleImagePath;
            if (File.Exists(_selectedSingleImagePath))
            {
                try
                {
                    SingleImagePreview.Source = new BitmapImage(new Uri(_selectedSingleImagePath));
                }
                catch { }
            }
        }

        // 2. Folder Slideshow
        if (!string.IsNullOrEmpty(_settings.LastFolderPath))
        {
            _selectedFolderPath = _settings.LastFolderPath;
            FolderPathText.Text = _selectedFolderPath;
        }
        foreach (ComboBoxItem item in SlideshowIntervalComboBox.Items)
        {
            if (item.Tag is string tagStr && int.TryParse(tagStr, out int val) && val == _settings.LastSlideshowIntervalMinutes)
            {
                SlideshowIntervalComboBox.SelectedItem = item;
                break;
            }
        }
        SlideshowShuffleCheckBox.IsChecked = _settings.LastSlideshowShuffle;

        // 3. Solid Color
        Color solidColor;
        if (!string.IsNullOrEmpty(_settings.LastSolidColorHex))
        {
            solidColor = ParseHexStringToColor(_settings.LastSolidColorHex);
        }
        else
        {
            var (sr, sg, sb) = _themeService.GetSolidColorWallpaper();
            solidColor = Color.FromArgb(255, sr, sg, sb);
            _settings.LastSolidColorHex = $"#{sr:X2}{sg:X2}{sb:X2}";
        }
        SolidColorPicker.Color = solidColor;
        UpdateSolidColorUI(solidColor);

        // 4. Gradient Colors & Angle
        _gradientStops.Clear();
        if (_settings.LastGradientStopsHex != null && _settings.LastGradientStopsHex.Count >= 2)
        {
            int count = _settings.LastGradientStopsHex.Count;
            if (count == 2) GradientStops2Radio.IsChecked = true;
            else if (count == 4) GradientStops4Radio.IsChecked = true;
            else GradientStops3Radio.IsChecked = true;

            for (int i = 0; i < count; i++)
            {
                Color c = ParseHexStringToColor(_settings.LastGradientStopsHex[i]);
                _gradientStops.Add(new GradientColorStop($"Color {i + 1}", c));
            }
        }
        else
        {
            GradientStops3Radio.IsChecked = true;
            _gradientStops.Add(new GradientColorStop("Color 1", Color.FromArgb(255, 255, 0, 100)));
            _gradientStops.Add(new GradientColorStop("Color 2", Color.FromArgb(255, 120, 0, 255)));
            _gradientStops.Add(new GradientColorStop("Color 3", Color.FromArgb(255, 0, 210, 255)));
        }

        AngleSlider.Value = _settings.LastGradientAngle;
    }

    private void LoadBackgroundMode()
    {
        string mode = _settings.LastSelectedBackgroundMode ?? "SingleImage";
        switch (mode)
        {
            case "Folder":
                ModeFolderRadio.IsChecked = true;
                break;
            case "Solid":
                ModeSolidRadio.IsChecked = true;
                break;
            case "Gradient":
                ModeGradientRadio.IsChecked = true;
                break;
            default:
                ModeSingleImageRadio.IsChecked = true;
                break;
        }
        UpdateBackgroundPanelsVisibility();
    }

    #endregion

    #region System & Apps Theme Mode Handlers

    private void SystemTheme_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool light = SystemLightRadio.IsChecked == true;
        _themeService.SetSystemUsesLightTheme(light);
    }

    private void AppsTheme_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        bool light = AppsLightRadio.IsChecked == true;
        _themeService.SetAppsUseLightTheme(light);
    }

    #endregion

    #region Accent Color Handlers

    private void AccentColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        UpdateAccentColorUI(args.NewColor);
    }

    private void ApplyAccentColor_Click(object sender, RoutedEventArgs e)
    {
        Color c = AccentColorPicker.Color;
        _themeService.SetAccentColor(c.R, c.G, c.B);
        UpdateAccentColorUI(c);

        AccentColorFlyout.Hide();
    }

    private void QuickPaletteColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Color c)
        {
            AccentColorPicker.Color = c;
            _themeService.SetAccentColor(c.R, c.G, c.B);
            UpdateAccentColorUI(c);
        }
    }

    private void UpdateAccentColorUI(Color c)
    {
        AccentColorPreview.Background = new SolidColorBrush(c);
        AccentColorHexText.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    #endregion

    #region Background Mode Switching

    private void BgMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        UpdateBackgroundPanelsVisibility();
    }

    private void UpdateBackgroundPanelsVisibility()
    {
        SingleImagePanel.Visibility = Visibility.Collapsed;
        FolderPanel.Visibility = Visibility.Collapsed;
        SolidColorPanel.Visibility = Visibility.Collapsed;
        GradientPanel.Visibility = Visibility.Collapsed;

        string mode = "SingleImage";
        if (ModeFolderRadio.IsChecked == true)
        {
            FolderPanel.Visibility = Visibility.Visible;
            mode = "Folder";
        }
        else if (ModeSolidRadio.IsChecked == true)
        {
            SolidColorPanel.Visibility = Visibility.Visible;
            mode = "Solid";
        }
        else if (ModeGradientRadio.IsChecked == true)
        {
            GradientPanel.Visibility = Visibility.Visible;
            mode = "Gradient";
            UpdateGradientPreview();
        }
        else
        {
            SingleImagePanel.Visibility = Visibility.Visible;
            mode = "SingleImage";
        }

        if (!_isInitializing)
        {
            _settings.LastSelectedBackgroundMode = mode;
            _settingsService.SaveSettings(_settings);
        }
    }

    #endregion

    #region Single Image Wallpaper

    private async void BrowseSingleImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            picker.FileTypeFilter.Add(".webp");

            if (App.MainWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                _selectedSingleImagePath = file.Path;
                SingleImagePathText.Text = file.Path;
                SingleImagePreview.Source = new BitmapImage(new Uri(file.Path));

                _settings.LastSingleImagePath = file.Path;
                _settingsService.SaveSettings(_settings);
            }
        }
        catch { }
    }

    private void ApplySingleImage_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_selectedSingleImagePath) && File.Exists(_selectedSingleImagePath))
        {
            _themeService.SetSingleImageWallpaper(_selectedSingleImagePath);
            _settings.LastSingleImagePath = _selectedSingleImagePath;
            _settingsService.SaveSettings(_settings);
        }
    }

    #endregion

    #region Folder Slideshow Wallpaper

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");

            if (App.MainWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                _selectedFolderPath = folder.Path;
                FolderPathText.Text = folder.Path;

                _settings.LastFolderPath = folder.Path;
                _settingsService.SaveSettings(_settings);
            }
        }
        catch { }
    }

    private void SlideshowIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (SlideshowIntervalComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr && int.TryParse(tagStr, out int val))
        {
            _settings.LastSlideshowIntervalMinutes = val;
            _settingsService.SaveSettings(_settings);
        }
    }

    private void SlideshowShuffleCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _settings.LastSlideshowShuffle = SlideshowShuffleCheckBox.IsChecked == true;
        _settingsService.SaveSettings(_settings);
    }

    private void ApplySlideshow_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_selectedFolderPath) && Directory.Exists(_selectedFolderPath))
        {
            int minutes = 30;
            if (SlideshowIntervalComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string tagStr && int.TryParse(tagStr, out int val))
            {
                minutes = val;
            }
            bool shuffle = SlideshowShuffleCheckBox.IsChecked == true;
            _themeService.SetSlideshowWallpaper(_selectedFolderPath, minutes, shuffle);

            _settings.LastFolderPath = _selectedFolderPath;
            _settings.LastSlideshowIntervalMinutes = minutes;
            _settings.LastSlideshowShuffle = shuffle;
            _settingsService.SaveSettings(_settings);
        }
    }

    #endregion

    #region Solid Color Wallpaper

    private void SolidColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        UpdateSolidColorUI(args.NewColor);
        if (_isInitializing) return;
        Color c = args.NewColor;
        _settings.LastSolidColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        _settingsService.SaveSettings(_settings);
    }

    private void ApplySolidColor_Click(object sender, RoutedEventArgs e)
    {
        Color c = SolidColorPicker.Color;
        _themeService.SetSolidColorWallpaper(c.R, c.G, c.B);
        UpdateSolidColorUI(c);

        _settings.LastSolidColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        _settingsService.SaveSettings(_settings);

        SolidColorFlyout.Hide();
    }

    private void UpdateSolidColorUI(Color c)
    {
        SolidColorPreview.Background = new SolidColorBrush(c);
        SolidColorHexText.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    #endregion

    #region Gradient Options Builder

    private void SaveGradientState()
    {
        if (_isInitializing) return;
        _settings.LastGradientAngle = (int)AngleSlider.Value;
        _settings.LastGradientStopsHex = _gradientStops.Select(s => s.HexCode).ToList();
        _settingsService.SaveSettings(_settings);
    }

    private void GradientStopsCount_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        int targetCount = 3;
        if (GradientStops2Radio.IsChecked == true) targetCount = 2;
        else if (GradientStops3Radio.IsChecked == true) targetCount = 3;
        else if (GradientStops4Radio.IsChecked == true) targetCount = 4;

        while (_gradientStops.Count > targetCount)
        {
            _gradientStops.RemoveAt(_gradientStops.Count - 1);
        }

        var defaultColors = new[]
        {
            Color.FromArgb(255, 255, 0, 100),
            Color.FromArgb(255, 120, 0, 255),
            Color.FromArgb(255, 0, 210, 255),
            Color.FromArgb(255, 255, 200, 0)
        };

        while (_gradientStops.Count < targetCount)
        {
            int idx = _gradientStops.Count;
            Color c = defaultColors[idx % defaultColors.Length];
            _gradientStops.Add(new GradientColorStop($"Color {idx + 1}", c));
        }

        ReindexStopLabels();
        UpdateGradientPreview();
        SaveGradientState();
    }

    private void ReindexStopLabels()
    {
        for (int i = 0; i < _gradientStops.Count; i++)
        {
            _gradientStops[i].Label = $"Color {i + 1}";
        }
    }

    private void StopColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (sender.DataContext is GradientColorStop stop)
        {
            stop.Color = args.NewColor;
            UpdateGradientPreview();
            SaveGradientState();
        }
    }

    private void MoveStopLeft_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GradientColorStop stop)
        {
            int idx = _gradientStops.IndexOf(stop);
            if (idx > 0)
            {
                _gradientStops.Move(idx, idx - 1);
                ReindexStopLabels();
                UpdateGradientPreview();
                SaveGradientState();
            }
        }
    }

    private void MoveStopRight_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GradientColorStop stop)
        {
            int idx = _gradientStops.IndexOf(stop);
            if (idx >= 0 && idx < _gradientStops.Count - 1)
            {
                _gradientStops.Move(idx, idx + 1);
                ReindexStopLabels();
                UpdateGradientPreview();
                SaveGradientState();
            }
        }
    }

    private void GradientStopsListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        ReindexStopLabels();
        UpdateGradientPreview();
        SaveGradientState();
    }

    private void SetAngle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string angleStr && int.TryParse(angleStr, out int angle))
        {
            AngleSlider.Value = angle;
        }
    }

    private void AngleSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (AngleText != null)
        {
            AngleText.Text = $"{(int)e.NewValue}°";
        }
        UpdateGradientPreview();
        SaveGradientState();
    }

    private void UpdateGradientPreview()
    {
        if (GradientPreviewBorder == null || _gradientStops == null || _gradientStops.Count < 2) return;

        double angle = AngleSlider.Value;
        double rad = angle * Math.PI / 180.0;

        // Convert angle into start and end points for LinearGradientBrush
        double startX = 0.5 - 0.5 * Math.Cos(rad);
        double startY = 0.5 - 0.5 * Math.Sin(rad);
        double endX = 0.5 + 0.5 * Math.Cos(rad);
        double endY = 0.5 + 0.5 * Math.Sin(rad);

        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(startX, startY),
            EndPoint = new Windows.Foundation.Point(endX, endY)
        };

        int count = _gradientStops.Count;
        for (int i = 0; i < count; i++)
        {
            double offset = (double)i / (count - 1);
            brush.GradientStops.Add(new GradientStop
            {
                Color = _gradientStops[i].Color,
                Offset = offset
            });
        }

        GradientPreviewBorder.Background = brush;
    }

    private void SaveAndSetGradient_Click(object sender, RoutedEventArgs e)
    {
        if (_gradientStops.Count < 2) return;

        List<string> hexColors = _gradientStops.Select(s => s.HexCode).ToList();
        int angle = (int)AngleSlider.Value;

        string savedPath = _themeService.GenerateAndSaveGradientWallpaper(hexColors, angle);
        _settings.LastGradientFilename = Path.GetFileName(savedPath);
        _settings.LastGradientAngle = angle;
        _settings.LastGradientStopsHex = hexColors;
        _settingsService.SaveSettings(_settings);

        LoadSavedGradientsGallery();
    }

    private void LoadSavedGradientsGallery()
    {
        SavedGradientsItemsControl.Items.Clear();

        var savedFiles = _themeService.GetSavedGradients();
        foreach (string file in savedFiles)
        {
            string filename = Path.GetFileName(file);

            var border = new Border
            {
                Width = 140,
                Height = 85,
                CornerRadius = new CornerRadius(8),
                BorderBrush = (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 8),
                Tag = file
            };

            try
            {
                var img = new Image
                {
                    Source = new BitmapImage(new Uri(file)),
                    Stretch = Stretch.UniformToFill
                };
                border.Child = img;
            }
            catch { }

            var btn = new Button
            {
                Padding = new Thickness(0),
                Content = border,
                Tag = file
            };
            ToolTipService.SetToolTip(btn, filename);
            btn.Click += SavedGradientItem_Click;

            SavedGradientsItemsControl.Items.Add(btn);
        }
    }

    private void SavedGradientItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string file && File.Exists(file))
        {
            if (WindowsThemeService.TryParseGradientFilename(file, out var hexColors, out int angle))
            {
                _isInitializing = true;
                try
                {
                    AngleSlider.Value = angle;
                    _gradientStops.Clear();
                    for (int i = 0; i < hexColors.Count; i++)
                    {
                        Color c = ParseHexStringToColor(hexColors[i]);
                        _gradientStops.Add(new GradientColorStop($"Color {i + 1}", c));
                    }

                    if (hexColors.Count == 2) GradientStops2Radio.IsChecked = true;
                    else if (hexColors.Count == 3) GradientStops3Radio.IsChecked = true;
                    else if (hexColors.Count == 4) GradientStops4Radio.IsChecked = true;
                }
                finally
                {
                    _isInitializing = false;
                }

                UpdateGradientPreview();

                _settings.LastGradientAngle = angle;
                _settings.LastGradientStopsHex = hexColors;
                _settings.LastGradientFilename = Path.GetFileName(file);
                _settingsService.SaveSettings(_settings);

                // Set as current wallpaper
                _themeService.SetSingleImageWallpaper(file);
            }
        }
    }

    private Color ParseHexStringToColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Color.FromArgb(255, r, g, b);
        }
        else if (hex.Length == 8)
        {
            byte a = Convert.ToByte(hex.Substring(0, 2), 16);
            byte r = Convert.ToByte(hex.Substring(2, 2), 16);
            byte g = Convert.ToByte(hex.Substring(4, 2), 16);
            byte b = Convert.ToByte(hex.Substring(6, 2), 16);
            return Color.FromArgb(a, r, g, b);
        }
        return Color.FromArgb(255, 0, 0, 0);
    }

    #endregion
}

public class GradientColorStop : INotifyPropertyChanged
{
    private string _label = string.Empty;
    private Color _color;

    public event PropertyChangedEventHandler? PropertyChanged;

    public GradientColorStop(string label, Color color)
    {
        _label = label;
        _color = color;
    }

    public string Label
    {
        get => _label;
        set
        {
            if (_label != value)
            {
                _label = value;
                OnPropertyChanged();
            }
        }
    }

    public Color Color
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HexCode));
                OnPropertyChanged(nameof(ColorBrush));
            }
        }
    }

    public string HexCode => $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}";

    public SolidColorBrush ColorBrush => new SolidColorBrush(Color);

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
