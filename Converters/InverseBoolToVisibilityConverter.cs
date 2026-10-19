using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ProjectCleaner.Converters;

/// <summary>Maps <c>true</c> to <see cref="Visibility.Collapsed"/> - for "show when off" toggles.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool flag = value is true;

        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}