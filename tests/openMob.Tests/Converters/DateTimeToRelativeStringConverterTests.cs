using System.Globalization;
using openMob.Core.Converters;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="DateTimeToRelativeStringConverter"/>.
/// </summary>
public sealed class DateTimeToRelativeStringConverterTests
{
    private readonly DateTimeToRelativeStringConverter _sut = new();

    // ─── Convert — "Just now" (within 60 seconds) ─────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(59)]
    public void Convert_WhenWithinSixtySeconds_ReturnsJustNow(int secondsAgo)
    {
        // Arrange
        var value = DateTimeOffset.Now.AddSeconds(-secondsAgo);

        // Act
        var result = _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Just now");
    }

    // ─── Convert — minutes ago ────────────────────────────────────────────────

    [Theory]
    [InlineData(1, "1m ago")]
    [InlineData(5, "5m ago")]
    [InlineData(30, "30m ago")]
    [InlineData(59, "59m ago")]
    public void Convert_WhenMinutesAgo_ReturnsMinutesFormat(int minutesAgo, string expected)
    {
        // Arrange
        var value = DateTimeOffset.Now.AddMinutes(-minutesAgo);

        // Act
        var result = _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    // ─── Convert — hours ago ──────────────────────────────────────────────────

    [Theory]
    [InlineData(1, "1h ago")]
    [InlineData(3, "3h ago")]
    [InlineData(12, "12h ago")]
    [InlineData(23, "23h ago")]
    public void Convert_WhenHoursAgo_ReturnsHoursFormat(int hoursAgo, string expected)
    {
        // Arrange
        var value = DateTimeOffset.Now.AddHours(-hoursAgo);

        // Act
        var result = _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    // ─── Convert — yesterday (24–48 hours) ────────────────────────────────────

    [Theory]
    [InlineData(25)]
    [InlineData(36)]
    [InlineData(47)]
    public void Convert_WhenBetween24And48HoursAgo_ReturnsYesterday(int hoursAgo)
    {
        // Arrange
        var value = DateTimeOffset.Now.AddHours(-hoursAgo);

        // Act
        var result = _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Yesterday");
    }

    // ─── Convert — days ago (2–6 days) ────────────────────────────────────────

    [Theory]
    [InlineData(3, "3d ago")]
    [InlineData(4, "4d ago")]
    [InlineData(6, "6d ago")]
    public void Convert_WhenDaysAgo_ReturnsDaysFormat(int daysAgo, string expected)
    {
        // Arrange
        var value = DateTimeOffset.Now.AddDays(-daysAgo);

        // Act
        var result = _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    // ─── Convert — older than 7 days ──────────────────────────────────────────

    [Fact]
    public void Convert_WhenOlderThanWeek_ReturnsDateFormat()
    {
        // Arrange
        var value = DateTimeOffset.Now.AddDays(-30);
        var expected = value.ToString("MMM d", CultureInfo.InvariantCulture);

        // Act
        var result = _sut.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    // ─── Convert — null and invalid input ─────────────────────────────────────

    [Fact]
    public void Convert_WhenNull_ReturnsEmptyString()
    {
        // Act
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData(42)]
    [InlineData(true)]
    public void Convert_WhenNonDateTimeOffset_ReturnsEmptyString(object input)
    {
        // Act
        var result = _sut.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(string.Empty);
    }

    // ─── ConvertBack — not supported ──────────────────────────────────────────

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act
        var act = () => _sut.ConvertBack("Just now", typeof(DateTimeOffset), null, CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
