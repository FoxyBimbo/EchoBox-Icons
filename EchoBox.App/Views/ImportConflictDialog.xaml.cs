using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using EchoBox.Core.Models;

namespace EchoBox.App.Views;

public class ImportConflictItem : INotifyPropertyChanged
{
    public string ImportedFilePath { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public IconItem ExistingIcon { get; set; } = null!;

    private string _newName = string.Empty;
    public string NewName
    {
        get => _newName;
        set
        {
            if (_newName != value)
            {
                _newName = value;
                OnPropertyChanged(nameof(NewName));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public partial class ImportConflictDialog : ContentDialog
{
    public ObservableCollection<ImportConflictItem> Items { get; } = new();

    public ImportConflictDialog(IEnumerable<ImportConflictItem> conflictItems)
    {
        InitializeComponent();

        foreach (var item in conflictItems)
        {
            Items.Add(item);
        }

        ConflictItemsControl.ItemsSource = Items;
    }
}
