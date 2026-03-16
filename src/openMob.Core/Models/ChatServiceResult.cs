namespace openMob.Core.Models;

/// <summary>
/// Discriminated union result for <see cref="IChatService"/> operations.
/// Never throws — callers inspect <see cref="IsSuccess"/> and branch accordingly.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public sealed record ChatServiceResult<T>
{
    // Private constructor — use factory methods Ok() and Fail()
    private ChatServiceResult() { }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>
    /// Gets the success value. Non-null when <see cref="IsSuccess"/> is <c>true</c>.
    /// </summary>
    public T? Value { get; private init; }

    /// <summary>
    /// Gets the error details. Non-null when <see cref="IsSuccess"/> is <c>false</c>.
    /// </summary>
    public ChatServiceError? Error { get; private init; }

    /// <summary>Creates a successful result wrapping the given value.</summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="ChatServiceResult{T}"/>.</returns>
    public static ChatServiceResult<T> Ok(T value) =>
        new() { IsSuccess = true, Value = value };

    /// <summary>Creates a failure result wrapping the given error.</summary>
    /// <param name="error">The error details.</param>
    /// <returns>A failed <see cref="ChatServiceResult{T}"/>.</returns>
    public static ChatServiceResult<T> Fail(ChatServiceError error) =>
        new() { IsSuccess = false, Error = error };
}
