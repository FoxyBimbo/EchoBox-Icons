using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EchoBox.Core.Models;

public class IconMapping : INotifyPropertyChanged
{
    private IconTargetType _targetType;
    private string _targetName = string.Empty;
    private string? _fileExtension;
    private List<string> _selectedIconPaths = new();
    private bool _isSingleIconOnly;
    private int _lastAppliedIndex;
    private bool _isCustom;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IconTargetType TargetType
    {
        get => _targetType;
        set => SetField(ref _targetType, value);
    }

    public string TargetName
    {
        get => _targetName;
        set => SetField(ref _targetName, value);
    }

    public string? FileExtension
    {
        get => _fileExtension;
        set => SetField(ref _fileExtension, value);
    }

    public List<string> SelectedIconPaths
    {
        get => _selectedIconPaths;
        set => SetField(ref _selectedIconPaths, value);
    }

    public bool IsSingleIconOnly
    {
        get => _isSingleIconOnly;
        set => SetField(ref _isSingleIconOnly, value);
    }

    public int LastAppliedIndex
    {
        get => _lastAppliedIndex;
        set => SetField(ref _lastAppliedIndex, value);
    }

    public bool IsCustom
    {
        get => _isCustom;
        set => SetField(ref _isCustom, value);
    }

    public string GetNextIconPath()
    {
        if (SelectedIconPaths == null || SelectedIconPaths.Count == 0) return string.Empty;
        if (IsSingleIconOnly || SelectedIconPaths.Count == 1)
        {
            return SelectedIconPaths[0];
        }

        int index = (LastAppliedIndex + 1) % SelectedIconPaths.Count;
        LastAppliedIndex = index;
        return SelectedIconPaths[index];
    }

    public static bool IsFileExtensionEntry(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry)) return false;
        entry = entry.Trim();
        // A file extension entry starts with '.' and contains no spaces or path separators
        return entry.StartsWith(".") && !entry.Contains(" ") && !entry.Contains("\\") && !entry.Contains("/");
    }

    public IEnumerable<string> GetParsedEntries()
    {
        string rawText = IsCustom ? TargetName : (FileExtension ?? TargetName);
        if (string.IsNullOrWhiteSpace(rawText)) yield break;

        var tokens = rawText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                yield return token;
            }
        }
    }

    public void NotifySelectedIconPathsChanged()
    {
        OnPropertyChanged(nameof(SelectedIconPaths));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

