using System.Globalization;
using openMob.Core.Converters;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="IntToBoolConverter"/>.
/// </summary>
public sealed class IntToBoolConverterTests
{
    private readonly IntToBoolConverter _sut = new();

    // ─── Convert — standard integer-to-bool mapping ───────────────────────────

    [Theory]
    [InlineData(1,  true)]
    [InlineData(5,  true)]
    [InlineData(0,  false)]
    [InlineData(-1, false)]
    public void Convert_ReturnsExpectedBoolForIntInput(int input, bool expected)
    {
        // Act
        var result = _sut.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    // ─── Convert — inverted logic ─────────────────────────────────────────────

    [Theory]
    [InlineData(1,  false)]
    [InlineData(5,  false)]
    [InlineData(0,  true)]
    [InlineData(-1, true)]
    public void Convert_WhenInvertParameter_ReturnsInvertedBool(int input, bool expected)
    {
        // Act
        var result = _sut.Convert(input, typeof(bool), "Invert", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    // ─── Convert — non-integer input ──────────────────────────────────────────

    [Fact]
    public void Convert_WhenValueIsNull_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WhenValueIsNotInt_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert("not an int", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    // ─── ConvertBack — not supported ──────────────────────────────────────────

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act
        var act = () => _sut.ConvertBack(true, typeof(int), null, CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
