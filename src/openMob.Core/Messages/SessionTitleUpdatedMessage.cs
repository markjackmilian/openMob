namespace openMob.Core.Messages;

/// <summary>
/// Published via <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/>
/// when the opencode server updates a session's title (e.g. after the title subagent runs).
/// Subscribers should update the session title in their UI without a full reload.
/// </summary>
/// <param name="SessionId">The ID of the session whose title was updated.</param>
/// <param name="NewTitle">The new title assigned by the server.</param>
public sealed record SessionTitleUpdatedMessage(string SessionId, string NewTitle);
