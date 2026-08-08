using System;
using System.Collections.Generic;

namespace EchoBox.Core.Models;

public enum TargetScopeOption
{
    FullDrive = 0,
    SpecificFolder = 1,
    SystemIconsOnly = 2
}

public class IconProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Default Profile";
    public string Description { get; set; } = "Default icon mapping profile";
    public List<IconMapping> Mappings { get; set; } = new();
    public string TargetFolderPath { get; set; } = string.Empty;
    public bool IsFullDriveScan { get; set; } = true;
    public TargetScopeOption Scope { get; set; } = TargetScopeOption.FullDrive;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

