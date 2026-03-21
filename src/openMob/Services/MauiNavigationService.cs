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
        await Shell.Current.GoToAsync(route, true);
    }

    /// <inheritdoc />
    public async Task GoToAsync(string route, IDictionary<string, object> parameters, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
#if DEBUG
        DebugLogger.LogNavigation(route, parameters);
#endif
        await Shell.Current.GoToAsync(route, true, parameters);
    }

    /// <inheritdoc />
    public async Task PopAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
#if DEBUG
        DebugLogger.LogNavigation("..");
#endif
        await Shell.Current.GoToAsync("..", true);
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
