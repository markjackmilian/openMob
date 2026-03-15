namespace openMob.Core.Models;

/// <summary>
/// Display model for a single AI model entry in the model picker UI.
/// </summary>
/// <param name="Id">The model identifier (e.g. <c>"gpt-4o"</c>).</param>
/// <param name="Name">The display name of the model.</param>
/// <param name="ProviderName">The name of the provider that offers this model.</param>
/// <param name="ContextSize">The context window size description (e.g. <c>"200k tokens"</c>), or <c>null</c> if unknown.</param>
/// <param name="IsSelected">Whether this model is currently selected.</param>
public sealed record ModelItem(string Id, string Name, string ProviderName, string? ContextSize, bool IsSelected);
