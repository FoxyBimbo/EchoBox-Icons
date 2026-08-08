using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;

namespace EchoBox.Engine.Services;

public class FastFileSystemScanner
{
    private static readonly EnumerationOptions SafeOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint, // avoid infinite symlink loops
        MatchCasing = MatchCasing.PlatformDefault,
        MatchType = MatchType.Simple
    };

    public IEnumerable<string> EnumerateFolders(string rootPath, SafetyFilter safetyFilter)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            yield break;
        }

        var queue = new Queue<string>();
        if (safetyFilter.IsSafeToModify(rootPath))
        {
            queue.Enqueue(rootPath);
        }

        while (queue.Count > 0)
        {
            string currentDir = queue.Dequeue();
            yield return currentDir;

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(currentDir, "*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                });
            }
            catch
            {
                continue;
            }

            foreach (var subDir in subDirs)
            {
                if (safetyFilter.IsSafeToModify(subDir))
                {
                    queue.Enqueue(subDir);
                }
            }
        }
    }

    public IEnumerable<string> EnumerateFilesByExtension(string rootPath, string extension, SafetyFilter safetyFilter)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            yield break;
        }

        string searchPattern = extension.StartsWith(".") ? $"*{extension}" : $"*.{extension}";

        foreach (var folder in EnumerateFolders(rootPath, safetyFilter))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder, searchPattern, new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                });
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (safetyFilter.IsSafeToModify(file))
                {
                    yield return file;
                }
            }
        }
    }
}
