using System.Globalization;
using openMob.Core.Converters;
using openMob.Core.Models;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="MessageStatusToIconConverter"/>.
/// </summary>
public sealed class MessageStatusToIconConverterTests
{
    private readonly MessageStatusToIconConverter _sut = new();

    // ─── Convert — enum to glyph mapping ──────────────────────────────────────

    [Fact]
    public void Convert_WhenSending_ReturnsScheduleGlyph()
    {
        // Act
        var result = _sut.Convert(MessageDeliveryStatus.Sending, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(MessageStatusToIconConverter.ScheduleGlyph);
    }

    [Fact]
    public void Convert_WhenSent_ReturnsCheckGlyph()
    {
        // Act
        var result = _sut.Convert(MessageDeliveryStatus.Sent, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(MessageStatusToIconConverter.CheckGlyph);
    }

    [Fact]
    public void Convert_WhenError_ReturnsErrorGlyph()
    {
        // Act
        var result = _sut.Convert(MessageDeliveryStatus.Error, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(MessageStatusToIconConverter.ErrorGlyph);
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
    [InlineData("Sending")]
    [InlineData(42)]
    [InlineData(true)]
    public void Convert_WhenNonEnumValue_ReturnsEmptyString(object input)
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
        var act = () => _sut.ConvertBack(
            MessageStatusToIconConverter.CheckGlyph,
            typeof(MessageDeliveryStatus),
            null,
            CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
