using openMob.Core.Models;

namespace openMob.Core.Messages;

/// <summary>
/// Sent by <see cref="ViewModels.MessageComposerViewModel"/> when the user taps Send.
/// <see cref="ViewModels.ChatViewModel"/> subscribes and dispatches the send operation.
/// </summary>
public sealed record MessageComposedMessage(
    string ProjectId,
    string SessionId,
    string Text,
    string? AgentOverride,
    ThinkingLevel ThinkingLevelOverride,
    bool AutoAcceptOverride
);
