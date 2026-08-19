using System.Globalization;
using System.Windows.Media;
using OpenTheWindows.App.Converters;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Tests.Converters;

/// <summary>The risk-accent brush is distinct per tier, frozen, and one-way.</summary>
public sealed class RiskToBrushConverterTests
{
    private readonly RiskToBrushConverter _converter = new();

    private Brush Convert(RiskTier risk) => (Brush)_converter.Convert(risk, typeof(Brush), null, CultureInfo.InvariantCulture);

    [Fact]
    public void Each_risk_tier_maps_to_a_distinct_frozen_brush()
    {
        Brush safe = Convert(RiskTier.Safe);
        Brush caution = Convert(RiskTier.Caution);
        Brush advanced = Convert(RiskTier.Advanced);
        Brush breaking = Convert(RiskTier.Breaking);

        Assert.True(safe.IsFrozen);
        HashSet<Brush> distinct = [safe, caution, advanced, breaking];
        Assert.Equal(4, distinct.Count);
    }

    [Fact]
    public void ConvertBack_is_not_supported()
    {
        Assert.Throws<NotSupportedException>(
            () => _converter.ConvertBack(Brushes.Red, typeof(RiskTier), null, CultureInfo.InvariantCulture));
    }
}
