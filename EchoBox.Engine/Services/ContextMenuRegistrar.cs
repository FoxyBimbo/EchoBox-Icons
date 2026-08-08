using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace EchoBox.Engine.Services;

public static class ContextMenuRegistrar
{
    private const string MenuName = "EchoBoxIcons";
    private const string MenuTitle = "Apply EchoBox - Icons";

    public static bool RegisterContextMenu(string? appExePath = null)
    {
        try
        {
            string exePath = appExePath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;

            string commandString = $"\"{exePath}\" --apply-folder \"%1\"";

            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Directory\shell\{MenuName}"))
            {
                key?.SetValue("", MenuTitle);
                key?.SetValue("Icon", exePath);
                using (var cmdKey = key?.CreateSubKey("command"))
                {
                    cmdKey?.SetValue("", commandString);
                }
            }

            string bgCommandString = $"\"{exePath}\" --apply-folder \"%V\"";
            using (var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Directory\Background\shell\{MenuName}"))
            {
                key?.SetValue("", MenuTitle);
                key?.SetValue("Icon", exePath);
                using (var cmdKey = key?.CreateSubKey("command"))
                {
                    cmdKey?.SetValue("", bgCommandString);
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool UnregisterContextMenu()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Directory\shell\{MenuName}", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Directory\Background\shell\{MenuName}", throwOnMissingSubKey: false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
