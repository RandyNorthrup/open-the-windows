using System.Globalization;
using System.Windows.Data;
using OpenTheWindows.App.Resources;

namespace OpenTheWindows.App.Converters;

/// <summary>
/// Converts a <see cref="bool"/> to a "Yes"/"No" string for read-only display.
/// Fixes the M0 defect where <c>CanApply</c> surfaced as "True"/"False".
/// One-way: display only.
/// </summary>
[ValueConversion(typeof(bool), typeof(string))]
internal sealed class BoolToYesNoConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is true ? Strings.Common_Yes : Strings.Common_No;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException("BoolToYesNoConverter is display-only (one-way).");
}
