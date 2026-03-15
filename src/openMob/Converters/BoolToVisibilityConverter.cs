using System.Globalization;

namespace openMob.Converters;

/// <summary>
/// Converts a boolean value to a visibility boolean.
/// Pass "Invert" as ConverterParameter to reverse the logic.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is true;
        return parameter is "Invert" ? !result : result;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
