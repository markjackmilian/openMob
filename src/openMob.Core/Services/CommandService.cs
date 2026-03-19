using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Loads, caches, searches, and executes commands from the opencode server.
/// Commands are fetched once and cached for the session lifetime until
/// <see cref="InvalidateCache"/> is called.
/// </summary>
internal sealed class CommandService : ICommandService
{
    private readonly IOpencodeApiClient _apiClient;

    /// <summary>Cached command list. <c>null</c> means not yet loaded.</summary>
    private IReadOnlyList<CommandItem>? _cachedCommands;

    /// <summary>Initialises the CommandService with the required API client.</summary>
    /// <param name="apiClient">The opencode API client for fetching commands.</param>
    public CommandService(IOpencodeApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommandItem>> GetCommandsAsync(CancellationToken ct = default)
    {
        if (_cachedCommands is not null)
            return _cachedCommands;

        try
        {
            var result = await _apiClient.GetCommandsAsync(ct).ConfigureAwait(false);

            if (result.IsSuccess && result.Value is not null)
            {
                _cachedCommands = result.Value
                    .Select(dto => new CommandItem(
                        Name: dto.Name,
                        Description: dto.Description,
                        IsSubtask: dto.Subtask ?? false))
                    .ToList();

                return _cachedCommands;
            }

            return Array.Empty<CommandItem>();
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "CommandService.GetCommandsAsync",
            });
            return Array.Empty<CommandItem>();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommandItem>> SearchCommandsAsync(string query, CancellationToken ct = default)
    {
        var commands = await GetCommandsAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(query))
            return commands;

        return commands
            .Where(c =>
                c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (c.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ChatServiceResult<bool>> ExecuteCommandAsync(string sessionId, string commandName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);

        try
        {
            var result = await _apiClient.SendCommandAsync(
                sessionId,
                new SendCommandRequest(commandName, null),
                ct).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                return ChatServiceResult<bool>.Ok(true);
            }

            var errorMessage = result.Error?.Message ?? "Failed to execute command.";
            return ChatServiceResult<bool>.Fail(new ChatServiceError(
                ChatServiceErrorKind.ServerError,
                errorMessage,
                result.Error?.HttpStatusCode));
        }
        catch (OperationCanceledException)
        {
            return ChatServiceResult<bool>.Fail(new ChatServiceError(
                ChatServiceErrorKind.Cancelled,
                "Command execution was cancelled."));
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "CommandService.ExecuteCommandAsync",
                ["sessionId"] = sessionId,
                ["commandName"] = commandName,
            });

            return ChatServiceResult<bool>.Fail(new ChatServiceError(
                ChatServiceErrorKind.Unknown,
                $"An unexpected error occurred: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        _cachedCommands = null;
    }
}
