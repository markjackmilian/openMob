using openMob.Core.Data.Entities;

namespace openMob.Core.Messages;

/// <summary>
/// Published via <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/> when
/// a project preference is saved by <see cref="openMob.Core.ViewModels.ContextSheetViewModel"/>.
/// <see cref="openMob.Core.ViewModels.ChatViewModel"/> subscribes to update its observable state.
/// </summary>
/// <param name="ProjectId">The project identifier whose preference changed.</param>
/// <param name="UpdatedPreference">The full updated preference record.</param>
public sealed record ProjectPreferenceChangedMessage(string ProjectId, ProjectPreference UpdatedPreference);
