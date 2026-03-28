using System.Globalization;
using openMob.Core.Models;

namespace openMob.Core.Converters;

/// <summary>
/// Converts a <see cref="ToolCallStatus"/> to a boolean indicating whether a loading
/// indicator should be shown. Returns <see langword="true"/> for <see cref="ToolCallStatus.Pending"/>
/// and <see cref="ToolCallStatus.Running"/>; <see langword="false"/> otherwise.
/// </summary>
public sealed class ToolCallStatusToLoadingConverter
{
    /// <summary>
    /// Converts a <see cref="ToolCallStatus"/> to a loading visibility boolean.
    /// </summary>
    /// <param name="value">The <see cref="ToolCallStatus"/> value to convert.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">The converter parameter (unused).</param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>
    /// <see langword="true"/> when the status is <see cref="ToolCallStatus.Pending"/> or
    /// <see cref="ToolCallStatus.Running"/>; <see langword="false"/> otherwise.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ToolCallStatus status && (status == ToolCallStatus.Pending || status == ToolCallStatus.Running);

    /// <summary>
    /// Not supported. Throws <see cref="NotSupportedException"/>.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
