using openMob.Core.Infrastructure.Logging;
using openMob.Core.Services;

namespace openMob.Services;

/// <summary>
/// MAUI implementation of <see cref="INavigationService"/> wrapping <see cref="Shell.Current"/>.
/// Registered as Singleton in <c>MauiProgram.cs</c>.
/// </summary>
internal sealed class MauiNavigationService : INavigationService
{
    /// <inheritdoc />
    public async Task GoToAsync(string route, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
#if DEBUG
        DebugLogger.LogNavigation(route);
#endif
        // Shell.Current.GoToAsync must run on the main thread.
        // Callers in Core use ConfigureAwait(false), so continuations may resume
        // on a thread pool thread. We marshal here unconditionally.
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            return Shell.Current.GoToAsync(route, true);
        });
    }

    /// <inheritdoc />
    public async Task GoToAsync(string route, IDictionary<string, object> parameters, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
#if DEBUG
        DebugLogger.LogNavigation(route, parameters);
#endif
        // Shell.Current.GoToAsync must run on the main thread.
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            return Shell.Current.GoToAsync(route, true, parameters);
        });
    }

    /// <inheritdoc />
    public async Task PopAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
#if DEBUG
        DebugLogger.LogNavigation("..");
#endif
        // Shell.Current.GoToAsync must run on the main thread.
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            return Shell.Current.GoToAsync("..", true);
        });
    }

    /// <inheritdoc />
    public Task CloseFlyoutAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (Shell.Current is not null)
            Shell.Current.FlyoutIsPresented = false;
        return Task.CompletedTask;
    }
}
