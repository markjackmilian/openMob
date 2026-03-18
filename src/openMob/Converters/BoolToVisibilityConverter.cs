using System.Globalization;
using CoreConverter = openMob.Core.Converters.BoolToVisibilityConverter;

namespace openMob.Converters;

/// <summary>
/// MAUI wrapper for <see cref="CoreConverter"/>. Implements <see cref="IValueConverter"/>
/// and delegates conversion logic to the pure .NET Core class.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    private static readonly CoreConverter _core = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.Convert(value, targetType, parameter, culture);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.ConvertBack(value, targetType, parameter, culture);
}
