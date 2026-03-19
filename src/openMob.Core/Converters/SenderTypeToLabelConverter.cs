using System.Globalization;
using openMob.Core.Models;

namespace openMob.Core.Converters;

/// <summary>
/// Pure logic converter that maps a <see cref="SenderType"/> and sender name
/// to a display label string.
/// </summary>
/// <remarks>
/// This is a pure logic class with no MAUI dependencies.
/// A thin MAUI wrapper in <c>openMob.Converters</c> implements <c>IMultiValueConverter</c>
/// and delegates to this class.
/// </remarks>
public sealed class SenderTypeToLabelConverter
{
    /// <summary>
    /// Converts a <see cref="SenderType"/> and sender name to a display label.
    /// </summary>
    /// <param name="senderType">The sender type.</param>
    /// <param name="senderName">The sender display name.</param>
    /// <returns>
    /// <c>"You"</c> for <see cref="SenderType.User"/>,
    /// the sender name (or <c>"Assistant"</c>) for <see cref="SenderType.Agent"/>,
    /// the sender name (or <c>"Subagent"</c>) for <see cref="SenderType.Subagent"/>.
    /// </returns>
    public string Convert(SenderType senderType, string? senderName)
    {
        return senderType switch
        {
            SenderType.User => "You",
            SenderType.Agent => string.IsNullOrWhiteSpace(senderName) ? "Assistant" : senderName,
            SenderType.Subagent => string.IsNullOrWhiteSpace(senderName) ? "Subagent" : senderName,
            _ => "Unknown",
        };
    }

    /// <summary>
    /// Converts using the standard IValueConverter signature.
    /// Expects <paramref name="value"/> to be a <see cref="SenderType"/> and
    /// <paramref name="parameter"/> to be the sender name string.
    /// </summary>
    /// <param name="value">The <see cref="SenderType"/> value.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">The sender name string, or <c>null</c>.</param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>The display label string.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var senderType = value is SenderType st ? st : SenderType.Agent;
        var senderName = parameter as string;
        return Convert(senderType, senderName);
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
