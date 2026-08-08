using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EchoBox.Engine.Services;

public class SafetyFilter
{
    private readonly HashSet<string> _restrictedRoots;
    private readonly List<string> _customExclusions;

    public SafetyFilter(IEnumerable<string>? customExclusions = null)
    {
        _restrictedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // System directories to protect
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)); // ProgramData
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.System));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));

        // Drive roots default system folders
        string cDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        AddIfValid(Path.Combine(cDrive, "$Recycle.Bin"));
        AddIfValid(Path.Combine(cDrive, "System Volume Information"));
        AddIfValid(Path.Combine(cDrive, "Recovery"));
        AddIfValid(Path.Combine(cDrive, "PerfLogs"));
        AddIfValid(Path.Combine(cDrive, "MSOCache"));

        _customExclusions = customExclusions?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
    }

    private void AddIfValid(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            _restrictedRoots.Add(Path.GetFullPath(path).TrimEnd('\\'));
        }
    }

    public bool IsSafeToModify(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(targetPath).TrimEnd('\\');
        }
        catch
        {
            return false;
        }

        // Check root system restrictions
        foreach (var restrictedRoot in _restrictedRoots)
        {
            if (fullPath.Equals(restrictedRoot, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(restrictedRoot + @"\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check custom exclusions
        foreach (var customExclusion in _customExclusions)
        {
            try
            {
                string normCustom = Path.GetFullPath(customExclusion).TrimEnd('\\');
                if (fullPath.Equals(normCustom, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(normCustom + @"\", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            catch { }
        }

        // Check file attribute safety (skip hidden OS system files)
        try
        {
            if (File.Exists(fullPath))
            {
                var attrs = File.GetAttributes(fullPath);
                if (attrs.HasFlag(FileAttributes.System) && attrs.HasFlag(FileAttributes.Hidden))
                {
                    return false;
                }
            }
        }
        catch { }

        return true;
    }
}
