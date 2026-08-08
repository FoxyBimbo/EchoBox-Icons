using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EchoBox.Core.Services;
using EchoBox.Engine.Services;

namespace EchoBox.ShellExtension;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("EchoBox - Icons Shell Extension CLI");

        if (args.Length == 0)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  --register              Register Windows Explorer Context Menu");
            Console.WriteLine("  --unregister            Unregister Context Menu");
            Console.WriteLine("  --apply-folder <path>   Apply default profile icons to target folder");
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        if (command == "--register")
        {
            bool success = ContextMenuRegistrar.RegisterContextMenu();
            Console.WriteLine(success ? "Context menu successfully registered!" : "Failed to register context menu.");
            return success ? 0 : 1;
        }

        if (command == "--unregister")
        {
            bool success = ContextMenuRegistrar.UnregisterContextMenu();
            Console.WriteLine(success ? "Context menu successfully unregistered!" : "Failed to unregister context menu.");
            return success ? 0 : 1;
        }

        if (command == "--apply-folder" && args.Length >= 2)
        {
            string targetPath = args[1];
            if (!Directory.Exists(targetPath))
            {
                Console.WriteLine($"Directory does not exist: {targetPath}");
                return 1;
            }

            Console.WriteLine($"Applying active profile icons to folder: {targetPath}");

            var profileService = new ProfileService();
            var settingsService = new SettingsService();

            var settings = settingsService.LoadSettings();
            var profile = profileService.LoadProfile(settings.SelectedProfileId);

            profile.IsFullDriveScan = false;
            profile.TargetFolderPath = targetPath;

            var pipeline = new IconApplierPipeline();
            var progress = new Progress<ApplyProgressReport>(report =>
            {
                Console.WriteLine($"Progress: {report.StatusMessage}");
            });

            using var cts = new CancellationTokenSource();
            await pipeline.ApplyProfileAsync(profile, settings, progress, cts.Token);
            Console.WriteLine("Done!");
            return 0;
        }

        Console.WriteLine($"Unknown command: {args[0]}");
        return 1;
    }
}
