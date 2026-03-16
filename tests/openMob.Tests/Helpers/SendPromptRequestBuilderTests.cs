using System.Text.Json;
using openMob.Core.Helpers;

namespace openMob.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="SendPromptRequestBuilder"/>.
/// Verifies that <see cref="SendPromptRequestBuilder.FromText"/> produces correctly
/// structured <see cref="openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests.SendPromptRequest"/> instances.
/// </summary>
public sealed class SendPromptRequestBuilderTests
{
    // ─── Parts count and null defaults ────────────────────────────────────────

    [Fact]
    public void FromText_WithTextOnly_ReturnsSinglePartRequest()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("hello");

        // Assert
        result.Parts.Should().HaveCount(1);
        result.ModelId.Should().BeNull();
        result.ProviderId.Should().BeNull();
    }

    // ─── Wire format of the part ──────────────────────────────────────────────

    [Fact]
    public void FromText_PartHasCorrectWireFormat_TypeIsText()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("hello");

        // Assert
        var part = result.Parts[0];
        part.TryGetProperty("type", out var typeProp).Should().BeTrue();
        typeProp.GetString().Should().Be("text");
    }

    [Fact]
    public void FromText_PartHasCorrectWireFormat_TextMatchesInput()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("hello");

        // Assert
        var part = result.Parts[0];
        part.TryGetProperty("text", out var textProp).Should().BeTrue();
        textProp.GetString().Should().Be("hello");
    }

    // ─── Optional modelId ─────────────────────────────────────────────────────

    [Fact]
    public void FromText_WithModelId_SetsModelId()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("hello", modelId: "claude-3");

        // Assert
        result.ModelId.Should().Be("claude-3");
    }

    // ─── Optional providerId ──────────────────────────────────────────────────

    [Fact]
    public void FromText_WithProviderId_SetsProviderId()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("hello", providerId: "anthropic");

        // Assert
        result.ProviderId.Should().Be("anthropic");
    }

    // ─── Empty text ───────────────────────────────────────────────────────────

    [Fact]
    public void FromText_WithEmptyText_StillBuildsValidRequest_WithSinglePart()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("");

        // Assert
        result.Parts.Should().HaveCount(1);
    }

    [Fact]
    public void FromText_WithEmptyText_PartHasEmptyTextValue()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("");

        // Assert
        var part = result.Parts[0];
        part.TryGetProperty("text", out var textProp).Should().BeTrue();
        textProp.GetString().Should().Be("");
    }

    // ─── Both optional parameters ─────────────────────────────────────────────

    [Fact]
    public void FromText_WithBothModelIdAndProviderId_SetsBothProperties()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("test", modelId: "claude-3", providerId: "anthropic");

        // Assert
        result.ModelId.Should().Be("claude-3");
        result.ProviderId.Should().Be("anthropic");
    }
}
