using System.Globalization;
using openMob.Core.Models;

namespace openMob.Core.Converters;

/// <summary>
/// Pure logic converter that maps a <see cref="SenderType"/> to a color key string.
/// The MAUI layer resolves the key to an actual <c>Color</c> resource.
/// </summary>
/// <remarks>
/// This is a pure logic class with no MAUI dependencies.
/// A thin MAUI wrapper in <c>openMob.Converters</c> implements <c>IValueConverter</c>
/// and delegates to this class.
/// </remarks>
public sealed class SenderTypeToColorKeyConverter
{
    /// <summary>
    /// Converts a <see cref="SenderType"/> value to a color resource key string.
    /// </summary>
    /// <param name="value">The <see cref="SenderType"/> value to convert.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">Optional parameter (unused).</param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>
    /// <c>"ColorPrimary"</c> for <see cref="SenderType.User"/>,
    /// <c>"ColorAgentAccent"</c> for <see cref="SenderType.Agent"/>,
    /// <c>"ColorSubagentAccent"</c> for <see cref="SenderType.Subagent"/>.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SenderType senderType)
        {
            return senderType switch
            {
                SenderType.User => "ColorPrimary",
                SenderType.Agent => "ColorAgentAccent",
                SenderType.Subagent => "ColorSubagentAccent",
                _ => "ColorPrimary",
            };
        }

        return "ColorPrimary";
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
