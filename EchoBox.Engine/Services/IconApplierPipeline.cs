using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EchoBox.Core.Models;
using EchoBox.Engine.Interop;

namespace EchoBox.Engine.Services;

public class ApplyProgressReport
{
    public long ScannedCount;
    public long UpdatedCount;

    public long GetScannedCount() => Interlocked.Read(ref ScannedCount);
    public long GetUpdatedCount() => Interlocked.Read(ref UpdatedCount);

    public string CurrentItemPath { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsCancelled { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TargetItemJob
{
    public string Path { get; set; } = string.Empty;
    public IconMapping Mapping { get; set; } = new();
    public bool IsShortcut { get; set; }
}

public class IconApplierPipeline
{
    private readonly DesktopIniWriter _iniWriter = new();
    private readonly RegistryIconWriter _registryWriter = new();
    private readonly ShortcutIconWriter _shortcutWriter = new();
    private readonly FastFileSystemScanner _scanner = new();

    public async Task ApplyProfileAsync(
        IconProfile profile,
        AppSettings settings,
        IProgress<ApplyProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        var report = new ApplyProgressReport { StatusMessage = "Initializing safety checks..." };
        progress?.Report(report);

        var safetyFilter = new SafetyFilter(settings.CustomExcludedPaths);

        // 1. Apply system-wide registry icon mappings (My Computer, Network, Recycle Bin, File Extensions)
        foreach (var mapping in profile.Mappings)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (mapping.SelectedIconPaths == null || mapping.SelectedIconPaths.Count == 0) continue;

            string primaryIcon = mapping.GetNextIconPath();
            if (string.IsNullOrEmpty(primaryIcon)) continue;

            switch (mapping.TargetType)
            {
                case IconTargetType.MyComputer:
                    if (_registryWriter.ApplyMyComputerIcon(primaryIcon))
                        Interlocked.Increment(ref report.UpdatedCount);
                    break;

                case IconTargetType.Network:
                    if (_registryWriter.ApplyNetworkIcon(primaryIcon))
                        Interlocked.Increment(ref report.UpdatedCount);
                    break;

                case IconTargetType.RecycleBinEmpty:
                    if (_registryWriter.ApplyRecycleBinIcons(primaryIcon, null))
                        Interlocked.Increment(ref report.UpdatedCount);
                    break;

                case IconTargetType.RecycleBinFull:
                    if (_registryWriter.ApplyRecycleBinIcons(null, primaryIcon))
                        Interlocked.Increment(ref report.UpdatedCount);
                    break;

                case IconTargetType.Executable:
                case IconTargetType.Document:
                case IconTargetType.CustomExtension:
                case IconTargetType.CustomShortcut:
                    // Process file extension tokens using IsFileExtensionEntry
                    foreach (var entry in mapping.GetParsedEntries())
                    {
                        if (IconMapping.IsFileExtensionEntry(entry))
                        {
                            if (_registryWriter.ApplyFileExtensionIcon(entry, primaryIcon))
                            {
                                Interlocked.Increment(ref report.UpdatedCount);
                            }
                        }
                    }
                    break;
            }
        }

        // 2. Check Target Scope: SystemIconsOnly skips disk scanning completely
        if (profile.Scope == TargetScopeOption.SystemIconsOnly)
        {
            report.StatusMessage = "Refreshing Windows Shell Icon Cache...";
            progress?.Report(report);
            NativeMethods.RefreshShell();

            report.IsCompleted = true;
            report.StatusMessage = "Successfully applied system and file extension icons!";
            progress?.Report(report);
            return;
        }

        string rootPath = profile.Scope == TargetScopeOption.FullDrive || string.IsNullOrWhiteSpace(profile.TargetFolderPath)
            ? @"C:\"
            : profile.TargetFolderPath;

        if (!Directory.Exists(rootPath))
        {
            report.IsCompleted = true;
            report.ErrorMessage = $"Target root directory '{rootPath}' does not exist.";
            progress?.Report(report);
            return;
        }

        // 3. Find folder and shortcut mappings for disk crawling
        var folderMappings = profile.Mappings.FindAll(m => m.TargetType == IconTargetType.Folder && m.SelectedIconPaths.Count > 0);
        
        // Collect shortcut word mappings (entries that are NOT file extensions)
        var shortcutRules = new List<(string ShortcutWord, IconMapping Mapping)>();
        foreach (var m in profile.Mappings)
        {
            if (m.SelectedIconPaths.Count == 0) continue;
            foreach (var entry in m.GetParsedEntries())
            {
                if (!IconMapping.IsFileExtensionEntry(entry))
                {
                    shortcutRules.Add((entry, m));
                }
            }
        }

        if (folderMappings.Count > 0 || shortcutRules.Count > 0)
        {
            var channel = Channel.CreateBounded<TargetItemJob>(new BoundedChannelOptions(2000)
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            int workerCount = Math.Max(1, settings.MaxParallelThreads);

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    foreach (var folderPath in _scanner.EnumerateFolders(rootPath, safetyFilter))
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        Interlocked.Increment(ref report.ScannedCount);

                        // Queue Folder jobs
                        foreach (var mapping in folderMappings)
                        {
                            await channel.Writer.WriteAsync(new TargetItemJob
                            {
                                Path = folderPath,
                                Mapping = mapping,
                                IsShortcut = false
                            }, cancellationToken);
                        }

                        // Queue Shortcut jobs by searching for .lnk and .url files in folder
                        if (shortcutRules.Count > 0)
                        {
                            try
                            {
                                var shortcutFiles = Directory.EnumerateFiles(folderPath, "*", new EnumerationOptions
                                {
                                    IgnoreInaccessible = true,
                                    RecurseSubdirectories = false,
                                    AttributesToSkip = FileAttributes.ReparsePoint
                                }).Where(f => f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                              f.EndsWith(".url", StringComparison.OrdinalIgnoreCase));

                                foreach (var shortcutFile in shortcutFiles)
                                {
                                    string fileName = Path.GetFileName(shortcutFile);
                                    string extension = Path.GetExtension(shortcutFile);
                                    string shortcutName = fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                                        ? fileName.Substring(0, fileName.Length - extension.Length)
                                        : fileName;

                                    foreach (var rule in shortcutRules)
                                    {
                                        if (shortcutName.Equals(rule.ShortcutWord, StringComparison.OrdinalIgnoreCase) ||
                                            shortcutName.Contains(rule.ShortcutWord, StringComparison.OrdinalIgnoreCase) ||
                                            fileName.Equals(rule.ShortcutWord, StringComparison.OrdinalIgnoreCase) ||
                                            fileName.Contains(rule.ShortcutWord, StringComparison.OrdinalIgnoreCase))
                                        {
                                            await channel.Writer.WriteAsync(new TargetItemJob
                                            {
                                                Path = shortcutFile,
                                                Mapping = rule.Mapping,
                                                IsShortcut = true
                                            }, cancellationToken);
                                            break;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Ignore inaccessible folders
                            }
                        }
                    }
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);

            var workerTasks = new List<Task>();
            for (int i = 0; i < workerCount; i++)
            {
                workerTasks.Add(Task.Run(async () =>
                {
                    await foreach (var job in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        string iconToUse = job.Mapping.GetNextIconPath();
                        if (!string.IsNullOrEmpty(iconToUse))
                        {
                            if (job.IsShortcut)
                            {
                                if (_shortcutWriter.ApplyShortcutIcon(job.Path, iconToUse))
                                {
                                    Interlocked.Increment(ref report.UpdatedCount);
                                }
                            }
                            else
                            {
                                if (_iniWriter.ApplyFolderIcon(job.Path, iconToUse))
                                {
                                    Interlocked.Increment(ref report.UpdatedCount);
                                }
                            }
                        }

                        report.CurrentItemPath = job.Path;
                        report.StatusMessage = $"Scanning & applying icons... Scanned: {report.GetScannedCount():N0}, Applied: {report.GetUpdatedCount():N0}";
                        progress?.Report(report);
                    }
                }, cancellationToken));
            }

            try
            {
                await Task.WhenAll(producerTask, Task.WhenAll(workerTasks));
            }
            catch (OperationCanceledException)
            {
                report.IsCancelled = true;
            }
        }

        report.StatusMessage = "Refreshing Windows Shell Icon Cache...";
        progress?.Report(report);
        NativeMethods.RefreshShell();

        report.IsCompleted = true;
        report.StatusMessage = report.IsCancelled ? "Apply operation cancelled." : "Successfully applied icons across selected target!";
        progress?.Report(report);
    }
}

