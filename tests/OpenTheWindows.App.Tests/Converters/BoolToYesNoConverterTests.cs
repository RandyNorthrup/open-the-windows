using System.Globalization;
using OpenTheWindows.App.Converters;
using OpenTheWindows.App.Resources;

namespace OpenTheWindows.App.Tests.Converters;

/// <summary>
/// Verifies the M0 fix: <c>CanApply</c> and similar booleans display as
/// "Yes"/"No" (from the resource strings), not "True"/"False", and that the
/// converter is one-way.
/// </summary>
public sealed class BoolToYesNoConverterTests
{
    private readonly BoolToYesNoConverter _converter = new();

    [Fact]
    public void True_converts_to_the_Yes_resource_string()
    {
        object result = _converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(Strings.Common_Yes, result);
        Assert.Equal("Yes", result);
    }

    [Fact]
    public void False_converts_to_the_No_resource_string()
    {
        object result = _converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(Strings.Common_No, result);
        Assert.Equal("No", result);
    }

    [Fact]
    public void Null_converts_to_No()
    {
        object result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("No", result);
    }

    [Fact]
    public void ConvertBack_is_not_supported()
    {
        Assert.Throws<NotSupportedException>(
            () => _converter.ConvertBack("Yes", typeof(bool), null, CultureInfo.InvariantCulture));
    }
}
