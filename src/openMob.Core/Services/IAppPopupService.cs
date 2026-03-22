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

    /// <summary>
    /// Opens the agent picker popup in primary-agent selection mode and invokes the callback
    /// when an agent is selected.
    /// </summary>
    /// <remarks>
    /// The MAUI implementation resolves <c>AgentPickerSheet</c> from DI, sets
    /// <c>PickerMode = PickerMode.Primary</c> and the callback on <c>AgentPickerViewModel</c>,
    /// and presents the popup modally. Core ViewModels call this method without touching any MAUI types.
    /// </remarks>
    /// <param name="onAgentSelected">
    /// Callback invoked with the selected agent name, or <c>null</c> if the user selects "Default".
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowAgentPickerAsync(Action<string?> onAgentSelected, CancellationToken ct = default);

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

    /// <summary>
    /// Opens the agent picker popup in subagent invocation mode (<see cref="openMob.Core.Models.PickerMode.Subagent"/>)
    /// and invokes the callback when a subagent is selected.
    /// </summary>
    /// <remarks>
    /// The MAUI implementation resolves <c>AgentPickerSheet</c> from DI, sets
    /// <c>PickerMode = PickerMode.Subagent</c> and <c>OnAgentSelected = onSubagentSelected</c>
    /// on <c>AgentPickerViewModel</c>, then presents the popup modally.
    /// </remarks>
    /// <param name="onSubagentSelected">Callback invoked with the selected subagent name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowSubagentPickerAsync(Action<string> onSubagentSelected, CancellationToken ct = default);

    /// <summary>Opens the Project Switcher popup to allow the user to switch the active project.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task ShowProjectSwitcherAsync(CancellationToken ct = default);

    /// <summary>Opens the Add Project popup to allow the user to create a new project.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task ShowAddProjectAsync(CancellationToken ct = default);

    /// <summary>Opens the message composer popup for the specified project and session.</summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="isStreaming">Whether the AI is currently streaming a response (disables Send button).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowMessageComposerAsync(string projectId, string sessionId, bool isStreaming, CancellationToken ct = default);

    /// <summary>Opens the file picker popup. Invokes <paramref name="onFileSelected"/> with the relative path on selection.</summary>
    /// <param name="onFileSelected">Callback invoked with the selected file's relative path.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowFilePickerAsync(Action<string> onFileSelected, CancellationToken ct = default);

    /// <summary>Opens the command palette in callback mode. Invokes <paramref name="onCommandSelected"/> with the command on selection.</summary>
    /// <param name="onCommandSelected">Callback invoked with the selected command string.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ShowCommandPaletteAsync(Action<string> onCommandSelected, CancellationToken ct = default);
}
