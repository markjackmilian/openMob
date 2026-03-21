using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the AgentPickerSheet popup. Displays available agents
/// and allows the user to select one.
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
    /// Gets or sets the operating mode of the picker.
    /// <see cref="PickerMode.Primary"/> loads primary agents and prepends a "Default" entry.
    /// <see cref="PickerMode.Subagent"/> loads subagent-mode agents only, with no "Default" entry.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SheetTitle))]
    private PickerMode _pickerMode = PickerMode.Primary;

    /// <summary>
    /// Gets the sheet title based on the current mode.
    /// Returns "Invoke Subagent" in subagent mode, "Select Agent" otherwise.
    /// </summary>
    public string SheetTitle => PickerMode == PickerMode.Subagent ? "Invoke Subagent" : "Select Agent";

    /// <summary>
    /// Gets or sets the name of the agent selected for subagent invocation.
    /// Only set when <see cref="PickerMode"/> is <see cref="PickerMode.Subagent"/>.
    /// </summary>
    [ObservableProperty]
    private string? _selectedSubagentName;

    /// <summary>
    /// Gets or sets the callback invoked when the user selects an agent.
    /// Receives the selected agent name. May receive <c>null</c> if a reset is triggered externally,
    /// but the picker itself always passes a non-null name.
    /// Set by the MAUI layer (via <see cref="IAppPopupService.ShowAgentPickerAsync"/> or
    /// <see cref="IAppPopupService.ShowSubagentPickerAsync"/>) before presenting the sheet.
    /// Invoked in both <see cref="PickerMode.Primary"/> and <see cref="PickerMode.Subagent"/> modes.
    /// </summary>
    public Action<string?>? OnAgentSelected { get; set; }

    /// <summary>
    /// Loads agents from the server and maps them to display models.
    /// In primary mode, calls <see cref="IAgentService.GetPrimaryAgentsAsync"/> (hidden agents excluded).
    /// In subagent mode, calls <see cref="IAgentService.GetSubagentAgentsAsync"/> (filtered to subagent/all modes).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadAgentsAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadAgentsAsync), "start");
        try
        {
#endif
        if (IsLoading)
            return;

        IsLoading = true;

        try
        {
            IReadOnlyList<AgentDto> agents;

            if (PickerMode == PickerMode.Subagent)
                agents = await _agentService.GetSubagentAgentsAsync(ct);
            else
                agents = await _agentService.GetPrimaryAgentsAsync(ct);

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
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(LoadAgentsAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadAgentsAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Selects an agent and closes the popup.
    /// In primary mode: updates <see cref="SelectedAgentName"/>, invokes <see cref="OnAgentSelected"/>, and pops the popup.
    /// In subagent mode: sets <see cref="SelectedSubagentName"/>, invokes <see cref="OnAgentSelected"/>, and pops the popup.
    /// </summary>
    /// <param name="agentName">The name of the agent to select.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task SelectAgentAsync(string agentName, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(SelectAgentAsync), "start");
        try
        {
#endif
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);

        if (PickerMode == PickerMode.Subagent)
        {
            SelectedSubagentName = agentName;
            OnAgentSelected?.Invoke(agentName);
        }
        else
        {
            SelectedAgentName = agentName;
            OnAgentSelected?.Invoke(agentName);
        }

        // Update the IsSelected state in the collection
        var updatedItems = Agents.Select(a => a with { IsSelected = a.Name == agentName }).ToList();
        Agents = new ObservableCollection<AgentItem>(updatedItems);

        await _popupService.PopPopupAsync(ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(SelectAgentAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(SelectAgentAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }
}
