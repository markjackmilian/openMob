namespace openMob.Core.Messages;

/// <summary>
/// Published via <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/> when
/// a <c>session.created</c> SSE event is received by <see cref="openMob.Core.ViewModels.ChatViewModel"/>.
/// <see cref="openMob.Core.ViewModels.FlyoutViewModel"/> subscribes to prepend the new session to the drawer list.
/// </summary>
/// <param name="SessionId">The identifier of the created session.</param>
/// <param name="ProjectId">The project identifier the session belongs to.</param>
/// <param name="Title">The initial title of the session.</param>
/// <param name="UpdatedAt">The last-updated timestamp of the session.</param>
public sealed record SessionCreatedMessage(
    string SessionId,
    string ProjectId,
    string Title,
    DateTimeOffset UpdatedAt);
