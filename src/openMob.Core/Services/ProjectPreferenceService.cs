using openMob.Core.Data;
using openMob.Core.Data.Entities;

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
    public async Task SetDefaultModelAsync(string projectId, string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

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
    }
}
