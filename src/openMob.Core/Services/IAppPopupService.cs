namespace openMob.Core.Services;

/// <summary>
/// Abstraction over UXDivers popup operations for testability in Core ViewModels.
/// Named <c>IAppPopupService</c> to avoid collision with UXDivers <c>IPopupService</c>.
/// The MAUI project provides the concrete implementation.
/// </summary>
/// <remarks>
/// All popup interactions in ViewModels go through this interface — never through
/// <c>IPopupService.Current</c> static access or native <c>DisplayAlert</c> / <c>DisplayActionSheet</c>.
/// </remarks>
public interface IAppPopupService
{
    /// <summary>Shows a destructive confirmation dialog (e.g. delete session/project).</summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the user confirmed the action; <c>false</c> if cancelled.</returns>
    Task<bool> ShowConfirmDeleteAsync(string title, string message, CancellationToken ct = default);

    /// <summary>Shows a rename dialog with a pre-filled text field.</summary>
    /// <param name="currentName">The current name to pre-fill in the text field.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new name entered by the user, or <c>null</c> if cancelled.</returns>
    Task<string?> ShowRenameAsync(string currentName, CancellationToken ct = default);

    /// <summary>Shows a brief toast notification.</summary>
    /// <param name="message">The toast message.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowToastAsync(string message, CancellationToken ct = default);

    /// <summary>Shows an error dialog with a title and detailed message.</summary>
    /// <param name="title">The error title.</param>
    /// <param name="message">The error details.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowErrorAsync(string title, string message, CancellationToken ct = default);

    /// <summary>Shows an option sheet with a list of selectable actions.</summary>
    /// <param name="title">The sheet title.</param>
    /// <param name="options">The list of option labels to display.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The selected option label, or <c>null</c> if dismissed.</returns>
    Task<string?> ShowOptionSheetAsync(string title, IReadOnlyList<string> options, CancellationToken ct = default);

    /// <summary>
    /// Opens the model picker popup and invokes the callback when a model is selected.
    /// </summary>
    /// <remarks>
    /// The MAUI implementation resolves <c>ModelPickerSheet</c>, sets the callback on the
    /// ViewModel, and presents the popup. Core ViewModels call this method without touching
    /// any MAUI types.
    /// </remarks>
    /// <param name="onModelSelected">Callback invoked with the selected model ID in "providerId/modelId" format.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowModelPickerAsync(Action<string> onModelSelected, CancellationToken ct = default);

    /// <summary>Pushes a custom popup page onto the popup stack.</summary>
    /// <param name="popup">The popup page instance to display.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PushPopupAsync(object popup, CancellationToken ct = default);

    /// <summary>Pops the topmost popup from the popup stack.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task PopPopupAsync(CancellationToken ct = default);

    /// <summary>
    /// Opens the Context Sheet bottom sheet for the specified project.
    /// The MAUI implementation resolves <see cref="openMob.Core.ViewModels.ContextSheetViewModel"/>
    /// from DI, calls <c>InitializeAsync</c>, then pushes the sheet modally.
    /// </summary>
    /// <param name="projectId">The project identifier to load preferences for.</param>
    /// <param name="sessionId">The session identifier (reserved for future session-level overrides).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowContextSheetAsync(string projectId, string sessionId, CancellationToken ct = default);

    /// <summary>Shows the Command Palette bottom sheet (REQ-029).</summary>
    /// <param name="ct">Cancellation token.</param>
    Task ShowCommandPaletteAsync(CancellationToken ct = default);

    /// <summary>Shows the agent picker in subagent invocation mode (REQ-031).</summary>
    /// <param name="ct">Cancellation token.</param>
    Task ShowAgentPickerSubagentModeAsync(CancellationToken ct = default);
}
