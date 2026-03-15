namespace openMob.Core.Infrastructure.Http;

/// <summary>
/// Categorises the kind of error returned by the opencode API client.
/// </summary>
public enum ErrorKind
{
    /// <summary>The request exceeded the configured timeout.</summary>
    Timeout,

    /// <summary>The server could not be reached (network-level failure).</summary>
    NetworkUnreachable,

    /// <summary>The server returned HTTP 401 Unauthorized.</summary>
    Unauthorized,

    /// <summary>The server returned HTTP 404 Not Found.</summary>
    NotFound,

    /// <summary>The server returned a 5xx error.</summary>
    ServerError,

    /// <summary>No active server connection is configured.</summary>
    NoActiveServer,

    /// <summary>An unexpected error occurred that does not fit any other category.</summary>
    Unknown,
}

/// <summary>
/// Describes an error returned by the opencode API client.
/// </summary>
/// <param name="Kind">The category of the error.</param>
/// <param name="Message">A user-readable description of the error.</param>
/// <param name="HttpStatusCode">The HTTP status code, or <c>null</c> for network-level errors.</param>
/// <param name="InnerException">The underlying exception, if any.</param>
public sealed record OpencodeApiError(
    ErrorKind Kind,
    string Message,
    int? HttpStatusCode,
    Exception? InnerException
);

/// <summary>
/// A discriminated union result type for opencode API calls.
/// Carries either a success value of type <typeparamref name="T"/> or an <see cref="OpencodeApiError"/>.
/// API methods never throw for expected HTTP errors — they always return a result.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly struct OpencodeResult<T>
{
    private readonly T? _value;
    private readonly OpencodeApiError? _error;

    private OpencodeResult(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private OpencodeResult(OpencodeApiError error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the success value. Only valid when <see cref="IsSuccess"/> is <c>true</c>.
    /// </summary>
    public T? Value => _value;

    /// <summary>
    /// Gets the error. Only valid when <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public OpencodeApiError? Error => _error;

    /// <summary>Creates a successful result wrapping the given value.</summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="OpencodeResult{T}"/>.</returns>
    public static OpencodeResult<T> Success(T value) => new(value);

    /// <summary>Creates a failure result wrapping the given error.</summary>
    /// <param name="error">The error details.</param>
    /// <returns>A failed <see cref="OpencodeResult{T}"/>.</returns>
    public static OpencodeResult<T> Failure(OpencodeApiError error) => new(error);

    /// <summary>Implicitly converts a value to a successful result.</summary>
    public static implicit operator OpencodeResult<T>(T value) => Success(value);

    /// <summary>Implicitly converts an error to a failure result.</summary>
    public static implicit operator OpencodeResult<T>(OpencodeApiError error) => Failure(error);
}
