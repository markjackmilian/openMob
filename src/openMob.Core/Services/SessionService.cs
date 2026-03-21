using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Infrastructure.Monitoring;

namespace openMob.Core.Services;

/// <summary>
/// Implementation of <see cref="ISessionService"/> that wraps <see cref="IOpencodeApiClient"/>
/// session methods with error handling and result unwrapping.
/// </summary>
internal sealed class SessionService : ISessionService
{
    private readonly IOpencodeApiClient _apiClient;

    /// <summary>Initialises the service with the opencode API client.</summary>
    /// <param name="apiClient">The opencode API client.</param>
    public SessionService(IOpencodeApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionDto>> GetAllSessionsAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetSessionsAsync(ct).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
            return result.Value;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to get sessions: {result.Error.Message}"),
                new Dictionary<string, object> { ["errorKind"] = result.Error.Kind.ToString() });
        }

        return [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SessionDto>> GetSessionsByProjectAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var allSessions = await GetAllSessionsAsync(ct).ConfigureAwait(false);

        return allSessions
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.Time.Updated)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<SessionDto?> GetSessionAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var result = await _apiClient.GetSessionAsync(id, ct).ConfigureAwait(false);

        if (result.IsSuccess)
            return result.Value;

        if (result.Error is not null && result.Error.Kind != ErrorKind.NotFound)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to get session '{id}': {result.Error.Message}"),
                new Dictionary<string, object>
                {
                    ["sessionId"] = id,
                    ["errorKind"] = result.Error.Kind.ToString(),
                });
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<SessionDto?> CreateSessionAsync(string? title, CancellationToken ct = default)
    {
        var request = new CreateSessionRequest(Title: title, ParentId: null);
        var result = await _apiClient.CreateSessionAsync(request, ct).ConfigureAwait(false);

        if (result.IsSuccess)
            return result.Value;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to create session: {result.Error.Message}"),
                new Dictionary<string, object> { ["errorKind"] = result.Error.Kind.ToString() });
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<SessionDto> CreateSessionForProjectAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var request = new CreateSessionRequest(Title: null, ParentId: null);
        var result = await _apiClient.CreateSessionAsync(request, ct).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
            return result.Value;

        var errorMessage = result.Error?.Message ?? "Unknown error";
        var errorKind = result.Error?.Kind.ToString() ?? "Unknown";

        SentryHelper.CaptureException(
            new InvalidOperationException($"Failed to create session: {errorMessage}"),
            new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["errorKind"] = errorKind,
            });

        throw new InvalidOperationException($"Failed to create session: {errorMessage}");
    }

    /// <inheritdoc />
    public async Task<bool> UpdateSessionTitleAsync(string id, string newTitle, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(newTitle);

        var request = new UpdateSessionRequest(Title: newTitle);
        var result = await _apiClient.UpdateSessionAsync(id, request, ct).ConfigureAwait(false);

        if (result.IsSuccess)
            return true;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to update session '{id}': {result.Error.Message}"),
                new Dictionary<string, object>
                {
                    ["sessionId"] = id,
                    ["errorKind"] = result.Error.Kind.ToString(),
                });
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSessionAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var result = await _apiClient.DeleteSessionAsync(id, ct).ConfigureAwait(false);

        if (result.IsSuccess)
            return true;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to delete session '{id}': {result.Error.Message}"),
                new Dictionary<string, object>
                {
                    ["sessionId"] = id,
                    ["errorKind"] = result.Error.Kind.ToString(),
                });
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<SessionDto?> ForkSessionAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // Fork from the latest message (MessageId = null)
        var request = new ForkSessionRequest(MessageId: null, Title: null);
        var result = await _apiClient.ForkSessionAsync(id, request, ct).ConfigureAwait(false);

        if (result.IsSuccess)
            return result.Value;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to fork session '{id}': {result.Error.Message}"),
                new Dictionary<string, object>
                {
                    ["sessionId"] = id,
                    ["errorKind"] = result.Error.Kind.ToString(),
                });
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<SessionDto?> GetLastSessionForProjectAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var sessions = await GetSessionsByProjectAsync(projectId, ct).ConfigureAwait(false);

        // GetSessionsByProjectAsync already orders by Time.Updated descending
        return sessions.FirstOrDefault();
    }
}
