using System.Text.Json;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

namespace openMob.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="SendPromptRequestBuilder"/>.
/// Verifies that <see cref="SendPromptRequestBuilder.FromText"/> produces correctly
/// structured <see cref="SendPromptRequest"/> instances with the correct wire format.
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
        result.Model.Should().BeNull();
        result.Agent.Should().BeNull();
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

    // ─── Model nested object (wire format confirmed from opencode server source) ─

    [Fact]
    public void FromText_WithBothModelIdAndProviderId_SetsNestedModelObject()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("hello", modelId: "claude-3-5-haiku-20241022", providerId: "anthropic");

        // Assert — model is a nested object, not flat fields
        result.Model.Should().NotBeNull();
        result.Model!.ModelId.Should().Be("claude-3-5-haiku-20241022");
        result.Model!.ProviderId.Should().Be("anthropic");
    }

    [Fact]
    public void FromText_WithModelIdOnly_ModelIsNull()
    {
        // Act — only modelId without providerId → no model object (both required)
        var result = SendPromptRequestBuilder.FromText("hello", modelId: "claude-3");

        // Assert
        result.Model.Should().BeNull();
    }

    [Fact]
    public void FromText_WithProviderIdOnly_ModelIsNull()
    {
        // Act — only providerId without modelId → no model object (both required)
        var result = SendPromptRequestBuilder.FromText("hello", providerId: "anthropic");

        // Assert
        result.Model.Should().BeNull();
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

    // ─── Optional agentName (AC-004) ──────────────────────────────────────────

    [Fact]
    public void FromText_WhenAgentNameProvided_SetsAgentProperty()
    {
        // Act
        var result = SendPromptRequestBuilder.FromText("text", agentName: "om-mobile-core");

        // Assert
        result.Agent.Should().Be("om-mobile-core");
    }

    // ─── JSON serialization — model nested object (AC-001) ───────────────────

    [Fact]
    public void FromText_WhenModelProvided_SerializedJsonContainsNestedModelObject()
    {
        // Arrange
        var result = SendPromptRequestBuilder.FromText("text",
            modelId: "claude-3-5-haiku-20241022",
            providerId: "anthropic");

        // Act
        var json = JsonSerializer.Serialize(result);

        // Assert — wire format: { "model": { "providerID": "anthropic", "modelID": "claude-3-5-haiku-20241022" } }
        json.Should().Contain("\"model\":{");
        json.Should().Contain("\"providerID\":\"anthropic\"");
        json.Should().Contain("\"modelID\":\"claude-3-5-haiku-20241022\"");
        // Must NOT have flat top-level modelID/providerID (i.e. not at root level outside "model":{...})
        json.Should().NotMatchRegex("^\\{[^{]*\"modelID\"");
    }

    [Fact]
    public void FromText_WhenModelIsNull_SerializedJsonOmitsModelKey()
    {
        // Arrange
        var result = SendPromptRequestBuilder.FromText("text");

        // Act
        var json = JsonSerializer.Serialize(result);

        // Assert
        json.Should().NotContain("\"model\"");
    }

    // ─── JSON serialization of Agent field (AC-005) ───────────────────────────

    [Fact]
    public void FromText_WhenAgentNameIsNull_SerializedJsonOmitsAgentKey()
    {
        // Arrange
        var result = SendPromptRequestBuilder.FromText("text");

        // Act
        var json = JsonSerializer.Serialize(result);

        // Assert
        json.Should().NotContain("\"agent\"");
    }

    [Fact]
    public void FromText_WhenAgentNameProvided_SerializedJsonContainsAgentKey()
    {
        // Arrange
        var result = SendPromptRequestBuilder.FromText("text", agentName: "test-agent");

        // Act
        var json = JsonSerializer.Serialize(result);

        // Assert
        json.Should().Contain("\"agent\":\"test-agent\"");
    }
}
