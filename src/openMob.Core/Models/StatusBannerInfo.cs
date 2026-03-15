namespace openMob.Core.Models;

/// <summary>
/// Describes a conditional status banner displayed below the chat header.
/// </summary>
/// <param name="Type">The category of the banner.</param>
/// <param name="Message">The user-facing message to display.</param>
/// <param name="ActionLabel">An optional label for an actionable button (e.g. "Configura"), or <c>null</c> if no action.</param>
/// <param name="IsDismissible">Whether the user can dismiss the banner.</param>
public sealed record StatusBannerInfo(StatusBannerType Type, string Message, string? ActionLabel, bool IsDismissible);
