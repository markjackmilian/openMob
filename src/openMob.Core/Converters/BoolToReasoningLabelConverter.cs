using System.Globalization;

namespace openMob.Core.Converters;

/// <summary>
/// Converts a boolean expanded-state value to a reasoning toggle label string.
/// Returns <c>"Hide thinking"</c> when <see langword="true"/> (expanded);
/// <c>"Show thinking"</c> when <see langword="false"/> (collapsed).
/// </summary>
public sealed class BoolToReasoningLabelConverter
{
    /// <summary>
    /// Converts a boolean expanded state to a reasoning toggle label.
    /// </summary>
    /// <param name="value">The boolean expanded state.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">The converter parameter (unused).</param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>
    /// <c>"Hide thinking"</c> when <paramref name="value"/> is <see langword="true"/>;
    /// <c>"Show thinking"</c> otherwise.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Hide thinking" : "Show thinking";

    /// <summary>
    /// Not supported. Throws <see cref="NotSupportedException"/>.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
