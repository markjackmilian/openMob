namespace openMob.Core.Models;

/// <summary>
/// Groups AI models by their provider for display in the model picker UI.
/// </summary>
/// <param name="ProviderName">The display name of the provider.</param>
/// <param name="Models">The list of models offered by this provider.</param>
public sealed record ProviderModelGroup(string ProviderName, IReadOnlyList<ModelItem> Models);
