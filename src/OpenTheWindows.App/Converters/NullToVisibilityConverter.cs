using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenTheWindows.App.Converters;

/// <summary>
/// Maps <see langword="null"/> to <see cref="Visibility.Visible"/> and any value
/// to <see cref="Visibility.Collapsed"/>; pass <c>Invert</c> as the converter
/// parameter to swap (visible when the value is present). Used to toggle a
/// detail pane against its "nothing selected" hint.
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
internal sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        bool visible = value is null ? !invert : invert;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException("NullToVisibilityConverter is display-only (one-way).");
}
