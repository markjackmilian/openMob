using System.Globalization;
using openMob.Core.Converters;
using openMob.Core.Models;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="ToolCallStatusToLoadingConverter"/>.
/// </summary>
public sealed class ToolCallStatusToLoadingConverterTests
{
    private readonly ToolCallStatusToLoadingConverter _sut = new();

    // ─── Convert — all ToolCallStatus values ──────────────────────────────────

    [Theory]
    [InlineData(ToolCallStatus.Pending,   true)]
    [InlineData(ToolCallStatus.Running,   true)]
    [InlineData(ToolCallStatus.Completed, false)]
    [InlineData(ToolCallStatus.Error,     false)]
    public void Convert_ReturnsExpectedLoadingState(ToolCallStatus status, bool expected)
    {
        // Act
        var result = _sut.Convert(status, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expected);
    }

    // ─── Convert — non-ToolCallStatus input ───────────────────────────────────

    [Fact]
    public void Convert_WhenValueIsNull_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WhenValueIsNotToolCallStatus_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert("not a status", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    // ─── ConvertBack — not supported ──────────────────────────────────────────

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        // Act
        var act = () => _sut.ConvertBack(true, typeof(ToolCallStatus), null, CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
