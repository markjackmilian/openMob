using System.Globalization;
using openMob.Core.Converters;
using openMob.Core.Models;

namespace openMob.Tests.Converters;

/// <summary>
/// Unit tests for <see cref="SenderTypeToLabelConverter"/>.
/// Covers all <see cref="SenderType"/> values with various sender name inputs.
/// </summary>
public sealed class SenderTypeToLabelConverterTests
{
    private readonly SenderTypeToLabelConverter _sut = new();

    // ─── Typed Convert method ────────────────────────────────────────────────

    [Fact]
    public void Convert_WhenUser_ReturnsYou()
    {
        // Act
        var result = _sut.Convert(SenderType.User, null);

        // Assert
        result.Should().Be("You");
    }

    [Fact]
    public void Convert_WhenAgentWithName_ReturnsName()
    {
        // Act
        var result = _sut.Convert(SenderType.Agent, "Claude");

        // Assert
        result.Should().Be("Claude");
    }

    [Fact]
    public void Convert_WhenAgentWithEmptyName_ReturnsAssistant()
    {
        // Act
        var result = _sut.Convert(SenderType.Agent, "");

        // Assert
        result.Should().Be("Assistant");
    }

    [Fact]
    public void Convert_WhenAgentWithNullName_ReturnsAssistant()
    {
        // Act
        var result = _sut.Convert(SenderType.Agent, null);

        // Assert
        result.Should().Be("Assistant");
    }

    [Fact]
    public void Convert_WhenSubagentWithName_ReturnsName()
    {
        // Act
        var result = _sut.Convert(SenderType.Subagent, "Researcher");

        // Assert
        result.Should().Be("Researcher");
    }

    [Fact]
    public void Convert_WhenSubagentWithEmptyName_ReturnsSubagent()
    {
        // Act
        var result = _sut.Convert(SenderType.Subagent, "");

        // Assert
        result.Should().Be("Subagent");
    }

    [Fact]
    public void Convert_WhenSubagentWithNullName_ReturnsSubagent()
    {
        // Act
        var result = _sut.Convert(SenderType.Subagent, null);

        // Assert
        result.Should().Be("Subagent");
    }

    [Fact]
    public void Convert_WhenAgentWithWhitespaceName_ReturnsAssistant()
    {
        // Act
        var result = _sut.Convert(SenderType.Agent, "   ");

        // Assert
        result.Should().Be("Assistant");
    }

    // ─── IValueConverter-style Convert method ────────────────────────────────

    [Fact]
    public void ConvertObject_WhenUserSenderType_ReturnsYou()
    {
        // Act
        var result = _sut.Convert(SenderType.User, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("You");
    }

    [Fact]
    public void ConvertObject_WhenAgentWithNameParameter_ReturnsName()
    {
        // Act
        var result = _sut.Convert(SenderType.Agent, typeof(string), "Claude", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Claude");
    }

    [Fact]
    public void ConvertObject_WhenValueIsNotSenderType_DefaultsToAgent()
    {
        // Act
        var result = _sut.Convert("not a sender type", typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("Assistant");
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
