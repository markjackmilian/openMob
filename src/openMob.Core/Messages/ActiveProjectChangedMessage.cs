using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Messages;

/// <summary>
/// Published via <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/>
/// when the user switches the active project. Subscribers should reload project-dependent
/// state (session lists, project name headers, etc.).
/// </summary>
/// <param name="Project">The newly active project.</param>
public sealed record ActiveProjectChangedMessage(ProjectDto Project);
