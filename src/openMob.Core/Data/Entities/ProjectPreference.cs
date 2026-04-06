using SQLite;
using openMob.Core.Models;

namespace openMob.Core.Data.Entities;

/// <summary>
/// Stores per-project user preferences such as the default model, agent, thinking level, and auto-accept.
/// Uses ProjectId as primary key (1:1 relationship with external project).
/// </summary>
[Table("ProjectPreferences")]
[Preserve(AllMembers = true)]
public sealed class ProjectPreference
{
    /// <summary>
    /// The external project identifier (from the opencode server). Acts as primary key.
    /// </summary>
    [PrimaryKey]
    [MaxLength(500)]
    [NotNull]
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
    /// The thinking/reasoning depth level stored as its integer value.
    /// sqlite-net-pcl does not automatically convert enums, so we store as int.
    /// Default is <see cref="ThinkingLevel.Medium"/> (value 1).
    /// </summary>
    /// <remarks>
    /// Use <see cref="ThinkingLevelEnum"/> for typed access to the enum value.
    /// </remarks>
    public int ThinkingLevelValue { get; set; } = (int)ThinkingLevel.Medium;

    /// <summary>
    /// Gets or sets the <see cref="ThinkingLevel"/> enum value.
    /// This is a computed property that maps to <see cref="ThinkingLevelValue"/>.
    /// Marked <c>[Ignore]</c> so sqlite-net-pcl does not attempt to persist it.
    /// </summary>
    [Ignore]
    public ThinkingLevel ThinkingLevel
    {
        get => (ThinkingLevel)ThinkingLevelValue;
        set => ThinkingLevelValue = (int)value;
    }

    /// <summary>
    /// Whether auto-accept is enabled for agent tool suggestions. Default is false.
    /// </summary>
    public bool AutoAccept { get; set; }

    /// <summary>
    /// Whether unhandled SSE event debug cards are shown in the chat. Default is <c>false</c> (hidden).
    /// When <c>false</c>, <see cref="openMob.Core.ViewModels.ChatViewModel"/> suppresses
    /// <c>UnknownEvent</c> and <c>UnknownPart</c> cards from the Messages collection.
    /// </summary>
    public bool ShowUnhandledSseEvents { get; set; }
}
