using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EchoBox.Core.Models;

namespace EchoBox.Core.Services;

public class ProfileService
{
    private readonly string _profilesDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ProfileService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _profilesDirectory = Path.Combine(localAppData, "EchoBox-Icons", "profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public string ProfilesDirectory => _profilesDirectory;

    public List<IconProfile> LoadAllProfiles()
    {
        var profiles = new List<IconProfile>();
        var files = Directory.GetFiles(_profilesDirectory, "*.json");

        foreach (var file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<IconProfile>(json, JsonOptions);
                if (profile != null)
                {
                    SanitizeProfile(profile);
                    profiles.Add(profile);
                }
            }
            catch
            {
                // Skip invalid JSON
            }
        }

        if (profiles.Count == 0)
        {
            var defaultProfile = CreateDefaultProfile();
            SaveProfile(defaultProfile);
            profiles.Add(defaultProfile);
        }

        return profiles;
    }

    private static void SanitizeProfile(IconProfile profile)
    {
        if (profile?.Mappings == null) return;
        profile.Mappings.RemoveAll(m =>
            m.TargetType == IconTargetType.Executable ||
            string.Equals(m.FileExtension, ".exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.TargetName, "Applications (.exe)", StringComparison.OrdinalIgnoreCase));
    }

    public IconProfile LoadProfile(string profileId)
    {
        var profiles = LoadAllProfiles();
        var match = profiles.FirstOrDefault(p => p.Id == profileId);
        if (match != null) return match;
        
        return profiles.FirstOrDefault() ?? CreateDefaultProfile();
    }

    public void SaveProfile(IconProfile profile)
    {
        profile.LastModifiedDate = DateTime.UtcNow;
        string path = Path.Combine(_profilesDirectory, $"{profile.Id}.json");
        string json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public void DeleteProfile(string profileId)
    {
        string path = Path.Combine(_profilesDirectory, $"{profileId}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public IconProfile CreateDefaultProfile()
    {
        return new IconProfile
        {
            Id = "default-profile-001",
            Name = "Default Profile",
            Description = "Standard Windows icon customization profile",
            IsFullDriveScan = true,
            TargetFolderPath = @"C:\",
            Scope = TargetScopeOption.FullDrive,
            Mappings = new List<IconMapping>
            {
                new IconMapping
                {
                    TargetType = IconTargetType.Folder,
                    TargetName = "Folders",
                    IsSingleIconOnly = false,
                    SelectedIconPaths = new List<string>()
                },
                new IconMapping
                {
                    TargetType = IconTargetType.Document,
                    TargetName = "Documents (.doc / .docx)",
                    FileExtension = ".docx",
                    IsSingleIconOnly = true,
                    SelectedIconPaths = new List<string>()
                },
                new IconMapping
                {
                    TargetType = IconTargetType.MyComputer,
                    TargetName = "This PC / My Computer",
                    IsSingleIconOnly = true,
                    SelectedIconPaths = new List<string>()
                },
                new IconMapping
                {
                    TargetType = IconTargetType.Network,
                    TargetName = "Network",
                    IsSingleIconOnly = true,
                    SelectedIconPaths = new List<string>()
                },
                new IconMapping
                {
                    TargetType = IconTargetType.RecycleBinEmpty,
                    TargetName = "Recycle Bin (Empty)",
                    IsSingleIconOnly = true,
                    SelectedIconPaths = new List<string>()
                },
                new IconMapping
                {
                    TargetType = IconTargetType.RecycleBinFull,
                    TargetName = "Recycle Bin (Full)",
                    IsSingleIconOnly = true,
                    SelectedIconPaths = new List<string>()
                },
                new IconMapping
                {
                    TargetType = IconTargetType.CustomExtension,
                    TargetName = "PDF Files (.pdf)",
                    FileExtension = ".pdf",
                    IsSingleIconOnly = true,
                    SelectedIconPaths = new List<string>()
                },
                new IconMapping
                {
                    TargetType = IconTargetType.CustomExtension,
                    TargetName = "Text Files (.txt)",
                    FileExtension = ".txt",
                    IsSingleIconOnly = true,
                    SelectedIconPaths = new List<string>()
                },
                new IconMapping
                {
                    TargetType = IconTargetType.CustomExtension,
                    TargetName = "Zip Archives (.zip)",
                    FileExtension = ".zip",
                    IsSingleIconOnly = true,
                    SelectedIconPaths = new List<string>()
                }
            }
        };
    }
}
