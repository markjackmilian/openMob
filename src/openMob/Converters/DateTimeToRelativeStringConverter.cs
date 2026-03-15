using System.Globalization;

namespace openMob.Converters;

/// <summary>
/// Converts a <see cref="DateTimeOffset"/> to a human-readable relative time string.
/// Examples: "Just now", "2m ago", "1h ago", "Yesterday", "Mar 10".
/// </summary>
public sealed class DateTimeToRelativeStringConverter : IValueConverter
{
    /// <inheritdoc />
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

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
