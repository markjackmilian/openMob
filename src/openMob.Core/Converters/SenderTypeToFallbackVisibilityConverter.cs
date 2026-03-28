using System.Globalization;
using openMob.Core.Models;

namespace openMob.Core.Converters;

/// <summary>
/// Pure logic converter that returns <c>true</c> when the value is <see cref="SenderType.Fallback"/>,
/// and <c>false</c> for all other sender types.
/// </summary>
/// <remarks>
/// This is a pure logic class with no MAUI dependencies.
/// A thin MAUI wrapper in <c>openMob.Converters</c> implements <c>IValueConverter</c>
/// and delegates to this class.
/// </remarks>
public sealed class SenderTypeToFallbackVisibilityConverter
{
    /// <summary>
    /// Returns <c>true</c> if <paramref name="value"/> is <see cref="SenderType.Fallback"/>;
    /// otherwise <c>false</c>.
    /// </summary>
    /// <param name="value">The <see cref="SenderType"/> value to evaluate.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">Optional parameter (unused).</param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns><c>true</c> for <see cref="SenderType.Fallback"/>; <c>false</c> otherwise.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is SenderType senderType && senderType == SenderType.Fallback;
    }

    /// <summary>
    /// Not supported. Throws <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="value">The value to convert back (unused).</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">The parameter (unused).</param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>Never returns; always throws.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
