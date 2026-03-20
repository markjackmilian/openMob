using System.Text.Json;
using openMob.Core.Infrastructure.Logging;

namespace openMob.Tests.Infrastructure.Logging;

/// <summary>
/// Unit tests for <see cref="DebugLogger"/>.
/// Each test captures the static <see cref="DebugLogger.WriteAction"/> delegate,
/// invokes the method under test, and asserts the emitted tag and JSON payload.
/// The delegate is always restored in a finally block to prevent test pollution.
/// </summary>
public sealed class DebugLoggerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parses a JSON string and returns the root element.
    /// Throws if the string is null or not valid JSON.
    /// </summary>
    private static JsonElement ParseJson(string? json)
    {
        json.Should().NotBeNullOrEmpty("the WriteAction must have been called");
        return JsonDocument.Parse(json!).RootElement;
    }

    // =========================================================================
    // LogHttp tests
    // =========================================================================

    [Fact]
    public void LogHttp_WhenCalled_WritesToWriteAction()
    {
        // Arrange
        string? capturedTag = null;
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (tag, msg) => { capturedTag = tag; capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogHttp("GET", "http://localhost/api", null, 200, "{}", 42);

            // Assert
            capturedTag.Should().Be("OM_HTTP");
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("layer").GetString().Should().Be("HTTP");
            doc.GetProperty("method").GetString().Should().Be("GET");
            doc.GetProperty("url").GetString().Should().Be("http://localhost/api");
            doc.GetProperty("status").GetInt32().Should().Be(200);
            doc.GetProperty("duration_ms").GetInt64().Should().Be(42);
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogHttp_WhenCalled_JsonIsValidSingleLine()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogHttp("POST", "http://x/y", "body", 201, "resp", 10);

            // Assert
            capturedMessage.Should().NotContain("\n", "JSON must be a single line");
            capturedMessage.Should().NotContain("\r", "JSON must be a single line");
            var act = () => JsonDocument.Parse(capturedMessage!);
            act.Should().NotThrow("the emitted message must be valid JSON");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogHttp_WhenCalled_IncludesTimestampAndThreadId()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogHttp("DELETE", "http://x", null, 204, null, 5);

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("ts").GetString().Should().NotBeNullOrEmpty("ts must be present");
            doc.GetProperty("tid").GetInt32().Should().BeGreaterThan(0, "tid must be a positive thread ID");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    // =========================================================================
    // LogSse tests
    // =========================================================================

    [Fact]
    public void LogSse_Open_WritesOpenEvent()
    {
        // Arrange
        string? capturedTag = null;
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (tag, msg) => { capturedTag = tag; capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogSse("open", "http://localhost:4096/events");

            // Assert
            capturedTag.Should().Be("OM_SSE");
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("layer").GetString().Should().Be("SSE");
            doc.GetProperty("event").GetString().Should().Be("open");
            doc.GetProperty("url").GetString().Should().Be("http://localhost:4096/events");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogSse_Chunk_WritesChunkWithContent()
    {
        // Arrange
        string? capturedTag = null;
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (tag, msg) => { capturedTag = tag; capturedMessage = msg; };

        try
        {
            // Act — pass chunkIndex=1 as the caller now tracks state locally
            DebugLogger.LogSse("chunk", "hello world", chunkIndex: 1);

            // Assert
            capturedTag.Should().Be("OM_SSE");
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("event").GetString().Should().Be("chunk");
            doc.GetProperty("content").GetString().Should().Be("hello world");
            doc.GetProperty("chunk_index").GetInt32().Should().Be(1);
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogSse_Close_WritesTotalChunks()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act — caller now tracks totalChunks and streamDurationMs as local variables
            DebugLogger.LogSse("close", null, totalChunks: 2, streamDurationMs: 500);

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("event").GetString().Should().Be("close");
            doc.GetProperty("total_chunks").GetInt32().Should().Be(2);
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogSse_Error_WritesErrorMessage()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogSse("error", "connection reset");

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("event").GetString().Should().Be("error");
            doc.GetProperty("error").GetString().Should().Be("connection reset");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    // =========================================================================
    // LogCommand tests
    // =========================================================================

    [Fact]
    public void LogCommand_Start_WritesStartPhase()
    {
        // Arrange
        string? capturedTag = null;
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (tag, msg) => { capturedTag = tag; capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogCommand("LoadSessionsAsync", "start");

            // Assert
            capturedTag.Should().Be("OM_CMD");
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("layer").GetString().Should().Be("CMD");
            doc.GetProperty("command").GetString().Should().Be("LoadSessionsAsync");
            doc.GetProperty("phase").GetString().Should().Be("start");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogCommand_Complete_WritesCompletePhase()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogCommand("LoadSessionsAsync", "complete", 150L);

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("phase").GetString().Should().Be("complete");
            doc.GetProperty("duration_ms").ValueKind.Should().Be(JsonValueKind.Number);
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogCommand_Failed_WritesErrorDetail()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogCommand("SendMessageAsync", "failed", error: "NullReferenceException: Object reference not set");

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("phase").GetString().Should().Be("failed");
            doc.GetProperty("error").GetString().Should().Contain("NullReferenceException");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    // =========================================================================
    // LogNavigation tests
    // =========================================================================

    [Fact]
    public void LogNavigation_WithoutParameters_WritesRoute()
    {
        // Arrange
        string? capturedTag = null;
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (tag, msg) => { capturedTag = tag; capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogNavigation("server-detail");

            // Assert
            capturedTag.Should().Be("OM_NAV");
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("layer").GetString().Should().Be("NAV");
            doc.GetProperty("route").GetString().Should().Be("server-detail");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogNavigation_WithParameters_SerializesParameters()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogNavigation("project-detail", new Dictionary<string, object> { ["id"] = "abc123" });

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("route").GetString().Should().Be("project-detail");

            // params is serialised as a nested JSON string — verify it is non-null and contains the value
            var paramsValue = doc.GetProperty("params").GetString();
            paramsValue.Should().NotBeNull("params must be serialised when parameters are provided");
            paramsValue.Should().Contain("abc123");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    // =========================================================================
    // LogDatabase tests
    // =========================================================================

    [Fact]
    public void LogDatabase_WhenCalled_WritesAllFields()
    {
        // Arrange
        string? capturedTag = null;
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (tag, msg) => { capturedTag = tag; capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogDatabase("GetAll", "ServerConnection", null, 33);

            // Assert
            capturedTag.Should().Be("OM_DB");
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("layer").GetString().Should().Be("DB");
            doc.GetProperty("op").GetString().Should().Be("GetAll");
            doc.GetProperty("entity").GetString().Should().Be("ServerConnection");
            doc.GetProperty("duration_ms").GetInt64().Should().Be(33);
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogDatabase_WithKeyInfo_WritesKeyField()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogDatabase("Get", "ServerConnection", "id=abc", 5);

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("key").GetString().Should().Be("id=abc");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    // =========================================================================
    // LogConnection tests
    // =========================================================================

    [Fact]
    public void LogConnection_HealthCheck_WritesEvent()
    {
        // Arrange
        string? capturedTag = null;
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (tag, msg) => { capturedTag = tag; capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogConnection("health_check", "url=http://x status=true", 20);

            // Assert
            capturedTag.Should().Be("OM_CONN");
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("layer").GetString().Should().Be("CONN");
            doc.GetProperty("event").GetString().Should().Be("health_check");
            doc.GetProperty("detail").GetString().Should().Contain("url=http://x");
            doc.GetProperty("duration_ms").GetInt64().Should().Be(20);
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogConnection_ServerChanged_WritesEvent()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogConnection("server_changed", "id=abc123 name=my-server");

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("event").GetString().Should().Be("server_changed");
            doc.GetProperty("detail").GetString().Should().Contain("abc123");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    [Fact]
    public void LogConnection_DiscoveryResult_WritesEvent()
    {
        // Arrange
        string? capturedMessage = null;
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, msg) => { capturedMessage = msg; };

        try
        {
            // Act
            DebugLogger.LogConnection("discovery_result", "host=192.168.1.1:4096 name=opencode-dev");

            // Assert
            var doc = ParseJson(capturedMessage);
            doc.GetProperty("event").GetString().Should().Be("discovery_result");
            doc.GetProperty("detail").GetString().Should().Contain("opencode-dev");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }

    // =========================================================================
    // WriteAction default behaviour test
    // =========================================================================

    [Fact]
    public void WriteAction_Default_DoesNotThrow()
    {
        // Arrange — set to the canonical no-op to ensure we test the default behaviour
        var original = DebugLogger.WriteAction;
        DebugLogger.WriteAction = (_, _) => { };

        try
        {
            // Act + Assert — all 6 log methods must complete without throwing
            var act = () =>
            {
                DebugLogger.LogHttp("GET", "http://localhost/api", null, 200, null, 1);
                DebugLogger.LogSse("open", null);
                DebugLogger.LogSse("chunk", "data");
                DebugLogger.LogSse("close", null);
                DebugLogger.LogCommand("TestCommand", "start");
                DebugLogger.LogNavigation("//home");
                DebugLogger.LogDatabase("GetAll", "Session", null, 2);
                DebugLogger.LogConnection("health_check", "url=http://x", 5);
            };

            act.Should().NotThrow("the no-op WriteAction must never throw");
        }
        finally
        {
            DebugLogger.WriteAction = original;
        }
    }
}
