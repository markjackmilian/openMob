using System.ComponentModel.DataAnnotations;
using openMob.Core.Models;

namespace openMob.Core.Data.Entities;

/// <summary>
/// Stores per-project user preferences such as the default model, agent, thinking level, and auto-accept.
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

    /// <summary>
    /// The name of the selected primary agent. Null means the default agent.
    /// </summary>
    [MaxLength(500)]
    public string? AgentName { get; set; }

    /// <summary>
    /// The thinking/reasoning depth level. Stored as int (EF Core default for enums).
    /// Default is <see cref="ThinkingLevel.Medium"/> (value 1).
    /// </summary>
    public ThinkingLevel ThinkingLevel { get; set; } = ThinkingLevel.Medium;

    /// <summary>
    /// Whether auto-accept is enabled for agent tool suggestions. Default is false.
    /// </summary>
    public bool AutoAccept { get; set; }
}
