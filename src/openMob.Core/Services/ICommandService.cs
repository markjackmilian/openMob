using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Service for loading, caching, searching, and executing commands from the opencode server.
/// </summary>
public interface ICommandService
{
    /// <summary>
    /// Loads the list of available commands from the server.
    /// Results are cached for the session lifetime.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of available commands.</returns>
    Task<IReadOnlyList<CommandItem>> GetCommandsAsync(CancellationToken ct = default);

    /// <summary>
    /// Searches the cached command list by name or description.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A filtered read-only list of matching commands.</returns>
    Task<IReadOnlyList<CommandItem>> SearchCommandsAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Executes a command by name on the given session.
    /// </summary>
    /// <param name="sessionId">The session to execute the command on.</param>
    /// <param name="commandName">The command name to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<ChatServiceResult<bool>> ExecuteCommandAsync(string sessionId, string commandName, CancellationToken ct = default);

    /// <summary>
    /// Clears the cached command list, forcing a reload on next access.
    /// </summary>
    void InvalidateCache();
}
