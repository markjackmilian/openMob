namespace openMob.Core.Services;

/// <summary>
/// Abstraction over UI thread dispatching for testability.
/// The MAUI project provides the concrete implementation wrapping
/// <c>MainThread.BeginInvokeOnMainThread</c>.
/// In tests, the mock executes the action synchronously.
/// </summary>
public interface IDispatcherService
{
    /// <summary>Dispatches the specified action to the UI thread.</summary>
    /// <param name="action">The action to execute on the UI thread.</param>
    void Dispatch(Action action);
}
