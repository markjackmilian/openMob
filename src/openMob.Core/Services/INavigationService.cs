namespace openMob.Core.Services;

/// <summary>
/// Abstraction over Shell navigation for testability.
/// The MAUI project provides the concrete implementation wrapping <c>Shell.Current</c>.
/// </summary>
/// <remarks>
/// All ViewModels use this interface for navigation — never <c>Shell.Current</c> directly.
/// Routes use the <c>"//route"</c> syntax for absolute navigation (prevents back navigation to splash)
/// and <c>"route"</c> for relative push navigation.
/// </remarks>
public interface INavigationService
{
    /// <summary>Navigates to the specified route.</summary>
    /// <param name="route">The Shell route (e.g. <c>"//chat"</c>, <c>"project-detail"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task GoToAsync(string route, CancellationToken ct = default);

    /// <summary>Navigates to the specified route with query parameters.</summary>
    /// <param name="route">The Shell route.</param>
    /// <param name="parameters">The navigation parameters to pass to the target page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task GoToAsync(string route, IDictionary<string, object> parameters, CancellationToken ct = default);

    /// <summary>Pops the current page from the navigation stack.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task PopAsync(CancellationToken ct = default);

    /// <summary>Closes the Shell flyout drawer if it is open.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task CloseFlyoutAsync(CancellationToken ct = default);
}
