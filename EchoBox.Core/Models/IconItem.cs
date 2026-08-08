using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EchoBox.Core.Models;

public class IconItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public List<string> CategoryIds { get; set; } = new();
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public string CategorySortKey { get; set; } = "1_UnCategorized";

    [JsonIgnore]
    public string DisplayCategoryName { get; set; } = "UnCategorized";
}

