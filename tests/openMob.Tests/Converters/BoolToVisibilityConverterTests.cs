using System.Globalization;
using openMob.Core.Converters;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="BoolToVisibilityConverter"/>.
/// </summary>
public sealed class BoolToVisibilityConverterTests
{
    private readonly BoolToVisibilityConverter _sut = new();

    // ─── Convert — standard boolean mapping ───────────────────────────────────

    [Fact]
    public void Convert_WhenTrue_ReturnsTrue()
    {
        // Act
        var result = _sut.Convert(true, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WhenFalse_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(false, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WhenNull_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    // ─── Convert — inverted logic ─────────────────────────────────────────────

    [Fact]
    public void Convert_WhenTrueWithInvertParameter_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(true, typeof(bool), "Invert", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WhenFalseWithInvertParameter_ReturnsTrue()
    {
        // Act
        var result = _sut.Convert(false, typeof(bool), "Invert", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    // ─── Convert — non-boolean input ──────────────────────────────────────────

    [Theory]
    [InlineData("not a bool")]
    [InlineData(42)]
    [InlineData(3.14)]
    public void Convert_WhenNonBoolValue_ReturnsFalse(object input)
    {
        // Act
        var result = _sut.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    // ─── ConvertBack — not supported ──────────────────────────────────────────

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act
        var act = () => _sut.ConvertBack(true, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
