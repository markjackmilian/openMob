using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

namespace openMob.Core.Infrastructure.Http;

/// <summary>
/// Full HTTP client interface for communicating with a running opencode server.
/// All methods return <see cref="OpencodeResult{T}"/> — they never throw for expected
/// HTTP errors (4xx, 5xx, timeout, network unreachable).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsWaitingForServer"/> is <c>true</c> for the entire duration of any
/// in-flight request and <c>false</c> when idle. Subscribe to
/// <see cref="IsWaitingForServerChanged"/> to drive UI loading indicators.
/// </para>
/// <para>
/// The implementation uses <c>IHttpClientFactory</c> with the named client <c>"opencode"</c>.
/// Base URL and auth headers are resolved at runtime from <see cref="IOpencodeConnectionManager"/>.
/// </para>
/// </remarks>
public interface IOpencodeApiClient
{
    // ─── Waiting state ────────────────────────────────────────────────────────

    /// <summary>
    /// Gets a value indicating whether any HTTP request is currently in flight.
    /// </summary>
    bool IsWaitingForServer { get; }

    /// <summary>
    /// Raised whenever <see cref="IsWaitingForServer"/> changes.
    /// Subscribers receive the new value.
    /// </summary>
    event Action<bool>? IsWaitingForServerChanged;

    // ─── Global ───────────────────────────────────────────────────────────────

    /// <summary>Checks whether the opencode server is healthy. Maps to <c>GET /global/health</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<HealthDto>> GetHealthAsync(CancellationToken ct = default);

    // ─── Project ──────────────────────────────────────────────────────────────

    /// <summary>Lists all projects known to the server. Maps to <c>GET /project</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<ProjectDto>>> GetProjectsAsync(CancellationToken ct = default);

    /// <summary>Gets the current project. Maps to <c>GET /project/current</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<ProjectDto>> GetCurrentProjectAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new session for the specified directory context. The implementation injects
    /// the directory via the <c>x-opencode-directory</c> header so the server registers the
    /// project if it does not already exist.
    /// </summary>
    /// <param name="directory">The server directory to register.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<SessionDto>> CreateSessionForDirectoryAsync(string directory, CancellationToken ct = default);

    // ─── Path & VCS ───────────────────────────────────────────────────────────

    /// <summary>Gets the server's path configuration. Maps to <c>GET /path</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<PathDto>> GetPathAsync(CancellationToken ct = default);

    /// <summary>Gets VCS information for the current project. Maps to <c>GET /vcs</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<VcsInfoDto>> GetVcsInfoAsync(CancellationToken ct = default);

    // ─── Config ───────────────────────────────────────────────────────────────

    /// <summary>Gets the server configuration. Maps to <c>GET /config</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<ConfigDto>> GetConfigAsync(CancellationToken ct = default);

    /// <summary>Updates the server configuration. Maps to <c>PUT /config</c>.</summary>
    /// <param name="request">The configuration update request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<ConfigDto>> UpdateConfigAsync(UpdateConfigRequest request, CancellationToken ct = default);

    /// <summary>Gets the provider section of the server configuration. Maps to <c>GET /config/providers</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<ConfigProvidersDto>> GetConfigProvidersAsync(CancellationToken ct = default);

    // ─── Provider ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all providers known to the server. Maps to <c>GET /provider</c>.
    /// </summary>
    /// <remarks>
    /// The response is an envelope containing <c>all</c> (every provider), <c>default</c>
    /// (per-provider default model IDs), and <c>connected</c> (IDs of providers that have
    /// a valid credential configured). Use <see cref="ProviderListResponseDto.Connected"/>
    /// rather than <see cref="ProviderDto.Key"/> to determine connectivity.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<ProviderListResponseDto>> GetProvidersAsync(CancellationToken ct = default);

    /// <summary>Lists available authentication methods for providers. Maps to <c>GET /provider/auth</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<ProviderAuthMethodDto>>> GetProviderAuthMethodsAsync(CancellationToken ct = default);

