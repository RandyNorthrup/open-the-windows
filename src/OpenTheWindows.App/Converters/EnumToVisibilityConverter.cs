using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenTheWindows.App.Converters;

/// <summary>
/// Shows an element when an enum value matches the converter parameter. The
/// parameter may be a comma-separated list of names, so one section can be
/// visible for several states (e.g. <c>Completed,Failed</c>). One-way.
/// </summary>
[ValueConversion(typeof(Enum), typeof(Visibility))]
internal sealed class EnumToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string name = value?.ToString() ?? string.Empty;
        string targets = parameter as string ?? string.Empty;
        bool match = targets.Split(',').Any(t => string.Equals(t.Trim(), name, StringComparison.Ordinal));
        return match ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException("EnumToVisibilityConverter is display-only (one-way).");
}
