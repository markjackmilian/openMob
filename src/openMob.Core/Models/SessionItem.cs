namespace openMob.Core.Models;

/// <summary>
/// Display model for a session list item in the UI.
/// </summary>
/// <param name="Id">The unique session identifier.</param>
/// <param name="Title">The session title.</param>
/// <param name="ProjectId">The ID of the project this session belongs to.</param>
/// <param name="UpdatedAt">The timestamp when the session was last updated.</param>
/// <param name="IsSelected">Whether this session is currently selected/active in the UI.</param>
public sealed record SessionItem(string Id, string Title, string ProjectId, DateTimeOffset UpdatedAt, bool IsSelected);
