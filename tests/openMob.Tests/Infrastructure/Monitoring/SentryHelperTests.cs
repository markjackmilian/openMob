using FluentAssertions;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Monitoring;
using Xunit;

namespace openMob.Tests.Infrastructure.Monitoring;

/// <summary>
/// Unit tests for <see cref="SentryHelper"/>.
/// </summary>
public sealed class SentryHelperTests
{
    [Theory]
    [InlineData(ErrorKind.NoActiveServer)]
    [InlineData(ErrorKind.NetworkUnreachable)]
    [InlineData(ErrorKind.Timeout)]
    [InlineData(ErrorKind.Unauthorized)]
    [InlineData(ErrorKind.NotFound)]
    [InlineData(ErrorKind.ServerError)]
    public void CaptureOpencodeError_WhenExpectedKind_DoesNotCaptureException(ErrorKind kind)
    {
        // Arrange
        var error = new OpencodeApiError(kind, "Expected error", null, null);
        var original = SentryHelper.CaptureExceptionImpl;
        var captured = false;

        SentryHelper.CaptureExceptionImpl = (_, _) => captured = true;

        try
        {
            // Act
            SentryHelper.CaptureOpencodeError(error);

            // Assert
            captured.Should().BeFalse();
        }
        finally
        {
            SentryHelper.CaptureExceptionImpl = original;
        }
    }

    [Fact]
    public void CaptureOpencodeError_WhenUnknownKind_CapturesExceptionWithExtras()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.Unknown, "Unexpected error", 500, null);
        var extras = new Dictionary<string, object> { ["errorKind"] = ErrorKind.Unknown.ToString() };
        var original = SentryHelper.CaptureExceptionImpl;
        Exception? capturedException = null;
        IDictionary<string, object>? capturedExtras = null;

        SentryHelper.CaptureExceptionImpl = (exception, context) =>
        {
            capturedException = exception;
            capturedExtras = context;
        };

        try
        {
            // Act
            SentryHelper.CaptureOpencodeError(error, extras);

            // Assert
            capturedException.Should().BeOfType<InvalidOperationException>();
            capturedException!.Message.Should().Be("Unexpected error");
            capturedExtras.Should().NotBeNull();
            var capturedContext = capturedExtras!;
            capturedContext.Should().ContainKey("errorKind");
            var capturedErrorKind = capturedContext["errorKind"] as string;
            capturedErrorKind.Should().NotBeNull();
            capturedErrorKind!.Should().Be(ErrorKind.Unknown.ToString());
        }
        finally
        {
            SentryHelper.CaptureExceptionImpl = original;
        }
    }
}
