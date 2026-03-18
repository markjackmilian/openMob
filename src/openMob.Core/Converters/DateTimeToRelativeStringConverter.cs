using System.Globalization;

namespace openMob.Core.Converters;

/// <summary>
/// Converts a <see cref="DateTimeOffset"/> to a human-readable relative time string.
/// Examples: "Just now", "2m ago", "1h ago", "Yesterday", "Mar 10".
/// </summary>
/// <remarks>
/// This is a pure logic class with no MAUI dependencies.
/// A thin MAUI wrapper in <c>openMob.Converters</c> implements <c>IValueConverter</c>
/// and delegates to this class.
/// </remarks>
public sealed class DateTimeToRelativeStringConverter
{
    /// <summary>
    /// Converts a <see cref="DateTimeOffset"/> value to a relative time string.
    /// </summary>
    /// <param name="value">
    /// The <see cref="DateTimeOffset"/> value to convert.
    /// Returns <see cref="string.Empty"/> if the value is not a <see cref="DateTimeOffset"/>.
    /// </param>
    /// <param name="targetType">The target type (unused).</param>
    /// <param name="parameter">Optional parameter (unused).</param>
    /// <param name="culture">
    /// The culture info used for date formatting when the elapsed time exceeds 7 days.
    /// </param>
    /// <returns>
    /// A human-readable relative time string such as "Just now", "5m ago", "3h ago",
    /// "Yesterday", "4d ago", or a formatted date like "Mar 10".
    /// </returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset dto)
            return string.Empty;

        var now = DateTimeOffset.Now;
        var elapsed = now - dto;

        if (elapsed.TotalMinutes < 1)
            return "Just now";

        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m ago";

        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";

        if (elapsed.TotalDays < 2)
            return "Yesterday";

        if (elapsed.TotalDays < 7)
            return $"{(int)elapsed.TotalDays}d ago";

        return dto.ToString("MMM d", culture);
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
