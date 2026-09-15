using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SkopiStation.App.Converters;

/// <summary>
/// Paints a measurement according to <see cref="Domain.Measurement.IsOutOfRange"/>.
/// </summary>
/// <remarks>
/// The rule is presentation, not domain: the model already says whether a value sits outside its
/// reference range, and turning that boolean into a colour is the view's business. A converter
/// keeps it out of both the entity and the ViewModel, and out of the code-behind.
/// Being outside the reference range is a clinical signal, not an invalid value — such a
/// measurement is recorded like any other.
/// </remarks>
[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class OutOfRangeBrushConverter : IValueConverter
{
    public Brush OutOfRangeBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B));

    public Brush InRangeBrush { get; set; } = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? OutOfRangeBrush : InRangeBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A brush cannot be turned back into a range indication.");
}
