using System.Globalization;

namespace openMob.Converters;

/// <summary>
/// Converts a boolean value to its inverse.
/// </summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not true;
}
