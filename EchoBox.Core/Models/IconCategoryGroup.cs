using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EchoBox.Core.Models;

public class IconCategoryGroup : INotifyPropertyChanged
{
    private string _categoryId = string.Empty;
    private string _name = string.Empty;
    private bool _isExpanded = true;
    private ObservableCollection<IconItem> _icons = new();
    private List<IconItem> _allIcons = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CategoryId
    {
        get => _categoryId;
        set => SetField(ref _categoryId, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ChevronGlyph));
            }
        }
    }

    public string ChevronGlyph => _isExpanded ? "\uE70D" : "\uE76C";

    public List<IconItem> AllIcons
    {
        get => _allIcons;
        set
        {
            if (SetField(ref _allIcons, value))
            {
                OnPropertyChanged(nameof(IconCountText));
            }
        }
    }

    public string IconCountText => $"{AllIcons.Count} icon{(AllIcons.Count == 1 ? "" : "s")}";

    public ObservableCollection<IconItem> Icons
    {
        get => _icons;
        set => SetField(ref _icons, value);
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

