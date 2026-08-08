using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using EchoBox.Engine.Interop;

namespace EchoBox.Engine.Services;

public class WindowsThemeService
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AccentKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string DwmKeyPath = @"Software\Microsoft\Windows\DWM";
    private const string TextInputKeyPath = @"Software\Microsoft\InputMethod\CandidateWindow";
    private const string DesktopKeyPath = @"Control Panel\Desktop";
    private const string DesktopColorsKeyPath = @"Control Panel\Colors";
    private const string DesktopSlideshowKeyPath = @"Control Panel\Personalization\Desktop Slideshow";

    public string BackgroundsDirectory
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string baseDir = Path.Combine(localAppData, "EchoBox-Icons");
            string backgroundsDir = Path.Combine(baseDir, "backgrounds");
            Directory.CreateDirectory(backgroundsDir);
            return backgroundsDir;
        }
    }

    #region Windows Theme Mode (Light / Dark)

    public bool GetSystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            var val = key?.GetValue("SystemUsesLightTheme");
            if (val != null)
            {
                return Convert.ToInt32(val) != 0;
            }
        }
        catch { }
        return true;
    }

    public bool GetAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            var val = key?.GetValue("AppsUseLightTheme");
            if (val != null)
            {
                return Convert.ToInt32(val) != 0;
            }
        }
        catch { }
        return true;
    }

    public void SetSystemUsesLightTheme(bool lightMode)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PersonalizeKeyPath);
            key?.SetValue("SystemUsesLightTheme", lightMode ? 1 : 0, RegistryValueKind.DWord);
            NativeMethods.BroadcastThemeChange();
        }
        catch { }
    }

    public void SetAppsUseLightTheme(bool lightMode)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(PersonalizeKeyPath);
            key?.SetValue("AppsUseLightTheme", lightMode ? 1 : 0, RegistryValueKind.DWord);
            NativeMethods.BroadcastThemeChange();
        }
        catch { }
    }

    #endregion

    #region Accent Color

    public (byte R, byte G, byte B, byte A) GetAccentColor()
    {
        try
        {
            var uiSettings = new Windows.UI.ViewManagement.UISettings();
            var c = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            return (c.R, c.G, c.B, c.A);
        }
        catch
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(DwmKeyPath);
                var val = key?.GetValue("AccentColor");
                if (val != null)
                {
                    uint dwmAccent = Convert.ToUInt32(val);
                    byte a = (byte)((dwmAccent >> 24) & 0xFF);
                    byte b = (byte)((dwmAccent >> 16) & 0xFF);
                    byte g = (byte)((dwmAccent >> 8) & 0xFF);
                    byte r = (byte)(dwmAccent & 0xFF);
                    if (a == 0) a = 255;
                    return (r, g, b, a);
                }
            }
            catch { }
        }
        return (0, 120, 215, 255); // Default Windows Blue
    }

    public void SetAccentColor(byte r, byte g, byte b)
    {
        try
        {
            // ABGR format for AccentColorMenu & DWM
            uint abgr = 0xFF000000 | ((uint)b << 16) | ((uint)g << 8) | r;
            uint argb = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;

            using (var key = Registry.CurrentUser.CreateSubKey(DwmKeyPath))
            {
                key?.SetValue("AccentColor", (int)abgr, RegistryValueKind.DWord);
                key?.SetValue("ColorizationColor", (int)argb, RegistryValueKind.DWord);
            }

            using (var key = Registry.CurrentUser.CreateSubKey(AccentKeyPath))
            {
                key?.SetValue("AccentColorMenu", (int)abgr, RegistryValueKind.DWord);
                key?.SetValue("StartColorMenu", (int)abgr, RegistryValueKind.DWord);

                // Generate a 32-byte AccentPalette array
                byte[] palette = CreateAccentPalette(r, g, b);
                key?.SetValue("AccentPalette", palette, RegistryValueKind.Binary);
            }

            NativeMethods.BroadcastThemeChange();
        }
        catch { }
    }

    private byte[] CreateAccentPalette(byte r, byte g, byte b)
    {
        // 32-byte array (8 RGBA color variations)
        byte[] palette = new byte[32];
        double[] factors = { 0.5, 0.7, 0.85, 1.0, 1.15, 1.3, 1.5, 1.7 };
        for (int i = 0; i < 8; i++)
        {
            double f = factors[i];
            palette[i * 4 + 0] = (byte)Math.Clamp((int)(r * f), 0, 255);
            palette[i * 4 + 1] = (byte)Math.Clamp((int)(g * f), 0, 255);
            palette[i * 4 + 2] = (byte)Math.Clamp((int)(b * f), 0, 255);
            palette[i * 4 + 3] = 255;
        }
        return palette;
    }

    #endregion

    #region Text Input Settings

    public (byte R, byte G, byte B, byte A) GetTextInputAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TextInputKeyPath);
            var val = key?.GetValue("ColorAccent");
            if (val != null)
            {
                uint colorVal = Convert.ToUInt32(val);
                byte a = (byte)((colorVal >> 24) & 0xFF);
                byte r = (byte)((colorVal >> 16) & 0xFF);
                byte g = (byte)((colorVal >> 8) & 0xFF);
                byte b = (byte)(colorVal & 0xFF);
                if (a == 0) a = 255;
                return (r, g, b, a);
            }
        }
        catch { }
        return GetAccentColor();
    }

    public void SetTextInputAccentColor(byte r, byte g, byte b)
    {
        try
        {
            int colorVal = unchecked((int)(0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b));

            using var key = Registry.CurrentUser.CreateSubKey(TextInputKeyPath);
            key?.SetValue("ColorAccent", colorVal, RegistryValueKind.DWord);
            key?.SetValue("ThemeType", 2, RegistryValueKind.DWord); // Custom theme
            
            using var themeSubKey = Registry.CurrentUser.CreateSubKey(TextInputKeyPath + @"\Theme");
            themeSubKey?.SetValue("AccentColor", colorVal, RegistryValueKind.DWord);

            NativeMethods.BroadcastThemeChange();
        }
        catch { }
    }

    public int GetTextInputThemeMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(TextInputKeyPath);
            var val = key?.GetValue("ThemeType");
            if (val != null)
            {
                return Convert.ToInt32(val);
            }
        }
        catch { }
        return 3; // Default: Match System
    }

    public void SetTextInputThemeMode(int themeMode)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(TextInputKeyPath);
            key?.SetValue("ThemeType", themeMode, RegistryValueKind.DWord);
            NativeMethods.BroadcastThemeChange();
        }
        catch { }
    }

    #endregion

    #region Desktop Backgrounds (Single Image, Slideshow, Solid Color)

    public void SetSingleImageWallpaper(string imagePath)
    {
        if (!File.Exists(imagePath)) return;

        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(DesktopKeyPath))
            {
                key?.SetValue("Wallpaper", imagePath);
                key?.SetValue("WallpaperStyle", "10"); // Fill
                key?.SetValue("TileWallpaper", "0");
            }

            NativeMethods.SystemParametersInfo(
                NativeMethods.SPI_SETDESKWALLPAPER,
                0,
                imagePath,
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
        }
        catch { }
    }

    public (byte R, byte G, byte B) GetSolidColorWallpaper()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DesktopColorsKeyPath);
            var val = key?.GetValue("Background") as string;
            if (!string.IsNullOrWhiteSpace(val))
            {
                var parts = val.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3 &&
                    byte.TryParse(parts[0], out byte r) &&
                    byte.TryParse(parts[1], out byte g) &&
                    byte.TryParse(parts[2], out byte b))
                {
                    return (r, g, b);
                }
            }
        }
        catch { }
        return (0, 120, 215); // Default Blue
    }

    public void SetSolidColorWallpaper(byte r, byte g, byte b)
    {
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(DesktopKeyPath))
            {
                key?.SetValue("Wallpaper", "");
                key?.SetValue("WallpaperStyle", "10");
                key?.SetValue("TileWallpaper", "0");
            }

            using (var colorsKey = Registry.CurrentUser.CreateSubKey(DesktopColorsKeyPath))
            {
                colorsKey?.SetValue("Background", $"{r} {g} {b}");
            }

            uint rgbVal = (uint)(r | (g << 8) | (b << 16));
            NativeMethods.SetSysColors(1, new int[] { NativeMethods.COLOR_BACKGROUND }, new uint[] { rgbVal });

            NativeMethods.SystemParametersInfo(
                NativeMethods.SPI_SETDESKWALLPAPER,
                0,
                "",
                NativeMethods.SPIF_UPDATEINIFILE | NativeMethods.SPIF_SENDCHANGE);
        }
        catch { }
    }

    public void SetSlideshowWallpaper(string folderPath, int intervalMinutes = 30, bool shuffle = false)
    {
        if (!Directory.Exists(folderPath)) return;

        var images = Directory.GetFiles(folderPath)
            .Where(f => new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" }
                .Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (images.Count == 0) return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(DesktopSlideshowKeyPath);
            key?.SetValue("Directory", folderPath);
            key?.SetValue("Interval", intervalMinutes * 60 * 1000); // ms
            key?.SetValue("Shuffle", shuffle ? 1 : 0);

            // Set current wallpaper to first image
            SetSingleImageWallpaper(images[0]);
        }
        catch { }
    }

    #endregion

    #region Gradient Generation & Desktop Resolution

    public (int Width, int Height) GetScreenResolution()
    {
        int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        if (width <= 0 || height <= 0)
        {
            width = 1920;
            height = 1080;
        }
        return (width, height);
    }

    public string GenerateAndSaveGradientWallpaper(List<string> hexColors, int angleDegrees)
    {
        if (hexColors == null || hexColors.Count < 2)
        {
            throw new ArgumentException("Gradient requires at least 2 colors.", nameof(hexColors));
        }

        var (width, height) = GetScreenResolution();

        // Convert hex strings to Rgba32 colors
        List<Rgba32> colors = hexColors.Select(ParseHexColor).ToList();

        string filename = BuildGradientFilename(hexColors, angleDegrees);
        string outputPath = Path.Combine(BackgroundsDirectory, filename);

        using (Image<Rgba32> image = new Image<Rgba32>(width, height))
        {
            double angleRad = angleDegrees * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    double ny = (double)y / Math.Max(1, height - 1);

                    for (int x = 0; x < width; x++)
                    {
                        double nx = (double)x / Math.Max(1, width - 1);

                        // Project (nx - 0.5, ny - 0.5) along angle vector
                        double t = cos * (nx - 0.5) + sin * (ny - 0.5) + 0.5;
                        t = Math.Clamp(t, 0.0, 1.0);

                        row[x] = InterpolateMultiStopColor(colors, t);
                    }
                }
            });

            image.SaveAsPng(outputPath);
        }

        // Set as desktop wallpaper
        SetSingleImageWallpaper(outputPath);

        return outputPath;
    }

    private Rgba32 InterpolateMultiStopColor(List<Rgba32> colors, double t)
    {
        int count = colors.Count;
        if (count == 1) return colors[0];

        double step = 1.0 / (count - 1);
        int index = (int)(t / step);
        if (index >= count - 1) return colors[count - 1];

        double localT = (t - (index * step)) / step;
        Rgba32 c1 = colors[index];
        Rgba32 c2 = colors[index + 1];

        byte r = (byte)(c1.R + (c2.R - c1.R) * localT);
        byte g = (byte)(c1.G + (c2.G - c1.G) * localT);
        byte b = (byte)(c1.B + (c2.B - c1.B) * localT);
        byte a = (byte)(c1.A + (c2.A - c1.A) * localT);

        return new Rgba32(r, g, b, a);
    }

    private Rgba32 ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new Rgba32(r, g, b, 255);
        }
        else if (hex.Length == 8)
        {
            byte a = Convert.ToByte(hex.Substring(0, 2), 16);
            byte r = Convert.ToByte(hex.Substring(2, 2), 16);
            byte g = Convert.ToByte(hex.Substring(4, 2), 16);
            byte b = Convert.ToByte(hex.Substring(6, 2), 16);
            return new Rgba32(r, g, b, a);
        }
        return new Rgba32(0, 0, 0, 255);
    }

    public static string BuildGradientFilename(List<string> hexColors, int angleDegrees)
    {
        string colorParts = string.Join("_", hexColors.Select(c => c.TrimStart('#').ToUpperInvariant()));
        return $"gradient_a{angleDegrees}_c{hexColors.Count}_{colorParts}.png";
    }

    public static bool TryParseGradientFilename(string filename, out List<string> hexColors, out int angleDegrees)
    {
        hexColors = new List<string>();
        angleDegrees = 90;

        string name = Path.GetFileNameWithoutExtension(filename);
        if (!name.StartsWith("gradient_a", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            string[] parts = name.Split('_');
            if (parts.Length < 4) return false;

            // parts[1] is e.g. "a90"
            if (!int.TryParse(parts[1].Substring(1), out angleDegrees))
            {
                angleDegrees = 90;
            }

            // parts[2] is e.g. "c3"
            // parts[3..] are hex colors
            for (int i = 3; i < parts.Length; i++)
            {
                string hex = parts[i];
                if (hex.Length == 6 || hex.Length == 8)
                {
                    hexColors.Add("#" + hex);
                }
            }

            return hexColors.Count >= 2;
        }
        catch
        {
            return false;
        }
    }

    public List<string> GetSavedGradients()
    {
        if (!Directory.Exists(BackgroundsDirectory)) return new List<string>();

        return Directory.GetFiles(BackgroundsDirectory, "gradient_*.png")
            .OrderByDescending(File.GetLastWriteTime)
            .ToList();
    }

    #endregion
}
