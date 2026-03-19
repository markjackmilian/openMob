using System.Globalization;
using openMob.Core.Converters;
using openMob.Core.Models;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="SenderTypeToColorKeyConverter"/>.
/// Covers all <see cref="SenderType"/> values and edge cases.
/// </summary>
public sealed class SenderTypeToColorKeyConverterTests
{
    private readonly SenderTypeToColorKeyConverter _sut = new();

    [Fact]
    public void Convert_WhenUser_ReturnsColorPrimary()
    {
        // Act
        var result = _sut.Convert(SenderType.User, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("ColorPrimary");
    }

    [Fact]
    public void Convert_WhenAgent_ReturnsColorAgentAccent()
    {
        // Act
        var result = _sut.Convert(SenderType.Agent, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("ColorAgentAccent");
    }

    [Fact]
    public void Convert_WhenSubagent_ReturnsColorSubagentAccent()
    {
        // Act
        var result = _sut.Convert(SenderType.Subagent, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("ColorSubagentAccent");
    }

    [Fact]
    public void Convert_WhenValueIsNull_ReturnsColorPrimary()
    {
        // Act
        var result = _sut.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("ColorPrimary");
    }

    [Fact]
    public void Convert_WhenValueIsNotSenderType_ReturnsColorPrimary()
    {
        // Act
        var result = _sut.Convert("not a sender type", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("ColorPrimary");
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
