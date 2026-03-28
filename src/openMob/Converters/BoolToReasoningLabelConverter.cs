using System.Globalization;
using CoreConverter = openMob.Core.Converters.BoolToReasoningLabelConverter;

namespace openMob.Converters;

/// <summary>
/// MAUI wrapper for <see cref="CoreConverter"/>. Converts a boolean expanded-state value
/// to a reasoning toggle label: <c>"Hide thinking"</c> when <see langword="true"/>,
/// <c>"Show thinking"</c> when <see langword="false"/>.
/// </summary>
public sealed class BoolToReasoningLabelConverter : IValueConverter
{
    private static readonly CoreConverter _core = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.Convert(value, targetType, parameter, culture);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.ConvertBack(value, targetType, parameter, culture);
}
