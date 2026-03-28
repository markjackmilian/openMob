using System.Globalization;
using openMob.Core.Models;

namespace openMob.Core.Converters;

/// <summary>
/// Converts a <see cref="ToolCallStatus"/> to a boolean indicating whether the tool call
/// has failed. Returns <see langword="true"/> for <see cref="ToolCallStatus.Error"/>;
/// <see langword="false"/> otherwise.
/// </summary>
public sealed class ToolCallStatusToErrorConverter
{
    /// <summary>
    /// Converts a <see cref="ToolCallStatus"/> to an error-state visibility boolean.
    /// </summary>
    /// <param name="value">The <see cref="ToolCallStatus"/> value to convert.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">The converter parameter (unused).</param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>
    /// <see langword="true"/> when the status is <see cref="ToolCallStatus.Error"/>;
    /// <see langword="false"/> otherwise.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ToolCallStatus.Error;

    /// <summary>
    /// Not supported. Throws <see cref="NotSupportedException"/>.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
