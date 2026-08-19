using System.Globalization;
using System.Windows;
using OpenTheWindows.App.Converters;

namespace OpenTheWindows.App.Tests.Converters;

/// <summary>Detail-pane hint toggling: null shows the hint, a value shows the pane (with Invert).</summary>
public sealed class NullToVisibilityConverterTests
{
    private readonly NullToVisibilityConverter _converter = new();

    private object Convert(object? value, object? parameter) => _converter.Convert(value, typeof(Visibility), parameter, CultureInfo.InvariantCulture);

    [Fact]
    public void Null_is_visible_and_a_value_is_collapsed_by_default()
    {
        Assert.Equal(Visibility.Visible, Convert(null, null));
        Assert.Equal(Visibility.Collapsed, Convert(new object(), null));
    }

    [Fact]
    public void Invert_shows_a_value_and_hides_null()
    {
        Assert.Equal(Visibility.Collapsed, Convert(null, "Invert"));
        Assert.Equal(Visibility.Visible, Convert(new object(), "Invert"));
    }

    [Fact]
    public void ConvertBack_is_not_supported()
    {
        Assert.Throws<NotSupportedException>(
            () => _converter.ConvertBack(Visibility.Visible, typeof(object), null, CultureInfo.InvariantCulture));
    }
}