    /// <summary>
    /// Initiates an OAuth authorization flow for a provider.
    /// Maps to <c>GET /provider/{id}/auth/authorize</c>.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<ProviderAuthAuthorizationDto>> AuthorizeProviderOAuthAsync(string providerId, CancellationToken ct = default);

    /// <summary>
    /// Handles the OAuth callback for a provider.
    /// Maps to <c>POST /provider/{id}/auth/callback</c>.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="request">The OAuth callback request containing the authorization code.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> HandleProviderOAuthCallbackAsync(string providerId, OAuthCallbackRequest request, CancellationToken ct = default);

    // ─── Sessions ─────────────────────────────────────────────────────────────

    /// <summary>Lists all sessions. Maps to <c>GET /session</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<SessionDto>>> GetSessionsAsync(CancellationToken ct = default);

    /// <summary>Gets a session by ID. Maps to <c>GET /session/{id}</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<SessionDto>> GetSessionAsync(string id, CancellationToken ct = default);

    /// <summary>Gets the status of all sessions. Maps to <c>GET /session/status</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyDictionary<string, SessionStatusDto>>> GetSessionStatusAsync(CancellationToken ct = default);

    /// <summary>Gets the child sessions of a session. Maps to <c>GET /session/{id}/children</c>.</summary>
    /// <param name="id">The parent session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<SessionDto>>> GetSessionChildrenAsync(string id, CancellationToken ct = default);

    /// <summary>Gets the todo list for a session. Maps to <c>GET /session/{id}/todo</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<TodoDto>>> GetSessionTodoAsync(string id, CancellationToken ct = default);

    /// <summary>Creates a new session. Maps to <c>POST /session</c>.</summary>
    /// <param name="request">The session creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<SessionDto>> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct = default);

    /// <summary>Updates a session. Maps to <c>PUT /session/{id}</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="request">The session update request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<SessionDto>> UpdateSessionAsync(string id, UpdateSessionRequest request, CancellationToken ct = default);

    /// <summary>Deletes a session. Maps to <c>DELETE /session/{id}</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> DeleteSessionAsync(string id, CancellationToken ct = default);

    /// <summary>Initializes a session. Maps to <c>POST /session/{id}/init</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="request">The initialization request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> InitSessionAsync(string id, InitSessionRequest request, CancellationToken ct = default);

    /// <summary>Forks a session from a given message. Maps to <c>POST /session/{id}/fork</c>.</summary>
    /// <param name="id">The session identifier to fork from.</param>
    /// <param name="request">The fork request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<SessionDto>> ForkSessionAsync(string id, ForkSessionRequest request, CancellationToken ct = default);

    /// <summary>Aborts a running session. Maps to <c>POST /session/{id}/abort</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> AbortSessionAsync(string id, CancellationToken ct = default);

    /// <summary>Shares a session publicly. Maps to <c>POST /session/{id}/share</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<SessionDto>> ShareSessionAsync(string id, CancellationToken ct = default);

    /// <summary>Removes the public share for a session. Maps to <c>DELETE /session/{id}/share</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<SessionDto>> UnshareSessionAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Gets the file diff for a session, optionally scoped to a specific message.
    /// Maps to <c>GET /session/{id}/diff</c>.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="messageId">Optional message ID to scope the diff to.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<FileDiffDto>>> GetSessionDiffAsync(string id, string? messageId = null, CancellationToken ct = default);

    /// <summary>Summarizes a session. Maps to <c>POST /session/{id}/summarize</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="request">The summarize request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> SummarizeSessionAsync(string id, SummarizeSessionRequest request, CancellationToken ct = default);

    /// <summary>Reverts a session to a previous message state. Maps to <c>POST /session/{id}/revert</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="request">The revert request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> RevertSessionAsync(string id, RevertSessionRequest request, CancellationToken ct = default);

