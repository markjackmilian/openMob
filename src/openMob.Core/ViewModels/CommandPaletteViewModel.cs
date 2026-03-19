using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Monitoring;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the Command Palette bottom sheet (REQ-029, REQ-030).
/// Displays a searchable list of commands loaded from the opencode server.
/// </summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private readonly ICommandService _commandService;
    private readonly IAppPopupService _popupService;

    /// <summary>
    /// Gets or sets the session ID to execute commands against.
    /// Must be set before executing a command.
    /// </summary>
    public string? CurrentSessionId { get; set; }

    /// <summary>Initialises the CommandPaletteViewModel with required dependencies.</summary>
    /// <param name="commandService">Service for command operations.</param>
    /// <param name="popupService">Service for popup/dialog operations.</param>
    public CommandPaletteViewModel(
        ICommandService commandService,
        IAppPopupService popupService)
    {
        ArgumentNullException.ThrowIfNull(commandService);
        ArgumentNullException.ThrowIfNull(popupService);

        _commandService = commandService;
        _popupService = popupService;
    }

    // ─── Observable Properties ────────────────────────────────────────────────

    /// <summary>Gets or sets the filtered collection of commands for display.</summary>
    [ObservableProperty]
    private ObservableCollection<CommandItem> _commands = [];

    /// <summary>Gets or sets the current search text. Triggers filtering on change.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Gets or sets whether the command list is currently loading.</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Gets whether the command list is empty and not loading.</summary>
    public bool IsEmpty => Commands.Count == 0 && !IsLoading;

    /// <summary>
    /// Called by the source generator when <see cref="SearchText"/> changes.
    /// Triggers an asynchronous search of the command list.
    /// </summary>
    /// <param name="value">The new search text value.</param>
    partial void OnSearchTextChanged(string value)
    {
        _ = FilterCommandsAsync(value);
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all available commands from the server and populates the list.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task LoadCommandsAsync(CancellationToken ct)
    {
        IsLoading = true;

        try
        {
            var commands = await _commandService.GetCommandsAsync(ct).ConfigureAwait(false);
            Commands = new ObservableCollection<CommandItem>(commands);
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "CommandPaletteViewModel.LoadCommandsAsync",
            });
            Commands = [];
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>
    /// Executes the selected command and dismisses the palette.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ExecuteCommandAsync(CommandItem command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (CurrentSessionId is null)
        {
            await _popupService.ShowErrorAsync("Error", "No active session.", ct);
            return;
        }

        try
        {
            var result = await _commandService.ExecuteCommandAsync(CurrentSessionId, command.Name, ct)
                .ConfigureAwait(false);

            if (!result.IsSuccess && result.Error is not null)
            {
                await _popupService.ShowErrorAsync("Command Failed", result.Error.Message, ct);
            }
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "CommandPaletteViewModel.ExecuteCommandAsync",
                ["commandName"] = command.Name,
            });
        }

        await _popupService.PopPopupAsync(ct);
    }

    /// <summary>
    /// Invalidates the command cache and reloads the list.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task RefreshCommandsAsync(CancellationToken ct)
    {
        _commandService.InvalidateCache();
        await LoadCommandsAsync(ct);
    }

    /// <summary>
    /// Filters the command list based on the search query.
    /// </summary>
    /// <param name="query">The search query.</param>
    private async Task FilterCommandsAsync(string query)
    {
        try
        {
            var filtered = await _commandService.SearchCommandsAsync(query).ConfigureAwait(false);
            Commands = new ObservableCollection<CommandItem>(filtered);
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "CommandPaletteViewModel.FilterCommandsAsync",
                ["query"] = query,
            });
        }
    }
}
