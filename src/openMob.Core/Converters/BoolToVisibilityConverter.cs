using System.Globalization;

namespace openMob.Core.Converters;

/// <summary>
/// Converts a boolean value to a visibility boolean.
/// When the input is <see langword="true"/>, returns <see langword="true"/> (visible).
/// When <see langword="false"/>, returns <see langword="false"/> (hidden).
/// Pass <c>"Invert"</c> as the <paramref name="parameter"/> to reverse the logic.
/// </summary>
/// <remarks>
/// This is a pure logic class with no MAUI dependencies.
/// A thin MAUI wrapper in <c>openMob.Converters</c> implements <c>IValueConverter</c>
/// and delegates to this class.
/// </remarks>
public sealed class BoolToVisibilityConverter
{
    /// <summary>
    /// Converts a boolean value to a visibility boolean.
    /// </summary>
    /// <param name="value">The boolean value to convert.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">
    /// Optional <c>"Invert"</c> string parameter to reverse the logic.
    /// </param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>
    /// <see langword="true"/> if visible, <see langword="false"/> if hidden.
    /// When <paramref name="parameter"/> is <c>"Invert"</c>, the result is reversed.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is true;
        return parameter is "Invert" ? !result : result;
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
