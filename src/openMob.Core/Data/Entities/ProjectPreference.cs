using System.ComponentModel.DataAnnotations;

namespace openMob.Core.Data.Entities;

/// <summary>
/// Stores per-project user preferences such as the default model.
/// Uses ProjectId as primary key (1:1 relationship with external project).
/// </summary>
public sealed class ProjectPreference
{
    /// <summary>
    /// The external project identifier (from the opencode server). Acts as primary key.
    /// </summary>
    [Key]
    [Required]
    [MaxLength(500)]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The default model identifier in "providerId/modelId" format.
    /// Null if no default model has been selected.
    /// </summary>
    [MaxLength(500)]
    public string? DefaultModelId { get; set; }
}
