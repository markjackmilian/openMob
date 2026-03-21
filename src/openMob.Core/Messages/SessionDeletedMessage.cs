namespace openMob.Core.Messages;

/// <summary>
/// Published via <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/> when
/// a session is successfully deleted by <see cref="openMob.Core.ViewModels.ContextSheetViewModel"/>.
/// <see cref="openMob.Core.ViewModels.FlyoutViewModel"/> subscribes to refresh the session list.
/// </summary>
/// <param name="SessionId">The identifier of the deleted session.</param>
/// <param name="ProjectId">The project identifier the session belonged to.</param>
public sealed record SessionDeletedMessage(string SessionId, string ProjectId);
