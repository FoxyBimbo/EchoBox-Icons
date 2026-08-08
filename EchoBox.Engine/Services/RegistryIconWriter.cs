using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace EchoBox.Engine.Services;

public class RegistryIconWriter
{
    private const string RecycleBinClsid = @"{645FF040-5081-101B-9F08-00AA002F954E}";
    private const string MyComputerClsid = @"{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string NetworkClsid = @"{F02C509E-3756-45E7-B660-E580DE92B198}";

    public bool ApplyFileExtensionIcon(string extension, string iconPath)
    {
        if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(iconPath)) return false;

        if (!extension.StartsWith("."))
        {
            extension = "." + extension;
        }

        bool success = false;
        try
        {
            // 1. Set HKCU\Software\Classes\<.ext>\DefaultIcon
            string extKeyPath = $@"Software\Classes\{extension}\DefaultIcon";
            using (var key = Registry.CurrentUser.CreateSubKey(extKeyPath))
            {
                if (key != null)
                {
                    key.SetValue("", $"{iconPath},0");
                    success = true;
                }
            }

            // 2. Resolve all associated ProgIDs for this extension
            var progIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Check UserChoice (HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\<.ext>\UserChoice)
            using (var userChoiceKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice"))
            {
                var userChoiceProgId = userChoiceKey?.GetValue("ProgId") as string;
                if (!string.IsNullOrWhiteSpace(userChoiceProgId))
                {
                    progIds.Add(userChoiceProgId);
                }
            }

            // Check HKCU\Software\Classes\<.ext>
            using (var extKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}"))
            {
                var progId = extKey?.GetValue("") as string;
                if (!string.IsNullOrWhiteSpace(progId))
                {
                    progIds.Add(progId);
                }
            }

            // Check HKCR (or HKLM\Software\Classes)
            using (var hkcrExtKey = Registry.ClassesRoot.OpenSubKey(extension))
            {
                var hkcrProgId = hkcrExtKey?.GetValue("") as string;
                if (!string.IsNullOrWhiteSpace(hkcrProgId))
                {
                    progIds.Add(hkcrProgId);
                }
            }

            // Check OpenWithProgids
            using (var openWithKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithProgids"))
            {
                if (openWithKey != null)
                {
                    foreach (var valueName in openWithKey.GetValueNames())
                    {
                        if (!string.IsNullOrWhiteSpace(valueName))
                        {
                            progIds.Add(valueName);
                        }
                    }
                }
            }

            // If extension is .zip, add common zip ProgIDs to guarantee .zip handling
            if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                progIds.Add("CompressedFolder");
                progIds.Add("7-Zip.zip");
                progIds.Add("WinRAR.ZIP");
                progIds.Add("WinRAR");
                progIds.Add("Bandizip.ZIP");
            }

            // 3. Write DefaultIcon for each ProgID in HKCU\Software\Classes\<ProgID>\DefaultIcon
            foreach (var progId in progIds)
            {
                try
                {
                    using var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}\DefaultIcon");
                    if (progIdKey != null)
                    {
                        progIdKey.SetValue("", $"{iconPath},0");
                        success = true;
                    }
                }
                catch
                {
                    // Continue with other ProgIDs
                }
            }

            return success;
        }
        catch
        {
            return false;
        }
    }

    public bool ApplyRecycleBinIcons(string? emptyIconPath, string? fullIconPath)
    {
        try
        {
            string[] keyPaths = new[]
            {
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{RecycleBinClsid}\DefaultIcon",
                $@"Software\Classes\CLSID\{RecycleBinClsid}\DefaultIcon"
            };

            bool success = false;
            foreach (var keyPath in keyPaths)
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                if (key == null) continue;

                if (!string.IsNullOrEmpty(emptyIconPath))
                {
                    key.SetValue("empty", $"{emptyIconPath},0");
                    key.SetValue("", $"{emptyIconPath},0"); // Default
                }
                if (!string.IsNullOrEmpty(fullIconPath))
                {
                    key.SetValue("full", $"{fullIconPath},0");
                }
                success = true;
            }

            return success;
        }
        catch
        {
            return false;
        }
    }

    public bool ApplyMyComputerIcon(string iconPath)
    {
        try
        {
            string[] keyPaths = new[]
            {
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{MyComputerClsid}\DefaultIcon",
                $@"Software\Classes\CLSID\{MyComputerClsid}\DefaultIcon"
            };

            bool success = false;
            foreach (var keyPath in keyPaths)
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                if (key == null) continue;

                key.SetValue("", $"{iconPath},0");
                success = true;
            }

            return success;
        }
        catch
        {
            return false;
        }
    }

    public bool ApplyNetworkIcon(string iconPath)
    {
        try
        {
            string[] keyPaths = new[]
            {
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{NetworkClsid}\DefaultIcon",
                $@"Software\Classes\CLSID\{NetworkClsid}\DefaultIcon"
            };

            bool success = false;
            foreach (var keyPath in keyPaths)
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                if (key == null) continue;

                key.SetValue("", $"{iconPath},0");
                success = true;
            }

            return success;
        }
        catch
        {
            return false;
        }
    }
}
