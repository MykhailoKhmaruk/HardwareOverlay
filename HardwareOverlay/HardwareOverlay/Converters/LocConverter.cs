using System.Globalization;
using System.Windows.Data;
using HardwareOverlay.Localization;

namespace HardwareOverlay.Converters;

/// <summary>
/// MultiBinding converter that resolves a localization key stored in the data.
///
/// values[0] — key to translate (e.g. NameKey); falls back to values[2] if empty.
/// values[1] — Loc.Current; only here so the binding re-evaluates on language change.
/// values[2] — optional suffix/fallback (e.g. the drive name).
/// values[3] — optional override; a non-empty string wins over the translation
///             (e.g. a metric's CustomName).
/// </summary>
public sealed class LocConverter : IMultiValueConverter
{
    public static LocConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // A user-entered name wins over the translation.
        if (values.Length > 3 && values[3] is string @override && @override.Length > 0)
            return @override;

        if (values.Length > 0 && values[0] is string key && key.Length > 0)
        {
            var text = Loc.Instance.Resolve(key);

            if (values.Length > 2 && values[2] is string suffix && suffix.Length > 0)
                text += " " + suffix;

            return text;
        }

        return values.Length > 2 && values[2] is string fallback ? fallback : string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
