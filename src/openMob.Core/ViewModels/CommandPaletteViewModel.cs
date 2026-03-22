using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Logging;
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
    /// When set, command selection invokes this callback instead of executing the command.
    /// Used by the message composer for token insertion mode.
    /// </summary>
    public Action<string>? OnCommandSelected { get; set; }

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
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(LoadCommandsAsync), "start");
        try
        {
#endif
        IsLoading = true;

        try
        {
            var commands = await _commandService.GetCommandsAsync(ct);
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
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(LoadCommandsAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(LoadCommandsAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Executes the selected command and dismisses the palette.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ExecuteCommandAsync(CommandItem command, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ExecuteCommandAsync), "start");
        try
        {
#endif
        ArgumentNullException.ThrowIfNull(command);

        // Callback mode: return the command name to the caller instead of executing [REQ-014]
        if (OnCommandSelected is not null)
        {
            OnCommandSelected(command.Name);
            await _popupService.PopPopupAsync(ct);
            return;
        }

        if (CurrentSessionId is null)
        {
            await _popupService.ShowErrorAsync("Error", "No active session.", ct);
            return;
        }

        try
        {
            var result = await _commandService.ExecuteCommandAsync(CurrentSessionId, command.Name, ct)
                ;

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
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ExecuteCommandAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ExecuteCommandAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Invalidates the command cache and reloads the list.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task RefreshCommandsAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(RefreshCommandsAsync), "start");
        try
        {
#endif
        _commandService.InvalidateCache();
        await LoadCommandsAsync(ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(RefreshCommandsAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(RefreshCommandsAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Filters the command list based on the search query.
    /// </summary>
    /// <param name="query">The search query.</param>
    private async Task FilterCommandsAsync(string query)
    {
        try
        {
            var filtered = await _commandService.SearchCommandsAsync(query);
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
