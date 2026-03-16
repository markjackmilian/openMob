using openMob.Core.Infrastructure.Helpers;

namespace openMob.Tests.Infrastructure.Helpers;

/// <summary>
/// Unit tests for <see cref="ServerUrlHelper"/>.
/// </summary>
public sealed class ServerUrlHelperTests
{
    // ─── TryParse — valid HTTP/HTTPS URLs ────────────────────────────────────

    [Theory]
    [InlineData("http://192.168.1.10:4096",    "192.168.1.10",    4096, false)]
    [InlineData("http://myserver.local:8080",   "myserver.local",  8080, false)]
    [InlineData("https://secure.server.com:443","secure.server.com", 443, true)]
    [InlineData("https://secure.server.com",    "secure.server.com", 443, true)]
    [InlineData("http://192.168.1.1",           "192.168.1.1",      80, false)]
    public void TryParse_WhenValidHttpUrl_ReturnsTrueAndCorrectValues(
        string rawUrl, string expectedHost, int expectedPort, bool expectedUseHttps)
    {
        // Act
        var result = ServerUrlHelper.TryParse(rawUrl, out var host, out var port, out var useHttps);

        // Assert
        result.Should().BeTrue();
        host.Should().Be(expectedHost);
        port.Should().Be(expectedPort);
        useHttps.Should().Be(expectedUseHttps);
    }

    // ─── TryParse — null / empty / whitespace ────────────────────────────────

    [Fact]
    public void TryParse_WhenNullInput_ReturnsFalse()
    {
        // Act
        var result = ServerUrlHelper.TryParse(null, out _, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParse_WhenEmptyString_ReturnsFalse()
    {
        // Act
        var result = ServerUrlHelper.TryParse(string.Empty, out _, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParse_WhenWhitespaceOnly_ReturnsFalse()
    {
        // Act
        var result = ServerUrlHelper.TryParse("   ", out _, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    // ─── TryParse — unsupported schemes ──────────────────────────────────────

    [Fact]
    public void TryParse_WhenFtpScheme_ReturnsFalse()
    {
        // Act
        var result = ServerUrlHelper.TryParse("ftp://192.168.1.10:4096", out _, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    // ─── TryParse — malformed / relative URLs ────────────────────────────────

    [Fact]
    public void TryParse_WhenMalformedUrl_ReturnsFalse()
    {
        // Act
        var result = ServerUrlHelper.TryParse("not-a-url", out _, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParse_WhenRelativeUrl_ReturnsFalse()
    {
        // Act
        var result = ServerUrlHelper.TryParse("/path/only", out _, out _, out _);

        // Assert
        result.Should().BeFalse();
    }

    // ─── BuildUrl — non-default port (port included) ─────────────────────────

    [Theory]
    [InlineData("192.168.1.10", 4096, false, "http://192.168.1.10:4096")]
    [InlineData("myserver",     8443, true,  "https://myserver:8443")]
    public void BuildUrl_WhenNonDefaultPort_IncludesPort(
        string host, int port, bool useHttps, string expectedUrl)
    {
        // Act
        var result = ServerUrlHelper.BuildUrl(host, port, useHttps);

        // Assert
        result.Should().Be(expectedUrl);
    }

    // ─── BuildUrl — default port (port omitted) ───────────────────────────────

    [Theory]
    [InlineData("myserver", 80,  false, "http://myserver")]
    [InlineData("myserver", 443, true,  "https://myserver")]
    public void BuildUrl_WhenDefaultPort_OmitsPort(
        string host, int port, bool useHttps, string expectedUrl)
    {
        // Act
        var result = ServerUrlHelper.BuildUrl(host, port, useHttps);

        // Assert
        result.Should().Be(expectedUrl);
    }
}
