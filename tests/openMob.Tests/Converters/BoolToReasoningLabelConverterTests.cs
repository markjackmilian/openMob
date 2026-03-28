using System.Globalization;
using openMob.Core.Converters;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="BoolToReasoningLabelConverter"/>.
/// </summary>
public sealed class BoolToReasoningLabelConverterTests
{
    private readonly BoolToReasoningLabelConverter _sut = new();

    // ─── Convert — boolean expanded-state mapping ─────────────────────────────

    [Fact]
    public void Convert_WhenTrue_ReturnsHideThinking()
    {
        // Act
        var result = _sut.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Hide thinking");
    }

    [Fact]
    public void Convert_WhenFalse_ReturnsShowThinking()
    {
        // Act
        var result = _sut.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Show thinking");
    }

    // ─── Convert — non-boolean input falls back to "Show thinking" ────────────

    [Fact]
    public void Convert_WhenNull_ReturnsShowThinking()
    {
        // Act
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Show thinking");
    }

    [Theory]
    [InlineData("not a bool")]
    [InlineData(42)]
    public void Convert_WhenNonBoolValue_ReturnsShowThinking(object input)
    {
        // Act
        var result = _sut.Convert(input, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Show thinking");
    }

    // ─── ConvertBack — not supported ──────────────────────────────────────────

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act
        var act = () => _sut.ConvertBack("Hide thinking", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
