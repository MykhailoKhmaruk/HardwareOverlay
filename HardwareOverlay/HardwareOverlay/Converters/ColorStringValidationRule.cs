using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using HardwareOverlay.Localization;

namespace HardwareOverlay.Converters;

public sealed class ColorStringValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try
            {
                ColorConverter.ConvertFromString(s);
                return ValidationResult.ValidResult;
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException)
            {
            }
        }

        return new ValidationResult(false, Loc.Instance.Resolve("Validation_Color"));
    }
}
