using System.Globalization;

namespace openMob.Core.Converters;

/// <summary>
/// Converts an integer count to a boolean visibility value.
/// Returns <see langword="true"/> when the value is greater than zero;
/// <see langword="false"/> when zero or negative.
/// Pass <c>"Invert"</c> as the <paramref name="parameter"/> to reverse the logic.
/// </summary>
public sealed class IntToBoolConverter
{
    /// <summary>
    /// Converts an integer to a boolean indicating whether it is greater than zero.
    /// </summary>
    /// <param name="value">The integer value to convert.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">
    /// Optional <c>"Invert"</c> string parameter to reverse the logic.
    /// </param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> is greater than zero;
    /// <see langword="false"/> otherwise. Reversed when <paramref name="parameter"/> is <c>"Invert"</c>.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is int count && count > 0;
        return parameter is "Invert" ? !result : result;
    }

    /// <summary>
    /// Not supported. Throws <see cref="NotSupportedException"/>.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
