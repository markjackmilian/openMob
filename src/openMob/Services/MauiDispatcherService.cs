using openMob.Core.Services;

namespace openMob.Services;

/// <summary>
/// MAUI implementation of <see cref="IDispatcherService"/> that dispatches
/// actions to the UI thread via <see cref="MainThread.BeginInvokeOnMainThread"/>.
/// </summary>
/// <remarks>
/// Registered as Singleton in <c>MauiProgram.cs</c>.
/// Platform behaviour:
/// - iOS: dispatches to the main (UI) thread via GCD main queue.
/// - Android: dispatches to the main Looper thread.
/// If already on the main thread, the action is executed synchronously to avoid unnecessary overhead.
/// </remarks>
internal sealed class MauiDispatcherService : IDispatcherService
{
    /// <inheritdoc />
    public void Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }
}
