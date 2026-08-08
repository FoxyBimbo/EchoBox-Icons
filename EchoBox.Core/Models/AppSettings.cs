using System.Collections.Generic;

namespace EchoBox.Core.Models;

public class AppSettings
{
    public string LastActivePageTag { get; set; } = "Home";
    public string SelectedProfileId { get; set; } = string.Empty;
    public int MaxParallelThreads { get; set; } = System.Environment.ProcessorCount;
    public List<string> CustomExcludedPaths { get; set; } = new();
    public bool EnableContextMenuIntegration { get; set; } = false;
    public string Theme { get; set; } = "Default";
    public Dictionary<string, bool> CategoryExpandedStates { get; set; } = new();

    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowWidth { get; set; } = -1;
    public int WindowHeight { get; set; } = -1;
    public bool IsMaximized { get; set; } = false;
    public string LastSelectedBackgroundMode { get; set; } = "SingleImage";
    public string LastGradientFilename { get; set; } = string.Empty;

    // Windows Background Section Options
    public string LastSingleImagePath { get; set; } = string.Empty;
    public string LastFolderPath { get; set; } = string.Empty;
    public int LastSlideshowIntervalMinutes { get; set; } = 30;
    public bool LastSlideshowShuffle { get; set; } = false;
    public string LastSolidColorHex { get; set; } = "#0078D7";
    public int LastGradientAngle { get; set; } = 90;
    public List<string> LastGradientStopsHex { get; set; } = new() { "#FF0064", "#7800FF", "#00D2FF" };
}

