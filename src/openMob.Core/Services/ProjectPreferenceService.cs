using openMob.Core.Data;
using openMob.Core.Data.Entities;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Manages per-project user preferences using sqlite-net-pcl and SQLite.
/// </summary>
/// <remarks>
/// Registered as Transient in DI — follows the same lifetime pattern as other business services.
/// Injects <see cref="IAppDatabase"/> (Singleton) for persistence.
/// </remarks>
public sealed class ProjectPreferenceService : IProjectPreferenceService
{
    private readonly IAppDatabase _db;

    /// <summary>Initialises the service with the required database.</summary>
    /// <param name="db">The application database (Singleton).</param>
    public ProjectPreferenceService(IAppDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ProjectPreference?> GetAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        return await _db.Connection
            .Table<ProjectPreference>()
            .Where(p => p.ProjectId == projectId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ProjectPreference> GetOrDefaultAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var existing = await _db.Connection
            .Table<ProjectPreference>()
            .Where(p => p.ProjectId == projectId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is not null)
            return existing;

        // Return a transient default — do NOT insert into DB to avoid polluting
        // the database with rows for projects the user never customised.
        return new ProjectPreference
        {
            ProjectId = projectId,
            ThinkingLevel = ThinkingLevel.Medium,
            AutoAccept = false,
        };
    }

    /// <inheritdoc />
    public async Task<bool> SetDefaultModelAsync(string projectId, string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        try
        {
            var existing = await _db.Connection
                .Table<ProjectPreference>()
                .Where(p => p.ProjectId == projectId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.DefaultModelId = modelId;
                await _db.Connection.UpdateAsync(existing).ConfigureAwait(false);
            }
            else
            {
                await _db.Connection.InsertAsync(new ProjectPreference
                {
                    ProjectId = projectId,
                    DefaultModelId = modelId,
                }).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["modelId"] = modelId,
                ["operation"] = "SetDefaultModelAsync",
            });
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ClearDefaultModelAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        try
        {
            var existing = await _db.Connection
                .Table<ProjectPreference>()
                .Where(p => p.ProjectId == projectId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.DefaultModelId = null;
                await _db.Connection.UpdateAsync(existing).ConfigureAwait(false);
            }
            else
            {
                await _db.Connection.InsertAsync(new ProjectPreference
                {
                    ProjectId = projectId,
                    DefaultModelId = null,
                    ThinkingLevel = ThinkingLevel.Medium,
                }).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["operation"] = "ClearDefaultModelAsync",
            });
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetAgentAsync(string projectId, string? agentName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        try
        {
            var existing = await _db.Connection
                .Table<ProjectPreference>()
                .Where(p => p.ProjectId == projectId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.AgentName = agentName;
                await _db.Connection.UpdateAsync(existing).ConfigureAwait(false);
            }
            else
            {
                await _db.Connection.InsertAsync(new ProjectPreference
                {
                    ProjectId = projectId,
                    AgentName = agentName,
                    ThinkingLevel = ThinkingLevel.Medium,
                }).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["agentName"] = agentName ?? "(null)",
                ["operation"] = "SetAgentAsync",
            });
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetThinkingLevelAsync(string projectId, ThinkingLevel level, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        try
        {
            var existing = await _db.Connection
                .Table<ProjectPreference>()
                .Where(p => p.ProjectId == projectId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.ThinkingLevel = level;
                await _db.Connection.UpdateAsync(existing).ConfigureAwait(false);
            }
            else
            {
                await _db.Connection.InsertAsync(new ProjectPreference
                {
                    ProjectId = projectId,
                    ThinkingLevel = level,
                }).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["level"] = level.ToString(),
                ["operation"] = "SetThinkingLevelAsync",
            });
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetAutoAcceptAsync(string projectId, bool autoAccept, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        try
        {
            var existing = await _db.Connection
                .Table<ProjectPreference>()
                .Where(p => p.ProjectId == projectId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.AutoAccept = autoAccept;
                await _db.Connection.UpdateAsync(existing).ConfigureAwait(false);
            }
            else
            {
                await _db.Connection.InsertAsync(new ProjectPreference
                {
                    ProjectId = projectId,
                    AutoAccept = autoAccept,
                    ThinkingLevel = ThinkingLevel.Medium,
                }).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["projectId"] = projectId,
                ["autoAccept"] = autoAccept,
                ["operation"] = "SetAutoAcceptAsync",
            });
            return false;
        }
    }
}