    /// <summary>Unrevert a previously reverted session. Maps to <c>POST /session/{id}/unrevert</c>.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> UnrevertSessionAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Responds to a pending permission request.
    /// Maps to <c>POST /session/{id}/permission/{permissionId}</c>.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="permissionId">The permission request identifier.</param>
    /// <param name="request">The permission response.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> RespondToPermissionAsync(string id, string permissionId, PermissionResponseRequest request, CancellationToken ct = default);

    /// <summary>
    /// Replies to a pending permission request.
    /// Maps to <c>POST /permission/{requestId}/reply</c>.</summary>
    /// <param name="requestId">The permission request identifier.</param>
    /// <param name="reply">The reply value to send.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> ReplyToPermissionAsync(string requestId, string reply, CancellationToken ct = default);

    // ─── TUI Control ──────────────────────────────────────────────────────────

    /// <summary>
    /// Submits an answer to a pending TUI control request.
    /// Maps to <c>POST /tui/control/response</c> with body <c>{ "requestID": "&lt;id&gt;", "body": "&lt;answer&gt;" }</c>.
    /// </summary>
    /// <param name="requestId">The TUI control request identifier.</param>
    /// <param name="body">The answer text to submit.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> RespondToTuiControlAsync(string requestId, string body, CancellationToken ct = default);

    /// <summary>
    /// Waits for and returns the next pending TUI control request.
    /// Maps to <c>GET /tui/control/next</c>.
    /// Returns <c>null</c> wrapped in <see cref="OpencodeResult{T}"/> when no control request
    /// is pending (HTTP 204 or timeout).
    /// </summary>
    /// <remarks>
    /// The server may long-poll this endpoint. Always pass a short-timeout
    /// <see cref="CancellationToken"/> (≤ 2 seconds) when calling from <c>LoadMessagesAsync</c>
    /// to prevent the session load from hanging indefinitely.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<TuiControlRequestDto?>> GetNextTuiControlAsync(CancellationToken ct = default);

    // ─── Permissions ──────────────────────────────────────────────────────────

