using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace EchoBox.App.Helpers;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class FilePathToThumbnailConverter : IValueConverter
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Microsoft.UI.Xaml.Media.Imaging.BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string filePath && !string.IsNullOrEmpty(filePath))
        {
            if (Cache.TryGetValue(filePath, out var cached) && cached != null)
            {
                return cached;
            }

            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage
                    {
                        DecodePixelWidth = 88,
                        UriSource = new Uri(filePath)
                    };
                    Cache[filePath] = bitmap;
                    return bitmap;
                }
            }
            catch
            {
                // Fallback
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}


