using SQLite;

namespace openMob.Core.Data.Entities;

/// <summary>
/// sqlite-net-pcl entity for global application state stored as key-value pairs.
/// Used for persisting app-wide settings like the last active project ID.
/// </summary>
[Table("AppStates")]
[Preserve(AllMembers = true)]
public sealed class AppState
{
    /// <summary>Gets or sets the unique key identifying this state entry.</summary>
    [PrimaryKey]
    [MaxLength(100)]
    [NotNull]
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the value associated with this key, or <c>null</c> if not set.</summary>
    [MaxLength(500)]
    public string? Value { get; set; }
}
