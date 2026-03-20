using openMob.Core.Data;
using openMob.Core.Data.Entities;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Manages per-project user preferences using EF Core and SQLite.
/// </summary>
/// <remarks>
/// Registered as Transient in DI — follows the same lifetime pattern as other business services.
/// Uses <see cref="AppDbContext"/> for persistence.
/// </remarks>
public sealed class ProjectPreferenceService : IProjectPreferenceService
{
    private readonly AppDbContext _db;

    /// <summary>Initialises the service with the required database context.</summary>
    /// <param name="db">The EF Core database context.</param>
    public ProjectPreferenceService(AppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ProjectPreference?> GetAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        return await _db.ProjectPreferences
            .FindAsync([projectId], ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ProjectPreference> GetOrDefaultAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var existing = await _db.ProjectPreferences
            .FindAsync([projectId], ct)
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
            var existing = await _db.ProjectPreferences
                .FindAsync([projectId], ct)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.DefaultModelId = modelId;
            }
            else
            {
                var preference = new ProjectPreference
                {
                    ProjectId = projectId,
                    DefaultModelId = modelId,
                };
                _db.ProjectPreferences.Add(preference);
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
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
    public async Task<bool> SetAgentAsync(string projectId, string? agentName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        try
        {
            var existing = await _db.ProjectPreferences
                .FindAsync([projectId], ct)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.AgentName = agentName;
            }
            else
            {
                _db.ProjectPreferences.Add(new ProjectPreference
                {
                    ProjectId = projectId,
                    AgentName = agentName,
                    ThinkingLevel = ThinkingLevel.Medium,
                });
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
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
            var existing = await _db.ProjectPreferences
                .FindAsync([projectId], ct)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.ThinkingLevel = level;
            }
            else
            {
                _db.ProjectPreferences.Add(new ProjectPreference
                {
                    ProjectId = projectId,
                    ThinkingLevel = level,
                });
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
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
            var existing = await _db.ProjectPreferences
                .FindAsync([projectId], ct)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.AutoAccept = autoAccept;
            }
            else
            {
                _db.ProjectPreferences.Add(new ProjectPreference
                {
                    ProjectId = projectId,
                    AutoAccept = autoAccept,
                    ThinkingLevel = ThinkingLevel.Medium,
                });
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
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
