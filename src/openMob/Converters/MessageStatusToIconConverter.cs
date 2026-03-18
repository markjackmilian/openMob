using System.Globalization;
using CoreConverter = openMob.Core.Converters.MessageStatusToIconConverter;

namespace openMob.Converters;

/// <summary>
/// MAUI wrapper for <see cref="CoreConverter"/>. Implements <see cref="IValueConverter"/>
/// and delegates conversion logic to the pure .NET Core class.
/// Maps <see cref="openMob.Core.Models.MessageDeliveryStatus"/> to Material Symbols Unicode glyph strings.
/// </summary>
public sealed class MessageStatusToIconConverter : IValueConverter
{
    private static readonly CoreConverter _core = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.Convert(value, targetType, parameter, culture);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => _core.ConvertBack(value, targetType, parameter, culture);
}
