namespace openMob.Tests.Infrastructure.Http;

/// <summary>
/// Unit tests for <see cref="OpencodeResult{T}"/> and <see cref="OpencodeApiError"/>.
/// </summary>
public sealed class OpencodeResultTests
{
    // ──────────────────────────────────────────────────────────────
    // OpencodeResult<T>.Success
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Success_WhenCalledWithValue_SetsIsSuccessTrue()
    {
        // Act
        var result = OpencodeResult<string>.Success("hello");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Success_WhenCalledWithValue_SetsValueCorrectly()
    {
        // Act
        var result = OpencodeResult<string>.Success("hello");

        // Assert
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Success_WhenCalledWithValue_SetsErrorNull()
    {
        // Act
        var result = OpencodeResult<string>.Success("hello");

        // Assert
        result.Error.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // OpencodeResult<T>.Failure
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void Failure_WhenCalledWithError_SetsIsSuccessFalse()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null);

        // Act
        var result = OpencodeResult<string>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Failure_WhenCalledWithError_SetsErrorCorrectly()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.Unauthorized, "Unauthorized", 401, null);

        // Act
        var result = OpencodeResult<string>.Failure(error);

        // Assert
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
        result.Error.Message.Should().Be("Unauthorized");
        result.Error.HttpStatusCode.Should().Be(401);
    }

    [Fact]
    public void Failure_WhenCalledWithError_SetsValueDefault()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.NotFound, "Not found", 404, null);

        // Act
        var result = OpencodeResult<string>.Failure(error);

        // Assert
        result.Value.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // Implicit conversions
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccessResult()
    {
        // Act
        OpencodeResult<int> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_FromOpencodeApiError_CreatesFailureResult()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.NoActiveServer, "No server", null, null);

        // Act
        OpencodeResult<int> result = error;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    // ──────────────────────────────────────────────────────────────
    // OpencodeApiError properties
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void OpencodeApiError_WhenCreated_HasCorrectProperties()
    {
        // Arrange
        var innerEx = new Exception("inner");

        // Act
        var error = new OpencodeApiError(ErrorKind.Timeout, "Timed out", null, innerEx);

        // Assert
        error.Kind.Should().Be(ErrorKind.Timeout);
        error.Message.Should().Be("Timed out");
        error.HttpStatusCode.Should().BeNull();
        error.InnerException.Should().Be(innerEx);
    }
}
