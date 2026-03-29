using System.Diagnostics;
using Sentry;
using openMob.Core.Infrastructure.Http;

namespace openMob.Core.Infrastructure.Monitoring;

/// <summary>
/// Helper utilities for Sentry error monitoring and breadcrumb tracking.
/// </summary>
public static class SentryHelper
{
    private static readonly AsyncLocal<Action<Exception, IDictionary<string, object>?>?> CaptureExceptionOverride = new();

    private static readonly Action<Exception, IDictionary<string, object>?> DefaultCaptureExceptionImpl = (exception, extras) =>
    {
        SentrySdk.CaptureException(exception, scope =>
        {
            if (extras is not null)
            {
                foreach (var (key, value) in extras)
                    scope.SetExtra(key, value);
            }
        });
    };

    /// <summary>
    /// Gets or sets the exception capture implementation used by <see cref="CaptureException"/>.
    /// </summary>
    internal static Action<Exception, IDictionary<string, object>?> CaptureExceptionImpl
    {
        get => CaptureExceptionOverride.Value ?? DefaultCaptureExceptionImpl;
        set => CaptureExceptionOverride.Value = value;
    }

    /// <summary>Captures an exception and sends it to Sentry.</summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="extras">Optional key-value pairs to attach as extra context.</param>
    public static void CaptureException(Exception exception, IDictionary<string, object>? extras = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        CaptureExceptionImpl(exception, extras);
    }

    /// <summary>Captures an opencode API error when it is unexpected.</summary>
    /// <param name="error">The opencode API error to classify.</param>
    /// <param name="extras">Optional key-value pairs to attach as extra context.</param>
    public static void CaptureOpencodeError(OpencodeApiError error, IDictionary<string, object>? extras = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        Debug.WriteLine($"[openMob] Expected API error ({error.Kind}): {error.Message}");

        if (error.Kind != ErrorKind.Unknown)
            return;

        CaptureException(new InvalidOperationException(error.Message, error.InnerException), extras);
    }

    /// <summary>Adds a breadcrumb to the current Sentry scope.</summary>
    /// <param name="message">The breadcrumb message.</param>
    /// <param name="category">Optional category (e.g. "navigation", "http").</param>
    /// <param name="level">Breadcrumb severity level.</param>
    public static void AddBreadcrumb(
        string message,
        string? category = null,
        BreadcrumbLevel level = BreadcrumbLevel.Info)
    {
        SentrySdk.AddBreadcrumb(message, category, level: level);
    }
}
