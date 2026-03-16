namespace openMob.Core.Models;

/// <summary>Categorises the kind of error returned by <see cref="IChatService"/> operations.</summary>
public enum ChatServiceErrorKind
{
    /// <summary>A network-level failure occurred (DNS, connection refused, etc.).</summary>
    NetworkError,

    /// <summary>The server returned a 5xx error response.</summary>
    ServerError,

    /// <summary>The request exceeded the configured timeout.</summary>
    Timeout,

    /// <summary>The circuit breaker is open; the request was rejected without being sent.</summary>
    CircuitOpen,

    /// <summary>The operation was cancelled via the <see cref="CancellationToken"/>.</summary>
    Cancelled,

    /// <summary>An unexpected error occurred that does not fit any other category.</summary>
    Unknown,
}

/// <summary>
/// Describes an error returned by an <see cref="IChatService"/> operation.
/// </summary>
/// <param name="Kind">The category of the error.</param>
/// <param name="Message">A human-readable description of the error.</param>
/// <param name="HttpStatusCode">The HTTP status code, or <c>null</c> for non-HTTP errors.</param>
public sealed record ChatServiceError(
    ChatServiceErrorKind Kind,
    string Message,
    int? HttpStatusCode = null
);
