using System.Globalization;
using CoreConverter = openMob.Core.Converters.ToolCallStatusToCompletedConverter;

namespace openMob.Converters;

/// <summary>
/// MAUI wrapper for <see cref="CoreConverter"/>. Returns <see langword="true"/> when the
/// <see cref="openMob.Core.Models.ToolCallStatus"/> is <c>Completed</c>.
/// </summary>
public sealed class ToolCallStatusToCompletedConverter : IValueConverter
{
    private static readonly CoreConverter _core = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.Convert(value, targetType, parameter, culture);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.ConvertBack(value, targetType, parameter, culture);
}
