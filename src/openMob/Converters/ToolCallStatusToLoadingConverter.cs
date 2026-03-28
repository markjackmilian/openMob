using System.Globalization;
using CoreConverter = openMob.Core.Converters.ToolCallStatusToLoadingConverter;

namespace openMob.Converters;

/// <summary>
/// MAUI wrapper for <see cref="CoreConverter"/>. Returns <see langword="true"/> when the
/// <see cref="openMob.Core.Models.ToolCallStatus"/> is <c>Pending</c> or <c>Running</c>,
/// indicating that a loading indicator should be shown.
/// </summary>
public sealed class ToolCallStatusToLoadingConverter : IValueConverter
{
    private static readonly CoreConverter _core = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.Convert(value, targetType, parameter, culture);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.ConvertBack(value, targetType, parameter, culture);
}
