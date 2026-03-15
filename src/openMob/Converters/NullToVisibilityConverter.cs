using System.Globalization;

namespace openMob.Converters;

/// <summary>
/// Converts a nullable object to a boolean visibility value.
/// Returns <c>true</c> when the value is not null; <c>false</c> when null.
/// Pass "Invert" as ConverterParameter to reverse the logic.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNotNull = value is not null && (value is not string s || !string.IsNullOrEmpty(s));
        return parameter is "Invert" ? !isNotNull : isNotNull;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
