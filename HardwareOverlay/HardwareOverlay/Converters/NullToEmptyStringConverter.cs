using System.Globalization;
using System.Windows.Data;

namespace HardwareOverlay.Converters;

public sealed class NullToEmptyStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value ?? string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: 0 } ? null : value;
}
