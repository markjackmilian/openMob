using System.Globalization;
using CoreConverter = openMob.Core.Converters.IntToBoolConverter;

namespace openMob.Converters;

/// <summary>
/// MAUI wrapper for <see cref="CoreConverter"/>. Returns <see langword="true"/> when the
/// integer value is greater than zero. Pass <c>"Invert"</c> as the converter parameter
/// to reverse the logic.
/// </summary>
public sealed class IntToBoolConverter : IValueConverter
{
    private static readonly CoreConverter _core = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.Convert(value, targetType, parameter, culture);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.ConvertBack(value, targetType, parameter, culture);
}
