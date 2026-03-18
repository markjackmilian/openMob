using System.Globalization;
using openMob.Core.Models;

namespace openMob.Core.Converters;

/// <summary>
/// Converts a <see cref="MessageDeliveryStatus"/> value to a Material Symbols Unicode glyph string.
/// </summary>
/// <remarks>
/// <para>
/// This is a pure logic class with no MAUI dependencies.
/// A thin MAUI wrapper in <c>openMob.Converters</c> implements <c>IValueConverter</c>
/// and delegates to this class.
/// </para>
/// <para>
/// The returned Unicode strings are intended to be rendered with the
/// <c>MaterialSymbols-Outlined.ttf</c> font (registered as <c>MaterialSymbols</c>).
/// </para>
/// </remarks>
public sealed class MessageStatusToIconConverter
{
    /// <summary>Unicode glyph for Material Symbols "schedule" icon.</summary>
    internal const string ScheduleGlyph = "\ue8b5";

    /// <summary>Unicode glyph for Material Symbols "check" icon.</summary>
    internal const string CheckGlyph = "\ue5ca";

    /// <summary>Unicode glyph for Material Symbols "error" icon.</summary>
    internal const string ErrorGlyph = "\ue000";

    /// <summary>
    /// Converts a <see cref="MessageDeliveryStatus"/> to the corresponding Material Symbols glyph string.
    /// </summary>
    /// <param name="value">The <see cref="MessageDeliveryStatus"/> value.</param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">Optional parameter (unused).</param>
    /// <param name="culture">The culture info (unused).</param>
    /// <returns>
    /// A Unicode glyph string for the corresponding status icon,
    /// or <see cref="string.Empty"/> for unknown or <see langword="null"/> values.
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageDeliveryStatus status)
        {
            return status switch
            {
                MessageDeliveryStatus.Sending => ScheduleGlyph,
                MessageDeliveryStatus.Sent => CheckGlyph,
                MessageDeliveryStatus.Error => ErrorGlyph,
                _ => string.Empty,
            };
        }

        return string.Empty;
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