    /// <summary>
    /// Lists all pending permission requests across all sessions.
    /// Maps to <c>GET /permission</c>.
    /// </summary>
    /// <remarks>
    /// The endpoint returns permissions for all sessions. Filter by <paramref name="sessionId"/>
    /// client-side to obtain only the permissions relevant to the active session.
    /// </remarks>
    /// <param name="sessionId">The session identifier to filter results by.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<PermissionRequestDto>>> GetPendingPermissionsAsync(
        string sessionId, CancellationToken ct = default);

    // ─── Messages ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists messages in a session, optionally limited to the most recent <paramref name="limit"/> messages.
    /// Maps to <c>GET /session/{sessionId}/message</c>.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="limit">Maximum number of messages to return, or <c>null</c> for all.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<MessageWithPartsDto>>> GetMessagesAsync(string sessionId, int? limit = null, CancellationToken ct = default);

    /// <summary>Gets a specific message by ID. Maps to <c>GET /session/{sessionId}/message/{messageId}</c>.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="messageId">The message identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<MessageWithPartsDto>> GetMessageAsync(string sessionId, string messageId, CancellationToken ct = default);

    /// <summary>
    /// Sends a prompt synchronously and waits for the full response.
    /// Maps to <c>POST /session/{sessionId}/message</c>.
    /// <see cref="IsWaitingForServer"/> remains <c>true</c> for the entire duration.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="request">The prompt request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<MessageWithPartsDto>> SendPromptAsync(string sessionId, SendPromptRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sends a prompt asynchronously (fire-and-forget). Returns immediately with HTTP 204.
    /// Maps to <c>POST /session/{sessionId}/prompt_async</c>.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="request">The prompt request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> SendPromptAsyncNoWait(string sessionId, SendPromptRequest request, CancellationToken ct = default);

    /// <summary>Sends a slash command to a session. Maps to <c>POST /session/{sessionId}/command</c>.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="request">The command request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<MessageWithPartsDto>> SendCommandAsync(string sessionId, SendCommandRequest request, CancellationToken ct = default);

    /// <summary>Runs a shell command within a session. Maps to <c>POST /session/{sessionId}/shell</c>.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="request">The shell command request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<MessageWithPartsDto>> RunShellAsync(string sessionId, RunShellRequest request, CancellationToken ct = default);

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>Lists all available slash commands. Maps to <c>GET /command</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<CommandDto>>> GetCommandsAsync(CancellationToken ct = default);

    // ─── Files ────────────────────────────────────────────────────────────────

    /// <summary>Searches for text matches across the project. Maps to <c>GET /file/text?pattern=</c>.</summary>
    /// <param name="pattern">The text pattern to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<TextMatchDto>>> FindTextAsync(string pattern, CancellationToken ct = default);

    /// <summary>
    /// Lists file-system entries matching a pattern at a given path.
    /// Maps to <c>GET /file?pattern=&amp;path=</c>.
    /// </summary>
    /// <remarks>
    /// The server always returns <see cref="FileNodeDto"/> objects regardless of the pattern.
    /// When <c>path</c> is empty the server ignores the pattern and returns all root entries.
    /// Use <see cref="GetFileTreeAsync"/> for recursive traversal instead.
    /// </remarks>
    /// <param name="request">The find files request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<FileNodeDto>>> FindFilesAsync(FindFilesRequest request, CancellationToken ct = default);

    /// <summary>Searches for code symbols by name. Maps to <c>GET /file/symbol?query=</c>.</summary>
    /// <param name="query">The symbol name query.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<SymbolDto>>> FindSymbolsAsync(string query, CancellationToken ct = default);

    /// <summary>Gets the file tree for a directory. Maps to <c>GET /file/tree?path=</c>.</summary>
    /// <param name="path">The directory path, or <c>null</c> for the project root.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<FileNodeDto>>> GetFileTreeAsync(string? path = null, CancellationToken ct = default);

    /// <summary>Reads the content of a file. Maps to <c>GET /file/read?path=</c>.</summary>
    /// <param name="path">The file path to read.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<FileContentDto>> ReadFileAsync(string path, CancellationToken ct = default);

    /// <summary>Gets the VCS status of all changed files. Maps to <c>GET /file/status</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<FileStatusDto>>> GetFileStatusAsync(CancellationToken ct = default);

    // ─── Agents ───────────────────────────────────────────────────────────────

    /// <summary>Lists all available agents. Maps to <c>GET /agent</c>.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<IReadOnlyList<AgentDto>>> GetAgentsAsync(CancellationToken ct = default);

    // ─── Auth ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the authentication credentials for a provider.
    /// Maps to <c>POST /provider/{id}/auth</c>.
    /// </summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="request">The auth credentials request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> SetProviderAuthAsync(string providerId, SetProviderAuthRequest request, CancellationToken ct = default);

    // ─── Logging ──────────────────────────────────────────────────────────────

    /// <summary>Writes a log entry to the server. Maps to <c>POST /log</c>.</summary>
    /// <param name="request">The log entry request.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OpencodeResult<bool>> WriteLogAsync(WriteLogRequest request, CancellationToken ct = default);

    // ─── Events (SSE) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a server-sent event stream and yields events as they arrive.
    /// Maps to <c>GET /global/event</c>.
    /// </summary>
    /// <remarks>
    /// The stream is cancelled and disposed when <paramref name="cancellationToken"/> is triggered.
    /// The first event received is always <c>server.connected</c>.
    /// This method is intended for foreground use only — cancel the token when the app goes to background.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token. Cancel to stop the stream.</param>
    IAsyncEnumerable<OpencodeEventDto> SubscribeToEventsAsync(CancellationToken cancellationToken);
}
