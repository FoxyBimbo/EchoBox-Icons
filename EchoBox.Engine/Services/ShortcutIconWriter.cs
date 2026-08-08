using System;
using System.IO;
using EchoBox.Core.Services;
using EchoBox.Engine.Interop;

namespace EchoBox.Engine.Services;

public class ShortcutIconWriter
{
    public bool ApplyShortcutIcon(string shortcutPath, string iconPath, int iconIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath) || !File.Exists(shortcutPath)) return false;
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath)) return false;

        try
        {
            // Clear ReadOnly attribute if present
            var attr = File.GetAttributes(shortcutPath);
            if ((attr & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(shortcutPath, attr & ~FileAttributes.ReadOnly);
            }

            string ext = Path.GetExtension(shortcutPath);
            if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                NativeMethods.WritePrivateProfileStringW("InternetShortcut", "IconFile", iconPath, shortcutPath);
                NativeMethods.WritePrivateProfileStringW("InternetShortcut", "IconIndex", iconIndex.ToString(), shortcutPath);
                NativeMethods.NotifyFolderUpdated(shortcutPath);
                return true;
            }

            // For .lnk files using Windows Script Host COM (WshShell)
            Type? wshType = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
            if (wshType != null)
            {
                dynamic shell = Activator.CreateInstance(wshType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.IconLocation = $"{iconPath}, {iconIndex}";
                shortcut.Save();

                NativeMethods.NotifyFolderUpdated(shortcutPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, $"ShortcutIconWriter.ApplyIcon ({shortcutPath})");
        }

        return false;
    }
}
