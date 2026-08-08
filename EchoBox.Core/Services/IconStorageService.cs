using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EchoBox.Core.Models;

namespace EchoBox.Core.Services;

public class IconStorageService
{
    private readonly string _icoDirectory;
    private readonly string _categoriesFilePath;
    private readonly string _iconsMetadataPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IconStorageService()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string baseDir = Path.Combine(localAppData, "EchoBox-Icons");
        _icoDirectory = Path.Combine(baseDir, "ico");
        _categoriesFilePath = Path.Combine(baseDir, "categories.json");
        _iconsMetadataPath = Path.Combine(baseDir, "icons_metadata.json");

        Directory.CreateDirectory(_icoDirectory);
    }

    public string IcoDirectory => _icoDirectory;

    public List<IconCategory> LoadCategories()
    {
        if (File.Exists(_categoriesFilePath))
        {
            try
            {
                string json = File.ReadAllText(_categoriesFilePath);
                var categories = JsonSerializer.Deserialize<List<IconCategory>>(json, JsonOptions);
                if (categories != null && categories.Count > 0) return categories;
            }
            catch
            {
                // Fallback default
            }
        }

        var defaults = new List<IconCategory>
        {
            new IconCategory { Id = "cat-folders", Name = "Folders", ColorHex = "#0078D4" },
            new IconCategory { Id = "cat-apps", Name = "Applications", ColorHex = "#107C41" },
            new IconCategory { Id = "cat-documents", Name = "Documents", ColorHex = "#D83B01" },
            new IconCategory { Id = "cat-system", Name = "System", ColorHex = "#5C2D91" }
        };
        SaveCategories(defaults);
        return defaults;
    }

    public void SaveCategories(List<IconCategory> categories)
    {
        string json = JsonSerializer.Serialize(categories, JsonOptions);
        File.WriteAllText(_categoriesFilePath, json);
    }

    public string GetCategoryFolderPath(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return _icoDirectory;
        return Path.Combine(_icoDirectory, categoryName.Trim().ToLowerInvariant());
    }

    public List<IconItem> LoadIcons()
    {
        var categories = LoadCategories();
        var categoryFolderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cat in categories)
        {
            if (!string.IsNullOrWhiteSpace(cat.Name))
                categoryFolderMap[cat.Name.Trim().ToLowerInvariant()] = cat.Id;
            if (!string.IsNullOrWhiteSpace(cat.Id))
                categoryFolderMap[cat.Id.Trim().ToLowerInvariant()] = cat.Id;
        }

        var iconMetadataMapByPath = new Dictionary<string, IconItem>(StringComparer.OrdinalIgnoreCase);
        var iconMetadataMapByRel = new Dictionary<string, IconItem>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(_iconsMetadataPath))
        {
            try
            {
                string json = File.ReadAllText(_iconsMetadataPath);
                var items = JsonSerializer.Deserialize<List<IconItem>>(json, JsonOptions);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (!string.IsNullOrEmpty(item.FilePath))
                            iconMetadataMapByPath[item.FilePath] = item;
                        if (!string.IsNullOrEmpty(item.RelativePath))
                            iconMetadataMapByRel[item.RelativePath] = item;
                    }
                }
            }
            catch
            {
                // Fall back to scanning files
            }
        }

        var result = new List<IconItem>();
        var seenHashes = new Dictionary<string, IconItem>(StringComparer.OrdinalIgnoreCase);
        var seenNames = new Dictionary<string, IconItem>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(_icoDirectory))
        {
            var dirInfo = new DirectoryInfo(_icoDirectory);
            var icoFiles = dirInfo.EnumerateFiles("*.ico", SearchOption.AllDirectories)
                .OrderBy(f => f.FullName.Length)
                .ToList();

            foreach (var fileInfo in icoFiles)
            {
                string filePath = fileInfo.FullName;
                string relPath = Path.GetRelativePath(_icoDirectory, filePath);
                string fileHash = GetFileHash(filePath);
                string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath).Trim();

                bool isDuplicate = false;
                IconItem? existingIcon = null;

                if (!string.IsNullOrEmpty(fileHash) && seenHashes.TryGetValue(fileHash, out existingIcon))
                {
                    isDuplicate = true;
                }
                else if (!string.IsNullOrEmpty(fileNameNoExt) && seenNames.TryGetValue(fileNameNoExt, out existingIcon))
                {
                    if (existingIcon.FilePath != null && File.Exists(existingIcon.FilePath))
                    {
                        var existingInfo = new FileInfo(existingIcon.FilePath);
                        if (existingInfo.Length == fileInfo.Length || AreFilesEqualByHash(filePath, existingIcon.FilePath))
                        {
                            isDuplicate = true;
                        }
                    }
                }

                string[] relParts = relPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string? folderCatId = null;
                if (relParts.Length > 1)
                {
                    string folderName = relParts[0].Trim().ToLowerInvariant();
                    categoryFolderMap.TryGetValue(folderName, out folderCatId);
                }

                if (isDuplicate && existingIcon != null)
                {
                    existingIcon.CategoryIds ??= new List<string>();
                    if (folderCatId != null && !existingIcon.CategoryIds.Contains(folderCatId))
                    {
                        existingIcon.CategoryIds.Add(folderCatId);
                    }
                    if (iconMetadataMapByPath.TryGetValue(filePath, out var metaItem) || iconMetadataMapByRel.TryGetValue(relPath, out metaItem))
                    {
                        if (metaItem.CategoryIds != null)
                        {
                            foreach (var catId in metaItem.CategoryIds)
                            {
                                if (!existingIcon.CategoryIds.Contains(catId))
                                {
                                    existingIcon.CategoryIds.Add(catId);
                                }
                            }
                        }
                    }

                    try
                    {
                        if (File.Exists(filePath) && existingIcon.FilePath != null && !string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(existingIcon.FilePath), StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(filePath);
                        }
                    }
                    catch
                    {
                        // Ignore file deletion if locked
                    }

                    continue;
                }

                IconItem iconItem;
                if (iconMetadataMapByPath.TryGetValue(filePath, out var existingMeta) ||
                    iconMetadataMapByRel.TryGetValue(relPath, out existingMeta))
                {
                    existingMeta.FilePath = filePath;
                    existingMeta.RelativePath = relPath;
                    existingMeta.CategoryIds ??= new List<string>();
                    iconItem = existingMeta;
                }
                else
                {
                    iconItem = new IconItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = fileNameNoExt,
                        FilePath = filePath,
                        RelativePath = relPath,
                        CategoryIds = new List<string>(),
                        DateAdded = fileInfo.CreationTimeUtc
                    };
                }

                if (folderCatId != null && !iconItem.CategoryIds.Contains(folderCatId))
                {
                    iconItem.CategoryIds.Add(folderCatId);
                }

                if (!string.IsNullOrEmpty(fileHash))
                {
                    seenHashes[fileHash] = iconItem;
                }
                if (!string.IsNullOrEmpty(fileNameNoExt))
                {
                    seenNames[fileNameNoExt] = iconItem;
                }

                result.Add(iconItem);
            }
        }

        SaveIconsMetadata(result);
        return result;
    }

    public void MoveIconToCategoryFolder(IconItem icon, IconCategory? category)
    {
        string targetDir = category != null && !string.IsNullOrWhiteSpace(category.Name)
            ? GetCategoryFolderPath(category.Name)
            : _icoDirectory;

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string fileName = Path.GetFileName(icon.FilePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{icon.Name}.ico";
        }
        string targetPath = Path.Combine(targetDir, fileName);

        if (File.Exists(icon.FilePath) && !string.Equals(Path.GetFullPath(icon.FilePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(icon.FilePath, targetPath);
        }

        icon.FilePath = targetPath;
        icon.RelativePath = Path.GetRelativePath(_icoDirectory, targetPath);
    }

    public void RenameCategory(IconCategory category, string newName, List<IconItem> allIcons)
    {
        string oldName = category.Name;
        string newNameTrimmed = newName.Trim();
        string oldFolder = GetCategoryFolderPath(oldName);
        string newFolder = GetCategoryFolderPath(newNameTrimmed);

        if (Directory.Exists(oldFolder) && !string.Equals(oldFolder, newFolder, StringComparison.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(newFolder))
            {
                Directory.CreateDirectory(newFolder);
            }

            var files = Directory.GetFiles(oldFolder, "*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                string dest = Path.Combine(newFolder, Path.GetFileName(file));
                if (File.Exists(dest)) File.Delete(dest);
                File.Move(file, dest);
            }

            if (Directory.GetFileSystemEntries(oldFolder).Length == 0)
            {
                try { Directory.Delete(oldFolder); } catch { }
            }
        }

        category.Name = newNameTrimmed;

        var categories = LoadCategories();
        var existingCat = categories.FirstOrDefault(c => c.Id == category.Id);
        if (existingCat != null)
        {
            existingCat.Name = newNameTrimmed;
            SaveCategories(categories);
        }

        foreach (var icon in allIcons)
        {
            if (icon.CategoryIds.Contains(category.Id) || (icon.FilePath.StartsWith(oldFolder, StringComparison.OrdinalIgnoreCase)))
            {
                string fileName = Path.GetFileName(icon.FilePath);
                string newPath = Path.Combine(newFolder, fileName);
                icon.FilePath = newPath;
                icon.RelativePath = Path.GetRelativePath(_icoDirectory, newPath);
            }
        }

        SaveIconsMetadata(allIcons);
    }

    public List<(string CategoryFilePath, string RootFilePath, string Name)> DeleteCategory(IconCategory category, List<IconCategory> categories, List<IconItem> allIcons)
    {
        string categoryFolder = GetCategoryFolderPath(category.Name);
        var conflicts = new List<(string CategoryFilePath, string RootFilePath, string Name)>();

        foreach (var icon in allIcons)
        {
            if (icon.CategoryIds.Contains(category.Id))
            {
                icon.CategoryIds.Remove(category.Id);
                if (icon.CategoryIds.Count == 0)
                {
                    MoveIconToCategoryFolder(icon, null);
                }
            }
        }

        if (Directory.Exists(categoryFolder))
        {
            var remainingFiles = Directory.GetFiles(categoryFolder, "*", SearchOption.TopDirectoryOnly);
            foreach (var file in remainingFiles)
            {
                string fileName = Path.GetFileName(file);
                string dest = Path.Combine(_icoDirectory, fileName);
                if (File.Exists(dest) && !string.Equals(Path.GetFullPath(file), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                {
                    if (AreFilesEqualByHash(file, dest))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    else
                    {
                        conflicts.Add((file, dest, Path.GetFileNameWithoutExtension(file)));
                    }
                }
                else if (!File.Exists(dest))
                {
                    File.Move(file, dest);
                }
            }

            if (conflicts.Count == 0 && Directory.Exists(categoryFolder) && Directory.GetFileSystemEntries(categoryFolder).Length == 0)
            {
                try { Directory.Delete(categoryFolder); } catch { }
            }
        }

        categories.RemoveAll(c => c.Id == category.Id);
        SaveCategories(categories);
        SaveIconsMetadata(allIcons);

        return conflicts;
    }

    public static string GetFileHash(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        try
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool AreFilesEqualByHash(string path1, string path2)
    {
        if (!File.Exists(path1) || !File.Exists(path2)) return false;
        try
        {
            var info1 = new FileInfo(path1);
            var info2 = new FileInfo(path2);
            if (info1.Length != info2.Length) return false;

            string hash1 = GetFileHash(path1);
            string hash2 = GetFileHash(path2);
            return !string.IsNullOrEmpty(hash1) && string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void SyncIconToCategoryFolders(IconItem icon, List<IconCategory> categories)
    {
        if (categories != null)
        {
            icon.CategoryIds = categories.Select(c => c.Id).Distinct().ToList();
        }

        if (!File.Exists(icon.FilePath)) return;

        string sourcePath = icon.FilePath;
        string fileName = Path.GetFileName(sourcePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{icon.Name}.ico";
        }

        string targetDir = (categories != null && categories.Count > 0 && !string.IsNullOrWhiteSpace(categories[0].Name))
            ? GetCategoryFolderPath(categories[0].Name)
            : _icoDirectory;

        Directory.CreateDirectory(targetDir);
        string targetPath = Path.Combine(targetDir, fileName);

        if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(sourcePath, targetPath);
        }

        icon.FilePath = targetPath;
        icon.RelativePath = Path.GetRelativePath(_icoDirectory, targetPath);
    }

    public void SaveIconsMetadata(List<IconItem> icons)
    {
        string json = JsonSerializer.Serialize(icons, JsonOptions);
        File.WriteAllText(_iconsMetadataPath, json);
    }

    public void DeleteIcon(IconItem icon)
    {
        if (File.Exists(icon.FilePath))
        {
            try { File.Delete(icon.FilePath); } catch { }
        }

        var icons = LoadIcons().Where(i => i.FilePath != icon.FilePath).ToList();
        SaveIconsMetadata(icons);
    }
}
