namespace openMob.Core.Messages;

/// <summary>
/// Sent by <see cref="ViewModels.ChatViewModel"/> when <c>IsAiResponding</c> changes.
/// <see cref="ViewModels.MessageComposerViewModel"/> subscribes to update the streaming guard.
/// </summary>
public sealed record StreamingStateChangedMessage(bool IsStreaming);
