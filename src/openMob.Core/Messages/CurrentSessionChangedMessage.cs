namespace openMob.Core.Messages;

/// <summary>
/// Published via <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/> when
/// the active session changes in <see cref="openMob.Core.ViewModels.ChatViewModel"/>.
/// <see cref="openMob.Core.ViewModels.FlyoutViewModel"/> subscribes to highlight the active session.
/// </summary>
/// <param name="SessionId">The identifier of the newly active session. Null if no session is active.</param>
public sealed record CurrentSessionChangedMessage(string? SessionId);
