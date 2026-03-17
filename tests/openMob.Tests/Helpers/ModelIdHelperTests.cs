using openMob.Core.Helpers;

namespace openMob.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="ModelIdHelper"/>.
/// </summary>
public sealed class ModelIdHelperTests
{
    // ─── ExtractModelName — with provider prefix ──────────────────────────────

    [Theory]
    [InlineData("anthropic/claude-sonnet-4-5", "claude-sonnet-4-5")]
    [InlineData("openai/gpt-4o", "gpt-4o")]
    [InlineData("google/gemini-1.5-pro", "gemini-1.5-pro")]
    [InlineData("meta/llama-3-70b", "llama-3-70b")]
    public void ExtractModelName_WhenSlashPresent_ReturnsPartAfterSlash(
        string fullModelId, string expectedModelName)
    {
        // Act
        var result = ModelIdHelper.ExtractModelName(fullModelId);

        // Assert
        result.Should().Be(expectedModelName);
    }

    // ─── ExtractModelName — without provider prefix ───────────────────────────

    [Theory]
    [InlineData("claude-sonnet-4-5")]
    [InlineData("gpt-4")]
    [InlineData("gemini-pro")]
    public void ExtractModelName_WhenNoSlashPresent_ReturnsFullString(string fullModelId)
    {
        // Act
        var result = ModelIdHelper.ExtractModelName(fullModelId);

        // Assert
        result.Should().Be(fullModelId);
    }

    // ─── ExtractModelName — edge cases ────────────────────────────────────────

    [Fact]
    public void ExtractModelName_WhenMultipleSlashes_ReturnsEverythingAfterFirstSlash()
    {
        // Act
        var result = ModelIdHelper.ExtractModelName("provider/model/variant");

        // Assert
        result.Should().Be("model/variant");
    }

    [Fact]
    public void ExtractModelName_WhenSlashAtEnd_ReturnsEmptyString()
    {
        // Act
        var result = ModelIdHelper.ExtractModelName("provider/");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractModelName_WhenSlashAtStart_ReturnsEverythingAfterSlash()
    {
        // Act
        var result = ModelIdHelper.ExtractModelName("/model-name");

        // Assert
        result.Should().Be("model-name");
    }
}
