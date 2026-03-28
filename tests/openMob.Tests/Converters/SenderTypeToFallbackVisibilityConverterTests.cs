using System.Globalization;
using openMob.Core.Converters;
using openMob.Core.Models;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="SenderTypeToFallbackVisibilityConverter"/>.
/// Covers all <see cref="SenderType"/> values and edge cases.
/// </summary>
public sealed class SenderTypeToFallbackVisibilityConverterTests
{
    private readonly SenderTypeToFallbackVisibilityConverter _sut = new();

    [Fact]
    public void Convert_WhenFallback_ReturnsTrue()
    {
        // Act
        var result = _sut.Convert(SenderType.Fallback, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_WhenUser_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(SenderType.User, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WhenAgent_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(SenderType.Agent, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_WhenSubagent_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert(SenderType.Subagent, typeof(bool), null, CultureInfo.InvariantCulture);

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

    [Fact]
    public void Convert_WhenNotSenderType_ReturnsFalse()
    {
        // Act
        var result = _sut.Convert("not a sender type", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void ConvertBack_AlwaysThrowsNotSupportedException()
    {
        // Act
        var act = () => _sut.ConvertBack(null, typeof(SenderType), null, CultureInfo.InvariantCulture);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }
}
