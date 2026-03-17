using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the ModelPickerSheet popup. Displays a flat list of AI models
/// and allows the user to select one (REQ-031, REQ-032).
/// </summary>
/// <remarks>
/// Models are presented in a flat list (no provider grouping) to enable
/// <c>CollectionView</c> virtualisation in the UI layer. Each <see cref="ModelItem"/>
/// still carries its <see cref="ModelItem.ProviderName"/> for display purposes.
/// </remarks>
public sealed partial class ModelPickerViewModel : ObservableObject
{
    private readonly IProviderService _providerService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;

    /// <summary>Initialises the ModelPickerViewModel with required dependencies.</summary>
    /// <param name="providerService">Service for provider/model operations.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    public ModelPickerViewModel(
        IProviderService providerService,
        INavigationService navigationService,
        IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(providerService);
        ArgumentNullException.ThrowIfNull(navigationService);
        ArgumentNullException.ThrowIfNull(popupService);

        _providerService = providerService;
        _navigationService = navigationService;
        _popupService = popupService;
    }

    /// <summary>
    /// Optional callback invoked when the user selects a model.
    /// Set by the caller (e.g., MauiPopupService) before presenting the picker.
    /// The callback receives the full model ID in "providerId/modelId" format.
    /// </summary>
    public Action<string>? OnModelSelected { get; set; }

    /// <summary>Gets or sets the flat list of all available models across all providers.</summary>
    [ObservableProperty]
    private ObservableCollection<ModelItem> _models = [];

    /// <summary>Gets or sets the ID of the currently selected model.</summary>
    [ObservableProperty]
    private string? _selectedModelId;

    /// <summary>Gets or sets whether the model list is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets whether the model list is empty (no models available).</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>
    /// Loads providers and extracts models from <c>ProviderDto.Models</c> JsonElement
    /// into a flat list for virtualised display.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadModelsAsync(CancellationToken ct)
    {
        if (IsLoading)
            return;
        IsLoading = true;

        try
        {
            var providers = await _providerService.GetConfiguredProvidersAsync(ct).ConfigureAwait(false);

            var allModels = new List<ModelItem>();

            foreach (var provider in providers)
            {
                var models = ExtractModelsFromProvider(provider.Id, provider.Name, provider.Models);
                allModels.AddRange(models);
            }

            Models = new ObservableCollection<ModelItem>(allModels);
            IsEmpty = allModels.Count == 0;
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ModelPickerViewModel.LoadModelsAsync",
            });
            Models = [];
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Selects a model, updates the visual selection state, and closes the popup.
    /// The caller reads <see cref="SelectedModelId"/> to apply the selection.
    /// </summary>
    /// <param name="modelId">The ID of the model to select (e.g. <c>"anthropic/claude-3-opus"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectModelAsync(string modelId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        SelectedModelId = modelId;

        // Rebuild the collection with updated IsSelected state
        var updatedModels = Models.Select(m => m with { IsSelected = m.Id == modelId }).ToList();
        Models = new ObservableCollection<ModelItem>(updatedModels);

        OnModelSelected?.Invoke(SelectedModelId);

        await _popupService.PopPopupAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the popup and navigates to the settings page for provider configuration (REQ-032).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ConfigureProvidersAsync(CancellationToken ct)
    {
        await _popupService.PopPopupAsync(ct).ConfigureAwait(false);
        await _navigationService.GoToAsync("settings", ct).ConfigureAwait(false);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Extracts model items from a provider's <c>Models</c> JsonElement.
    /// The Models field is a raw JSON object where keys are model IDs and values
    /// contain model metadata.
    /// </summary>
    /// <param name="providerId">The provider identifier (used as prefix for model IDs).</param>
    /// <param name="providerName">The provider display name.</param>
    /// <param name="modelsJson">The raw JSON element containing model definitions.</param>
    /// <returns>A list of model items extracted from the JSON.</returns>
    private IReadOnlyList<ModelItem> ExtractModelsFromProvider(
        string providerId,
        string providerName,
        JsonElement modelsJson)
    {
        var models = new List<ModelItem>();

        try
        {
            if (modelsJson.ValueKind != JsonValueKind.Object)
                return models;

            foreach (var modelProperty in modelsJson.EnumerateObject())
            {
                var modelId = modelProperty.Name;
                var modelName = modelId; // Default to ID as name
                string? contextSize = null;

                // Try to extract display name from the model object
                if (modelProperty.Value.ValueKind == JsonValueKind.Object)
                {
                    if (modelProperty.Value.TryGetProperty("name", out var nameElement)
                        && nameElement.ValueKind == JsonValueKind.String)
                    {
                        modelName = nameElement.GetString() ?? modelId;
                    }

                    // Try to extract context window size from "limit.context"
                    // (server shape: "limit": { "context": 131072, "output": 32768 })
                    if (modelProperty.Value.TryGetProperty("limit", out var limitElement) &&
                        limitElement.TryGetProperty("context", out var contextElement) &&
                        contextElement.ValueKind == JsonValueKind.Number &&
                        contextElement.TryGetInt64(out var contextLength))
                    {
                        contextSize = contextLength >= 1000
                            ? $"{contextLength / 1000}k tokens"
                            : $"{contextLength} tokens";
                    }
                }

                models.Add(new ModelItem(
                    Id: $"{providerId}/{modelId}",
                    Name: modelName,
                    ProviderName: providerName,
                    ContextSize: contextSize,
                    IsSelected: $"{providerId}/{modelId}" == SelectedModelId));
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ModelPickerViewModel.ExtractModelsFromProvider",
                ["providerId"] = providerId,
            });
        }

        return models;
    }
}
