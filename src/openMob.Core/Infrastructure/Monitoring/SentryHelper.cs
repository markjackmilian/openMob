using Sentry;

namespace openMob.Core.Infrastructure.Monitoring;

/// <summary>
/// Helper utilities for Sentry error monitoring and breadcrumb tracking.
/// </summary>
public static class SentryHelper
{
    /// <summary>Captures an exception and sends it to Sentry.</summary>
    /// <param name="exception">The exception to capture.</param>
    /// <param name="extras">Optional key-value pairs to attach as extra context.</param>
    public static void CaptureException(Exception exception, IDictionary<string, object>? extras = null)
    {
        SentrySdk.CaptureException(exception, scope =>
        {
            if (extras is not null)
            {
                foreach (var (key, value) in extras)
                    scope.SetExtra(key, value);
            }
        });
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
