using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Converters;

/// <summary>
/// Maps a <see cref="RiskTier"/> to an accent <see cref="Brush"/> for its badge
/// border. The badge always shows the risk text as well, so colour is never the
/// only signal (accessibility: research 02 §9).
/// </summary>
[ValueConversion(typeof(RiskTier), typeof(Brush))]
internal sealed class RiskToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Safe = Freeze(0x2E, 0x7D, 0x32);
    private static readonly SolidColorBrush Caution = Freeze(0xB0, 0x74, 0x00);
    private static readonly SolidColorBrush Advanced = Freeze(0xE6, 0x51, 0x00);
    private static readonly SolidColorBrush Breaking = Freeze(0xC6, 0x28, 0x28);
    private static readonly SolidColorBrush Unknown = Freeze(0x75, 0x75, 0x75);

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RiskTier.Safe => Safe,
        RiskTier.Caution => Caution,
        RiskTier.Advanced => Advanced,
        RiskTier.Breaking => Breaking,
        _ => Unknown,
    };

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException("RiskToBrushConverter is display-only (one-way).");

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        SolidColorBrush brush = new(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
