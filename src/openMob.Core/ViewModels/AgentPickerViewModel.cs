using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the AgentPickerSheet popup. Displays available agents
/// and allows the user to select one (REQ-030).
/// </summary>
public sealed partial class AgentPickerViewModel : ObservableObject
{
    private readonly IAgentService _agentService;
    private readonly IAppPopupService _popupService;

    /// <summary>Initialises the AgentPickerViewModel with required dependencies.</summary>
    /// <param name="agentService">Service for agent operations.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    public AgentPickerViewModel(
        IAgentService agentService,
        IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(agentService);
        ArgumentNullException.ThrowIfNull(popupService);

        _agentService = agentService;
        _popupService = popupService;
    }

    /// <summary>Gets or sets the collection of agent items for display.</summary>
    [ObservableProperty]
    private ObservableCollection<AgentItem> _agents = [];

    /// <summary>Gets or sets the name of the currently selected agent.</summary>
    [ObservableProperty]
    private string? _selectedAgentName;

    /// <summary>Gets or sets whether the agent list is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets or sets whether the agent list is empty.</summary>
    [ObservableProperty]
    private bool _isEmpty;

    /// <summary>
    /// Loads all available agents from the server and maps them to display models.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadAgentsAsync(CancellationToken ct)
    {
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            var agents = await _agentService.GetAgentsAsync(ct);

            var items = agents.Select(a => new AgentItem(
                Name: a.Name,
                Description: a.Description,
                IsSelected: a.Name == SelectedAgentName
            )).ToList();

            Agents = new ObservableCollection<AgentItem>(items);
            IsEmpty = Agents.Count == 0;
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "AgentPickerViewModel.LoadAgentsAsync",
            });
            Agents = [];
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Selects an agent and closes the popup. The caller reads <see cref="SelectedAgentName"/>
    /// to apply the selection.
    /// </summary>
    /// <param name="agentName">The name of the agent to select.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectAgentAsync(string agentName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        SelectedAgentName = agentName;

        // Update the IsSelected state in the collection
        var updatedItems = Agents.Select(a => a with { IsSelected = a.Name == agentName }).ToList();
        Agents = new ObservableCollection<AgentItem>(updatedItems);

        await _popupService.PopPopupAsync(ct);
    }
}
