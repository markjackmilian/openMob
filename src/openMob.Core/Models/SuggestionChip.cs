namespace openMob.Core.Models;

/// <summary>Represents a suggestion chip with a title, subtitle, and prompt text.</summary>
/// <param name="Title">The chip display title.</param>
/// <param name="Subtitle">The chip subtitle text.</param>
/// <param name="PromptText">The text to insert into the input bar when tapped.</param>
public sealed record SuggestionChip(string Title, string Subtitle, string PromptText);
