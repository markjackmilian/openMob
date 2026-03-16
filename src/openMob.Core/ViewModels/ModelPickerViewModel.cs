using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the ModelPickerSheet popup. Displays AI models grouped by provider
/// and allows the user to select one (REQ-031, REQ-032).
/// </summary>
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

    /// <summary>Gets or sets the provider model groups for display.</summary>
    [ObservableProperty]
    private ObservableCollection<ProviderModelGroup> _providerGroups = [];

    /// <summary>Gets or sets the ID of the currently selected model.</summary>
    [ObservableProperty]
    private string? _selectedModelId;

    /// <summary>Gets or sets whether the model list is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets whether the model list is empty (no models available).</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>Gets or sets whether any providers are configured on the server.</summary>
    [ObservableProperty]
    private bool _hasProviders;

    /// <summary>
    /// Loads providers and extracts models from <c>ProviderDto.Models</c> JsonElement,
    /// grouping them by provider name.
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
            var providers = await _providerService.GetProvidersAsync(ct);
            HasProviders = providers.Count > 0;

            var groups = new List<ProviderModelGroup>();

            foreach (var provider in providers)
            {
                var models = ExtractModelsFromProvider(provider.Id, provider.Name, provider.Models);
                if (models.Count > 0)
                {
                    groups.Add(new ProviderModelGroup(provider.Name, models));
                }
            }

            ProviderGroups = new ObservableCollection<ProviderModelGroup>(groups);
            IsEmpty = groups.Count == 0 || groups.All(g => g.Models.Count == 0);
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ModelPickerViewModel.LoadModelsAsync",
            });
            ProviderGroups = [];
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Selects a model and closes the popup. The caller reads <see cref="SelectedModelId"/>
    /// to apply the selection.
    /// </summary>
    /// <param name="modelId">The ID of the model to select.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectModelAsync(string modelId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        SelectedModelId = modelId;

        // Update the IsSelected state in all groups
        var updatedGroups = ProviderGroups.Select(g => g with
        {
            Models = g.Models.Select(m => m with { IsSelected = m.Id == modelId }).ToList().AsReadOnly(),
        }).ToList();

        ProviderGroups = new ObservableCollection<ProviderModelGroup>(updatedGroups);

        OnModelSelected?.Invoke(SelectedModelId);

        await _popupService.PopPopupAsync(ct);
    }

    /// <summary>
    /// Closes the popup and navigates to the settings page for provider configuration (REQ-032).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ConfigureProvidersAsync(CancellationToken ct)
    {
        await _popupService.PopPopupAsync(ct);
        await _navigationService.GoToAsync("settings", ct);
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

                    // Try to extract context window size
                    if (modelProperty.Value.TryGetProperty("context_length", out var contextElement))
                    {
                        if (contextElement.ValueKind == JsonValueKind.Number && contextElement.TryGetInt64(out var contextLength))
                        {
                            contextSize = contextLength >= 1000
                                ? $"{contextLength / 1000}k tokens"
                                : $"{contextLength} tokens";
                        }
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
