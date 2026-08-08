using System;
using System.IO;
using System.Text.Json;
using EchoBox.Core.Models;

namespace EchoBox.Core.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string baseDir = Path.Combine(localAppData, "EchoBox-Icons");
        Directory.CreateDirectory(baseDir);
        _settingsFilePath = Path.Combine(baseDir, "settings.json");
    }

    public AppSettings LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null) return settings;
            }
            catch
            {
                // Fall back to default
            }
        }

        var defaultSettings = new AppSettings();
        SaveSettings(defaultSettings);
        return defaultSettings;
    }

    public void SaveSettings(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }
}
